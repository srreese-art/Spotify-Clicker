using System;

namespace SpotifyClicker.Core
{
    public enum CommandKind { Toggle, Next, Previous, Volume }
    public sealed class Command
    {
        public CommandKind Kind;
        public int Delta;
        public long Created;
        public Command(CommandKind kind, int delta, long created) { Kind = kind; Delta = delta; Created = created; }
    }

    public sealed class CommandQueue
    {
        private readonly Command[] _items = new Command[DeviceOptions.QueueCapacity];
        private int _count;
        public bool HasPending { get { lock (this) { return _count > 0; } } }
        public void Clear() { lock (this) { for (int i = 0; i < _count; i++) _items[i] = null; _count = 0; } }
        public void Push(Command item)
        {
            lock (this)
            {
                if (_count > 0 && item.Kind == CommandKind.Volume && _items[_count - 1].Kind == CommandKind.Volume)
                {
                    Command last = _items[_count - 1];
                    last.Delta = Clamp(last.Delta + item.Delta, -100, 100);
                    // Keep the oldest timestamp so a held key cannot keep an old command alive.
                    return;
                }
                if (_count < _items.Length) _items[_count++] = item;
            }
        }
        public Command Take(long now)
        {
            lock (this)
            {
                while (_count > 0)
                {
                    Command item = _items[0];
                    for (int i = 1; i < _count; i++) _items[i - 1] = _items[i];
                    _items[--_count] = null;
                    if (now - item.Created <= DeviceOptions.CommandMaxAgeMs) return item;
                }
                return null;
            }
        }
        public static int Clamp(int value, int min, int max) { return value < min ? min : value > max ? max : value; }
    }

}
