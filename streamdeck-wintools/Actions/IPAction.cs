using BarRaider.SdTools;
using BarRaider.SdTools.Wrappers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using WinTools.Backend;
using WinTools.Wrappers;

namespace WinTools
{
    [PluginActionId("com.barraider.wintools.ip")]
    public class IPAction : PluginBase
    {

        //---------------------------------------------------
        //          BarRaider's Hall Of Fame
        // 9000 Bits: nubby_ninja
        //---------------------------------------------------
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    Provider = DEFAULT_IP_PROVIDER,
                    RefreshSeconds = DEFAULT_REFRESH_TIME_SECONDS.ToString(),
                    SaveFilePath = String.Empty
                };
                return instance;
            }

            [JsonProperty(PropertyName = "provider")]
            public string Provider { get; set; }

            [JsonProperty(PropertyName = "refreshSeconds")]
            public string RefreshSeconds { get; set; }

            [FilenameProperty]
            [JsonProperty(PropertyName = "saveFilePath")]
            public string SaveFilePath { get; set; }
        }

        #region Private Members
        private const string DEFAULT_IP_PROVIDER = "https://api.ipify.org/?format=json";
        private const int DEFAULT_REFRESH_TIME_SECONDS = 60;
        private readonly PluginSettings settings;

        private DateTime lastIPRefresh = DateTime.MinValue;
        private int refreshSeconds = DEFAULT_REFRESH_TIME_SECONDS;
        private TitleParameters titleParameters;

        #endregion
        public IPAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            Connection.OnTitleParametersDidChange += Connection_OnTitleParametersDidChange;
            Connection.OnSendToPlugin += Connection_OnSendToPlugin;
            InitializeSettings();
        }

        public override void Dispose()
        {
            Connection.OnTitleParametersDidChange -= Connection_OnTitleParametersDidChange;
            Connection.OnSendToPlugin -= Connection_OnSendToPlugin;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");
        }

        public override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Key Pressed {this.GetType()} - Refetching IP");
            lastIPRefresh = DateTime.MinValue;
        }

        public override void KeyReleased(KeyPayload payload)
        {
        }

        public async override void OnTick()
        {
            if (String.IsNullOrEmpty(settings.Provider))
            {
                return;
            }

            if ((DateTime.Now - lastIPRefresh).TotalSeconds < refreshSeconds)
            {
                return;
            }

            string ip = await FetchIPAddress();
            SaveToFile(ip);
            await Connection.SetTitleAsync(Tools.SplitStringToFit(ip, titleParameters));
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
            lastIPRefresh = DateTime.MinValue;
            if (!Int32.TryParse(settings.RefreshSeconds, out refreshSeconds))
            {
                settings.RefreshSeconds = DEFAULT_REFRESH_TIME_SECONDS.ToString();
                refreshSeconds= DEFAULT_REFRESH_TIME_SECONDS;
                SaveSettings();
            }
        }

        private void Connection_OnSendToPlugin(object sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Events.SendToPlugin> e)
        {
            var payload = e.Event.Payload;

            if (payload["property_inspector"] != null)
            {
                switch (payload["property_inspector"].ToString().ToLowerInvariant())
                {
                    case "loadsavepicker":
                        string propertyName = (string)payload["property_name"];
                        string pickerTitle = (string)payload["picker_title"];
                        string pickerFilter = (string)payload["picker_filter"];
                        string fileName = PickersUtil.Pickers.SaveFilePicker(pickerTitle, null, pickerFilter);
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            if (!PickersUtil.Pickers.SetJsonPropertyValue(settings, propertyName, fileName))
                            {
                                Logger.Instance.LogMessage(TracingLevel.ERROR, "Failed to save picker value to settings");
                            }
                            SaveSettings();
                            InitializeSettings();
                        }
                        break;
                }
            }
        }

        private void SaveToFile(string uptime)
        {
            if (String.IsNullOrEmpty(settings.SaveFilePath))
            {
                return;
            }

            try
            {
                File.WriteAllText(settings.SaveFilePath, uptime);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Failed to save uptime to {settings.SaveFilePath}: {ex}");
            }
        }

        private async Task<string> FetchIPAddress()
        {
            try
            {
                if (string.IsNullOrEmpty(settings.Provider))
                {

                    Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} FetchIP called but provider is null!");
                    return null;

                }

                lastIPRefresh = DateTime.Now;
                using (HttpClient client = new HttpClient() { Timeout = new TimeSpan(0, 0, 10) })
                {
                    HttpResponseMessage response = await client.GetAsync(settings.Provider);
                    if (response.IsSuccessStatusCode)
                    {
                        string body = await response.Content.ReadAsStringAsync();
                        JObject obj = JObject.Parse(body);
                        return obj["ip"].ToString();
                    }
                    else
                    {
                        Logger.Instance.LogMessage(TracingLevel.ERROR, $"FetchIPAddress failed for app {settings.Provider}! Response: {response.ReasonPhrase} Status Code: {response.StatusCode}");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"FetchIPAddress Exception: {ex}");
            }
            return null;
        }

        private void Connection_OnTitleParametersDidChange(object sender, SDEventReceivedEventArgs<BarRaider.SdTools.Events.TitleParametersDidChange> e)
        {
            titleParameters = e?.Event?.Payload?.TitleParameters;
            lastIPRefresh = DateTime.MinValue;
        }


        #endregion
    }
}