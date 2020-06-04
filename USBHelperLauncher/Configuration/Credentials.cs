namespace USBHelperLauncher.Configuration
{
    public class Credentials : SettingsBase<Credentials>
    {
        private const string FilePath = "creds.json";

        [Setting("CloudSaves")]
        public static string DropboxToken { get; set; }
    }
}
