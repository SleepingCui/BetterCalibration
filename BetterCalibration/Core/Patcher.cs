using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace BetterCalibration.Core;

public enum PatchType {
    Prefix,
    Postfix,
    Transpiler,
    Finalizer
}


[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class JAPatchAttribute : Attribute {
    internal MethodInfo Method;
    public readonly Type ClassType;
    public readonly string ClassName;
    public readonly string MethodName;
    public readonly PatchType PatchType;
    public readonly bool Disable;
    public int MinVersion;
    public int MaxVersion = int.MaxValue;
    public Type[] ArgumentTypesType;
    public bool TryingCatch = true;
    public int Priority = -1;

    public JAPatchAttribute(string className, string methodName, PatchType patchType, bool disable) {
        ClassName = className;
        MethodName = methodName;
        PatchType = patchType;
        Disable = disable;
    }

    public JAPatchAttribute(Type classType, string methodName, PatchType patchType, bool disable) {
        ClassType = classType;
        MethodName = methodName;
        PatchType = patchType;
        Disable = disable;
    }

    internal string PatchId => Method.DeclaringType.Name + "." + Method.Name + "(" + PatchType + ")";
}


public class Patcher {
    private readonly List<JAPatchAttribute> patchList = [];
    private readonly Harmony harmony;
    private bool patched;
    
    public event Action<string> OnFatalPatchFailure;

    public Patcher(string ownerId) {
        harmony = new Harmony("BetterCalibration." + ownerId);
    }


    public Patcher AddPatch(Type type) {
        foreach(MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)) {
            foreach(JAPatchAttribute attribute in method.GetCustomAttributes<JAPatchAttribute>()) {
                attribute.Method = method;
                patchList.Add(attribute);
            }
        }
        return this;
    }

    public void Patch() {
        if(patched) return;
        patched = true;
        foreach(JAPatchAttribute attribute in patchList) {
            try {
                PatchOne(attribute);
            } catch(Exception e) {
                Main.Error("Failed to apply patch '" + attribute.PatchId + "'", e);
                if(!attribute.Disable) continue;
                Unpatch();
                OnFatalPatchFailure?.Invoke(attribute.PatchId);
                return;
            }
        }
    }

    public void Unpatch() {
        if(!patched) return;
        patched = false;
        try {
            harmony.UnpatchAll(harmony.Id);
        } catch(Exception e) {
            Main.Error("Failed to remove patches of " + harmony.Id, e);
        }
    }

    private void PatchOne(JAPatchAttribute attribute) {
        if(attribute.MinVersion > VersionControl.releaseNumber || attribute.MaxVersion < VersionControl.releaseNumber) return;

        MethodInfo patchMethod = attribute.Method;
        if(!patchMethod.IsPublic) throw new NotSupportedException("Patch method must be public: " + attribute.PatchId);

        Type type = attribute.ClassType ?? AccessTools.TypeByName(attribute.ClassName);
        if(type == null) throw new TypeLoadException("Cannot find type '" + attribute.ClassName + "'");

        MethodInfo original = FindMethod(type, attribute) ?? throw new MissingMethodException(type.FullName, attribute.MethodName);
        
        bool isolate = attribute.TryingCatch && attribute.PatchType is PatchType.Prefix or PatchType.Postfix;
        MethodInfo target = isolate ? PatchIsolation.Wrap(patchMethod, attribute.PatchId) : patchMethod;

        HarmonyMethod patch = new(target) { priority = attribute.Priority };
        switch(attribute.PatchType) {
            case PatchType.Prefix:
                harmony.Patch(original, prefix: patch);
                break;
            case PatchType.Postfix:
                harmony.Patch(original, postfix: patch);
                break;
            case PatchType.Transpiler:
                harmony.Patch(original, transpiler: patch);
                break;
            case PatchType.Finalizer:
                harmony.Patch(original, finalizer: patch);
                break;
            default:
                throw new NotSupportedException("Unsupported patch type " + attribute.PatchType);
        }
    }

    private static MethodInfo FindMethod(Type type, JAPatchAttribute attribute) {
        IEnumerable<MethodInfo> candidates = type.GetMethods(AccessTools.all).Where(method => method.Name == attribute.MethodName);
        if(attribute.ArgumentTypesType != null)
            candidates = candidates.Where(method => method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(attribute.ArgumentTypesType));
        MethodInfo[] found = candidates.ToArray();
        if(found.Length == 0) return null;
        if(found.Length > 1) throw new AmbiguousMatchException("Ambiguous method '" + type.FullName + "." + attribute.MethodName + "'");
        return found[0];
    }
}


internal static class PatchIsolation {
    private static readonly MethodInfo ReportMethod = AccessTools.Method(typeof(Main), nameof(Main.Error), [typeof(string), typeof(Exception)]);
    private static readonly Dictionary<MethodInfo, MethodInfo> Cache = new();
    private static ModuleBuilder moduleBuilder;
    private static int counter;

    private static ModuleBuilder ModuleBuilder {
        get {
            if(moduleBuilder == null) {
                AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("BetterCalibration.PatchIsolation"), AssemblyBuilderAccess.Run);
                moduleBuilder = assemblyBuilder.DefineDynamicModule("BetterCalibration.PatchIsolation");
            }
            return moduleBuilder;
        }
    }


    public static MethodInfo Wrap(MethodInfo patch, string patchId) {
        lock(Cache) {
            if(Cache.TryGetValue(patch, out MethodInfo cached)) return cached;
            MethodInfo wrapper;
            try {
                wrapper = WrapInternal(patch, patchId);
            } catch(Exception e) {
                Main.Error("Failed to build an exception-isolating wrapper for '" + patchId + "', the patch will run unprotected", e);
                wrapper = patch;
            }
            
            Cache[patch] = wrapper;
            return wrapper;
        }
    }

    private static MethodInfo WrapInternal(MethodInfo patch, string patchId) {
        ParameterInfo[] parameters = patch.GetParameters();
        Type[] parameterTypes = parameters.Select(parameter => parameter.ParameterType).ToArray();
        Type returnType = patch.ReturnType;

        TypeBuilder typeBuilder = ModuleBuilder.DefineType("PatchWrapper" + counter++, TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Class);
        MethodBuilder methodBuilder = typeBuilder.DefineMethod(patch.Name, MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig, returnType, parameterTypes);
        for(int i = 0; i < parameters.Length; i++)
            methodBuilder.DefineParameter(i + 1, parameters[i].Attributes & ~ParameterAttributes.HasDefault, parameters[i].Name);

        ILGenerator il = methodBuilder.GetILGenerator();
        LocalBuilder result = returnType == typeof(void) ? null : il.DeclareLocal(returnType);
        LocalBuilder exception = il.DeclareLocal(typeof(Exception));

        il.BeginExceptionBlock();
        for(int i = 0; i < parameterTypes.Length; i++) il.Emit(OpCodes.Ldarg, i);
        il.Emit(OpCodes.Call, patch);
        if(result != null) il.Emit(OpCodes.Stloc, result);

        il.BeginCatchBlock(typeof(Exception));
        il.Emit(OpCodes.Stloc, exception);
        il.Emit(OpCodes.Ldstr, patchId);
        il.Emit(OpCodes.Ldloc, exception);
        il.Emit(OpCodes.Call, ReportMethod);
        if(returnType == typeof(bool)) {
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Stloc, result);
        }
        il.EndExceptionBlock();

        if(result == null) il.Emit(OpCodes.Ret);
        else {
            il.Emit(OpCodes.Ldloc, result);
            il.Emit(OpCodes.Ret);
        }
        return typeBuilder.CreateType().GetMethod(methodBuilder.Name, AccessTools.all);
    }
}
