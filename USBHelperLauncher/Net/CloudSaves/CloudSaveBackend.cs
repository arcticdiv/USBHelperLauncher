using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace USBHelperLauncher.Net.CloudSaves
{
    abstract class CloudSaveBackend
    {
        /// <summary>
        /// Returns nothing on successful authorization and throws an exception otherwise
        /// </summary>
        public abstract Task Authorize();

        /// <summary>
        /// Returns nothing on successful login and throws an exception otherwise
        /// </summary>
        public abstract Task CheckLogin();

        /// <summary>
        /// Returns a list of cloud saves
        /// </summary>
        public abstract Task<List<CloudSaveListItem>> ListSaves();

        /// <summary>
        /// Returns a hex-encoded hash of the save data for the given title ID,
        /// or an empty string if no save exists
        /// </summary>
        public abstract Task<string> GetSaveHash(string titleId);

        /// <summary>
        /// Returns the save data for the given title ID as a byte array
        /// </summary>
        public abstract Task<byte[]> GetSave(string titleId);

        /// <summary>
        /// Uploads the given save data for the given title ID,
        /// and returns the hex-encoded hash
        /// </summary>
        public abstract Task<string> UploadSave(string titleId, byte[] saveData);

        /// <summary>
        /// Deletes the save data for the given title ID
        /// </summary>
        public abstract Task DeleteSave(string titleId);


        protected static bool IsValidFileName(string fileName)
        {
            return Regex.IsMatch(fileName, @"^[0-9A-Fa-f]{16}\.zip$");
        }

        protected static string FileNameForTitleId(string titleId)
        {
            return $"{titleId}.zip".ToLowerInvariant();
        }
    }
}
