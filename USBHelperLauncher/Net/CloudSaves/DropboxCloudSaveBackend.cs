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
using USBHelperLauncher.Utils;

namespace USBHelperLauncher.Net.CloudSaves
{
    class DropboxCloudSaveBackend : ICloudSaveBackend
    {
        private static DropboxClient _dropboxClient;
        private static DropboxClient dropboxClient
        {
            get
            {
                if (_dropboxClient == null)
                {
                    InitializeClient(Settings.DropboxToken);
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

        public async Task Authorize()
        {
            var token = await AuthorizationHandler.GetAccessToken();
            if (token != null)
            {
                Settings.DropboxToken = token;
                Settings.Save();
                // reinitialize client with new token
                InitializeClient(Settings.DropboxToken);
            }
        }


        public async Task<Result> Login()
        {
            return await TryAPI(async () =>
            {
                return await dropboxClient.Users.GetCurrentAccountAsync();
            });
        }

        public async Task<Result<List<CloudSaveListItem>>> ListSaves()
        {
            return await TryAPI(async () =>
            {
                var items = new List<Metadata>();
                var listResult = await dropboxClient.Files.ListFolderAsync("");
                items.AddRange(listResult.Entries);
                while (listResult.HasMore)
                {
                    listResult = await dropboxClient.Files.ListFolderContinueAsync(listResult.Cursor);
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
            });
        }

        public async Task<Result<string>> GetSaveHash(string titleId)
        {
            return await TryAPI(async () =>
            {
                try
                {
                    var file = await dropboxClient.Files.GetMetadataAsync($"/{titleId}.zip") as FileMetadata;
                    return file?.ContentHash ?? "";
                }
                catch (ApiException<GetMetadataError> e) when (e.ErrorResponse.AsPath.Value.IsNotFound)
                {
                    return "";
                }
            });
        }

        public async Task<Result<byte[]>> GetSave(string titleId)
        {
            return await TryAPI(async () =>
            {
                var downloadResponse = await dropboxClient.Files.DownloadAsync($"/{titleId}.zip");
                return await downloadResponse.GetContentAsByteArrayAsync();
            });
        }

        public async Task<Result<string>> UploadSave(string titleId, byte[] saveData)
        {
            return await TryAPI(async () =>
            {
                using (var stream = new MemoryStream(saveData))
                {
                    var file = await dropboxClient.Files.UploadAsync(
                        $"/{titleId}.zip",
                        WriteMode.Overwrite.Instance,
                        body: stream
                    );
                    return file.ContentHash;
                }
            });
        }

        public async Task<Result> DeleteSave(string titleId)
        {
            return await TryAPI(async () =>
            {
                return await dropboxClient.Files.DeleteV2Async($"/{titleId}.zip");
            });
        }

        private async Task<Result<T>> TryAPI<T>(Func<Task<T>> func)
        {
            try
            {
                var value = await func();
                return Result<T>.Success(value);
            }
            catch (DropboxException e)
            {
                return Result<T>.Failure(e.ToString());
            }
        }


        class AuthorizationHandler
        {
            private static readonly string AppKey = "ze6nrgrrhbr255d";
            private const int localPort = 53482;
            private static readonly string localHost = $"http://127.0.0.1:{localPort}/";
            private static readonly Uri redirectUri = new Uri(new Uri(localHost), "authorize");
            private static readonly Uri jsRedirectUri = new Uri(new Uri(localHost), "token");

            private static Task<string> currentTask;


            public static async Task<string> GetAccessToken()
            {
                // avoid going through the OAuth flow twice at the same time
                if (currentTask == null)
                {
                    currentTask = Task.Run(async () =>
                    {
                        Console.WriteLine("[Dropbox] Getting access token...");
                        var guid = Guid.NewGuid().ToString("N");
                        var authUri = DropboxOAuth2Helper.GetAuthorizeUri(
                            OAuthResponseType.Token,
                            AppKey,
                            redirectUri,
                            guid
                        );

                        using (var listener = new HttpListener())
                        {
                            listener.Prefixes.Add(localHost);
                            listener.Start();
                            Process.Start(authUri.ToString());

                            var response = await HandleOAuth2Redirect(listener, guid);
                            Console.WriteLine($"[Dropbox] {(response != null ? "OK" : "Error")}");

                            currentTask = null;  // unset currentTask once finished
                            return response?.AccessToken;
                        }
                    });
                }

                return await currentTask;
            }

            private static async Task<OAuth2Response> HandleOAuth2Redirect(HttpListener listener, string state)
            {
                // wait for callback
                HttpListenerContext context;
                do
                {
                    context = await listener.GetContextAsync();
                } while (context.Request.Url.AbsolutePath != redirectUri.AbsolutePath);
                Console.WriteLine("[Dropbox] Got /authorize request");

                // serve JS redirect to get token from url fragment
                SendResponse(context, Resources.DropboxJSRedirect, "text/html");

                // wait for JS redirection
                do
                {
                    context = await listener.GetContextAsync();
                } while (context.Request.Url.AbsolutePath != jsRedirectUri.AbsolutePath);
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
