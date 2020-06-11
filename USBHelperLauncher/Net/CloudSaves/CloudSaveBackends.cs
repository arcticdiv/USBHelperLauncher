using System.Collections.Generic;
using USBHelperInjector.Contracts;
using USBHelperLauncher.Configuration;

namespace USBHelperLauncher.Net.CloudSaves
{
    class CloudSaveBackends
    {
        private static readonly Dictionary<CloudSaveBackendType, CloudSaveBackend> _backends = new Dictionary<CloudSaveBackendType, CloudSaveBackend>()
        {
            { CloudSaveBackendType.USBHelper, new USBHelperCloudSaveBackend() },
            { CloudSaveBackendType.Dropbox, new DropboxCloudSaveBackend() },
            { CloudSaveBackendType.GoogleDrive, new GoogleDriveCloudSaveBackend() }
        };

        public static CloudSaveBackend Current => Get(Settings.CloudSaveBackend);

        public static CloudSaveBackend Get(CloudSaveBackendType type) => _backends[type];
    }
}
