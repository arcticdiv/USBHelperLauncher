using System;

namespace USBHelperInjector.Contracts
{
    public enum CloudSaveBackendType
    {
        USBHelper,
        Dropbox,
        GoogleDrive
    }

    public static class CloudSaveBackendTypeStrings
    {
        public static string Description(this CloudSaveBackendType backend)
        {
            switch (backend)
            {
                case CloudSaveBackendType.USBHelper:
                    return "USB Helper Cloud";
                case CloudSaveBackendType.Dropbox:
                    return "Dropbox";
                case CloudSaveBackendType.GoogleDrive:
                    return "Google Drive";
                default:
                    throw new ArgumentOutOfRangeException(nameof(backend));
            }
        }
    }
}
