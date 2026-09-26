using System;
using System.Device.Wifi;
using System.Net.NetworkInformation;
using System.Threading;
using nanoFramework.Networking;
using nanoFramework.Runtime.Native;

namespace SpotifyClicker
{
    public sealed class NetworkService
    {
        private readonly SettingsStore _store;
        private bool _connecting;
        private bool _connected;
        private long _retryAt;
        private int _retrySeconds = 2;
        public string Status = "Waiting for Wi-Fi.";

        public NetworkService(SettingsStore store) { _store = store; }

        private static NetworkInterface Find(NetworkInterfaceType type)
        {
            foreach (NetworkInterface network in NetworkInterface.GetAllNetworkInterfaces())
                if (network.NetworkInterfaceType == type) return network;
            throw new InvalidOperationException("Firmware lacks required Wi-Fi interface.");
        }

        public string Address { get { return Find(NetworkInterfaceType.Wireless80211).IPv4Address; } }
        // ConnectDhcp returns readiness directly; its event-based counterpart's
        // NetworkReady signal is not set by this API.
        private bool WifiConnected { get { return _connected && Address != "0.0.0.0"; } }
        public bool Ready { get { return !_connecting && WifiConnected && Clock.IsSynchronized; } }

        public void ConfigureBoot()
        {
            NetworkInterface station = Find(NetworkInterfaceType.Wireless80211);
            Wireless80211Configuration wifi = Wireless80211Configuration.GetAllWireless80211Configurations()[station.SpecificConfigId];
            bool reboot = false;
            if ((wifi.Options & Wireless80211Configuration.ConfigurationOptions.Enable) == 0)
            {
                wifi.Options = Wireless80211Configuration.ConfigurationOptions.Enable;
                wifi.SaveConfiguration();
                reboot = true;
            }
            // Migration from older firmware: disable its hotspot, without starting
            // any portal services or requiring an AP interface on future targets.
            foreach (NetworkInterface network in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (network.NetworkInterfaceType != NetworkInterfaceType.WirelessAP) continue;
                WirelessAPConfiguration ap = WirelessAPConfiguration.GetAllWirelessAPConfigurations()[network.SpecificConfigId];
                if (ap.Options == WirelessAPConfiguration.ConfigurationOptions.None) continue;
                ap.Options = WirelessAPConfiguration.ConfigurationOptions.None;
                ap.SaveConfiguration();
                reboot = true;
            }
            if (reboot)
            {
                Power.RebootDevice();
                Thread.Sleep(Timeout.Infinite);
            }
            WifiAdapter.FindAllAdapters()[0].SetDeviceName("spotify-clicker");
        }

        public void Start() { new Thread(Run).Start(); }

        private void Run()
        {
            while (true)
            {
                try
                {
                    if (!WifiConnected && Clock.Milliseconds >= _retryAt) Connect();
                }
                catch
                {
                    Status = "Network connection failed; retrying.";
                    _retryAt = Clock.Milliseconds + 30000;
                }
                finally { _connecting = false; }
                Thread.Sleep(1000);
            }
        }

        private void Connect()
        {
            string ssid, password;
            lock (_store.Sync)
            {
                ssid = _store.Current.Ssid;
                password = _store.Current.WifiPassword;
            }
            if (ssid.Length == 0) return;
            _connecting = true;
            Status = "Connecting to Wi-Fi and synchronizing time...";
            using (CancellationTokenSource timeout = new CancellationTokenSource(25000))
                _connected = WifiNetworkHelper.ConnectDhcp(ssid, password, WifiReconnectionKind.Automatic, requiresDateTime: true, token: timeout.Token);

            if (_connected)
            {
                _retrySeconds = 2;
                _retryAt = Clock.Milliseconds + 10000;
                Status = "Wi-Fi connected. IP address: " + Address;
            }
            else
            {
                Status = "Wi-Fi or time synchronization failed. Check PC configuration and internet access.";
                _retryAt = Clock.Milliseconds + _retrySeconds * 1000;
                _retrySeconds = Core.CommandQueue.Clamp(_retrySeconds * 2, 2, 60);
            }
        }
    }
}