using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Jezda.Common.Helpers;

/// <summary>
/// Helper class for encrypting and decrypting strings.
/// </summary>
public static class EncryptionHelper
{
    /// <summary>
    /// Encrypts a string using AES encryption.
    /// Parameters: 
    /// plainText: The string to encrypt.
    /// secretKey: The key used for encryption (must be 32 characters long).
    /// </summary>
    /// <param name="plainText"></param>
    /// <param name="secretKey"></param>
    /// <returns></returns>
    public static string Encrypt(string plainText, string secretKey)
    {
        using var aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(secretKey.PadRight(32)[..32]);
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length);

        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var writer = new StreamWriter(cs))
            writer.Write(plainText);

        return Convert.ToBase64String(ms.ToArray());
    }

    /// <summary>
    /// Decrypts a string using AES decryption.
    /// Parameters:
    /// encryptedText: The string to decrypt.
    /// secretKey: The key used for decryption (must be 32 characters long).
    /// </summary>
    /// <param name="encryptedText"></param>
    /// <param name="secretKey"></param>
    /// <returns></returns>
    public static string Decrypt(string encryptedText, string secretKey)
    {
        var fullCipher = Convert.FromBase64String(encryptedText);

        using var aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(secretKey.PadRight(32)[..32]);
        var iv = new byte[16];
        Array.Copy(fullCipher, iv, iv.Length);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream(fullCipher, 16, fullCipher.Length - 16);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var reader = new StreamReader(cs);
        return reader.ReadToEnd();
    }
}
