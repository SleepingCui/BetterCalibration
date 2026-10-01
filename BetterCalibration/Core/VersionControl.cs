using System.Reflection;
using HarmonyLib;

namespace BetterCalibration.Core;


public static class VersionControl {
    public static readonly int releaseNumber;

    static VersionControl() {
        FieldInfo field = AccessTools.Field(typeof(GCNS), "releaseNumber")
                          ?? AccessTools.Field(typeof(ADOBase).Assembly.GetType("Releases"), "releaseNumber");
        releaseNumber = (int) field.GetValue(null);
    }
}
