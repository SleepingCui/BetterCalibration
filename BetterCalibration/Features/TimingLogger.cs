using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ADOFAI;
using BetterCalibration.Core;
using BetterCalibration.Features.Multi;
using MonsterLove.StateMachine;
using UnityEngine;

namespace BetterCalibration.Features;

public class TimingLogger : Feature {
    private static bool _logging;
    private static Dictionary<Hash, List<float>> _timings;
    private static long _lastUseTime;
    private static TimingLoggerSettings _settings;
    private string _maxTimings;
    private string _maxTimingsPerMap;
    private static readonly Hash AllHash = new([]);

    public TimingLogger() : base(nameof(TimingLogger), Lang.FeatureTimingLogger, true, typeof(TimingLogger), typeof(TimingLoggerSettings)) {
        _settings = (TimingLoggerSettings) SettingObject;
        AddMultiFeatures(typeof(Timing));
    }

    protected override void OnGUI() {
        SettingGUI.AddSettingInt(ref _settings.MaxTimings, 15, ref _maxTimings, Lang.TimingLoggerMaxTimings);
        SettingGUI.AddSettingInt(ref _settings.MaxTimingsPerMap, 5, ref _maxTimingsPerMap, Lang.TimingLoggerMaxTimingsPerMap);
        bool inGame = ADOBase.controller && ADOBase.controller.gameworld;
        List<float> mapTimings = !inGame ? null : GetTiming(GetMapHash());
#if DEBUG
        if(inGame) GUILayout.Label("Hash: " + GetMapHash());
#endif
        GUILayout.BeginHorizontal();
            bool curMapOffset = inGame && mapTimings.Count > 0 && mapTimings[0] != 0;
            GUILayout.Label(Lang.TimingLoggerPrevOffset + ": " +
                            (inGame ? !curMapOffset ? Lang.TimingLoggerNoTimings : mapTimings[0] + "" :
                                 Lang.TimingLoggerNotOpenMap));
            if(curMapOffset && GUILayout.Button(Lang.TimingLoggerSetTiming)) {
                if(FloatOffset.Instance.Enabled) {
                    FloatOffset.Instance.Offset = mapTimings[0];
                    return;
                }
                if(scrConductor.currentPreset.inputOffset != (int) mapTimings[0]) {
                    scrConductor.currentPreset.inputOffset = (int) mapTimings[0];
                    scrConductor.SaveCurrentPreset();
                    Persistence.WriteSaveToDisk();
                }
            }
            GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
                GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(Lang.TimingLoggerAllTimings);
                    GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                List<float> allTimings = GetTiming(AllHash);
                GUIContent buttonContent = new(Lang.TimingLoggerSetTiming);
                Vector2 buttonSize = GUI.skin.button.CalcSize(buttonContent);
                GUILayout.BeginVertical(new GUIStyle(GUI.skin.box));
                    if(allTimings.Count <= 1) {
                        GUILayout.BeginVertical();
                            GUILayout.Label(Lang.TimingLoggerNoTimings);
                        GUILayout.EndVertical();
                    } else {
                        GUILayout.BeginHorizontal();
                            GUILayout.BeginVertical();
                                foreach(float timing in allTimings.Skip(1)) GUILayout.Label(FloatOffset.Instance.Enabled ? timing.ToString("0.##")
                                                                                        : Mathf.RoundToInt(timing).ToString(), GUILayout.Height(buttonSize.y));
                            GUILayout.EndVertical();
                            GUILayout.FlexibleSpace();
                            GUILayout.BeginVertical();
                                foreach(float timing in allTimings.Skip(1).Where(_ => GUILayout.Button(buttonContent))) {
                                    if(FloatOffset.Instance.Enabled) {
                                        FloatOffset.Instance.Offset = timing;
                                        continue;
                                    }
                                    int roundedTiming = Mathf.RoundToInt(timing);
                                    if(scrConductor.currentPreset.inputOffset == roundedTiming) continue;
                                    scrConductor.currentPreset.inputOffset = roundedTiming;
                                    scrConductor.SaveCurrentPreset();
                                    Persistence.WriteSaveToDisk();
                                }
                            GUILayout.EndVertical();
                        GUILayout.EndHorizontal();
                    }
                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
                GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(Lang.TimingLoggerMapTimings);
                    GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.BeginVertical(new GUIStyle(GUI.skin.box));
                    if(!inGame) {
                        GUILayout.BeginVertical();
                            GUILayout.FlexibleSpace();
                            GUILayout.Label(Lang.TimingLoggerNotOpenMap);
                            GUILayout.FlexibleSpace();
                        GUILayout.EndVertical();
                    } else if(mapTimings.Count <= 1) {
                        GUILayout.BeginVertical();
                            GUILayout.FlexibleSpace();
                            GUILayout.Label(Lang.TimingLoggerNoTimings);
                            GUILayout.FlexibleSpace();
                        GUILayout.EndVertical();
                    } else {
                        GUILayout.BeginHorizontal();
                            GUILayout.BeginVertical();
                                foreach(float timing in mapTimings.Skip(1)) GUILayout.Label(FloatOffset.Instance.Enabled ? timing.ToString("0.##")
                                                                                        : Mathf.RoundToInt(timing).ToString(), GUILayout.Height(buttonSize.y));
                            GUILayout.EndVertical();
                            GUILayout.FlexibleSpace();
                            GUILayout.BeginVertical();
                                foreach(float timing in mapTimings.Skip(1).Where(_ => GUILayout.Button(buttonContent))) {
                                    if(FloatOffset.Instance.Enabled) {
                                        FloatOffset.Instance.Offset = timing;
                                        continue;
                                    }
                                    int roundedTiming = Mathf.RoundToInt(timing);
                                    if(scrConductor.currentPreset.inputOffset == roundedTiming) continue;
                                    scrConductor.currentPreset.inputOffset = roundedTiming;
                                    scrConductor.SaveCurrentPreset();
                                    Persistence.WriteSaveToDisk();
                                }
                            GUILayout.EndVertical();
                        GUILayout.EndHorizontal();
                    }
                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        GUILayout.EndHorizontal();
    }

    [JAPatch(typeof(StateBehaviour), "ChangeState", PatchType.Postfix, true, ArgumentTypesType = [typeof(Enum)])]
    public static void OnChangeState(Enum newState) {
        States states = (States) newState;
        if(states == States.Start) {
            _logging = false;
            GetTiming(GetMapHash())[0] = FloatOffset.Instance.Enabled ? FloatOffset.Instance.Offset : scrConductor.currentPreset.inputOffset;
        } else if(states != States.Fail2) LogTiming();
    }

    [JAPatch(typeof(scrController), "TogglePauseGame", PatchType.Postfix, true)]
    public static void LogTiming() {
        if(_logging || Timing.Timings.Count == 0) return;
        Hash mapHash = GetMapHash();
        float timing = scrConductor.currentPreset.inputOffset + Timing.GetTrimmedMeanTiming();
        AddTiming(mapHash, timing, _settings.MaxTimingsPerMap);
        AddTiming(AllHash, timing, _settings.MaxTimings);
        _logging = true;
    }

    private static Hash GetMapHash() {
        using MD5 md5 = MD5.Create();
        return md5.ComputeHash(ADOBase.isOfficialLevel ? Encoding.UTF8.GetBytes(ADOBase.currentLevel) : GetHash());
    }

    private static byte[] GetHash() {
        using MemoryStream memoryStream = new();
        scrLevelMaker lm = ADOBase.lm;
        if(lm.isOldLevel) BinaryIO.WriteString(memoryStream, lm.leveldata);
        else BinaryIO.WriteFloatList(memoryStream, lm.floorAngles);
        foreach(LevelEvent levelEvent in ADOBase.customLevel.events) {
            switch(levelEvent.eventType) {
                case LevelEventType.SetSpeed:
                    BinaryIO.WriteInt(memoryStream, levelEvent.floor);
                    memoryStream.WriteByte(0);
                    memoryStream.WriteByte((byte) (SpeedType) levelEvent["speedType"]);
                    // ReSharper disable once PossibleInvalidCastException
                    BinaryIO.WriteFloat(memoryStream, (float) levelEvent[(SpeedType) levelEvent["speedType"] == SpeedType.Bpm ? "beatsPerMinute" : "bpmMultiplier"]);
                    break;
                case LevelEventType.Twirl:
                    BinaryIO.WriteInt(memoryStream, levelEvent.floor);
                    memoryStream.WriteByte(1);
                    break;
                case LevelEventType.Hold:
                    BinaryIO.WriteInt(memoryStream, levelEvent.floor);
                    memoryStream.WriteByte(2);
                    BinaryIO.WriteInt(memoryStream, (int) levelEvent["duration"]);
                    break;
                case LevelEventType.MultiPlanet:
                    BinaryIO.WriteInt(memoryStream, levelEvent.floor);
                    memoryStream.WriteByte(3);
                    memoryStream.WriteByte((byte) (PlanetCount) levelEvent["planets"]);
                    break;
                case LevelEventType.Pause:
                    BinaryIO.WriteInt(memoryStream, levelEvent.floor);
                    memoryStream.WriteByte(4);
                    BinaryIO.WriteFloat(memoryStream, (float) levelEvent["duration"]);
                    break;
                case LevelEventType.AutoPlayTiles:
                    BinaryIO.WriteInt(memoryStream, levelEvent.floor);
                    memoryStream.WriteByte(5);
                    BinaryIO.WriteBool(memoryStream, (bool) levelEvent["enabled"]);
                    break;
                case LevelEventType.ScaleMargin:
                    BinaryIO.WriteInt(memoryStream, levelEvent.floor);
                    memoryStream.WriteByte(6);
                    BinaryIO.WriteFloat(memoryStream, (float) levelEvent["scale"]);
                    break;
                case LevelEventType.Multitap:
                    BinaryIO.WriteInt(memoryStream, levelEvent.floor);
                    memoryStream.WriteByte(7);
                    BinaryIO.WriteFloat(memoryStream, (float) levelEvent["taps"]);
                    break;
                case LevelEventType.KillPlayer:
                    BinaryIO.WriteInt(memoryStream, levelEvent.floor);
                    memoryStream.WriteByte(8);
                    break;
            }
        }
        return memoryStream.ToArray();
    }

    public static Dictionary<Hash, List<float>> GetTimings() {
        if(_timings == null) {
            _timings = new Dictionary<Hash, List<float>>();
            string path = Path.Combine(Main.ModEntry.Path, "Timings.dat");
            if(File.Exists(path)) {
                try {
                    GetTimings(path);
                    goto WorkEnd;
                } catch (Exception e) {
                    Main.Error("Failed to load timings", e);
                }
            }
            path += ".bak";
            if(File.Exists(path)) {
                try {
                    _timings = new Dictionary<Hash, List<float>>();
                    GetTimings(path);
                    goto WorkEnd;
                } catch (Exception e) {
                    Main.Error("Failed to load backup timings", e);
                }
            }
            _timings = new Dictionary<Hash, List<float>>();
WorkEnd:
            Deleter();
        }
        _lastUseTime = DateTime.Now.Ticks;
        return _timings;
    }

    private static void GetTimings(string path) {
        using FileStream fileStream = File.OpenRead(path);
        _timings[AllHash] = BinaryIO.ReadFloatList(fileStream);
        int count = BinaryIO.ReadInt(fileStream);
        for(int i = 0; i < count; i++) {
            Hash key = BinaryIO.ReadBytes(fileStream, 16);
            _timings[key] = BinaryIO.ReadFloatList(fileStream);
        }
    }

    private static async void Deleter() {
        await Task.Delay(60000);
        long currentTime = DateTime.Now.Ticks;
        while(currentTime - _lastUseTime < 600000000) {
            await Task.Delay((int) (60000 - (currentTime - _lastUseTime) / 10000));
            currentTime = DateTime.Now.Ticks;
        }
        _timings = null;
    }

    public static void AddTiming(Hash mapHash, float timing, int maxTiming) {
        _timings = GetTimings();
        List<float> timings;
        if(!_timings.TryGetValue(mapHash, out List<float> timing1)) timings = _timings[mapHash] = [0f];
        else timings = timing1;
        timings.Add(timing);
        if(timings.Count > maxTiming + 1) timings.RemoveAt(1);
        SaveTiming();
    }

    public static List<float> GetTiming(Hash mapHash) {
        return GetTimings().TryGetValue(mapHash, out List<float> timing) ? timing : [0f];
    }

    public static void SaveTiming() {
        string path = Path.Combine(Main.ModEntry.Path, "Timings.dat");
        if(File.Exists(path)) File.Copy(path, path + ".bak", true);
        using FileStream fileStream = File.OpenWrite(path);
        BinaryIO.WriteFloatList(fileStream, GetTiming(AllHash));
        BinaryIO.WriteInt(fileStream, _timings.Count - 1);
        foreach(KeyValuePair<Hash, List<float>> valuePair in _timings.Where(valuePair => valuePair.Key != AllHash)) {
            fileStream.Write(valuePair.Key.Data, 0, valuePair.Key.Data.Length);
            BinaryIO.WriteFloatList(fileStream, valuePair.Value);
        }
    }

    private class TimingLoggerSettings {
        public int MaxTimings = 15;
        public int MaxTimingsPerMap = 5;
    }

    public readonly struct Hash(byte[] data) : IEquatable<Hash> {
        public readonly byte[] Data = data;

        public override bool Equals(object obj) => obj is Hash hash ? Equals(hash) : obj is byte[] bytes && Equals(bytes);
        public bool Equals(Hash other) => Equals(other.Data);
        public bool Equals(byte[] hash) {
            if(Data.Length != hash.Length) return false;
            return Data.Length == hash.Length && !Data.Where((t, i) => t != hash[i]).Any();
        }
        public override int GetHashCode() => Data != null ? ToString().GetHashCode() : 0;

        public static bool operator ==(Hash left, Hash right) => left.Equals(right);
        public static bool operator !=(Hash left, Hash right) => !(left == right);

        public static implicit operator Hash(byte[] hash) => new(hash);
        public static implicit operator byte[](Hash hash) => hash.Data;

        public override string ToString() {
            StringBuilder builder = new(Data.Length * 2);
            foreach(byte b in Data) builder.Append(b.ToString("x2"));
            return builder.ToString();
        }
    }
}
