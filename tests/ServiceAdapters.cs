using System;
using System.Collections.Generic;

namespace nanoFramework.Json
{
    // Test boundary only; firmware builds and uses the actual nanoFramework serializer.
    public static class JsonConvert
    { public static object DeserializeObject(string value, Type type) { return System.Text.Json.JsonSerializer.Deserialize(value,type); } }
}
namespace SpotifyClicker
{
    public static class Clock { public static long Milliseconds { get; set; } public static bool IsSynchronized => true; }
    public sealed class Settings
    { public string ClientId="0123456789abcdef0123456789abcdef",RedirectUri="https://example.org/",RefreshToken="old-refresh"; }
    public sealed class SettingsStore
    { public readonly object Sync=new object(); public Settings Current=new Settings(); public int Saves; public void Save(){ Saves++; } public void Clear(){Current=new Settings{RefreshToken=""};} }
    public sealed class NetworkService { public bool Ready=true; public void ClearSavedNetwork(){Ready=false;} }
}
namespace SpotifyClicker.Spotify
{
    public sealed class Reply { public int Status; public string Body=""; public int RetrySeconds; }
    public sealed class Transport
    {
        public static readonly Queue<Reply> Replies=new Queue<Reply>();
        public static readonly List<string> Requests=new List<string>();
        public static readonly List<string> Bodies=new List<string>();
        public static long BlockedUntil;
        public static long RequestDurationMs;
        public static Action AfterResponse;
        public bool Available => Clock.Milliseconds>=BlockedUntil;
        public Reply Send(string url,string method,string body,string token)
        {
            Requests.Add(method+" "+url); Bodies.Add(body);
            Clock.Milliseconds += RequestDurationMs;
            if(Replies.Count==0)throw new Exception("Unexpected HTTP request");
            Reply result=Replies.Dequeue(); if(result.Status==429)BlockedUntil=Clock.Milliseconds+result.RetrySeconds*1000;
            Action after=AfterResponse; AfterResponse=null; after?.Invoke();
            return result;
        }
        public static void Reset(){ Replies.Clear(); Requests.Clear(); Bodies.Clear(); BlockedUntil=0; Clock.Milliseconds=0; RequestDurationMs=0; AfterResponse=null; }
    }
}
