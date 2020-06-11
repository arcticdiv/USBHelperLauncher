using System.Collections.Generic;

namespace USBHelperLauncher.Configuration
{
    public class Credentials : SettingsBase<Credentials>
    {
        private const string FilePath = "creds.json";

        [Setting("CloudSaves")]
        public static string DropboxToken { get; set; }

        [Setting("CloudSaves")]
        public static Dictionary<string, string> GoogleDriveData { get; set; } = new Dictionary<string, string>();

        [Setting("CloudSaves")]
        public static string GoogleDriveAppFolderId { get; set; }
    }
}
