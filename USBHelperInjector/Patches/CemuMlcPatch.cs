using HarmonyLib;
using System.Reflection;
using System.Text.RegularExpressions;

namespace USBHelperInjector.Patches
{
    [HarmonyPatch]
    class CemuMlcPatch
    {
        static MethodBase TargetMethod()
        {
            return ReflectionHelper.MainModule
                .GetType("NusHelper.Emulators.Cemu")
                .GetMethod("GenerateArguments", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        static void Postfix(ref string __result)
        {
            __result = Regex.Replace(__result, " -mlc \".*?\"", "");
        }
    }
}
