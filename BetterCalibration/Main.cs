using System;
using System.Collections.Generic;
using System.Reflection;
using BetterCalibration.Core;
using BetterCalibration.Features;
using HarmonyLib;
using SA.GoogleDoc;
using UnityEngine;
using UnityModManagerNet;

using Settings = BetterCalibration.Core.Settings;

namespace BetterCalibration;

public static class Main {
    public static UnityModManager.ModEntry ModEntry { get; private set; }
    public static readonly List<Feature> Features = [];
    public static string OffsetString;

    private static Patcher patcher;
    private static MethodInfo legacyRdStringGet;

    public static bool Load(UnityModManager.ModEntry modEntry) {
        ModEntry = modEntry;
        Settings.Load();
        Features.AddRange(new Feature[] {
            new CalibrationPopup(),
            new CalibrationDetail(),
            new CalibrationSong(),
            new TimingLogger(),
            new FloatOffset()
        });
        patcher = new Patcher(nameof(Main)).AddPatch(typeof(Main));
        modEntry.OnToggle = OnToggle;
        modEntry.OnGUI = OnGUI0;
        modEntry.OnHideGUI = _ => OffsetString = null;
        modEntry.OnSaveGUI = _ => Settings.Save();
        return true;
    }

    private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value) {
        if(value) {
            patcher.Patch();
            foreach(Feature feature in Features)
                if(feature.Enabled) feature.Enable();
        } else {
            foreach(Feature feature in Features) feature.Disable();
            patcher.Unpatch();
        }
        return true;
    }

    private static void OnGUI0(UnityModManager.ModEntry modEntry) {
        foreach(Feature feature in Features) feature.OnGUI0();
        OnGUIBehind();
    }

    private static void OnGUIBehind() {
        if(FloatOffset.Instance.Enabled) return;
        GUILayout.BeginHorizontal();
        GUILayout.Label(Lang.InputOffset);
        GUILayout.Space(4f);
        if(GUILayout.Button("-", GUILayout.Width(25))) {
            scrConductor.currentPreset.inputOffset--;
            scrConductor.SaveCurrentPreset();
            Persistence.WriteSaveToDisk();
        }
        int offset = scrConductor.currentPreset.inputOffset;
        if(string.IsNullOrEmpty(OffsetString) || !int.TryParse(OffsetString, out int i) || i != offset) OffsetString = offset.ToString();
        OffsetString = GUILayout.TextField(OffsetString);
        int resultInt;
        try {
            resultInt = string.IsNullOrEmpty(OffsetString) ? offset : int.TryParse(OffsetString, out i) ? i : offset;
        } catch(FormatException) {
            resultInt = offset;
        }
        if(resultInt != offset) {
            scrConductor.currentPreset.inputOffset = resultInt;
            scrConductor.SaveCurrentPreset();
            Persistence.WriteSaveToDisk();
        }
        GUILayout.Label("ms");
        if(GUILayout.Button("+", GUILayout.Width(25))) {
            scrConductor.currentPreset.inputOffset++;
            scrConductor.SaveCurrentPreset();
            Persistence.WriteSaveToDisk();
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    [JAPatch(typeof(SettingsMenu), nameof(SettingsMenu.Show), PatchType.Prefix, true)]
    public static void ShowSettingsMenu(PauseSettingButton ___offsetButton) {
        if(!___offsetButton) return;
        if(FloatOffset.Instance.Enabled) FloatOffset.Instance.SetOffsetSettingString(___offsetButton);
        else ___offsetButton.valueLabel.text = scrConductor.currentPreset.inputOffset + RdStringGet("editor.unit." + ___offsetButton.unit);
    }

    public static string RdStringGet(string key) {
        if(VersionControl.releaseNumber >= 141) return RDString.Get(key);
        legacyRdStringGet ??= AccessTools.Method(typeof(RDString), "Get", [typeof(string), typeof(Dictionary<string, object>), typeof(LangSection)]);
        return (string) legacyRdStringGet.Invoke(null, [key, null, LangSection.Translations]);
    }

    public static void Log(string message) => ModEntry?.Logger?.Log(message);

    public static void Warning(string message) => ModEntry?.Logger?.Warning(message);

    public static void Error(string message) => ModEntry?.Logger?.Error(message);

    public static void Error(string message, Exception exception) => ModEntry?.Logger?.LogException(message, exception);
}
