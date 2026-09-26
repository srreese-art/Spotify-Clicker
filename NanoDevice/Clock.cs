using System;
using nanoFramework.Runtime.Native;

namespace SpotifyClicker
{
    public static class Clock
    {
        public static long Milliseconds { get { return (long)Environment.TickCount64; } }
        public static bool IsSynchronized { get { return DateTime.UtcNow.Year >= 2026; } }
    }
}
