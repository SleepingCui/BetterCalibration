using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace BetterCalibration.Core;


public abstract class Feature {
    private static GUIStyle expandStyle;
    private static GUIStyle enableStyle;
    private static GUIStyle enableLabelStyle;

    private readonly bool canExpand;
    private readonly List<MultiFeature> multiFeatures = [];
    private bool expanded;
    private bool patchFailed;
    private byte critical;

    public string Name { get; }
    public string DisplayName { get; }
    public bool CanEnable { get; }
    public bool Active { get; private set; }
    protected readonly Settings.FeatureData Data;
    protected readonly Patcher Patcher;
    protected readonly object SettingObject;

    public bool Enabled {
        get => Data.Enabled;
        set {
            if(Data.Enabled == value) return;
            Data.Enabled = value;
            Settings.Save();
            if(value) Enable();
            else Disable();
        }
    }

    protected Feature(string name, string displayName, bool canEnable, Type patchClass = null, Type settingType = null) {
        Name = name;
        DisplayName = displayName;
        CanEnable = canEnable;
        Data = Settings.Instance.GetFeature(name);
        if(settingType != null)
            SettingObject = Data.Setting == null ? Activator.CreateInstance(settingType, true) : Data.Setting.ToObject(settingType);
        Patcher = new Patcher(name);
        Patcher.OnFatalPatchFailure += OnFatalPatchFailure;
        if(patchClass != null) Patcher.AddPatch(patchClass);
        canExpand = GetType().GetMethod(nameof(OnGUI), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.DeclaringType != typeof(Feature);
    }

    private void OnFatalPatchFailure(string patchId) {
        patchFailed = true;
        Data.Enabled = false;
        Settings.Save();
        Main.Error("Feature '" + Name + "' was disabled because patch '" + patchId + "' failed");
    }

    internal void Enable() {
        if(Active) return;
        patchFailed = false;
        Patcher.Patch();
        if(patchFailed) return;
        Active = true;
        try {
            OnEnable();
            foreach(MultiFeature multiFeature in multiFeatures) multiFeature.ActiveFeature(this);
        } catch(Exception e) {
            Main.Error("Failed to enable feature '" + Name + "'", e);
            Disable();
        }
    }

    internal void Disable() {
        if(!Active) return;
        Active = false;
        Patcher.Unpatch();
        try {
            OnDisable();
            foreach(MultiFeature multiFeature in multiFeatures) multiFeature.InactiveFeature(this);
        } catch(Exception e) {
            Main.Error("Failed to disable feature '" + Name + "'", e);
        }
    }

    protected virtual void OnEnable() {
    }

    protected virtual void OnDisable() {
    }

    protected virtual void OnGUI() {
    }

    protected void AddMultiFeatures(params Type[] multiFeatureTypes) {
        foreach(Type type in multiFeatureTypes) multiFeatures.Add(MultiFeature.Get(type));
    }


    internal void CaptureSetting() {
        if(SettingObject != null) Data.Setting = Newtonsoft.Json.Linq.JObject.FromObject(SettingObject);
    }

    internal void OnGUI0() {
        expandStyle ??= new GUIStyle {
            fixedWidth = 10f,
            normal = new GUIStyleState { textColor = Color.white },
            fontSize = 15,
            margin = new RectOffset(4, 2, 6, 6)
        };
        enableStyle ??= new GUIStyle(GUI.skin.toggle) {
            fontStyle = FontStyle.Normal,
            margin = new RectOffset(0, 4, 4, 4)
        };
        enableLabelStyle ??= new GUIStyle(GUI.skin.label) {
            fontStyle = FontStyle.Normal,
            margin = new RectOffset(4, 4, 4, 4)
        };
        GUILayout.BeginHorizontal();
        bool enabled, expandedNext;
        try {
            expandedNext = GUILayout.Toggle(expanded, Enabled && canExpand ? expanded ? "◢" : "▶" : "", expandStyle);
            if(!CanEnable) {
                enabled = Enabled;
                GUILayout.Space(15f);
                GUILayout.Label(DisplayName, enableLabelStyle);
            } else enabled = GUILayout.Toggle(Enabled, DisplayName, enableStyle);
        } finally {
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        if(enabled != Enabled) {
            Enabled = enabled;
            if(enabled && canExpand) expandedNext = true;
        }
        expanded = expandedNext;
        if(!expanded || !Enabled) return;
        GUILayout.BeginHorizontal();
        GUILayout.Space(24f);
        GUILayout.BeginVertical();
        try {
            OnGUI();
            critical = 0;
        } catch(Exception e) {
            Main.Error("Error in OnGUI of feature '" + Name + "'", e);
            if(++critical > 3) expanded = false;
        }
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        GUILayout.Space(12f);
    }
}
