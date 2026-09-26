using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
internal static class SpotifyAuthorization
{
    private const string Scopes = "user-read-playback-state user-modify-playback-state";
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    internal static async Task<string> Authorize(Config config)
    {
        string verifier = Base64Url(RandomNumberGenerator.GetBytes(32)), state = Base64Url(RandomNumberGenerator.GetBytes(32));
        string challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var listener = new TcpListener(IPAddress.Loopback, 8000);
        listener.Start(); // Loopback only; no administrator URL registration required.
        try
        {
            string url = "https://accounts.spotify.com/authorize?response_type=code&client_id=" + config.ClientId + "&redirect_uri=" + Uri.EscapeDataString(config.RedirectUri) + "&scope=" + Uri.EscapeDataString(Scopes) + "&state=" + state + "&code_challenge_method=S256&code_challenge=" + challenge;
            Console.WriteLine("Opening Spotify. Sign in and approve playback access. Waiting up to five minutes...");
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            while (true)
            {
                using var client = await listener.AcceptTcpClientAsync(deadline.Token);
                using var requestDeadline = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
                requestDeadline.CancelAfter(TimeSpan.FromSeconds(10));
                using var stream = client.GetStream();
                // Bound request headers and tolerate browser preconnections.
                var header = new List<byte>();
                try
                {
                    byte[] one = new byte[1];
                    while (header.Count < 8192)
                    {
                        if (await stream.ReadAsync(one, requestDeadline.Token) == 0) break;
                        header.Add(one[0]);
                        if (header.Count >= 4 && Encoding.ASCII.GetString(header.TakeLast(4).ToArray()) == "\r\n\r\n") break;
                    }
                }
                catch (OperationCanceledException) when (!deadline.IsCancellationRequested) { continue; }
                string first = Encoding.ASCII.GetString(header.ToArray()).Split('\r')[0];
                string[] parts = first.Split(' ');
                if (parts.Length != 3 || parts[0] != "GET" || !parts[1].StartsWith("/callback?")) continue;
                var fields = DeviceConfiguration.Query(parts[1].Substring("/callback".Length));
                if (!fields.TryGetValue("state", out string? returnedState) || returnedState != state) continue;
                bool denied = fields.ContainsKey("error");
                string body = denied ? "Authorization declined. Return to the setup terminal." : "Spotify response received. Return to the setup terminal to check completion. You may close this tab.";
                byte[] response = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/plain; charset=utf-8\r\nCache-Control: no-store\r\nConnection: close\r\nContent-Length: " + Encoding.UTF8.GetByteCount(body) + "\r\n\r\n" + body);
                try { await stream.WriteAsync(response, requestDeadline.Token); } catch (IOException) { }
                if (denied) throw new Exception("Spotify authorization was declined. Run Setup-PC.ps1 -Authorize to retry.");
                if (!fields.TryGetValue("code", out string? code) || code.Length == 0) throw new Exception("Spotify did not return an authorization code.");
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                using var reply = await http.PostAsync("https://accounts.spotify.com/api/token", new FormUrlEncodedContent(new Dictionary<string, string> {
                    ["grant_type"] = "authorization_code", ["client_id"] = config.ClientId, ["code"] = code, ["redirect_uri"] = config.RedirectUri, ["code_verifier"] = verifier
                }), deadline.Token);
                if (!reply.IsSuccessStatusCode) throw new Exception("Spotify token exchange failed (HTTP " + (int)reply.StatusCode + "). Check the registered redirect URL and retry authorization.");
                using var json = JsonDocument.Parse(await reply.Content.ReadAsStringAsync(deadline.Token));
                var root = json.RootElement;
                string granted = root.GetProperty("scope").GetString() ?? "";
                if (!Scopes.Split(' ').All(s => granted.Split(' ').Contains(s))) throw new Exception("Spotify did not grant both playback permissions.");
                string token = root.GetProperty("refresh_token").GetString() ?? "";
                if (token.Length == 0) throw new Exception("Spotify did not return a refresh token.");
                return token;
            }
        }
        finally { listener.Stop(); }
    }
}
