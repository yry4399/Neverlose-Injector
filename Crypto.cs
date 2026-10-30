using System;
using System.IO;
using System.Security.Cryptography;

namespace Loader
{
    /// <summary>
    /// AES-256-CBC 解密。文件格式: [16字节 IV][PKCS7 密文]。
    /// 明文由 set-dll.ps1 在打包时加密生成，密钥在 CryptoKeys.g.cs。
    /// </summary>
    internal static class Crypto
    {
        internal static byte[] Decrypt(byte[] data)
        {
            if (data == null || data.Length < 17)
                throw new InvalidDataException("加密数据无效");

            var iv = new byte[16];
            Buffer.BlockCopy(data, 0, iv, 0, 16);

            using (var aes = Aes.Create())
            {
                aes.Key = CryptoKeys.GetKey();
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using (var d = aes.CreateDecryptor())
                    return d.TransformFinalBlock(data, 16, data.Length - 16);
            }
        }
    }
}
