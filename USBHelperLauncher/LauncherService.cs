using Fiddler;
using System.ServiceModel;
using USBHelperInjector.Contracts;
using USBHelperLauncher.Configuration;

namespace USBHelperLauncher
{
    class LauncherService : ILauncherService
    {
        public static IInjectorService Channel { get; private set; }

        public void SetKeySite(string site, string url)
        {
            Settings.TitleKeys[site] = url;
            Settings.Save();
        }

        public void SendInjectorSettings()
        {
            Program.Logger.WriteLine("Sending information to injector...");
            var factory = new ChannelFactory<IInjectorService>(new NetNamedPipeBinding(), "net.pipe://localhost/InjectorService");
            Channel = factory.CreateChannel();
            if (Program.OverridePublicKey)
            {
                Channel.SetDonationKey(Program.GenerateDonationKey());
                Channel.SetPublicKey(Program.PublicKey);
            }
            if (Settings.TitleKeys.Count == 0)
            {
                Channel.ForceKeySiteForm();
            }
            Channel.TrustCertificateAuthority(CertMaker.GetRootCertificate().GetRawCertData());
            Channel.SetProxy(Program.Proxy.GetWebProxy().Address.ToString());
            Channel.SetDownloaderMaxRetries(Settings.MaxRetries);
            Channel.SetDownloaderRetryDelay(Settings.DelayBetweenRetries);
            Channel.SetDisableOptionalPatches(Settings.DisableOptionalPatches);
            Channel.SetDisableTabs(Settings.DisableTabs);
            Channel.SetLocaleFile(Program.Locale.LocaleFile);
            Channel.SetEshopRegion(Program.Locale.ChosenLocale.Split('-')[1]);
            Channel.SetHelperVersion(Program.HelperVersion);
            Channel.SetPortable(Settings.Portable);
            Channel.SetForceHttp(Settings.ForceHttp);
            Channel.SetFunAllowed(!Settings.NoFunAllowed);
            Channel.SetCloudSaveBackend(Settings.CloudSaveBackend);
        }
    }
}
