using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BetterCalibration.Core;


public class Settings {
    public static Settings Instance { get; private set; }

    public Dictionary<string, FeatureData> Feature { get; set; } = new();

    private static string FilePath => Path.Combine(Main.ModEntry.Path, "Settings.json");

    public static void Load() {
        try {
            Instance = File.Exists(FilePath)
                ? JsonConvert.DeserializeObject<Settings>(File.ReadAllText(FilePath)) ?? new Settings()
                : new Settings();
        } catch(Exception e) {
            Main.Error("Failed to load settings, falling back to defaults", e);
            Instance = new Settings();
        }
    }

    public static void Save() {
        if(Instance == null) return;
        try {
            foreach(Feature feature in Main.Features) feature.CaptureSetting();
            string path = FilePath;
            if(File.Exists(path)) File.Copy(path, path + ".bak", true);
            File.WriteAllText(path, JsonConvert.SerializeObject(Instance, Formatting.Indented));
        } catch(Exception e) {
            Main.Error("Failed to save settings", e);
        }
    }


    public FeatureData GetFeature(string name) {
        if(Feature.TryGetValue(name, out FeatureData data) && data != null) return data;
        return Feature[name] = new FeatureData();
    }

    public class FeatureData {
        public bool Enabled = true;
        public JObject Setting;
    }
}
