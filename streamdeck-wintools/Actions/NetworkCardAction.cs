using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinTools.Wrappers;

namespace WinTools
{
    [PluginActionId("com.barraider.wintools.networkcard")]
    public class NetworkCardAction : PluginBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    NetworkCard = String.Empty,
                    NetworkCardTitle = String.Empty,
                    NetworkCards = null
                };
                return instance;
            }

            [JsonProperty(PropertyName = "networkCards")]
            public NetworkCard[] NetworkCards { get; set; }

            [JsonProperty(PropertyName = "networkCard")]
            public string NetworkCard { get; set; }

            [JsonProperty(PropertyName = "networkCardTitle")]
            public string NetworkCardTitle { get; set; }

            
        }

        #region Private Members

        private readonly PluginSettings settings;

        #endregion
        public NetworkCardAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
                SaveSettings();
            }
            else
            {
                this.settings = payload.Settings.ToObject<PluginSettings>();
            }
            LoadNetworkCards();
        }

        public override void Dispose()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");
        }

        public override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Pressed");
        }

        public override void KeyReleased(KeyPayload payload) { }

        public async override void OnTick()
        {
            if (String.IsNullOrEmpty(settings.NetworkCard))
            {
                return;
            }

            var nic = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.Id == settings.NetworkCard).FirstOrDefault();
            if (nic == null)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Could not retrieve network card with id {settings.NetworkCard}");
                await Connection.SetTitleAsync("Invalid\nSettings");
                return;
            }

            string name = string.IsNullOrEmpty(settings.NetworkCardTitle) ? nic.Name : settings.NetworkCardTitle;
            await Connection.SetTitleAsync($"{name}\n{nic.OperationalStatus}");
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(settings, payload.Settings);
            SaveSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #region Private Methods

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        private void LoadNetworkCards()
        {
            GetAllNetworkAdapters();
            SaveSettings();
        }

        private void GetAllNetworkAdapters()
        {
            try
            {
                settings.NetworkCards = null;
                List<NetworkCard> cards = new List<NetworkCard>();

                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    cards.Add(new NetworkCard() { DisplayName = nic.Name, Id = nic.Id });
                }
                
                settings.NetworkCards = cards.ToArray();
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"GetAllNetworkAdapters Exception: {ex}");
            }
        }

        #endregion
    }
}