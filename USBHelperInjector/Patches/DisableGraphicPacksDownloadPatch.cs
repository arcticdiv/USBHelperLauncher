using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using USBHelperInjector.Patches.Attributes;

namespace USBHelperInjector.Patches
{
    [Optional]
    [HarmonyPatch]
    class DisableGraphicPacksDownloadPatch
    {
        static MethodBase TargetMethod()
        {
            // v0.6.1.655: GClass2.smethod_0
            return (from type in ReflectionHelper.Types
                    where type.IsPublic && type.IsAbstract && type.IsSealed  // = public static
                    let methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                    where methods.Length == 1
                    select methods[0]).FirstOrDefault();
        }

        static bool Prefix()
        {
            return false;
        }
    }
}
