using System;
using System.IO;
using System.Text;
using nanoFramework.Json;
using SpotifyClicker.Core;

namespace SpotifyClicker
{
    public sealed class Settings
    {
        public int Version { get; set; } = 1;
        public int Generation { get; set; }
        public string Ssid { get; set; } = "";
        public string WifiPassword { get; set; } = "";
        public string ClientId { get; set; } = "";
        public string RedirectUri { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public string AuthRevision { get; set; } = "";
        // Legacy serialized field retained for existing settings files; no portal uses it.
        public bool SetupRequested { get; set; }
    }
    public sealed class SettingsStore
    {
        public readonly object Sync = new object();
        public Settings Current { get; private set; }
        private int _activeSlot;
        private string Path(int slot) { return DeviceOptions.StorageDirectory + "\\settings" + slot + ".dat"; }
        public SettingsStore()
        {
            // Never format a drive: unrelated user data might be present.
            if (!Directory.Exists(@"I:\")) throw new InvalidOperationException("Internal I: storage unavailable. Check firmware target.");
            Directory.CreateDirectory(DeviceOptions.StorageDirectory);
            Settings a = Read(0), b = Read(1);
            if (a == null && b == null)
            {
                if (File.Exists(Path(0)) || File.Exists(Path(1))) throw new InvalidOperationException("Both settings copies are damaged. Recover through USB; see documentation.");
                // First write happens after ConfigureBoot enables Wi-Fi entropy.
                Current = new Settings();
            }
            else if (b != null && (a == null || b.Generation > a.Generation)) { Current = b; _activeSlot = 1; }
            else Current = a;
        }
        private Settings Read(int slot)
        {
            try
            {
                if (!File.Exists(Path(slot))) return null;
                byte[] plaintext = SettingsCipher.Open(File.ReadAllBytes(Path(slot)));
                string json = Encoding.UTF8.GetString(plaintext, 0, plaintext.Length);
                Settings settings = (Settings)JsonConvert.DeserializeObject(json, typeof(Settings));
                return settings.Version == 1 ? settings : null;
            }
            catch { return null; }
        }
        public void Save()
        {
            lock (Sync)
            {
                Current.Generation++;
                string json = JsonConvert.SerializeObject(Current);
                byte[] data = SettingsCipher.Seal(Encoding.UTF8.GetBytes(json));
                int next = 1 - _activeSlot;
                File.WriteAllBytes(Path(next), data);
                Settings verified = Read(next);
                if (verified == null || verified.Generation != Current.Generation) throw new IOException("Could not persist settings.");
                _activeSlot = next;
            }
        }
        public void ApplyPcConfiguration()
        {
            if (ProvisionedConfig.Ssid.Length == 0 || ProvisionedConfig.RefreshToken.Length == 0)
                throw new InvalidOperationException("Run Setup-PC.ps1 with your device-config.json, then rebuild and deploy.");
            lock (Sync)
            {
                bool changed = Current.SetupRequested || Current.Ssid != ProvisionedConfig.Ssid || Current.WifiPassword != ProvisionedConfig.WifiPassword;
                Current.SetupRequested = false;
                Current.Ssid = ProvisionedConfig.Ssid;
                Current.WifiPassword = ProvisionedConfig.WifiPassword;
                // Import a new authorization once. Reboots and Wi-Fi edits must
                // preserve refresh-token rotation (and revocation) on the device.
                if (Current.AuthRevision != ProvisionedConfig.AuthRevision)
                {
                    Current.ClientId = ProvisionedConfig.ClientId;
                    Current.RedirectUri = ProvisionedConfig.RedirectUri;
                    Current.RefreshToken = ProvisionedConfig.RefreshToken;
                    Current.AuthRevision = ProvisionedConfig.AuthRevision;
                    changed = true;
                }
                if (changed) { Save(); Save(); }
            }
        }
    }
}
