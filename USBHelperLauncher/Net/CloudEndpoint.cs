using Fiddler;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using USBHelperInjector.Contracts;
using USBHelperLauncher.Configuration;
using USBHelperLauncher.Net.CloudSaves;

namespace USBHelperLauncher.Net
{
    class CloudEndpoint : Endpoint
    {
        public CloudEndpoint() : base("cloud.wiiuusbhelper.com") { }

        private static readonly Lazy<HttpClient> _client = new Lazy<HttpClient>(() =>
            new HttpClient(new HttpClientHandler()
            {
                Proxy = Program.Proxy.GetWebProxy(),
                UseProxy = true
            })
        );

        internal static HttpClient Client => _client.Value;

        private static readonly string _saveHashesPath = Path.Combine(Program.GetInstallPath(), "saveHashes");
        private byte[] _currentSaveData;

        [Request("/mods/list_mods.php")]
        public void GetMods(Session oS)
        {
            oS.utilCreateResponseAndBypassServer();
            oS.oResponse["Content-Type"] = "application/json";
            oS.utilSetResponseBody("[]");
            Proxy.LogRequest(oS, this, "Stubbed request to /mods/list_mods.php");
        }

        #region Community Saves

        [Request("/communitysaves/cdn/*")]
        [Request("/communitysaves/list.php")]
        public void GetCommunityGeneric(Session oS)
        {
            Proxy.RedirectRequest(oS, this, Settings.CloudSaveUrl);
        }

        [Request("/communitysaves/add.php")]
        public void HandleCommunitySaveAdd(Session oS)
        {
            var data = GetRequestData(oS);

            var content = new MultipartFormDataContent
            {
                { new ByteArrayContent(_currentSaveData), "file", "data.zip" },
                { new StringContent(data.Get("titleid")), "titleid" },
                { new StringContent(data.Get("description")), "description" }
            };
            _currentSaveData = null;

            var response = Client.PostAsync(Proxy.RewriteToBaseUrl(oS, Settings.CloudSaveUrl), content).Result;
            oS.oResponse.headers.SetStatus((int)response.StatusCode, response.ReasonPhrase);
            oS.ResponseBody = response.Content.ReadAsByteArrayAsync().Result;
            Proxy.LogRequest(oS, this, "Uploaded community save data for title ID " + data.Get("titleid"));
        }

        [Request("/communitysaves/upload.php")]
        public void HandleCommunitySaveUpload(Session oS)
        {
            _currentSaveData = ReadFormData(oS, out var _);
            oS.utilCreateResponseAndBypassServer();
            oS.utilSetResponseBody("00000000000000000000000000000000");
            Proxy.LogRequest(oS, this, "Stored community save data temporarily");
        }

        #endregion

        #region Cloud Saves

        [Request("/saves/login.php")]
        [Request("/saves/list_saves.php")]
        [Request("/saves/get_save.php")]
        [Request("/saves/delete_save.php")]
        public void PostCloudGeneric(Session oS)
        {
            var data = GetRequestData(oS);
            SetUSBHelperCloudAuth(data["username"], data["password"]);

            Task.Run(async () =>
            {
                oS.utilCreateResponseAndBypassServer();

                try
                {
                    switch (oS.PathAndQuery)
                    {
                        case "/saves/login.php":
                        {
                            await CloudSaveBackends.Current.Login();
                            oS.utilSetResponseBody("OK");
                            break;
                        }
                        case "/saves/list_saves.php":
                        {
                            var list = await CloudSaveBackends.Current.ListSaves();
                            var arr = new JArray();
                            foreach (var item in list)
                            {
                                arr.Add(new JObject()
                                {
                                    { "md5", item.Hash },
                                    { "titleid", item.TitleId },
                                    { "date", item.Timestamp },
                                    { "size", item.Size }
                                });
                            }
                            oS.utilSetResponseBody(arr.ToString()); // always send valid JSON
                            break;
                        }
                        case "/saves/get_save.php":
                        {
                            var titleId = data["titleid"];
                            if (string.IsNullOrEmpty(data["hash"]))
                            {
                                var saveData = await CloudSaveBackends.Current.GetSave(titleId);
                                oS.ResponseBody = saveData;
                            }
                            else
                            {
                                var hash = await CloudSaveBackends.Current.GetSaveHash(titleId);
                                // If the server returned a hash, compare to saved hash
                                if (hash.Length > 0)
                                {
                                    var hashPath = Path.Combine(_saveHashesPath, data["titleid"]);
                                    if (File.Exists(hashPath) && hash == File.ReadAllText(hashPath))
                                    {
                                        // respond with empty hash
                                        hash = "";
                                    }
                                    else
                                    {
                                        Directory.CreateDirectory(_saveHashesPath);
                                        File.WriteAllText(hashPath, hash);
                                    }
                                }
                                oS.utilSetResponseBody(hash);
                            }
                            break;
                        }
                        case "/saves/delete_save.php":
                        {
                            var titleId = data["titleid"];
                            await CloudSaveBackends.Current.DeleteSave(titleId);
                            oS.utilSetResponseBody("");
                            break;
                        }
                        default:
                            throw new ArgumentException("Unhandled request path");
                    }

                    Proxy.LogRequest(oS, this, "Rewrote request for " + oS.PathAndQuery);
                }
                catch (Exception e)
                {
                    HandleError(oS, e.ToString());
                }
            }).Wait();
        }

        [Request("/saves/upload_save*.php")]
        public void UploadCloudSave(Session oS)
        {
            var saveData = ReadFormData(oS, out var filename);
            if (oS.PathAndQuery == "/saves/upload_save_b64.php")
            {
                oS.PathAndQuery = oS.PathAndQuery.Replace("_b64", "");
                filename = Encoding.UTF8.GetString(Convert.FromBase64String(filename));
            }
            var parts = filename.Split(new[] { ' ' }, 3);

            SetUSBHelperCloudAuth(parts[0], parts[1]);
            oS.utilCreateResponseAndBypassServer();
            try
            {
                var hash = CloudSaveBackends.Current.UploadSave(parts[2], saveData).Result;
                Directory.CreateDirectory(_saveHashesPath);
                var hashPath = Path.Combine(_saveHashesPath, parts[2]);
                File.WriteAllText(hashPath, hash);

                oS.utilSetResponseBody("OK");
                Proxy.LogRequest(oS, this, $"Uploaded cloud save data for title ID {parts[2]}");
            }
            catch (Exception e)
            {
                HandleError(oS, e.ToString());
            }
        }

        private void HandleError(Session oS, string errorText)
        {
            // the get_save.php handler is the only one with proper error handling,
            //  respond with "200 OK" for all the others
            if (oS.PathAndQuery == "/saves/get_save.php")
            {
                oS.oResponse.headers.SetStatus(500, "Internal Server Error");
            }
            oS.utilSetResponseBody($"Error:\n{errorText}");
            Proxy.LogRequest(oS, this, $"Error in {oS.PathAndQuery} handler:\n{errorText}");
        }

        private static void SetUSBHelperCloudAuth(string username, string password)
        {
            if (Settings.CloudSaveBackend != CloudSaveBackendType.USBHelper)
            {
                return;
            }
            USBHelperCloudSaveBackend.Username = username;
            USBHelperCloudSaveBackend.Password = password;
        }

        #endregion

        private static byte[] ReadFormData(Session oS, out string filename)
        {
            // RFC 1521, Section 4; RFC 7578, Section 4.1; RFC 2046, Section 5.1.1
            var boundary = "--" + Regex.Match(oS.RequestHeaders["Content-Type"], ";\\s*boundary=\"?(.+)\"?$").Groups[1].Value;
            var boundaryBytes = Encoding.UTF8.GetBytes(boundary);

            // search start boundary
            var data = oS.RequestBody;
            var start = SearchBytes(data, boundaryBytes) + boundaryBytes.Length;

            // look for 'filename="<str>"'
            var filenameBytes = Encoding.UTF8.GetBytes("filename=\"");
            var filenameStart = SearchBytes(data, filenameBytes, start) + filenameBytes.Length;
            var filenameEnd = SearchBytes(data, new[] { (byte)'"' }, filenameStart);
            filename = Encoding.UTF8.GetString(data, filenameStart, filenameEnd - filenameStart);

            // look for next \r\n\r\n
            var dataStart = SearchBytes(data, new byte[] { 0xD, 0xA, 0xD, 0xA }, start) + 4;
            // search end boundary (take final line break into account by subtracting 2)
            var end = SearchBytes(data, boundaryBytes, dataStart) - 2;
            var dataLength = end - dataStart;

            var newData = new byte[dataLength];
            Array.Copy(data, dataStart, newData, 0, dataLength);
            return newData;
        }

        private static int SearchBytes(byte[] haystack, byte[] needle, int startOffset = 0)
        {
            var len = needle.Length;
            var limit = haystack.Length - len;
            for (var i = startOffset; i <= limit; i++)
            {
                var k = 0;
                for (; k < len; k++)
                {
                    if (needle[k] != haystack[i + k])
                    {
                        break;
                    }
                }
                if (k == len)
                {
                    return i;
                }
            }
            throw new FormatException("Pattern not found");
        }
    }
}
