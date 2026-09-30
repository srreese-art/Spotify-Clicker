using System;
using System.Device.Gpio;
using System.Diagnostics;
using System.Threading;
using SpotifyClicker.Core;
using SpotifyClicker.Spotify;

namespace SpotifyClicker
{
    public static class Program
    {
        public static void Main()
        {
            string stage = "validating configuration";
            Console.WriteLine("SPOTIFY CLICKER: application Main entered.");
            try
            {
                string error = ConfigurationValidation.Error(DeviceSecrets.StorageKey);
                if (error != null)
                {
                    Console.WriteLine(error);
                    throw new InvalidOperationException(error);
                }

                stage = "loading saved settings";
                SettingsStore store = new SettingsStore();
                NetworkService network = new NetworkService(store);
                stage = "configuring Wi-Fi";
                network.ConfigureBoot();
                store.ApplyPcConfiguration();
                network.Start();

                stage = "initializing Spotify HTTPS";
                SpotifyService spotify = new SpotifyService(store, network);
                CommandQueue queue = new CommandQueue();
                new Thread(() => RunPlayback(spotify, queue)).Start();
                new Thread(() => RunPlaybackStatus(spotify, queue)).Start();

                stage = "reading buttons";
                RunButtons(network, spotify, queue);
            }
            catch (Exception error)
            {
                // Never print exception text that might contain credentials.
                Console.WriteLine("Application stopped at " + stage + ": " + error.GetType().Name);
                Thread.Sleep(Timeout.Infinite);
            }
        }

        private static void RunPlayback(SpotifyService spotify, CommandQueue queue)
        {
            while (true)
            {
                if (!spotify.CanAcceptCommands) queue.Clear();
                else
                {
                    Command command = queue.Take(Clock.Milliseconds);
                    if (command != null) spotify.Execute(command);
                }

                Thread.Sleep(25);
            }
        }

        private static void RunPlaybackStatus(SpotifyService spotify, CommandQueue queue)
        {
            while (true)
            {
                spotify.PollPlaybackState(queue);
                Thread.Sleep(100);
            }
        }

        private static void RunButtons(NetworkService network, SpotifyService spotify, CommandQueue queue)
        {
            GpioController gpio = new GpioController();
            GpioPin[] pins = new GpioPin[DeviceOptions.Pins.Length];
            bool[] pressed = new bool[pins.Length];
            for (int i = 0; i < pins.Length; i++)
            {
                pins[i] = gpio.OpenPin(DeviceOptions.Pins[i], PinMode.InputPullUp);
                pressed[i] = pins[i].Read() == PinValue.Low;
            }

            ButtonEngine buttons = new ButtonEngine(command =>
            {
                if (spotify.CanAcceptCommands)
                {
                    queue.Push(command);
                }
                else Debug.WriteLine("Not sent: Wi-Fi, Spotify authorization, or retry delay is not ready.");
            });

            string lastNetworkStatus = "", lastSpotifyStatus = "";
            Console.WriteLine("Spotify Clicker started with PC configuration.");
            while (true)
            {
                for (int i = 0; i < pins.Length; i++)
                {
                    bool down = pins[i].Read() == PinValue.Low;
                    if (down || down != pressed[i]) spotify.NotifyButtonActivity();
                    pressed[i] = down;
                }
                buttons.Sample(pressed, Clock.Milliseconds);
                if (network.Status != lastNetworkStatus)
                {
                    lastNetworkStatus = network.Status;
                    Debug.WriteLine(lastNetworkStatus);
                }
                if (spotify.Status != lastSpotifyStatus)
                {
                    lastSpotifyStatus = spotify.Status;
                    Debug.WriteLine(lastSpotifyStatus);
                }
                Thread.Sleep(10);
            }
        }
    }
}
