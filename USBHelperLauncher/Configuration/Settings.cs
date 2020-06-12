using System.Collections.Generic;
using USBHelperInjector.Contracts;
using USBHelperLauncher.Net;

namespace USBHelperLauncher.Configuration
{
    public class Settings : SettingsBase<Settings>
    {
        private const string FilePath = "conf.json";

        [Setting("Launcher", false)]
        public static bool HostsExpert { get; set; }

        [Setting("Launcher", true)]
        public static bool ShowUpdateNag { get; set; }

        [Setting("Launcher", true)]
        public static bool ShowTranslateNag { get; set; }

        [Setting("Launcher", false)]
        public static bool ShowHostsWarning { get; set; }

        [Setting("Launcher", 1000)]
        public static int SessionBufferSize { get; set; }

        [Setting("Launcher", 64 * 1000)]
        public static int SessionSizeLimit { get; set; }

        [Setting("Launcher")]
        public static string Locale { get; set; }

        [Setting("Launcher")]
        public static string TranslationsBuild { get; set; }

        [Setting("Launcher")]
        public static string LastMessage { get; set; }

        [Setting("Launcher", forgetful: true)]
        public static Dictionary<string, string> EndpointFallbacks { get; set; } = new Dictionary<string, string>()
        {
            { typeof(ContentEndpoint).Name, "https://cdn.shiftinv.cc/wiiuusbhelper/cdn/" }
        };

        [Setting("Launcher", "https://usbhelper.shiftinv.cc/cloud/")]
        public static string CloudSaveUrl { get; set; }

        [Setting("Launcher", CloudSaveBackendType.USBHelper)]
        public static CloudSaveBackendType CloudSaveBackend { get; set; }

        [Setting("Launcher", "USBHelperLauncher")]
        public static string GoogleDriveAppFolder { get; set; }

        [Setting("Launcher")]
        public static string LocalCloudSaveFolder { get; set; }

        [Setting("Launcher")]
        public static Dictionary<string, string> TitleKeys { get; set; } = new Dictionary<string, string>();

        [Setting("Injector", false)]
        public static bool DisableOptionalPatches { get; set; }

        [Setting("Injector", new string[] { "toolWeb", "toolMods", "toolChat" })]
        public static string[] DisableTabs { get; set; }

        [Setting("Injector", 5)]
        public static int MaxRetries { get; set; }

        [Setting("Injector", 1000)]
        public static int DelayBetweenRetries { get; set; }

        [Setting("Injector", false)]
        public static bool Portable { get; set; }

        [Setting("Injector", false)]
        public static bool ForceHttp { get; set; }

        [Setting("Injector", false)]
        public static bool NoFunAllowed { get; set; }
    }
}
