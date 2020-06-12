using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using USBHelperLauncher.Configuration;

namespace USBHelperLauncher.Net.CloudSaves
{
    class LocalCloudSaveBackend : CloudSaveBackend
    {
        public override Task Authorize()
        {
            return Task.FromResult(0);
        }

        public override Task CheckLogin()
        {
            if (!Directory.Exists(Settings.LocalCloudSaveFolder))
            {
                throw new Exception($"Directory \"{Settings.LocalCloudSaveFolder}\" does not exist");
            }
            return Task.FromResult(0);
        }

        public override Task<List<CloudSaveListItem>> ListSaves()
        {
            return Task.FromResult(
                (from file in new DirectoryInfo(Settings.LocalCloudSaveFolder).GetFiles()
                 where IsValidFileName(file.Name)
                 let titleId = Path.GetFileNameWithoutExtension(file.Name)
                 select new CloudSaveListItem(
                     GetSaveHashInternal(titleId),
                     titleId,
                     (ulong)file.LastWriteTimeUtc.Subtract(new DateTime(1970, 1, 1)).TotalSeconds,
                     (ulong)file.Length)).ToList()
            );
        }

        public override Task<string> GetSaveHash(string titleId)
        {
            return Task.FromResult(GetSaveHashInternal(titleId) ?? "");
        }

        public override Task<byte[]> GetSave(string titleId)
        {
            return Task.FromResult(File.ReadAllBytes(PathForTitleId(titleId)));
        }

        public override Task<string> UploadSave(string titleId, byte[] saveData)
        {
            var path = PathForTitleId(titleId);
            Directory.CreateDirectory(Directory.GetParent(path).FullName); // make sure directory exists
            File.WriteAllBytes(path, saveData);

            using (var memoryStream = new MemoryStream(saveData))
            {
                return Task.FromResult(HashStream(memoryStream));
            }
        }

        public override Task DeleteSave(string titleId)
        {
            File.Delete(PathForTitleId(titleId));
            return Task.FromResult(0);
        }


        private static string GetSaveHashInternal(string titleId)
        {
            var path = PathForTitleId(titleId);
            if (!File.Exists(path))
            {
                return null;
            }

            using (var fileStream = File.OpenRead(path))
            {
                return HashStream(fileStream);
            }
        }

        private static string HashStream(Stream stream)
        {
            using (var md5 = MD5.Create())
            {
                var hashBytes = md5.ComputeHash(stream);
                return BitConverter.ToString(hashBytes).Replace("-", "");
            }
        }

        private static string PathForTitleId(string titleId)
        {
            return Path.Combine(Settings.LocalCloudSaveFolder, FileNameForTitleId(titleId));
        }
    }
}
