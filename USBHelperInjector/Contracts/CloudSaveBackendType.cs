using System;

namespace USBHelperInjector.Contracts
{
    public enum CloudSaveBackendType
    {
        USBHelper,
        Dropbox
    }

    static class CloudSaveBackendTypeStrings
    {
        public static string ToString(this CloudSaveBackendType backend)
        {
            switch (backend)
            {
                case CloudSaveBackendType.USBHelper:
                    return "USB Helper Cloud";
                case CloudSaveBackendType.Dropbox:
                    return "Dropbox";
                default:
                    throw new ArgumentOutOfRangeException(nameof(backend));
            }
        }
    }
}
