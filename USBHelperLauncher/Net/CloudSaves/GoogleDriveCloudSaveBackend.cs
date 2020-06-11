using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Drive.v3;
using Google.Apis.Http;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using Newtonsoft.Json;
using USBHelperLauncher.Configuration;
using File = Google.Apis.Drive.v3.Data.File;

namespace USBHelperLauncher.Net.CloudSaves
{
    class GoogleDriveCloudSaveBackend : CloudSaveBackend
    {
        private static DriveService _gDriveService;

        private static DriveService GDriveService
        {
            get
            {
                if (_gDriveService == null)
                {
                    InitializeClient(new UserCredential(
                        AuthorizationHandler.Flow,
                        "user",
                        AuthorizationHandler.Flow.LoadTokenAsync("user", CancellationToken.None).Result
                    ));
                }
                return _gDriveService;
            }
        }

        // cache ID for GetOrCreateAppFolder()
        private static string _appFolderId;


        private static void InitializeClient(UserCredential credential)
        {
            _gDriveService = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "USBHelperLauncher",
                HttpClientFactory = new ProxyHttpClientFactory()
            });
            _appFolderId = null;
        }

        public override async Task Authorize()
        {
            var credentials = await AuthorizationHandler.GetUserCredentials();
            // credentials are automatically stored by DataStore, no need to save manually
            // reinitialize client with new token
            InitializeClient(credentials);
        }

        public override async Task CheckLogin()
        {
            var req = GDriveService.About.Get();
            req.Fields = "kind";
            await req.ExecuteAsync();
        }

        public override async Task<List<CloudSaveListItem>> ListSaves()
        {
            var appFolderId = await GetOrCreateAppFolder();
            var listReq = GDriveService.Files.List();
            listReq.Fields = "files(name, modifiedTime, size, md5Checksum)";
            listReq.Q = $"trashed = false and '{appFolderId}' in parents";
            var items = await ListDirectory(listReq);

            return (from item in items
                    where IsValidFileName(item.Name)
                    select new CloudSaveListItem(
                        item.Md5Checksum,
                        Path.GetFileNameWithoutExtension(item.Name),
                        (ulong)(item.ModifiedTime?.ToUniversalTime().Subtract(new DateTime(1970, 1, 1)).TotalSeconds ?? 0),
                        (ulong)(item.Size ?? 0)
                    )).ToList();
        }

        public override async Task<string> GetSaveHash(string titleId)
        {
            return (await GetFileForTitleId(titleId, "md5Checksum"))?.Md5Checksum ?? "";
        }

        public override async Task<byte[]> GetSave(string titleId)
        {
            var fileId = (await GetFileForTitleId(titleId, "id")).Id;
            using (var memoryStream = new MemoryStream())
            {
                await GDriveService.Files.Get(fileId).DownloadAsync(memoryStream);
                return memoryStream.ToArray();
            }
        }

        public override async Task<string> UploadSave(string titleId, byte[] saveData)
        {
            var existingFile = await GetFileForTitleId(titleId, "id");

            using (var memoryStream = new MemoryStream(saveData))
            {
                // No better way to do this, the common ancestor of CreateMediaUpload and UpdateMediaUpload doesn't have the `Fields` property
                IUploadProgress result;
                File response;
                if (existingFile == null)
                {
                    var appFolderId = await GetOrCreateAppFolder();
                    var file = new File
                    {
                        Name = FileNameForTitleId(titleId),
                        Parents = new[] { appFolderId }
                    };
                    var req = GDriveService.Files.Create(file, memoryStream, "application/zip");
                    req.Fields = "md5Checksum";
                    result = await req.UploadAsync();
                    response = req.ResponseBody;
                }
                else
                {
                    var req = GDriveService.Files.Update(new File(), existingFile.Id, memoryStream, "application/zip");
                    req.Fields = "md5Checksum";
                    result = await req.UploadAsync();
                    response = req.ResponseBody;
                }

                if (result.Exception != null)
                {
                    throw result.Exception;
                }
                return response.Md5Checksum;
            }
        }

        public override async Task DeleteSave(string titleId)
        {
            var fileId = (await GetFileForTitleId(titleId, "id")).Id;
            // Move to trash instead of deleting permanently
            await GDriveService.Files.Update(
                new File
                {
                    Trashed = true
                },
                fileId
            ).ExecuteAsync();
        }


        private static async Task<string> GetOrCreateAppFolder()
        {
            if (!string.IsNullOrEmpty(_appFolderId))
            {
                return _appFolderId;
            }

            string folderId = null;

            // Check for stored folder
            if (!string.IsNullOrEmpty(Credentials.GoogleDriveAppFolderId))
            {
                try
                {
                    var req = GDriveService.Files.Get(Credentials.GoogleDriveAppFolderId);
                    req.Fields = "trashed";
                    var existing = await req.ExecuteAsync();
                    if (!existing.Trashed.GetValueOrDefault(false))
                    {
                        folderId = Credentials.GoogleDriveAppFolderId;
                    }
                }
                catch (GoogleApiException e) when (e.HttpStatusCode == HttpStatusCode.NotFound) { }
            }

            if (string.IsNullOrEmpty(folderId))
            {
                // Search for folder by name
                var nameReq = GDriveService.Files.List();
                nameReq.Fields = "files(id)";
                nameReq.Q = $"trashed = false and name = '{Settings.GoogleDriveAppFolder}' and mimeType = 'application/vnd.google-apps.folder' and 'root' in parents";
                var nameList = await ListDirectory(nameReq);
                if (nameList.Count != 0)
                {
                    folderId = nameList[0].Id;
                }
                else
                {
                    // No existing folder was found, create a new one
                    var createReq = GDriveService.Files.Create(
                        new File
                        {
                            Name = Settings.GoogleDriveAppFolder,
                            MimeType = "application/vnd.google-apps.folder"
                        }
                    );
                    createReq.Fields = "id";
                    var folder = await createReq.ExecuteAsync();
                    folderId = folder.Id;
                }
            }

            _appFolderId = folderId;
            Credentials.GoogleDriveAppFolderId = _appFolderId;
            Credentials.Save();
            return _appFolderId;
        }

        private static async Task<List<File>> ListDirectory(FilesResource.ListRequest baseRequest)
        {
            if (baseRequest.Fields.Split(',').All(f => f.Trim() != "nextPageToken"))
            {
                baseRequest.Fields += ", nextPageToken";
            }

            var items = new List<File>();
            do
            {
                var result = await baseRequest.ExecuteAsync();
                baseRequest.PageToken = result.NextPageToken;
                items.AddRange(result.Files);
            } while (!string.IsNullOrEmpty(baseRequest.PageToken));
            return items;
        }

        private static async Task<File> GetFileForTitleId(string titleId, string subFields)
        {
            var appFolderId = await GetOrCreateAppFolder();
            var req = GDriveService.Files.List();
            req.Fields = $"files({subFields})";
            req.Q = $"trashed = false and name = '{FileNameForTitleId(titleId)}' and '{appFolderId}' in parents";

            var files = await ListDirectory(req);
            return files.Count == 0 ? null : files[0];
        }

        private class ProxyHttpClientFactory : HttpClientFactory
        {
            protected override HttpClientHandler CreateClientHandler()
            {
                return new HttpClientHandler
                {
                    Proxy = Program.Proxy.GetWebProxy(),
                    UseProxy = true
                };
            }
        }

        private class SettingsDataStore : IDataStore
        {
            public Task ClearAsync()
            {
                Credentials.GoogleDriveData.Clear();
                Credentials.Save();
                return Task.FromResult(0);
            }

            public Task DeleteAsync<T>(string key)
            {
                Credentials.GoogleDriveData.Remove(GenerateKey(key, typeof(T)));
                Credentials.Save();
                return Task.FromResult(0);
            }

            public Task<T> GetAsync<T>(string key)
            {
                return Task.FromResult(
                    Credentials.GoogleDriveData.TryGetValue(GenerateKey(key, typeof(T)), out var data)
                        ? JsonConvert.DeserializeObject<T>(data)
                        : default
                );
            }

            public Task StoreAsync<T>(string key, T value)
            {
                Credentials.GoogleDriveData[GenerateKey(key, typeof(T))] = JsonConvert.SerializeObject(value);
                Credentials.Save();
                return Task.FromResult(0);
            }

            private static string GenerateKey(string key, Type t)
            {
                return $"{t.FullName}-{key}";
            }
        }

        private static class AuthorizationHandler
        {
            private const string _clientID = "";
            private const string _clientSecret = "";

            public static readonly AuthorizationCodeFlow Flow = new GoogleAuthorizationCodeFlow(
                new GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = new ClientSecrets
                    {
                        ClientId = _clientID,
                        ClientSecret = _clientSecret
                    },
                    Scopes = new[] { DriveService.Scope.DriveFile },
                    DataStore = new SettingsDataStore()
                }
            );

            public static async Task<UserCredential> GetUserCredentials()
            {
                return await new AuthorizationCodeInstalledApp(
                    Flow,
                    new LocalServerCodeReceiver()
                ).AuthorizeAsync("user", CancellationToken.None);
            }
        }
    }
}
