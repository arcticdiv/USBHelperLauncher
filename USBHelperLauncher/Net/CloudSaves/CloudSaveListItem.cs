using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace USBHelperLauncher.Net.CloudSaves
{
    struct CloudSaveListItem
    {
        public string Hash;
        public string TitleId;
        public ulong Timestamp;
        public ulong Size;

        public CloudSaveListItem(string hash, string titleId, ulong timestamp, ulong size)
        {
            Hash = hash;
            TitleId = titleId;
            Timestamp = timestamp;
            Size = size;
        }
    }
}
