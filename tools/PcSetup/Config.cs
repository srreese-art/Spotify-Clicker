internal sealed class Config
{
    public string WifiSsid { get; set; } = "";
    public string WifiPassword { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string RedirectUri { get; set; } = "http://127.0.0.1:8000/callback";
    public string RefreshToken { get; set; } = "";
    public string AuthorizedClientId { get; set; } = "";
}

