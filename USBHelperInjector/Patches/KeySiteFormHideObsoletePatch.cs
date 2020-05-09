using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace USBHelperInjector.Patches
{
    [HarmonyPatch]
    class KeySiteFormHideObsoletePatch
    {
        internal static MethodBase TargetMethod()
        {
            return (from method in ReflectionHelper.FrmAskTicket.Type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    where method.GetParameters().Count() == 0
                    select method).FirstOrDefault();
        }

        static void Postfix(object __instance)
        {
            var textBoxWiiU = (Control)ReflectionHelper.FrmAskTicket.TextBoxes[0].GetValue(__instance);
            var siblings = textBoxWiiU.Parent.Controls.Cast<Control>();
            var assembly = textBoxWiiU.GetType().Assembly;

            foreach (var c in siblings)
            {
                if (c.Location.Y > textBoxWiiU.Location.Y && c.GetType() != ReflectionHelper.TelerikUI.RadButton)
                {
                    c.Visible = false;
                }
            }

            var largestLabel = siblings.Where(c => c.GetType() == ReflectionHelper.TelerikUI.RadLabel).OrderBy(c => c.Text.Length).Last();
            if (largestLabel.Text.Length > 30) // make sure to modify the correct label, it may not exist on older versions
            {
                largestLabel.Visible = false;
            }
        }
    }
}
