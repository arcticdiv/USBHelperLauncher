using System.Collections.Generic;
using USBHelperInjector.Contracts;
using USBHelperLauncher.Configuration;

namespace USBHelperLauncher.Net.CloudSaves
{
    class CloudSaveBackends
    {
        private static readonly Dictionary<CloudSaveBackendType, ICloudSaveBackend> backends = new Dictionary<CloudSaveBackendType, ICloudSaveBackend>()
        {
            { CloudSaveBackendType.USBHelper, new USBHelperCloudSaveBackend() },
            { CloudSaveBackendType.Dropbox, new DropboxCloudSaveBackend() }
        };

        public static ICloudSaveBackend Current => Get(Settings.CloudSaveBackend);

        public static ICloudSaveBackend Get(CloudSaveBackendType type) => backends[type];
    }
}
