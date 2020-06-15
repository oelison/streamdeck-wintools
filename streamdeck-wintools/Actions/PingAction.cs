using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WinTools
{

    //---------------------------------------------------
    //          BarRaider's Hall Of Fame
    // Subscriber: Nurfballs
    //---------------------------------------------------
    [PluginActionId("com.barraider.wintools.ping")]
    public class PingAction : PluginBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    ServerName = String.Empty,
                    ServerTitle = String.Empty,
                    PingFrequency = PING_FREQUENCY_DEFAULT_MS.ToString(),
                    NormalLatency = NORMAL_LATENCY_DEFAULT_MS.ToString(),
                    LowImage = String.Empty,
                    HighImage = String.Empty,
                    TimeoutImage = String.Empty
                };
                return instance;
            }

            [JsonProperty(PropertyName = "serverName")]
            public String ServerName { get; set; }

            [JsonProperty(PropertyName = "serverTitle")]
            public String ServerTitle { get; set; }

            [JsonProperty(PropertyName = "pingFrequency")]
            public String PingFrequency { get; set; }

            [JsonProperty(PropertyName = "normalLatency")]
            public String NormalLatency { get; set; }

            [FilenameProperty]
            [JsonProperty(PropertyName = "lowImage")]
            public String LowImage { get; set; }

            [FilenameProperty]
            [JsonProperty(PropertyName = "highImage")]
            public String HighImage { get; set; }

            [FilenameProperty]
            [JsonProperty(PropertyName = "timeoutImage")]
            public String TimeoutImage { get; set; }
        }

        #region Private Members
        private const int PING_FREQUENCY_DEFAULT_MS = 1000;
        private const int NORMAL_LATENCY_DEFAULT_MS = 40;

        private int pingFrequency = PING_FREQUENCY_DEFAULT_MS;
        private readonly PluginSettings settings;
        private readonly System.Net.NetworkInformation.Ping pingSender = new System.Net.NetworkInformation.Ping();
        private readonly System.Timers.Timer tmrPingServer = new System.Timers.Timer();
        private readonly byte[] pingBuffer;
        private IPAddress ipAddress = null;
        private bool isValidHost = false;
        private long pingLatency = 0;
        private int normalLatency = NORMAL_LATENCY_DEFAULT_MS;
        private bool pingCanceled = false;
        private bool isPaused = false;

        #endregion
        public PingAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
            }
            else
            {
                this.settings = payload.Settings.ToObject<PluginSettings>();
            }
            pingSender.PingCompleted += Ping_PingCompleted;
            tmrPingServer.Elapsed += TmrPingServer_Elapsed;

            // Create a buffer of 32 bytes of data to be transmitted.
            string pingData = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            pingBuffer = Encoding.ASCII.GetBytes(pingData);

            InitializeSettings();
        }

        public override void Dispose()
        {
            tmrPingServer.Stop();
            pingSender.PingCompleted -= Ping_PingCompleted;
            tmrPingServer.Elapsed -= TmrPingServer_Elapsed;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");
        }

        public override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Key Pressed {this.GetType()}");

            isPaused = !isPaused;
            if (isPaused)
            {
                tmrPingServer.Stop();
            }
            else
            {
                StartPing();
            }
        }

        public override void KeyReleased(KeyPayload payload) 
        {
        }

        public async override void OnTick()
        {
            string server = string.IsNullOrEmpty(settings.ServerTitle) ? settings.ServerName : settings.ServerTitle;
            if (isValidHost && tmrPingServer.Enabled) // Ping is running
            {
                if (pingCanceled)
                {
                    await Connection.SetTitleAsync($"{server}\nTIMEOUT");
                    await HandleLatencyImage(-1);
                }
                else
                {
                    await Connection.SetTitleAsync($"{server}\n{pingLatency} ms");
                    HandleLatencyImage(pingLatency);
                }
            }
            else if (isValidHost && isPaused)
            {
                await Connection.SetTitleAsync($"{server}\n[Paused]");
            }
            else
            {
                // Show some kind of alert
                await Connection.SetTitleAsync("Invalid\nSettings");
            }
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(settings, payload.Settings);
            InitializeSettings();
            SaveSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #region Private Methods

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        private void InitializeSettings()
        {
            tmrPingServer.Stop();
            if (String.IsNullOrEmpty(settings.PingFrequency) || !Int32.TryParse(settings.PingFrequency, out pingFrequency))
            {
                settings.PingFrequency = PING_FREQUENCY_DEFAULT_MS.ToString();
            }

            if (String.IsNullOrEmpty(settings.NormalLatency) || !Int32.TryParse(settings.NormalLatency, out normalLatency))
            {
                settings.NormalLatency = NORMAL_LATENCY_DEFAULT_MS.ToString();
            }

            ResolveHostName();
            SaveSettings();
            StartPing();
        }

        private void ResolveHostName()
        {
            ipAddress = null;
            isValidHost = false;
            if (String.IsNullOrEmpty(settings.ServerName))
            {
                return;
            }

            try
            {
                // Check if it's a valid IP address and not a hostname
                if (IPAddress.TryParse(settings.ServerName, out ipAddress))
                {
                    isValidHost = true;
                    return;
                }

                // Try resolving hostname
                IPHostEntry hostEntry = Dns.GetHostEntry(settings.ServerName);

                // Might get more than one ip for a hostname since DNS supports more than one record
                if (hostEntry.AddressList.Length > 0)
                {
                    ipAddress = hostEntry.AddressList[0];
                    isValidHost = true;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"ResolveHostName Exception for host {settings.ServerName} {ex}");
            }
        }

        private void StartPing()
        {
            tmrPingServer.Stop();

            if (ipAddress == null)
            {
                return;
            }

            tmrPingServer.Interval = pingFrequency;
            tmrPingServer.Start();
        }

        private void GeneratePing()
        {
            try
            {
                // Set options for transmission:
                // The data can go through 64 gateways or routers
                // before it is destroyed, and the data packet
                // cannot be fragmented.
                PingOptions options = new PingOptions(64, true);

                // Send the ping asynchronously.
                // Use the waiter as the user token.
                // When the callback completes, it can wake up this thread.
                pingSender.SendAsyncCancel();
                pingSender.SendAsync(ipAddress, pingFrequency, pingBuffer, options);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"GeneratePing failed: {ex}");
                pingCanceled = true;
            }
        }

        private void TmrPingServer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            // Generate a ping
            GeneratePing();
        }

        private void Ping_PingCompleted(object sender, System.Net.NetworkInformation.PingCompletedEventArgs e)
        {
            // Get Latency
            if (e.Error != null)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Ping error for host {settings.ServerName} {e.Error}");
                return;
            }

            if (e.Cancelled)
            {
                pingCanceled = true;
                return;
            }

            if (e.Reply == null)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Ping reply is null for host {settings.ServerName}");
                return;
            }

            if (e.Reply.Status == IPStatus.TimedOut || e.Reply.Status == IPStatus.DestinationHostUnreachable || e.Reply.Status == IPStatus.DestinationNetworkUnreachable)
            {
                pingCanceled = true;
                return;
            }

            pingCanceled = false;
            pingLatency = e.Reply.RoundtripTime;
        }

        private async Task HandleLatencyImage(long currentLatency)
        {
            if (currentLatency < 0)
            {
                // Show Timeout image
                await SetImage(settings.TimeoutImage);
            }
            else if (currentLatency > normalLatency)
            {
                // Show High Latency Image
                await SetImage(settings.HighImage);
            }
            else
            {
                // Show Low Latency Image
                await SetImage(settings.LowImage);
            }
        }

        private async Task SetImage(string fileName)
        {
            if (String.IsNullOrEmpty(fileName))
            {
                return;
            }
            
            if (!File.Exists(fileName))
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Cannot set ping image - file does not exist {fileName}");
                return;
            }

            using (Image img = Image.FromFile(fileName))
            {
                await Connection.SetImageAsync(img);
            }
        }

        #endregion
    }
}