using HarmonyLib;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using USBHelperInjector.Patches.Attributes;

namespace USBHelperInjector.Patches
{
    // Restrict usernames to [A-Za-z0-9_-], disallow spaces in passwords
    [Optional]
    [HarmonyPatch]
    class CloudSaveInputFormatTextChangingPatch
    {
        internal static MethodBase TargetMethod()
        {
            // v0.6.1.655: frmCloudSaving.txtPassword_TextChanging
            return (from type in ReflectionHelper.Types
                    where typeof(Form).IsAssignableFrom(type)
                       && type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                              .Any(f => f.FieldType == ReflectionHelper.TelerikUI.RadToggleSwitch)
                    from method in type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                    let parameters = method.GetParameters().Select(p => p.ParameterType).ToList()
                    where parameters.Count == 2 && parameters[1].Name == "TextChangingEventArgs"
                    select method).FirstOrDefault();
        }

        static bool Prefix(Control __0, CancelEventArgs __1)
        {
            var newValue = (string)AccessTools.Property(__1.GetType(), "NewValue").GetValue(__1);
            if (__0.GetType() == ReflectionHelper.TelerikUI.RadTextBox && !Regex.IsMatch(newValue, "^[A-Za-z0-9_-]{0,63}$")
                || __0.GetType() == ReflectionHelper.TelerikUI.RadTextBoxControl && newValue.Contains(" "))
            {
                __1.Cancel = true;
            }
            return false;
        }
    }

    // Disables KeyDown handler for username/password fields
    [Optional]
    [HarmonyPatch]
    class CloudSaveInputFormatKeyDownPatch
    {
        static MethodBase TargetMethod()
        {
            // v0.6.1.655: frmCloudSaving.txtPassword_KeyDown
            return (from method in CloudSaveInputFormatTextChangingPatch.TargetMethod().DeclaringType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                    where method.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(object), typeof(KeyEventArgs) })
                    && method.GetMethodBody().GetILAsByteArray().Length > 1
                    select method).FirstOrDefault();
        }

        static bool Prefix()
        {
            return false;
        }
    }
}
