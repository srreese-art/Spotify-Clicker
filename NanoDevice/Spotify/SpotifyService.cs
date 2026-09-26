using System;

using nanoFramework.Json;
using SpotifyClicker.Core;

namespace SpotifyClicker.Spotify
{
    public sealed class SpotifyService
    {
        private readonly SettingsStore _store;
        private readonly NetworkService _network;
        private readonly Transport _http = new Transport();
        private readonly object _sync = new object();
        private string _access;
        private long _expires;
        public string Status = "Spotify is not connected.";
        public bool CanAcceptCommands { get { return _network.Ready && _http.Available && _store.Current.RefreshToken.Length > 0; } }
        public SpotifyService(SettingsStore store, NetworkService network) { _store = store; _network = network; }
        private void AcceptToken(Reply reply)
        {
            TokenResponse token = (TokenResponse)JsonConvert.DeserializeObject(reply.Body, typeof(TokenResponse));
            if (string.IsNullOrEmpty(token.access_token) || token.expires_in < 60)
                throw new ArgumentException("Spotify returned an incomplete token response. Authorize again.");
            if (!string.IsNullOrEmpty(token.refresh_token))
            {
                lock (_store.Sync) { _store.Current.RefreshToken = token.refresh_token; _store.Save(); }
            }
            _access = token.access_token; _expires = Clock.Milliseconds + (long)(token.expires_in - 30) * 1000;
        }
        private bool EnsureToken()
        {
            if (_access != null && Clock.Milliseconds < _expires) return true;
            if (!CanAcceptCommands) return false;
            Status = "Refreshing Spotify access...";
            Reply reply = _http.Send("https://accounts.spotify.com/api/token", "POST", "grant_type=refresh_token&client_id=" + _store.Current.ClientId + "&refresh_token=" + EncodingTools.Encode(_store.Current.RefreshToken), null);
            if (reply.Status == 200) { AcceptToken(reply); Status = "Spotify connected."; return true; }
            if (reply.Status == 400 || reply.Status == 401)
            {
                TokenResponse failure = null;
                try { failure = (TokenResponse)JsonConvert.DeserializeObject(reply.Body, typeof(TokenResponse)); } catch { }
                if (failure != null && (failure.error == "invalid_grant" || failure.error == "invalid_client"))
                {
                    lock (_store.Sync) { _store.Current.RefreshToken = ""; _store.Save(); _store.Save(); }
                    Status = "Authorization is no longer valid. " + DeviceOptions.SetupInstructions + " Then authorize again.";
                    return false;
                }
            }
            DescribeFailure(reply); return false;
        }
        public void MaintainAuthorization()
        {
            lock (_sync)
            {
                if (!CanAcceptCommands) return;
                try { EnsureToken(); } catch { Status = "Could not refresh or persist authorization. " + DeviceOptions.SetupInstructions; }
            }
        }
        public void Execute(Command command)
        {
            lock (_sync)
            {
                try
                {
                    if (Clock.Milliseconds - command.Created > DeviceOptions.CommandMaxAgeMs) { Status = "Queued button command expired. Press again."; return; }
                    long started = Clock.Milliseconds;
                    if (!CanAcceptCommands || !EnsureToken()) return;
                    if (Clock.Milliseconds - started > DeviceOptions.CommandProcessingMs) { Status = "Spotify connection took too long. Press again."; return; }
                    Reply stateReply = _http.Send("https://api.spotify.com/v1/me/player", "GET", null, _access);
                    if (stateReply.Status == 204 || stateReply.Status == 404) { Status = "No playback session. Start Spotify on your listening device."; return; }
                    if (stateReply.Status != 200) { DescribeFailure(stateReply); return; }
                    PlaybackState state = (PlaybackState)JsonConvert.DeserializeObject(stateReply.Body, typeof(PlaybackState));
                    if (state.device == null || !state.device.is_active || state.device.is_restricted) { Status = "No active controllable playback device."; return; }
                    if (!_network.Ready) { Status = "Wi-Fi disconnected while checking playback. Press again."; return; }
                    if (Clock.Milliseconds - started > DeviceOptions.CommandProcessingMs) { Status = "Spotify playback check took too long. Press again."; return; }
                    Disallows denied = state.actions == null ? null : state.actions.disallows;
                    string path, method = "PUT";
                    switch (command.Kind)
                    {
                        case CommandKind.Toggle:
                            if (denied != null && (state.is_playing ? denied.pausing : denied.resuming)) return;
                            path = state.is_playing ? "/pause" : "/play"; break;
                        case CommandKind.Next:
                            if (denied != null && denied.skipping_next) return;
                            path = "/next"; method = "POST"; break;
                        case CommandKind.Previous:
                            if (denied != null && denied.skipping_prev) return;
                            path = "/previous"; method = "POST"; break;
                        case CommandKind.Volume:
                            if (!state.device.supports_volume || state.device.volume_percent == null || command.Delta == 0) return;
                            int volume = int.Parse(state.device.volume_percent.ToString());
                            path = "/volume?volume_percent=" + CommandQueue.Clamp(volume + command.Delta, 0, 100); break;
                        default: return;
                    }
                    // Omit device_id: Spotify targets the active device. Never transfer playback.
                    Reply result = _http.Send("https://api.spotify.com/v1/me/player" + path, method, "", _access);
                    if (result.Status >= 200 && result.Status < 300) Status = "Playback command accepted.";
                    else DescribeFailure(result);
                }
                catch { Status = "Playback response could not be processed. Command discarded."; }
            }
        }
        private void DescribeFailure(Reply reply)
        {
            if (reply.Status == 401) { _access = null; Status = "Access token rejected; refresh before the next command."; }
            else if (reply.Status == 403) Status = "Spotify refused this action. Check Premium, developer app access, and device restrictions.";
            else if (reply.Status == 429) Status = "Spotify rate limit: waiting " + reply.RetrySeconds + " seconds. Commands are discarded while waiting.";
            else if (reply.Status == 404) Status = "No controllable playback session.";
            else Status = "Spotify request failed (HTTP " + reply.Status + "). Command was not retried.";
        }
    }
}
