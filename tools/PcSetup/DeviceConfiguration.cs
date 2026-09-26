using System.Security.Cryptography;
using System.Text;
internal static class DeviceConfiguration
{
    private static string Literal(string text) => "\"" + string.Concat(text.Select(c => "\\u" + ((int)c).ToString("x4"))) + "\"";
    internal static void Validate(Config c)
    {
        if (Encoding.UTF8.GetByteCount(c.WifiSsid) is < 1 or > 32 || c.WifiSsid == "YOUR_2.4_GHZ_WIFI_NAME") throw new Exception("Set WifiSsid to your 2.4 GHz network name.");
        if (c.WifiPassword.Length is < 8 or > 63 || c.WifiPassword == "YOUR_WIFI_PASSWORD") throw new Exception("Set WifiPassword to your 8-63 character home Wi-Fi password.");
        if (c.ClientId.Length != 32 || !c.ClientId.All(Uri.IsHexDigit)) throw new Exception("ClientId must be the 32-character Spotify client ID.");
        if (c.RedirectUri != "http://127.0.0.1:8000/callback") throw new Exception("Register and use exactly http://127.0.0.1:8000/callback in Spotify.");
    }
    internal static string Generate(Config c)
    {
        string revision = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(c.ClientId + "\n" + c.RefreshToken)));
        return "// Generated privately by Setup-PC.ps1. Do not commit or share.\nnamespace SpotifyClicker { internal static class ProvisionedConfig {\n" +
            string.Join("\n", new[] { ("Ssid", c.WifiSsid), ("WifiPassword", c.WifiPassword), ("ClientId", c.ClientId), ("RedirectUri", c.RedirectUri), ("RefreshToken", c.RefreshToken), ("AuthRevision", revision) }.Select(p => "public const string " + p.Item1 + " = " + Literal(p.Item2) + ";")) + "\n} }\n";
    }
    internal static Dictionary<string, string> Query(string query)
    {
        var result = new Dictionary<string, string>();
        foreach (string item in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] pair = item.Split('=', 2);
            if (!result.TryAdd(Uri.UnescapeDataString(pair[0]), pair.Length == 2 ? Uri.UnescapeDataString(pair[1].Replace('+', ' ')) : "")) throw new Exception("Duplicate authorization response field.");
        }
        return result;
    }
}
