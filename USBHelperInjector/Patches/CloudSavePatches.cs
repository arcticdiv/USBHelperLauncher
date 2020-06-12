using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Reflection.Emit;
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
        private static readonly object BACKEND_LABEL_TAG = new object();

        static MethodBase TargetMethod()
        {
            return ReflectionHelper.GetInitializeComponentMethod(ReflectionHelper.FrmCloudSaving);
        }

        static void Postfix(Form __instance)
        {
            var groupBox = FindGroupBox(__instance);
            var controlList = __instance.Controls.Cast<Control>().ToList();


            // Create dropdown menu
            var dropdown = (Control)Activator.CreateInstance(ReflectionHelper.TelerikUI.RadDropDownList);
            AccessTools.Property(dropdown.GetType(), "DropDownStyle").SetValue(dropdown, 2); // RadDropDownStyle.DropDownList

            // Fix placement of UI elements
            var startX = controlList.OfType<PictureBox>().First().Location.X;
            dropdown.Location = new Point(startX, groupBox.Location.Y);
            foreach (var control in controlList.Where(c => c.Location.Y >= dropdown.Location.Y))
            {
                control.Location = new Point(control.Location.X, control.Location.Y + dropdown.Height + 8);
            }
            __instance.Height += dropdown.Height + 8;

            // Add items
            var items = (IList)AccessTools.Property(dropdown.GetType(), "Items").GetValue(dropdown);
            foreach (var backend in Enum.GetValues(typeof(CloudSaveBackendType)).Cast<CloudSaveBackendType>())
            {
                items.Add(Activator.CreateInstance(ReflectionHelper.TelerikUI.RadListDataItem, backend.Description(), backend));
            }
            AccessTools.Property(dropdown.GetType(), "SelectedValue").SetValue(dropdown, InjectorService.CloudSaveBackend);

            // Add event handler
            var eventInfo = dropdown.GetType().GetEvent("SelectedIndexChanged");
            var del = Delegate.CreateDelegate(
                eventInfo.EventHandlerType,
                AccessTools.Method(typeof(CloudSaveLoginFormPatch), nameof(DropdownHandler))
            );
            eventInfo.AddEventHandler(dropdown, del);
            __instance.Controls.Add(dropdown);


            // Add new label
            var backendLabel = (Control)Activator.CreateInstance(ReflectionHelper.TelerikUI.RadLabel);
            backendLabel.AutoSize = false;
            backendLabel.Dock = DockStyle.Fill;
            backendLabel.GetType().GetProperty("TextAlignment").SetValue(backendLabel, ContentAlignment.MiddleCenter);
            backendLabel.Tag = BACKEND_LABEL_TAG;
            groupBox.Controls.Add(backendLabel);

            // Show/hide controls
            UpdateControls(__instance, InjectorService.CloudSaveBackend);
        }

        private static void DropdownHandler(object sender, EventArgs args)
        {
            var dropdown = (Control)sender;
            var frmCloudSave = dropdown.FindForm();
            var selected = (CloudSaveBackendType)AccessTools.Property(dropdown.GetType(), "SelectedValue").GetValue(dropdown);

            // Save new value, update UI
            InjectorService.CloudSaveBackend = selected;
            InjectorService.LauncherService.SetCloudSaveBackend(selected);
            UpdateControls(frmCloudSave, selected);

            MessageBox.Show(
                "Wii U USB Helper will always overwrite local save data with save data from the cloud (provided that save data has previously been uploaded).\nIf you've just switched back to a cloud save backend you've used before, you might want to delete the cloud saves first before launching a game, as your save files might get overwritten with older ones otherwise.",
                "Warning",
                MessageBoxButtons.OK, MessageBoxIcon.Warning
            );

            // Call login check method
            var loginMethod = CloudSaveEmptyInputPatch.TargetMethod();
            loginMethod.Invoke(frmCloudSave, null);
        }

        private static Control FindGroupBox(Form form)
        {
            return (from control in form.Controls.Cast<Control>()
                    where control.GetType() == ReflectionHelper.TelerikUI.RadGroupBox
                    select control).FirstOrDefault();
        }

        private static void UpdateControls(Form form, CloudSaveBackendType backendType)
        {
            var groupBox = FindGroupBox(form);
            var backendLabel = groupBox.Controls.Cast<Control>().First(c => c.Tag == BACKEND_LABEL_TAG);

            // Gather default login controls, except new backend label and original login label+button
            var defaultControls = (from control in groupBox.Controls.Cast<Control>()
                                   orderby control.Location.Y
                                   select control).ToList();
            defaultControls.Remove(backendLabel);
            defaultControls.Remove(defaultControls.FindLast(c => c.GetType() == ReflectionHelper.TelerikUI.RadLabel));
            var loginButton = defaultControls.FindLast(c => c.GetType() == ReflectionHelper.TelerikUI.RadButton);
            defaultControls.Remove(loginButton);

            backendLabel.Text = $"Currently using {backendType.Description()} backend\n";
            var isUSBHelperBackend = backendType == CloudSaveBackendType.USBHelper;
            defaultControls.ForEach(c => c.Visible = isUSBHelperBackend);
            backendLabel.Visible = !isUSBHelperBackend;

            loginButton.Text = isUSBHelperBackend ? "Log In/Register" : "Authorize";
        }
    }

    [Optional]
    [HarmonyPatch]
    class CloudSaveLoginButtonPatch
    {
        static MethodBase TargetMethod()
        {
            var loginMethod = CloudSaveEmptyInputPatch.TargetMethod();
            // v0.6.1.655: frmCloudSaving.radButton1_Click
            return (from method in AccessTools.GetDeclaredMethods(ReflectionHelper.FrmCloudSaving)
                    where method.GetParameters().Select(p => p.ParameterType)
                        .SequenceEqual(new[] { typeof(object), typeof(EventArgs) })
                    let instructions = PatchProcessor.GetOriginalInstructions(method, out _)
                    where instructions.Any(i => i.opcode == OpCodes.Call && (MethodInfo)i.operand == loginMethod)
                    select method).FirstOrDefault();
        }

        static bool Prefix(Form __instance)
        {
            if (InjectorService.CloudSaveBackend == CloudSaveBackendType.USBHelper)
            {
                return true;
            }
            if (InjectorService.CloudSaveBackend == CloudSaveBackendType.Local)
            {
                var dialog = new FolderBrowserDialog();
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    InjectorService.LauncherService.SetLocalCloudSavePath(dialog.SelectedPath);
                }
            }

            // Authorize without blocking UI event loop
            using (var client = new WebClient())
            {
                client.UploadStringAsync(new Uri("https://cloud.wiiuusbhelper.com/saves/authorize.php"), "");
            }
            __instance.Close();
            return false;
        }
    }

    [Optional]
    [HarmonyPatch]
    class CloudSaveEmptyInputPatch
    {
        internal static MethodBase TargetMethod()
        {
            // v0.6.1.655: frmCloudSaving.method_0
            return (from method in AccessTools.GetDeclaredMethods(ReflectionHelper.FrmCloudSaving)
                    where method.GetParameters().Length == 0
                       && method.GetMethodBody().LocalVariables
                              .Select(l => l.LocalType)
                              .SequenceEqual(new[] { typeof(bool) })
                    select method).FirstOrDefault();
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var isNullOrEmptyMethod = typeof(string).GetMethod("IsNullOrEmpty");
            var checkCloudBackend = new List<CodeInstruction>
            {
                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(InjectorService), nameof(InjectorService.CloudSaveBackend))),
                new CodeInstruction(OpCodes.Ldc_I4, (int)CloudSaveBackendType.USBHelper),
                new CodeInstruction(OpCodes.Ceq),
                new CodeInstruction(OpCodes.And)
            };

            // append to first two occurrences of `string.IsNullOrEmpty`
            for (int i = 0, currIndex = -1; i < 2; i++)
            {
                currIndex = codes.FindIndex(
                    currIndex + 1,
                    instr => instr.opcode == OpCodes.Call && (MethodBase)instr.operand == isNullOrEmptyMethod
                );
                codes.InsertRange(currIndex + 1, checkCloudBackend);
            }

            return codes;
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
