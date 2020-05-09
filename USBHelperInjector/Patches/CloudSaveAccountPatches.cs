using HarmonyLib;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using USBHelperInjector.Patches.Attributes;

namespace USBHelperInjector.Patches
{
    [Optional]
    [HarmonyPatch]
    class CloudSaveChangePasswordPatch
    {
        internal static MethodBase TargetMethod()
        {
            // v0.6.1.655: frmCloudSaving.InitializeComponent
            Type t = (from type in ReflectionHelper.Types
                      where typeof(Form).IsAssignableFrom(type)
                      from field in type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                      where field.FieldType == ReflectionHelper.TelerikUI.RadToggleSwitch
                      select type).FirstOrDefault();
            return ReflectionHelper.GetInitializeComponentMethod(t);
        }

        static void Postfix(Form __instance)
        {
            var controls = (from field in __instance.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                            where typeof(Control).IsAssignableFrom(field.FieldType)
                            select (Control)field.GetValue(__instance));
            var bottom = controls.OrderByDescending(c => c.Location.Y + c.Size.Height).Take(3).OrderBy(c => c.Location.X).ToList();
            var enableLabel = bottom[0];
            var enableSwitch = bottom[1];
            var manageButton = bottom[2];


            // calculate positions/sizes
            var edgeTop = manageButton.Location.Y + manageButton.Size.Height + 4;
            var edgeLeft = enableLabel.Location.X;
            var edgeRight = manageButton.Location.X + manageButton.Size.Width;
            var totalWidth = (manageButton.Location.X + manageButton.Size.Width) - enableLabel.Location.X;
            var buttonSize = new Size((totalWidth - 8) / 2, manageButton.Size.Height);
            var centerX = edgeLeft + totalWidth / 2;

            // create new button
            var radButtonType = ReflectionHelper.TelerikUI.RadButton;
            var icon = (Image)ReflectionHelper.CommonResources.GetProperty("icnGearMini", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

            var changePasswordButton = (Control)Activator.CreateInstance(radButtonType);
            changePasswordButton.Location = new Point(edgeRight - buttonSize.Width, edgeTop);
            changePasswordButton.Size = buttonSize;
            changePasswordButton.Text = "Change Password";
            changePasswordButton.TabIndex = manageButton.TabIndex + 1;
            changePasswordButton.Enabled = false;
            changePasswordButton.Click += ChangePassword_Click;
            radButtonType.GetProperty("Image").SetValue(changePasswordButton, icon);
            __instance.Controls.Add(changePasswordButton);

            var passwordTextBox = controls.Where(c => ReflectionHelper.TelerikUI.RadTextBoxControl.IsAssignableFrom(c.GetType())).FirstOrDefault();
            changePasswordButton.Tag = passwordTextBox;

            manageButton.EnabledChanged += delegate (object sender, EventArgs e)
            {
                changePasswordButton.Enabled = manageButton.Enabled;
            };

            // move/resize existing controls
            manageButton.Location = new Point(edgeLeft, edgeTop);
            manageButton.Size = buttonSize;

            var diff = centerX - enableSwitch.Location.X;
            enableLabel.Location = new Point(enableLabel.Location.X + diff, enableLabel.Location.Y);
            enableSwitch.Location = new Point(enableSwitch.Location.X + diff, enableSwitch.Location.Y);

            // resize enclosing form
            __instance.ClientSize = new Size(__instance.ClientSize.Width, __instance.ClientSize.Height + buttonSize.Height + 8);
        }

        private static void ChangePassword_Click(object sender, EventArgs e)
        {
            // get new RadMessageBox
            var boxType = ReflectionHelper.TelerikUI.RadMessageBox;
            var form = (Form)boxType.GetProperty("Instance").GetValue(null);

            try
            {
                // create textbox
                var textBox = (Control)Activator.CreateInstance(ReflectionHelper.TelerikUI.RadTextBoxControl);
                textBox.Size = new Size(200, 20);
                textBox.Location = new Point(135, 4);
                textBox.GetType().GetProperty("PasswordChar").SetValue(textBox, '*');

                var passwordTextBox = (Control)((Control)sender).Tag;
                var textChangingField = textBox.GetType().GetField("TextChanging", BindingFlags.NonPublic | BindingFlags.Instance);
                textChangingField.SetValue(textBox, textChangingField.GetValue(passwordTextBox));

                // add textbox to messagebox
                form.Controls.Add(textBox);
                form.MinimumSize = new Size(360, 90);

                // show messagebox
                var showMethod = boxType.GetMethod("Show", new[] { typeof(string), typeof(string), typeof(MessageBoxButtons) });
                var result = (DialogResult)showMethod.Invoke(null, new object[] { "Enter a new password:", "Change Password", MessageBoxButtons.OKCancel });

                if (result == DialogResult.OK)
                {
                    var settings = (ApplicationSettingsBase)ReflectionHelper.Settings.GetProperty("Default").GetValue(null);
                    var username = (string)settings["CloudUserName"];
                    var password = (string)settings["CloudPassWord"];
                    var reqparams = new NameValueCollection()
                    {
                        { "username", username },
                        { "password", password },
                        { "newpassword", textBox.Text }
                    };

                    using (WebClient client = new WebClient())
                    {
                        client.UploadValues("https://cloud.wiiuusbhelper.com/saves/change_password.php", reqparams);
                    }

                    passwordTextBox.Text = textBox.Text;
                    settings["CloudPassWord"] = textBox.Text;
                    settings.Save();
                }
            }
            finally
            {
                form.Dispose();
            }
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
            return (from method in CloudSaveChangePasswordPatch.TargetMethod().DeclaringType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                    let parameters = method.GetParameters().Select(p => p.ParameterType).ToList()
                    where parameters.Count == 2
                    && parameters[1].Name == "TextChangingEventArgs"
                    select method).FirstOrDefault();
        }

        static bool Prefix(Control __0, CancelEventArgs __1)
        {
            string newValue = (string)__1.GetType().GetProperty("NewValue").GetValue(__1);
            Console.WriteLine("new: " + newValue);
            if (__0.GetType() == ReflectionHelper.TelerikUI.RadTextBox && !Regex.IsMatch(newValue, "^[A-Za-z0-9_-]{0,63}$")
                || __0.GetType() == ReflectionHelper.TelerikUI.RadTextBoxControl && newValue.Contains(" "))
                __1.Cancel = true;
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
            return (from method in CloudSaveChangePasswordPatch.TargetMethod().DeclaringType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
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
