using System;

namespace SpotifyClicker.Core
{
    public static class ConfigurationValidation
    {
        public static string Error(string storageKey)
        {
            try
            {
                if (Convert.FromBase64String(storageKey).Length == 32) return null;
            }
            catch { }
            return "StorageKey must be the original generated 32-byte Base64 key. Restore it from your private backup; do not regenerate it for existing settings.";
        }
    }
}
