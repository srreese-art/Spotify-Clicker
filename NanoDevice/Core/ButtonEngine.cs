namespace SpotifyClicker.Core
{
    // Debounced input state machine, shared with desktop tests. Track actions occur on
    // release; volume buttons repeat while held, and play/pause fires once per press.
    public sealed class ButtonEngine
    {
        private readonly bool[] _raw = new bool[5];
        private readonly bool[] _down = new bool[5];
        private readonly long[] _changed = new long[5];
        private readonly long[] _repeat = new long[5];
        public delegate void Emit(Command command);
        private readonly Emit _emit;
        public ButtonEngine(Emit emit) { _emit = emit; }
        public void Sample(bool[] pressed, long now)
        {
            for (int i = 0; i < 5; i++)
            {
                if (_raw[i] != pressed[i]) { _raw[i] = pressed[i]; _changed[i] = now; }
                if (_down[i] != _raw[i] && now - _changed[i] >= DeviceOptions.DebounceMs)
                {
                    _down[i] = _raw[i];
                    if (_down[i])
                    {
                        if (i == 0) Send(CommandKind.Toggle, 0, now);
                        if (i >= 3) { Send(CommandKind.Volume, i == 3 ? DeviceOptions.VolumeStep : -DeviceOptions.VolumeStep, now); _repeat[i] = now + DeviceOptions.HoldMs; }
                    }
                    else if ((i == 1 || i == 2))
                        Send(i == 1 ? CommandKind.Next : CommandKind.Previous, 0, now);
                }
            }
            for (int i = 3; i < 5; i++)
                if (_down[i] && now >= _repeat[i])
                { Send(CommandKind.Volume, i == 3 ? DeviceOptions.VolumeStep : -DeviceOptions.VolumeStep, now); _repeat[i] = now + DeviceOptions.RepeatMs; }
        }
        private void Send(CommandKind kind, int delta, long now) { _emit(new Command(kind, delta, now)); }
    }
}
