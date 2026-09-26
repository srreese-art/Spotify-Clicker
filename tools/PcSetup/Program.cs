using System.Text.Json;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Contains("--self-test")) { SelfTest.Run(); return 0; }
            string path = Path.GetFullPath(args[0]), output = Path.GetFullPath(args[1]);
            var config = JsonSerializer.Deserialize<Config>(File.ReadAllText(path)) ?? throw new Exception("Config file is empty.");
            DeviceConfiguration.Validate(config);
            if (args.Contains("--authorize") || config.RefreshToken.Length == 0 || config.AuthorizedClientId != config.ClientId)
            {
                config.RefreshToken = await SpotifyAuthorization.Authorize(config);
                config.AuthorizedClientId = config.ClientId;
                File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(path + ".tmp", path, true);
            }
            File.WriteAllText(output + ".tmp", DeviceConfiguration.Generate(config));
            File.Move(output + ".tmp", output, true);
            Console.WriteLine("PC configuration ready. Credentials were saved privately and were not printed. Build and deploy the controller next.");
            return 0;
        }
        catch (Exception ex)
        {
            // HTTP bodies/URLs and callback codes must never enter console logs.
            Console.Error.WriteLine(ex.GetType() == typeof(Exception) ? ex.Message : "Setup failed (" + ex.GetType().Name + "). Check your config, internet connection, and that port 8000 is free; retry Setup-PC.ps1.");
            return 1;
        }
    }
}
