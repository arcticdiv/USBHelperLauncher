using System.ServiceModel;
using System.Threading.Tasks;

namespace USBHelperInjector.Contracts
{
    [ServiceContract]
    public interface ILauncherService
    {
        [OperationContract]
        void SetKeySite(string site, string url);

        [OperationContract]
        void SetCloudSaveBackend(CloudSaveBackendType backend);

        [OperationContract]
        void SetLocalCloudSavePath(string path);

        [OperationContract]
        void SendInjectorSettings();
    }
}
