using System;
using SpotifyClicker;
using SpotifyClicker.Core;
using SpotifyClicker.Spotify;

internal static class ServiceTests
{
    private static int _checks;
    private static void Check(bool valid,string label){if(!valid)throw new Exception(label);_checks++;}
    private static SpotifyService Create(out SettingsStore store,out NetworkService network)
    { Transport.Reset();store=new SettingsStore();network=new NetworkService();return new SpotifyService(store,network); }
    private static void Token(string refresh="")
    { Transport.Replies.Enqueue(new Reply{Status=200,Body="{\"access_token\":\"test-access\",\"scope\":\"user-read-playback-state user-modify-playback-state\",\"expires_in\":3600,\"refresh_token\":\""+refresh+"\"}"}); }
    private static void State(bool playing=true,int volume=50,string actions="{}",bool supports=true)
    {Transport.Replies.Enqueue(new Reply{Status=200,Body="{\"is_playing\":"+(playing?"true":"false")+",\"device\":{\"is_active\":true,\"is_restricted\":false,\"supports_volume\":"+(supports?"true":"false")+",\"volume_percent\":"+volume+"},\"actions\":{\"disallows\":"+actions+"}}"});}
    private static void Press(SpotifyService service, CommandKind kind, int delta=0)
    { service.Execute(new Command(kind,delta,Clock.Milliseconds)); }
    private static void Ok() { Transport.Replies.Enqueue(new Reply{Status=204}); }
    public static int Run()
    {
        SettingsStore store; NetworkService network;
        SpotifyService service=Create(out store,out network);
        Press(service,CommandKind.Toggle);
        Check(Transport.Requests.Count==0,"startup button never fetches state or token");
        Token("rotated");State();service.RefreshPlaybackState();
        Check(Transport.Requests.Count==2,"background initializes token and state");
        Check(store.Current.RefreshToken=="rotated" && store.Saves==1,"refresh rotation saved");
        Ok();Press(service,CommandKind.Toggle);
        Check(Transport.Requests.Count==3 && Transport.Requests[2].EndsWith("/pause"),"cached playing state pauses in one request");
        Ok();Press(service,CommandKind.Toggle);
        Check(Transport.Requests.Count==4 && Transport.Requests[3].EndsWith("/play"),"successful toggle updates cache immediately");
        State(false);service.RefreshPlaybackState();Ok();Press(service,CommandKind.Toggle);
        Check(Transport.Requests[5].EndsWith("/play"),"background observes external pause");
        Check(!string.Join(" ",Transport.Requests).Contains("device_id"),"no playback targeting override");

        service=Create(out store,out network);Token();State();service.RefreshPlaybackState();
        Ok();Press(service,CommandKind.Volume,10);Ok();Press(service,CommandKind.Volume,10);
        Check(Transport.Requests.Count==4 && Transport.Requests[3].EndsWith("volume_percent=70"),"volume accumulates without GET per press");
        Ok();Press(service,CommandKind.Volume,-10);
        Check(Transport.Requests[4].EndsWith("volume_percent=60"),"volume direction changes from cached target");
        Ok();Press(service,CommandKind.Volume,100);Press(service,CommandKind.Volume,10);
        Check(Transport.Requests.Count==6 && Transport.Requests[5].EndsWith("volume_percent=100"),"upper clamp skips redundant requests");
        Ok();Press(service,CommandKind.Volume,-200);
        Check(Transport.Requests[6].EndsWith("volume_percent=0"),"lower clamp");
        State(volume:25);service.RefreshPlaybackState();Ok();Press(service,CommandKind.Volume,10);
        Check(Transport.Requests[8].EndsWith("volume_percent=35"),"external volume picked up by polling");
        Check(store.Current.RefreshToken=="old-refresh","omitted refresh token preserved");

        service=Create(out store,out network);Token();State(supports:false);service.RefreshPlaybackState();Press(service,CommandKind.Volume,10);
        Check(Transport.Requests.Count==2,"unsupported volume ignored");
        service=Create(out store,out network);Token();State(actions:"{\"skipping_next\":true}");service.RefreshPlaybackState();Press(service,CommandKind.Next);
        Check(Transport.Requests.Count==2,"cached restriction honored");
        foreach(int status in new[]{204,404,403,401,0})
        {
            service=Create(out store,out network);Token();State();service.RefreshPlaybackState();
            Transport.Replies.Enqueue(new Reply{Status=status});service.RefreshPlaybackState();Press(service,CommandKind.Toggle);
            Check(Transport.Requests.Count==3,"background failure clears old state: "+status);
        }
        service=Create(out store,out network);Token();State();service.RefreshPlaybackState();
        Transport.Replies.Enqueue(new Reply{Status=0});Press(service,CommandKind.Next);Press(service,CommandKind.Next);
        Check(Transport.Requests.Count==3,"uncertain command invalidates cache and is not replayed");
        State();service.RefreshPlaybackState();Ok();Press(service,CommandKind.Next);
        Check(Transport.Requests.Count==5,"background recovers after failed command");
        network.Ready=false;Press(service,CommandKind.Next);service.RefreshPlaybackState();
        Check(Transport.Requests.Count==5,"offline workers do not send requests");
        network.Ready=true;Press(service,CommandKind.Next);
        Check(Transport.Requests.Count==5,"reconnect waits for new background state");

        service=Create(out store,out network);Transport.Replies.Enqueue(new Reply{Status=400,Body="{\"error\":\"invalid_grant\"}"});service.RefreshPlaybackState();
        Check(store.Current.RefreshToken=="" && !service.CanAcceptCommands,"revoked token requires authorization");
        service=Create(out store,out network);Token();State();service.RefreshPlaybackState();
        Transport.Replies.Enqueue(new Reply{Status=429,RetrySeconds=20});Press(service,CommandKind.Next);service.RefreshPlaybackState();Press(service,CommandKind.Next);
        Check(Transport.Requests.Count==3 && !service.CanAcceptCommands,"rate limit blocks both workers");
        Clock.Milliseconds=20000;State();service.RefreshPlaybackState();Ok();Press(service,CommandKind.Next);
        Check(Transport.Requests.Count==5,"polling resumes after rate limit");

        service=Create(out store,out network);Token();State();service.RefreshPlaybackState();
        Clock.Milliseconds=DeviceOptions.PlaybackStateMaxAgeMs;Press(service,CommandKind.Toggle);
        Check(Transport.Requests.Count==2,"stale state never triggers button GET or mutation");
        State();service.RefreshPlaybackState();Ok();Press(service,CommandKind.Toggle);
        Check(Transport.Requests.Count==4,"fresh background status enables commands again");
        Clock.Milliseconds+=DeviceOptions.CommandMaxAgeMs+1;
        service.Execute(new Command(CommandKind.Toggle,0,0));
        Check(Transport.Requests.Count==4 && service.Status.Contains("expired"),"old queued command rejected");
        Clock.Milliseconds=4000000;Press(service,CommandKind.Toggle);
        Check(Transport.Requests.Count==4,"button never refreshes expired token");
        Token();State();service.RefreshPlaybackState();Ok();Press(service,CommandKind.Toggle);
        Check(Transport.Requests.Count==7,"background renews token and state");

        foreach(string body in new[]{"null","{}","not JSON","{\"device\":{\"is_active\":false}}","{\"device\":{\"is_active\":true,\"is_restricted\":true}}"})
        {
            service=Create(out store,out network);Token();State();service.RefreshPlaybackState();
            Transport.Replies.Enqueue(new Reply{Status=200,Body=body});service.RefreshPlaybackState();Press(service,CommandKind.Toggle);
            Check(Transport.Requests.Count==3,"invalid or uncontrollable state invalidates cache: "+body);
        }
        service=Create(out store,out network);Token();
        Transport.Replies.Enqueue(new Reply{Status=200,Body="{\"is_playing\":true,\"device\":{\"is_active\":true,\"supports_volume\":false,\"volume_percent\":null}}"});
        service.RefreshPlaybackState();Ok();Press(service,CommandKind.Next);
        Check(Transport.Requests.Count==3,"null volume does not block track commands");

        // A button arriving during a poll waits for that connection, then sends
        // only its action. No second HTTP connection can start in parallel.
        service=Create(out store,out network);Token();State();service.RefreshPlaybackState();
        State();Ok();
        System.Threading.Tasks.Task pending=null;
        var entered=new System.Threading.ManualResetEventSlim();
        Transport.AfterResponse=()=>
        {
            pending=System.Threading.Tasks.Task.Run(()=>{ entered.Set(); Press(service,CommandKind.Volume,10); });
            Check(entered.Wait(2000),"command worker started during poll");
            Check(!pending.Wait(100) && Transport.Requests.Count==3,"button cannot overlap background HTTP");
        };
        service.RefreshPlaybackState();Check(pending.Wait(2000),"waiting action runs after poll completes");
        Ok();Transport.AfterResponse=()=>service.RefreshPlaybackState();Press(service,CommandKind.Volume,10);
        Check(Transport.Requests.Count==5 && Transport.Requests[4].EndsWith("volume_percent=70"),"poll skips active command and repeated presses use cache");
        State();Transport.AfterResponse=()=>{ network.Ready=false; };service.RefreshPlaybackState();network.Ready=true;Press(service,CommandKind.Toggle);
        Check(Transport.Requests.Count==6,"disconnect during poll invalidates response");
        service=Create(out store,out network);var queue=new CommandQueue();
        Token();State();service.PollPlaybackState(queue);
        Check(Transport.Requests.Count==2,"idle scheduler initializes status immediately");
        Clock.Milliseconds=DeviceOptions.PlaybackPollMs-1;service.PollPlaybackState(queue);
        Check(Transport.Requests.Count==2,"idle scheduler limits polling frequency");
        Clock.Milliseconds=DeviceOptions.PlaybackPollMs;service.NotifyButtonActivity();service.PollPlaybackState(queue);
        Check(Transport.Requests.Count==2,"raw input defers due poll before debounce");
        Clock.Milliseconds+=DeviceOptions.PlaybackPollIdleMs;queue.Push(new Command(CommandKind.Volume,10,Clock.Milliseconds));service.PollPlaybackState(queue);
        Check(Transport.Requests.Count==2,"queued command has priority before worker takes it");
        Ok();service.Execute(queue.Take(Clock.Milliseconds));service.PollPlaybackState(queue);
        Check(Transport.Requests.Count==3,"completed command gets idle grace before polling");
        Clock.Milliseconds+=DeviceOptions.PlaybackPollIdleMs-1;service.PollPlaybackState(queue);
        Check(Transport.Requests.Count==3,"poll waits for entire idle grace");
        Clock.Milliseconds++;State(volume:60);service.PollPlaybackState(queue);
        Check(Transport.Requests.Count==4,"overdue poll resumes after controls idle");
        Clock.Milliseconds+=DeviceOptions.PlaybackStateMaxAgeMs;service.NotifyButtonActivity();State();service.PollPlaybackState(queue);
        Check(Transport.Requests.Count==5,"stale cache can recover despite sustained input");
        Clock.Milliseconds+=DeviceOptions.PlaybackPollMs;Transport.Replies.Enqueue(new Reply{Status=0});service.PollPlaybackState(queue);
        int requests=Transport.Requests.Count;
        service.PollPlaybackState(queue);Clock.Milliseconds+=DeviceOptions.PlaybackPollRetryMs-1;service.PollPlaybackState(queue);
        Check(Transport.Requests.Count==requests,"failed polls do not busy loop");
        Clock.Milliseconds++;State();service.PollPlaybackState(queue);
        Check(Transport.Requests.Count==requests+1,"invalid status retries in background after delay");
        service=Create(out store,out network);Token();State(actions:"{\"resuming\":true,\"skipping_next\":true}");service.RefreshPlaybackState();
        Ok();Press(service,CommandKind.Toggle);Ok();Press(service,CommandKind.Toggle);
        Check(Transport.Requests.Count==4 && Transport.Requests[2].EndsWith("/pause") && Transport.Requests[3].EndsWith("/play"),"pause clears stale resuming restriction for next press");
        Press(service,CommandKind.Next);
        Check(Transport.Requests.Count==4,"toggle preserves unrelated track restriction");
        State(false,actions:"{\"pausing\":true}");service.RefreshPlaybackState();Ok();Press(service,CommandKind.Toggle);Ok();Press(service,CommandKind.Toggle);
        Check(Transport.Requests.Count==7 && Transport.Requests[5].EndsWith("/play") && Transport.Requests[6].EndsWith("/pause"),"resume clears stale pausing restriction");
        State(actions:"{\"pausing\":true}");service.RefreshPlaybackState();Press(service,CommandKind.Toggle);
        Check(Transport.Requests.Count==8 && service.Status.Contains("disallows"),"fresh toggle restriction remains honored and explained");
        service=Create(out store,out network);Token();State(volume:100);service.RefreshPlaybackState();Press(service,CommandKind.Volume,10);
        Check(Transport.Requests.Count==2 && service.Status.Contains("already at 100%"),"volume ceiling explains skipped request");
        Ok();Press(service,CommandKind.Volume,-10);
        Check(Transport.Requests.Count==3 && Transport.Requests[2].EndsWith("volume_percent=90"),"volume down works from ceiling");
        State(supports:false);service.RefreshPlaybackState();Press(service,CommandKind.Volume,10);
        Check(Transport.Requests.Count==4 && service.Status.Contains("does not support"),"unsupported volume explains skipped request");
        Transport.Replies.Enqueue(new Reply{Status=200,Body="{\"device\":{\"is_active\":true,\"supports_volume\":true,\"volume_percent\":null}}"});service.RefreshPlaybackState();Press(service,CommandKind.Volume,10);
        Check(Transport.Requests.Count==5 && service.Status.Contains("did not report"),"missing volume explains skipped request");
        return _checks;
    }
}
