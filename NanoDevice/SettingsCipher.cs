using System;
using System.Security.Cryptography;
using System.Text;
using SpotifyClicker.Core;
#if DESKTOP_TEST
using Aes = TestSupport.NanoAes;
#endif

namespace SpotifyClicker
{
    // AES-128-CBC with PKCS#7 and encrypt-then-MAC (HMAC-SHA256).
    // Separate keys derived from a deployment-specific 256-bit random master key.
    // This protects a copied settings file, not a full firmware/flash extraction.
    public static class SettingsCipher
    {
        private static byte[] Key(string purpose)
        { using (HMACSHA256 hmac = new HMACSHA256(Convert.FromBase64String(DeviceSecrets.StorageKey))) return hmac.ComputeHash(Encoding.UTF8.GetBytes(purpose)); }
        private static byte[] EncryptionKey()
        {
            // The native stable API selects MBEDTLS_CIPHER_AES_128_CBC.
            byte[] key = new byte[16]; Array.Copy(Key("spotify-clicker/storage/encryption/v1"), key, 16); return key;
        }
        public static byte[] Seal(byte[] plaintext)
        {
            int padding = 16 - plaintext.Length % 16;
            byte[] padded = new byte[plaintext.Length + padding]; Array.Copy(plaintext, padded, plaintext.Length);
            for (int i = plaintext.Length; i < padded.Length; i++) padded[i] = (byte)padding;
            Aes aes = new Aes(CipherMode.CBC) { Key = EncryptionKey() };
            byte[] iv = (byte[])aes.IV.Clone();
            byte[] encrypted = aes.Encrypt(padded);
            byte[] payload = new byte[17 + encrypted.Length]; payload[0] = 1;
            Array.Copy(iv, 0, payload, 1, 16); Array.Copy(encrypted, 0, payload, 17, encrypted.Length);
            byte[] tag; using (HMACSHA256 mac = new HMACSHA256(Key("spotify-clicker/storage/authentication/v1"))) tag = mac.ComputeHash(payload);
            byte[] result = new byte[payload.Length + tag.Length];
            Array.Copy(payload, result, payload.Length); Array.Copy(tag, 0, result, payload.Length, tag.Length);
            return result;
        }
        public static byte[] Open(byte[] envelope)
        {
            if (envelope.Length < 65 || envelope.Length > 16384 || envelope[0] != 1 || (envelope.Length - 49) % 16 != 0) throw new ArgumentException("Invalid settings envelope.");
            byte[] payload = new byte[envelope.Length - 32]; Array.Copy(envelope, payload, payload.Length);
            byte[] expected; using (HMACSHA256 mac = new HMACSHA256(Key("spotify-clicker/storage/authentication/v1"))) expected = mac.ComputeHash(payload);
            int mismatch = 0; for (int i=0;i<32;i++) mismatch |= expected[i] ^ envelope[payload.Length+i];
            if (mismatch != 0) throw new ArgumentException("Settings authentication failed.");
            byte[] iv = new byte[16], encrypted = new byte[payload.Length-17];
            Array.Copy(payload,1,iv,0,16); Array.Copy(payload,17,encrypted,0,encrypted.Length);
            Aes aes = new Aes(CipherMode.CBC) { Key = EncryptionKey(), IV = iv };
            byte[] padded = aes.Decrypt(encrypted); int padding = padded[padded.Length-1];
            if(padding < 1 || padding > 16) throw new ArgumentException("Invalid settings padding.");
            for(int i=padded.Length-padding;i<padded.Length;i++) if(padded[i]!=padding) throw new ArgumentException("Invalid settings padding.");
            byte[] result = new byte[padded.Length-padding]; Array.Copy(padded,result,result.Length); return result;
        }
    }
}
