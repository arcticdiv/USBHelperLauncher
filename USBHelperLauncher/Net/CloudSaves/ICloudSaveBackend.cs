using System.Collections.Generic;
using System.Threading.Tasks;

namespace USBHelperLauncher.Net.CloudSaves
{
    interface ICloudSaveBackend
    {
        // All these methods should never throw under normal operation, use Result.Failure instead

        /// <summary>
        /// Returns .Success on successful login and .Failure otherwise
        /// </summary>
        Task Login();

        /// <summary>
        /// Returns a list of cloud saves
        /// </summary>
        Task<List<CloudSaveListItem>> ListSaves();

        /// <summary>
        /// Returns a hex-encoded hash of the save data for the given title ID,
        /// or an empty string if no save exists
        /// </summary>
        Task<string> GetSaveHash(string titleId);

        /// <summary>
        /// Returns the save data for the given title ID as a byte array
        /// </summary>
        Task<byte[]> GetSave(string titleId);

        /// <summary>
        /// Uploads the given save data for the given title ID,
        /// and returns the hex-encoded hash
        /// </summary>
        Task<string> UploadSave(string titleId, byte[] saveData);

        /// <summary>
        /// Deletes the save data for the given title ID
        /// </summary>
        Task DeleteSave(string titleId);
    }
}
