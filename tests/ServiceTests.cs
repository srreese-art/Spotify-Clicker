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
    public static int Run()
    {
        SettingsStore store;NetworkService network;
        SpotifyService service=Create(out store,out network);Token("rotated");State();Transport.Replies.Enqueue(new Reply{Status=204});
        service.Execute(new Command(CommandKind.Toggle,0,0));
        Check(Transport.Requests.Count==3 && Transport.Requests[2].EndsWith("/pause"),"playing pauses");
        Check(store.Current.RefreshToken=="rotated" && store.Saves==1,"refresh rotation saved");
        State(false);Transport.Replies.Enqueue(new Reply{Status=204});service.Execute(new Command(CommandKind.Toggle,0,0));
        Check(Transport.Requests[4].EndsWith("/play"),"external pause observed before toggle");
        Check(!string.Join(" ",Transport.Requests).Contains("device_id"),"no playback targeting override");

        service=Create(out store,out network);Token();State(volume:98);Transport.Replies.Enqueue(new Reply{Status=204});service.Execute(new Command(CommandKind.Volume,5,0));
        Check(Transport.Requests[2].EndsWith("volume_percent=100"),"volume upper clamp");
        Check(store.Current.RefreshToken=="old-refresh","refresh token preserved when omitted");
        State(volume:2);Transport.Replies.Enqueue(new Reply{Status=204});service.Execute(new Command(CommandKind.Volume,-5,0));
        Check(Transport.Requests[4].EndsWith("volume_percent=0"),"volume lower clamp");

        service=Create(out store,out network);Token();State(supports:false);service.Execute(new Command(CommandKind.Volume,5,0));Check(Transport.Requests.Count==2,"unsupported volume ignored");
        service=Create(out store,out network);Token();State(actions:"{\"skipping_next\":true}");service.Execute(new Command(CommandKind.Next,0,0));Check(Transport.Requests.Count==2,"restricted skip ignored");
        foreach(int status in new[]{204,404,403,401})
        {
            service=Create(out store,out network);Token();Transport.Replies.Enqueue(new Reply{Status=status});service.Execute(new Command(CommandKind.Toggle,0,0));
            Check(Transport.Requests.Count==2,"state HTTP "+status+" never mutates playback");
        }
        service=Create(out store,out network);Token();State();Transport.Replies.Enqueue(new Reply{Status=0});service.Execute(new Command(CommandKind.Next,0,0));Check(Transport.Requests.Count==3,"uncertain skip not replayed");
        service=Create(out store,out network);network.Ready=false;service.Execute(new Command(CommandKind.Next,0,0));Check(Transport.Requests.Count==0,"offline input ignored");
        service=Create(out store,out network);Transport.Replies.Enqueue(new Reply{Status=400,Body="{\"error\":\"invalid_grant\"}"});service.MaintainAuthorization();Check(store.Current.RefreshToken=="" && !service.CanAcceptCommands,"revoked token requires authorization");
        service=Create(out store,out network);Token();Transport.Replies.Enqueue(new Reply{Status=429,RetrySeconds=20});service.Execute(new Command(CommandKind.Next,0,0));service.Execute(new Command(CommandKind.Next,0,0));Check(Transport.Requests.Count==2 && !service.CanAcceptCommands,"rate limit blocks next input");

        service=Create(out store,out network);Token();
        Transport.Replies.Enqueue(new Reply{Status=200,Body="{\"is_playing\":true,\"device\":{\"is_active\":true,\"supports_volume\":false,\"volume_percent\":null}}"});
        Transport.Replies.Enqueue(new Reply{Status=204});service.Execute(new Command(CommandKind.Next,0,0));
        Check(Transport.Requests.Count==3 && Transport.Requests[2].EndsWith("/next"),"null volume does not block track controls");
        service=Create(out store,out network);Token();State();Transport.Replies.Enqueue(new Reply{Status=204});
        Transport.RequestDurationMs=4000;
        service.Execute(new Command(CommandKind.Toggle,0,0));
        Check(Transport.Requests.Count==3 && Transport.Requests[2].EndsWith("/pause"),"slow successful token and playback requests still pause");
        service=Create(out store,out network);Token();State();Transport.RequestDurationMs=16000;
        service.Execute(new Command(CommandKind.Toggle,0,0));
        Check(Transport.Requests.Count==2 && service.Status.Contains("too long"),"processing deadline prevents excessively delayed mutation");
        service=Create(out store,out network);Clock.Milliseconds=3000;
        service.Execute(new Command(CommandKind.Toggle,0,0));
        Check(Transport.Requests.Count==0 && service.Status.Contains("expired"),"old queued command rejected before network calls");
        return _checks;
    }
}
