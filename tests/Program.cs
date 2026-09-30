using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using SpotifyClicker;
using SpotifyClicker.Core;

internal static class Program
{
    private static int _checks;
    private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); _checks++; }
    private static void Reject(Action action, string name)
    { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, name); }
    public static void Main()
    {
        const string testKey = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";
        Check(ConfigurationValidation.Error(testKey)==null,"valid storage key accepted without setup password");
        Check(ConfigurationValidation.Error(null)!=null,"missing storage key rejected");
        Check(ConfigurationValidation.Error("")!=null,"empty storage key rejected");
        Check(ConfigurationValidation.Error("AA==")!=null,"short storage key rejected");
        Check(ConfigurationValidation.Error("broken")!=null,"invalid storage key rejected");
        CommandQueue queue = new CommandQueue();
        queue.Push(new Command(CommandKind.Volume,5,0)); queue.Push(new Command(CommandKind.Volume,5,20));
        Check(queue.Take(30).Delta == 10 && queue.Take(30) == null, "volume coalescing");
        for(int i=0;i<20;i++) queue.Push(new Command(CommandKind.Next,0,0));
        int count=0; while(queue.Take(50)!=null)count++; Check(count==DeviceOptions.QueueCapacity,"bounded queue");
        queue.Push(new Command(CommandKind.Next,0,0)); Check(queue.Take(2501)==null,"stale command discarded");
        queue.Push(new Command(CommandKind.Volume,5,0)); queue.Push(new Command(CommandKind.Volume,5,2400)); Check(queue.Take(2501)==null,"coalescing does not extend age");
        queue.Push(new Command(CommandKind.Next,0,0)); queue.Clear(); Check(queue.Take(1)==null,"outage clear");

        List<Command> emitted = new List<Command>(); bool[] keys=new bool[5];
        ButtonEngine engine=new ButtonEngine(c=>emitted.Add(c));
        keys[0]=true; engine.Sample(keys,0); keys[0]=false; engine.Sample(keys,10); keys[0]=true; engine.Sample(keys,20); engine.Sample(keys,50); engine.Sample(keys,1000);
        Check(emitted.Count==1 && emitted[0].Kind==CommandKind.Toggle,"bounce and toggle hold");
        emitted.Clear(); keys=new bool[5]; engine=new ButtonEngine(c=>emitted.Add(c));
        keys[1]=true; engine.Sample(keys,200); engine.Sample(keys,230); keys[1]=false; engine.Sample(keys,240); engine.Sample(keys,270);
        Check(emitted.Count==1 && emitted[0].Kind==CommandKind.Next,"single next on release");
        emitted.Clear(); keys=new bool[5]; engine=new ButtonEngine(c=>emitted.Add(c));
        keys[3]=true; engine.Sample(keys,0); engine.Sample(keys,30); engine.Sample(keys,499); Check(emitted.Count==1,"volume hold delay"); engine.Sample(keys,500); engine.Sample(keys,800);
        Check(emitted.Count==3 && emitted[2].Delta==DeviceOptions.VolumeStep,"volume repeat");
        emitted.Clear(); keys=new bool[5]; engine=new ButtonEngine(c=>emitted.Add(c));
        keys[0]=true; engine.Sample(keys,0); keys[0]=false; engine.Sample(keys,10); engine.Sample(keys,40);
        Check(emitted.Count==1,"short sampled tap registers without press delay");
        keys[0]=true; engine.Sample(keys,50); keys[0]=false; engine.Sample(keys,55); keys[0]=true; engine.Sample(keys,60);
        Check(emitted.Count==2,"release bounce cannot duplicate second press");
        keys[0]=false;engine.Sample(keys,70);engine.Sample(keys,100);keys[0]=true;engine.Sample(keys,110);
        Check(emitted.Count==3,"stable release rearms next tap");
        emitted.Clear();keys=new bool[5];engine=new ButtonEngine(c=>emitted.Add(c));
        keys[1]=true;engine.Sample(keys,0);keys[1]=false;engine.Sample(keys,10);engine.Sample(keys,40);
        Check(emitted.Count==1 && emitted[0].Kind==CommandKind.Next,"short track tap fires on stable release");
        emitted.Clear();keys=new bool[5];engine=new ButtonEngine(c=>emitted.Add(c));
        keys[3]=true;engine.Sample(keys,0);keys[3]=false;engine.Sample(keys,500);engine.Sample(keys,530);
        Check(emitted.Count==1,"no volume repeat while release is debouncing");
        foreach(int length in new[]{0,1,15,16,17,1000})
        {
            byte[] original=RandomNumberGenerator.GetBytes(length); byte[] encrypted=SettingsCipher.Seal(original);
            Check(Convert.ToHexString(SettingsCipher.Open(encrypted))==Convert.ToHexString(original),"encrypted settings round trip "+length);
            Check(Convert.ToHexString(encrypted)!=Convert.ToHexString(SettingsCipher.Seal(original)),"fresh storage IV "+length);
        }
        byte[] envelope=SettingsCipher.Seal(Encoding.UTF8.GetBytes("refresh-token-fixture"));
        for(int i=0;i<envelope.Length;i++)
        {
            byte[] changed=(byte[])envelope.Clone(); changed[i]^=1;
            Reject(()=>SettingsCipher.Open(changed),"settings tamper byte "+i);
        }
        Console.WriteLine("PASS: " + _checks + " core behavior checks (production source linked directly).");
        Console.WriteLine("PASS: " + ServiceTests.Run() + " Spotify service checks (production service, simulated network/clock/storage/JSON boundary).");
    }
}
