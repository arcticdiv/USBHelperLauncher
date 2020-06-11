using Dropbox.Api;
using Dropbox.Api.Files;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using USBHelperLauncher.Configuration;
using USBHelperLauncher.Properties;

namespace USBHelperLauncher.Net.CloudSaves
{
    class DropboxCloudSaveBackend : CloudSaveBackend
    {
        private static DropboxClient _dropboxClient;

        private static DropboxClient DropboxClient
        {
            get
            {
                if (_dropboxClient == null)
                {
                    InitializeClient(Credentials.DropboxToken);
                }
                return _dropboxClient;
            }
        }


        private static void InitializeClient(string token)
        {
            _dropboxClient = new DropboxClient(token ?? "", new DropboxClientConfig()
            {
                HttpClient = CloudEndpoint.Client
            });
        }


        public override async Task Authorize()
        {
            var token = await AuthorizationHandler.GetAccessToken();
            if (token != null)
            {
                Credentials.DropboxToken = token;
                Credentials.Save();
                // reinitialize client with new token
                InitializeClient(Credentials.DropboxToken);
            }
        }

        public override async Task CheckLogin()
        {
            if (string.IsNullOrEmpty(Credentials.DropboxToken))
            {
                throw new Exception("No Dropbox token set");
            }
            await DropboxClient.Users.GetCurrentAccountAsync();
        }

        public override async Task<List<CloudSaveListItem>> ListSaves()
        {
            var items = new List<Metadata>();
            var listResult = await DropboxClient.Files.ListFolderAsync("");
            items.AddRange(listResult.Entries);
            while (listResult.HasMore)
            {
                listResult = await DropboxClient.Files.ListFolderContinueAsync(listResult.Cursor);
                items.AddRange(listResult.Entries);
            }

            return (from item in items
                    where item.IsFile && Regex.IsMatch(item.Name, @"^[0-9A-Fa-f]{16}\.zip$")
                    let file = item.AsFile
                    select new CloudSaveListItem(
                        file.ContentHash,
                        Path.GetFileNameWithoutExtension(file.Name),
                        (ulong)file.ServerModified.Subtract(new DateTime(1970, 1, 1)).TotalSeconds,
                        file.Size
                    )).ToList();
        }

        public override async Task<string> GetSaveHash(string titleId)
        {
            try
            {
                var file = await DropboxClient.Files.GetMetadataAsync($"/{titleId}.zip") as FileMetadata;
                return file?.ContentHash ?? "";
            }
            catch (ApiException<GetMetadataError> e) when (e.ErrorResponse.AsPath.Value.IsNotFound)
            {
                return "";
            }
        }

        public override async Task<byte[]> GetSave(string titleId)
        {
            var downloadResponse = await DropboxClient.Files.DownloadAsync($"/{titleId}.zip");
            return await downloadResponse.GetContentAsByteArrayAsync();
        }

        public override async Task<string> UploadSave(string titleId, byte[] saveData)
        {
            using (var stream = new MemoryStream(saveData))
            {
                var file = await DropboxClient.Files.UploadAsync(
                    $"/{titleId}.zip",
                    WriteMode.Overwrite.Instance,
                    body: stream
                );
                return file.ContentHash;
            }
        }

        public override async Task DeleteSave(string titleId)
        {
            await DropboxClient.Files.DeleteV2Async($"/{titleId}.zip");
        }


        private static class AuthorizationHandler
        {
            private static readonly string _appKey = "ze6nrgrrhbr255d";

            private const int _localPort = 53482;
            private static readonly string _localHost = $"http://127.0.0.1:{_localPort}/";
            private static readonly Uri _redirectUri = new Uri(new Uri(_localHost), "authorize");
            private static readonly Uri _jsRedirectUri = new Uri(new Uri(_localHost), "token");

            private static Task<string> _currentTask;


            public static async Task<string> GetAccessToken()
            {
                // avoid going through the OAuth flow multiple times simultaneously
                if (_currentTask == null)
                {
                    _currentTask = Task.Run(async () =>
                    {
                        Console.WriteLine("[Dropbox] Getting access token...");
                        var guid = Guid.NewGuid().ToString("N");
                        var authUri = DropboxOAuth2Helper.GetAuthorizeUri(
                            OAuthResponseType.Token,
                            _appKey,
                            _redirectUri,
                            guid
                        );

                        using (var listener = new HttpListener())
                        {
                            listener.Prefixes.Add(_localHost);
                            listener.Start();
                            Process.Start(authUri.ToString());

                            var response = await HandleOAuth2Redirect(listener, guid);
                            Console.WriteLine($"[Dropbox] {(response != null ? "OK" : "Error")}");

                            _currentTask = null; // unset currentTask once finished
                            return response?.AccessToken;
                        }
                    });
                }

                return await _currentTask;
            }

            private static async Task<OAuth2Response> HandleOAuth2Redirect(HttpListener listener, string state)
            {
                // wait for callback
                HttpListenerContext context;
                do
                {
                    context = await listener.GetContextAsync();
                } while (context.Request.Url.AbsolutePath != _redirectUri.AbsolutePath);
                Console.WriteLine("[Dropbox] Got /authorize request");

                // serve JS redirect to get token from url fragment
                SendResponse(context, Resources.DropboxJSRedirect, "text/html");

                // wait for JS redirection
                do
                {
                    context = await listener.GetContextAsync();
                } while (context.Request.Url.AbsolutePath != _jsRedirectUri.AbsolutePath);
                Console.WriteLine("[Dropbox] Got /token request");

                try
                {
                    var result = DropboxOAuth2Helper.ParseTokenFragment(new Uri(context.Request.QueryString["url_with_fragment"]));
                    if (result.State == state)
                    {
                        SendResponse(context, "OK");
                        return result;
                    }
                    else
                    {
                        SendResponse(context, "Error");
                        return null;
                    }
                }
                catch
                {
                    SendResponse(context, "Error");
                    throw;
                }
            }

            private static void SendResponse(HttpListenerContext context, string text, string contentType = "text/plain")
            {
                context.Response.ContentType = contentType;
                using (var writer = new StreamWriter(context.Response.OutputStream))
                {
                    writer.Write(text);
                }
                context.Response.OutputStream.Close();
            }
        }
    }
}
