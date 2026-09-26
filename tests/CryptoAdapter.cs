using System.Security.Cryptography;
namespace TestSupport
{
    // Desktop implementation of the nanoFramework AES API, using the same CBC/no-padding contract.
    public sealed class NanoAes
    {
        public byte[] Key { get; set; }
        public byte[] IV { get; set; } = RandomNumberGenerator.GetBytes(16);
        public NanoAes(CipherMode mode) { if (mode != CipherMode.CBC) throw new System.ArgumentException(); }
        public byte[] Encrypt(byte[] input) { using (Aes aes=Aes.Create()) { aes.Key=Key; return aes.EncryptCbc(input,IV,PaddingMode.None); } }
        public byte[] Decrypt(byte[] input) { using (Aes aes=Aes.Create()) { aes.Key=Key; return aes.DecryptCbc(input,IV,PaddingMode.None); } }
    }
}
namespace SpotifyClicker
{
    // Synthetic test key. Never load the ignored real DeviceSecrets.cs into tests.
    internal static class DeviceSecrets { public const string StorageKey = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8="; }
}
