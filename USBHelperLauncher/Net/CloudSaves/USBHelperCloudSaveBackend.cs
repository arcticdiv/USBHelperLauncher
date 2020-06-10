using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using USBHelperLauncher.Configuration;

namespace USBHelperLauncher.Net.CloudSaves
{
    class USBHelperCloudSaveException : Exception
    {
        public USBHelperCloudSaveException(string message) : base(message) { }
    }

    class USBHelperCloudSaveBackend : ICloudSaveBackend
    {
        private static HttpClient Client => CloudEndpoint.Client;

        public static string Username, Password;


        // unused
        public Task Authorize()
        {
            throw new NotImplementedException();
        }

        public async Task Login()
        {
            var response = await Post("login.php");
            var text = await response.Content.ReadAsStringAsync();
            if (text != "OK")
            {
                throw new USBHelperCloudSaveException(text);
            }
        }

        public async Task<List<CloudSaveListItem>> ListSaves()
        {
            var response = await Post("list_saves.php");
            var text = await response.Content.ReadAsStringAsync();
            var jsonList = JArray.Parse(text);
            return (from obj in jsonList
                    select new CloudSaveListItem(
                        obj.Value<string>("md5"),
                        obj.Value<string>("titleid"),
                        obj.Value<ulong>("date"),
                        obj.Value<ulong>("size")
                    )).ToList();
        }

        public async Task<string> GetSaveHash(string titleId)
        {
            var response = await Post("get_save.php", new Dictionary<string, string>()
            {
                { "titleid", titleId },
                { "hash", "true" }
            });
            var bytes = await response.Content.ReadAsByteArrayAsync();
            if (bytes.Length == 0)
            {
                return "";
            }

            var str = Encoding.ASCII.GetString(bytes);
            if (bytes.Length != 16 || str.StartsWith("Error"))
            {
                throw new USBHelperCloudSaveException(str);
            }

            return BitConverter.ToString(bytes).Replace("-", "");
        }

        public async Task<byte[]> GetSave(string titleId)
        {
            var response = await Post("get_save.php", new Dictionary<string, string>()
            {
                { "titleid", titleId }
            });
            return await response.Content.ReadAsByteArrayAsync();
        }

        public async Task<string> UploadSave(string titleId, byte[] saveData)
        {
            var content = new MultipartFormDataContent
            {
                { new StringContent(Username), "username" },
                { new StringContent(HashPassword(Password)), "password" },
                { new StringContent(titleId), "titleid" },
                { new ByteArrayContent(saveData), "file", "data.zip" }
            };

            var response = await Client.PostAsync(GetUri("upload_save.php"), content);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            if (bytes.Length != 2 + 16 || !bytes.Take(2).SequenceEqual(new[] { (byte)'O', (byte)'K' }))
            {
                throw new USBHelperCloudSaveException(Encoding.UTF8.GetString(bytes));
            }

            var hash = bytes.Skip(2).ToArray();
            return BitConverter.ToString(hash).Replace("-", "");
        }

        public async Task DeleteSave(string titleId)
        {
            var response = await Post("delete_save.php", new Dictionary<string, string>()
            {
                { "titleid", titleId }
            });
            var text = await response.Content.ReadAsStringAsync();
            if (text != "OK")
            {
                throw new USBHelperCloudSaveException(text);
            }
        }


        private async Task<HttpResponseMessage> Post(string path, Dictionary<string, string> data = null)
        {
            IEnumerable<KeyValuePair<string, string>> postParams = new Dictionary<string, string>()
            {
                { "username", Username },
                { "password", HashPassword(Password) }
            };
            if (data != null)
            {
                postParams = postParams.Union(data);
            }

            var response = await Client.PostAsync(GetUri(path), new FormUrlEncodedContent(postParams));
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(await GetErrorString(response));
            }
            return response;
        }

        private static async Task<string> GetErrorString(HttpResponseMessage response)
        {
            var str = await response.Content.ReadAsStringAsync();
            return $"Request unsuccessful ({(int)response.StatusCode} {response.ReasonPhrase}):\n{str}";
        }

        private static Uri GetUri(string path)
        {
            var baseUri = new Uri(new Uri(Settings.CloudSaveUrl), "saves/");
            return new Uri(baseUri, path);
        }

        private static string HashPassword(string password)
        {
            using (SHA1Managed sha1 = new SHA1Managed())
            {
                var salted = "this is a good salt" + password;
                var hashBytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(salted));
                return BitConverter.ToString(hashBytes).Replace("-", "");
            }
        }
    }
}
