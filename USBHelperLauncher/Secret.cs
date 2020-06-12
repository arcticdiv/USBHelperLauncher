using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace USBHelperLauncher
{
    class Secret
    {
        private static readonly AesCryptoServiceProvider _aes = new AesCryptoServiceProvider
        {
            Mode = CipherMode.CBC,
            BlockSize = 128,
            Padding = PaddingMode.PKCS7,
            Key = Convert.FromBase64String("Tztr+FVmuRH/UfoAoImogQ=="),
            IV = Convert.FromBase64String("ziMDeW3YfmD2oxbsPf8QAw==")
        };

        internal readonly string Value;


        internal Secret(string encrypted)
        {
            try
            {
                using (var memoryStream = new MemoryStream(Convert.FromBase64String(encrypted)))
                using (var cryptoStream = new CryptoStream(memoryStream, _aes.CreateDecryptor(), CryptoStreamMode.Read))
                using (var reader = new StreamReader(cryptoStream, Encoding.ASCII))
                {
                    Value = reader.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                Program.Logger.WriteLine($"Failed to decrypt secret:\n{e}");
            }
        }
    }
}
