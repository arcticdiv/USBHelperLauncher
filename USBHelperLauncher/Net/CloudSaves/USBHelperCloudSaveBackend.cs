using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using USBHelperLauncher.Configuration;
using USBHelperLauncher.Utils;

namespace USBHelperLauncher.Net.CloudSaves
{
    class USBHelperCloudSaveBackend : ICloudSaveBackend
    {
        private static HttpClient client => CloudEndpoint.Client;

        public static string Username, Password;


        public async Task<Result> Login()
        {
            var response = await Post("login.php");
            return await CheckResponseText(
                response,
                (text) => text == "OK" ? Result.Success() : Result.Failure(text),
                (err) => Result.Failure(err)
            );
        }

        public async Task<Result<List<CloudSaveListItem>>> ListSaves()
        {
            var response = await Post("list_saves.php");
            return await CheckResponseText(
                response,
                (text) =>
                {
                    var jsonList = JArray.Parse(text);
                    var list = new List<CloudSaveListItem>();
                    foreach (JObject obj in jsonList)
                    {
                        list.Add(new CloudSaveListItem(
                            obj.Value<string>("md5"),
                            obj.Value<string>("titleid"),
                            obj.Value<ulong>("date"),
                            obj.Value<ulong>("size")
                        ));
                    }
                    return Result<List<CloudSaveListItem>>.Success(list);
                },
                (err) => Result<List<CloudSaveListItem>>.Failure(err)
            );
        }

        public async Task<Result<string>> GetSaveHash(string titleId)
        {
            var response = await Post("get_save.php", new Dictionary<string, string>()
            {
                { "titleid", titleId },
                { "hash", "true" }
            });
            return await CheckResponse(
                response,
                async () =>
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    if (bytes.Length == 0)
                    {
                        return Result<string>.Success("");
                    }
                    var str = Encoding.ASCII.GetString(bytes);
                    if (bytes.Length == 16 && !str.StartsWith("Error"))
                    {
                        var hex = BitConverter.ToString(bytes).Replace("-", "");
                        return Result<string>.Success(hex);
                    }
                    return Result<string>.Failure(str);
                },
                (err) => Result<string>.Failure(err)
            );
        }

        public async Task<Result<byte[]>> GetSave(string titleId)
        {
            var response = await Post("get_save.php", new Dictionary<string, string>()
            {
                { "titleid", titleId }
            });
            return await CheckResponse(
                response,
                async () => Result<byte[]>.Success(await response.Content.ReadAsByteArrayAsync()),
                (err) => Result<byte[]>.Failure(err)
            );
        }

        public async Task<Result<string>> UploadSave(string titleId, byte[] saveData)
        {
            MultipartFormDataContent content = new MultipartFormDataContent
            {
                { new StringContent(Username), "username" },
                { new StringContent(HashPassword(Password)), "password" },
                { new StringContent(titleId), "titleid" },
                { new ByteArrayContent(saveData), "file", "data.zip" }
            };

            var response = await client.PostAsync(GetUri("upload_save.php"), content);
            return await CheckResponse(
                response,
                async () =>
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    if (bytes.Length == 2+16 && bytes.Take(2).SequenceEqual(new byte[] { (byte)'O', (byte)'K' }))
                    {
                        var hash = bytes.Skip(2).ToArray();
                        var hex = BitConverter.ToString(hash).Replace("-", "");
                        return Result<string>.Success(hex);
                    }
                    return Result<string>.Failure(Encoding.ASCII.GetString(bytes));
                },
                (err) => Result<string>.Failure(err)
            );
        }

        public async Task<Result> DeleteSave(string titleId)
        {
            var response = await Post("delete_save.php", new Dictionary<string, string>()
            {
                { "titleid", titleId }
            });
            return await CheckResponseText(
                response,
                (text) => text == "OK" ? Result.Success() : Result.Failure(text),
                (err) => Result.Failure(err)
            );
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

            return await client.PostAsync(GetUri(path), new FormUrlEncodedContent(postParams));
        }

        private static async Task<T> CheckResponse<T>(
            HttpResponseMessage response,
            Func<Task<T>> responseOk,
            Func<string, T> responseUnsuccessful
        )
        {
            if (!response.IsSuccessStatusCode)
            {
                return responseUnsuccessful(await GetErrorString(response));
            }
            return await responseOk();
        }
        private static async Task<T> CheckResponseText<T>(
            HttpResponseMessage response,
            Func<string, T> responseOk,
            Func<string, T> responseUnsuccessful
        )
        {
            return await CheckResponse(
                response,
                async () => responseOk(await response.Content.ReadAsStringAsync()),
                responseUnsuccessful
            );
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
