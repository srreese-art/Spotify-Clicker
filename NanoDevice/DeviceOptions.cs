namespace SpotifyClicker
{
    public static class DeviceOptions
    {
        // Change these GPIO numbers to match your wiring. Each switch connects
        // its GPIO to GND; the firmware enables the internal pull-up resistor.
        public const int PlayPauseButtonPin = 25;
        public const int NextTrackButtonPin = 26;
        public const int PreviousTrackButtonPin = 27;
        public const int VolumeUpButtonPin = 32;
        public const int VolumeDownButtonPin = 33;

        // ButtonEngine reads inputs in this order. Configure the names above,
        // not this array: each entry connects a physical GPIO to its action.
        public static readonly int[] Pins = {
            PlayPauseButtonPin, NextTrackButtonPin, PreviousTrackButtonPin,
            VolumeUpButtonPin, VolumeDownButtonPin
        };
        public static string SetupInstructions
        {
            get { return "Run Setup-PC.ps1 -Authorize on your PC, then rebuild and deploy."; }
        }
        public const int DebounceMs = 30;
        public const int HoldMs = 500;
        public const int RepeatMs = 300;
        public const int CommandMaxAgeMs = 2500;
        public const int QueueCapacity = 8;
        public const int VolumeStep = 10;
        public const int PlaybackPollMs = 10000;
        public const int PlaybackPollIdleMs = 2000;
        public const int PlaybackPollRetryMs = 3000;
        public const int PlaybackStateMaxAgeMs = 60000;
        public const string StorageDirectory = @"I:\SpotifyClicker";
    }
}
