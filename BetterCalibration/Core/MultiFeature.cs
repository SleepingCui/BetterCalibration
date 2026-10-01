using System;
using System.Collections.Generic;

namespace BetterCalibration.Core;


public abstract class MultiFeature {
    private static readonly Dictionary<Type, MultiFeature> instances = new();
    private readonly HashSet<Feature> activeFeatures = [];
    protected readonly Patcher Patcher;

    protected MultiFeature() {
        Patcher = new Patcher(GetType().Name).AddPatch(GetType());
    }

    internal static MultiFeature Get(Type type) {
        if(instances.TryGetValue(type, out MultiFeature instance) && instance != null) return instance;
        return instances[type] = (MultiFeature) Activator.CreateInstance(type);
    }

    public void ActiveFeature(Feature feature) {
        if(activeFeatures.Count == 0) {
            Patcher.Patch();
            OnEnable();
        }
        activeFeatures.Add(feature);
    }

    public void InactiveFeature(Feature feature) {
        if(!activeFeatures.Remove(feature)) return;
        if(activeFeatures.Count != 0) return;
        Patcher.Unpatch();
        OnDisable();
    }

    protected virtual void OnEnable() {
    }

    protected virtual void OnDisable() {
    }
}
