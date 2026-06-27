using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MessagePack;

namespace EasyPlayscript.DataModel;

public static class PlayscriptLoader
{
    public static Dictionary<string, ScriptBlock> LoadScripts(string path, string key)
    {
        var data = LoadData(path, key);
        return data.Scripts;
    }

    public static Dictionary<string, TextBlock> LoadTexts(string path, string key)
    {
        var data = LoadData(path, key);
        return data.Texts;
    }

    private static PlayscriptData LoadData(string path, string key)
    {
        var bytes = File.ReadAllBytes(path);
        var decrypted = AesDecrypt(bytes, key);
        return MessagePackSerializer.Deserialize<PlayscriptData>(decrypted);
    }

    public static byte[] AesEncrypt(byte[] data, string? key)
    {
        if (string.IsNullOrEmpty(key))
            return data;

        using (var aes = Aes.Create())
        {
            aes.Key = DeriveKey(key!);
            aes.GenerateIV();
            using (var encryptor = aes.CreateEncryptor())
            {
                var encrypted = encryptor.TransformFinalBlock(data, 0, data.Length);
                var result = new byte[aes.IV.Length + encrypted.Length];
                Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
                Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);
                return result;
            }
        }
    }

    public static byte[] AesDecrypt(byte[] data, string? key)
    {
        if (string.IsNullOrEmpty(key))
            return data;

        using (var aes = Aes.Create())
        {
            aes.Key = DeriveKey(key!);
            var iv = new byte[16];
            Buffer.BlockCopy(data, 0, iv, 0, 16);
            aes.IV = iv;
            using (var decryptor = aes.CreateDecryptor())
            {
                return decryptor.TransformFinalBlock(data, 16, data.Length - 16);
            }
        }
    }

    private static byte[] DeriveKey(string key)
    {
        using (var sha = SHA256.Create())
        {
            return sha.ComputeHash(Encoding.UTF8.GetBytes(key));
        }
    }
}