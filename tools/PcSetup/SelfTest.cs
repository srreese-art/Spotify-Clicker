internal static class SelfTest
{
    public static void Run()
    {
        int checks = 0;
        void Check(bool condition) { if (!condition) throw new Exception("PC setup self-test failed."); checks++; }
        var c = new Config { WifiSsid = "Test WiFi", WifiPassword = "quotes\"\\\nsecret", ClientId = "0123456789abcdef0123456789abcdef", RefreshToken = "test-refresh" };
        DeviceConfiguration.Validate(c);
        string generated = DeviceConfiguration.Generate(c);
        Check(!generated.Contains(c.WifiPassword));
        Check(generated.Contains("\\u0022") && generated.Contains("\\u005c") && generated.Contains("\\u000a"));
        string revision = generated.Split("AuthRevision = ")[1];
        c.WifiSsid = "Different WiFi";
        Check(DeviceConfiguration.Generate(c).Split("AuthRevision = ")[1] == revision);
        c.RefreshToken = "new-authorization";
        Check(DeviceConfiguration.Generate(c).Split("AuthRevision = ")[1] != revision);
        Check(DeviceConfiguration.Query("?code=a%2Bb&state=test")["code"] == "a+b");
        bool rejected = false;
        try { DeviceConfiguration.Query("state=a&state=b"); } catch { rejected = true; }
        Check(rejected);
        c.RedirectUri = "http://192.168.0.161/callback";
        rejected = false;
        try { DeviceConfiguration.Validate(c); } catch { rejected = true; }
        Check(rejected);
        c.RedirectUri = "http://127.0.0.1:8000/callback";
        c.WifiSsid = "YOUR_2.4_GHZ_WIFI_NAME";
        rejected = false;
        try { DeviceConfiguration.Validate(c); } catch { rejected = true; }
        Check(rejected);
        Console.WriteLine("PASS: " + checks + " PC setup checks (escaping, authorization revisions, callback parsing, configuration validation).");
    }
}
