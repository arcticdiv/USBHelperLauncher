using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Windows.Forms;

namespace USBHelperInjector
{
    class ReflectionHelper
    {
        private static readonly Assembly assembly = Assembly.Load("WiiU_USB_Helper");

        public static readonly Type[] Types = assembly.GetTypes();

        public static readonly Module MainModule = assembly.GetModule("WiiU_USB_Helper.exe");

        public static readonly MethodInfo EntryPoint = assembly.EntryPoint;

        public static readonly Type Settings = assembly.GetType("WIIU_Downloader.Properties.Settings");

        public static class NusGrabberForm
        {
            private static readonly Lazy<Type> _type = new Lazy<Type>(
                () => (from type in Types
                       where typeof(Form).IsAssignableFrom(type)
                       from prop in type.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance)
                       where prop.Name == "Proxy"
                       select type).FirstOrDefault()
            );
            public static Type Type => _type.Value;

            private static readonly Lazy<ConstructorInfo> _constructor = new Lazy<ConstructorInfo>(
                () => Type.GetConstructor(Type.EmptyTypes)
            );
            public static ConstructorInfo Constructor => _constructor.Value;
        }

        private static readonly Lazy<Type> _frmCloudSaving = new Lazy<Type>(
            () => (from type in Types
                   where typeof(Form).IsAssignableFrom(type)
                      && type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                             .Any(f => f.FieldType == TelerikUI.RadToggleSwitch)
                   select type).FirstOrDefault()
        );
        public static Type FrmCloudSaving => _frmCloudSaving.Value;

        public static class FrmAskTicket
        {
            private static readonly Lazy<Type> _type = new Lazy<Type>(
                () => (from type in Types
                       where type.GetProperty("FileLocationWiiU") != null
                       select type).FirstOrDefault()
            );

            public static Type Type => _type.Value;

            private static readonly Lazy<MethodInfo> _okButtonHandler = new Lazy<MethodInfo>(
                () => (from method in Type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                       let instructions = PatchProcessor.GetOriginalInstructions(method, out _)
                       where instructions.Any(x => x.opcode == OpCodes.Call && ((MethodInfo)x.operand).Name == "set_FileLocation3DS")
                          && instructions.Any(x => x.opcode == OpCodes.Ldfld && ((FieldInfo)x.operand).FieldType == TelerikUI.RadTextBox)
                       select method).FirstOrDefault()
            );

            public static MethodInfo OkButtonHandler => _okButtonHandler.Value;

            private static readonly Lazy<List<FieldInfo>> _textBoxes = new Lazy<List<FieldInfo>>(
                () => (from instruction in PatchProcessor.GetOriginalInstructions(OkButtonHandler, out _)
                       where instruction.opcode == OpCodes.Ldfld
                       let field = (FieldInfo)instruction.operand
                       where field.FieldType == TelerikUI.RadTextBox
                       select field).ToList()
            );

            public static List<FieldInfo> TextBoxes => _textBoxes.Value;
        }


        public static MethodInfo GetInitializeComponentMethod(Type type)
        {
            return (from constructor in type.GetConstructors(AccessTools.all)
                    from instruction in PatchProcessor.GetOriginalInstructions(constructor, out _)
                    where (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
                    let target = (MethodBase)instruction.operand
                    where target.DeclaringType == type && target.GetParameters().Length == 0
                    select (MethodInfo)target).FirstOrDefault();
        }


        public static class TelerikUI
        {
            public static readonly Assembly Assembly = Assembly.Load("Telerik.WinControls.UI");

            public static readonly Type RadMessageBox = Assembly.GetType("Telerik.WinControls.RadMessageBox");
            public static readonly Type RadForm = Assembly.GetType("Telerik.WinControls.UI.RadForm");
            public static readonly Type RadLabel = Assembly.GetType("Telerik.WinControls.UI.RadLabel");
            public static readonly Type RadButton = Assembly.GetType("Telerik.WinControls.UI.RadButton");
            public static readonly Type RadGroupBox = Assembly.GetType("Telerik.WinControls.UI.RadGroupBox");
            public static readonly Type RadTextBox = Assembly.GetType("Telerik.WinControls.UI.RadTextBox");
            public static readonly Type RadTextBoxControl = Assembly.GetType("Telerik.WinControls.UI.RadTextBoxControl");
            public static readonly Type RadToggleSwitch = Assembly.GetType("Telerik.WinControls.UI.RadToggleSwitch");
        }
    }
}
