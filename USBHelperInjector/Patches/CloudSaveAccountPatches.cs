using System;
using HarmonyLib;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using USBHelperInjector.Contracts;
using USBHelperInjector.Patches.Attributes;

namespace USBHelperInjector.Patches
{
    [Optional]
    [HarmonyPatch]
    class CloudSaveLoginFormPatch
    {
        static MethodBase TargetMethod()
        {
            return ReflectionHelper.GetInitializeComponentMethod(ReflectionHelper.FrmCloudSaving);
        }

        static void Postfix(Form __instance)
        {
            if (InjectorService.CloudSaveBackend == CloudSaveBackendType.USBHelper)
            {
                return;
            }

            var controls = (from field in __instance.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                            where typeof(Control).IsAssignableFrom(field.FieldType)
                            select (Control)field.GetValue(__instance)).ToList();
            var groupBox = controls.First(c => c.GetType() == ReflectionHelper.TelerikUI.RadGroupBox);

            // move loggedIn label out of groupBox
            var loggedIn = (from control in groupBox.Controls.Cast<Control>()
                            where control.GetType() == ReflectionHelper.TelerikUI.RadLabel
                            orderby control.Location.Y descending
                            select control).First();
            groupBox.Controls.Remove(loggedIn);
            loggedIn.Location += (Size)groupBox.Location;
            __instance.Controls.Add(loggedIn);

            // remove GroupBox, replace with new label
            __instance.Controls.Remove(groupBox);
            var backendLabel = (Control)Activator.CreateInstance(ReflectionHelper.TelerikUI.RadLabel);
            backendLabel.Text = $"Currently using {InjectorService.CloudSaveBackend.Description()} backend";
            backendLabel.Location = new Point(__instance.Size.Width / 2, __instance.Size.Height / 2);
            backendLabel.AutoSize = false;
            backendLabel.Dock = DockStyle.Fill;
            backendLabel.GetType().GetProperty("TextAlignment").SetValue(backendLabel, ContentAlignment.MiddleCenter);
            __instance.Controls.Add(backendLabel);
        }
    }

    // Restrict usernames to [A-Za-z0-9_-], disallow spaces in passwords
    [Optional]
    [HarmonyPatch]
    class CloudSaveInputFormatTextChangingPatch
    {
        static MethodBase TargetMethod()
        {
            // v0.6.1.655: frmCloudSaving.txtPassword_TextChanging
            return (from method in ReflectionHelper.FrmCloudSaving.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
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
            return (from method in ReflectionHelper.FrmCloudSaving.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
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
