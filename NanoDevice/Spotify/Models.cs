namespace SpotifyClicker.Spotify
{
    public sealed class TokenResponse
    {
        public string access_token { get; set; }
        public string refresh_token { get; set; }
        public int expires_in { get; set; }
        public string error { get; set; }
        public string scope { get; set; }
    }
    public sealed class PlaybackState
    {
        public bool is_playing { get; set; }
        public PlaybackDevice device { get; set; }
        public PlaybackActions actions { get; set; }
    }
    public sealed class PlaybackDevice
    {
        public bool is_active { get; set; }
        public bool is_restricted { get; set; }
        public bool supports_volume { get; set; }
        // Spotify can return null when device volume is unavailable.
        public object volume_percent { get; set; }
    }
    public sealed class PlaybackActions { public Disallows disallows { get; set; } }
    public sealed class Disallows
    {
        public bool pausing { get; set; }
        public bool resuming { get; set; }
        public bool skipping_next { get; set; }
        public bool skipping_prev { get; set; }
    }
}
