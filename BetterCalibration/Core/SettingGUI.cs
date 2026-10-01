using System;
using UnityEngine;

namespace BetterCalibration.Core;


public static class SettingGUI {
    public static void AddSettingFloat(ref float value, float defaultValue, ref string valueString, string text,
                                       float min = float.MinValue, float max = float.MaxValue) {
        GUILayout.BeginHorizontal();
        GUILayout.Label(text);
        GUILayout.Space(4f);
        string original = valueString ??= value.ToString();
        string result = GUILayout.TextField(original);
        if(result != original) {
            float resultFloat;
            try {
                resultFloat = string.IsNullOrEmpty(valueString) ? defaultValue : float.Parse(valueString);
                if(resultFloat < min) {
                    resultFloat = min;
                    valueString = min.ToString();
                } else if(resultFloat > max) {
                    resultFloat = max;
                    valueString = max.ToString();
                } else {
                    valueString = result;
                }
            } catch(FormatException) {
                resultFloat = defaultValue;
                valueString = defaultValue.ToString();
            }
            if(resultFloat != value) {
                value = resultFloat;
                Settings.Save();
            }
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    public static void AddSettingInt(ref int value, int defaultValue, ref string valueString, string text,
                                     int min = int.MinValue, int max = int.MaxValue) {
        GUILayout.BeginHorizontal();
        GUILayout.Label(text);
        GUILayout.Space(4f);
        string original = valueString ??= value.ToString();
        string result = GUILayout.TextField(original);
        if(result != original) {
            int resultInt;
            try {
                resultInt = string.IsNullOrEmpty(valueString) ? defaultValue : int.Parse(valueString);
                if(resultInt < min) {
                    resultInt = min;
                    valueString = min.ToString();
                } else if(resultInt > max) {
                    resultInt = max;
                    valueString = max.ToString();
                } else {
                    valueString = result;
                }
            } catch(FormatException) {
                resultInt = defaultValue;
                valueString = defaultValue.ToString();
            }
            if(resultInt != value) {
                value = resultInt;
                Settings.Save();
            }
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    public static void AddSettingToggleInt(ref int value, int defaultValue, ref bool value2, ref string valueString, string text,
                                           int min = int.MinValue, int max = int.MaxValue) {
        GUILayout.BeginHorizontal();
        if(GUILayout.Toggle(value2, text)) {
            if(!value2) {
                value2 = true;
                Settings.Save();
            }
            GUILayout.Space(4f);
            string original = valueString ??= value.ToString();
            string result = GUILayout.TextField(original);
            if(result != original) {
                int resultInt;
                try {
                    resultInt = string.IsNullOrEmpty(valueString) ? defaultValue : int.Parse(valueString);
                    if(resultInt < min) {
                        resultInt = min;
                        valueString = min.ToString();
                    } else if(resultInt > max) {
                        resultInt = max;
                        valueString = max.ToString();
                    } else {
                        valueString = result;
                    }
                } catch(FormatException) {
                    resultInt = defaultValue;
                    valueString = defaultValue.ToString();
                }
                if(resultInt != value) {
                    value = resultInt;
                    Settings.Save();
                }
            }
        } else if(value2) {
            value2 = false;
            Settings.Save();
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }
}
