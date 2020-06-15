using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinTools.Backend;
using WinTools.Wrappers;

namespace WinTools
{
    [PluginActionId("com.barraider.wintools.uptime")]
    public class UptimeAction : PluginBase
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
                    SaveFilePath = String.Empty
                };
                return instance;
            }

            [JsonProperty(PropertyName = "saveFilePath")]
            public string SaveFilePath { get; set; }
        }

        #region Private Members
        private readonly PluginSettings settings;

        #endregion
        public UptimeAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            Connection.OnSendToPlugin += Connection_OnSendToPlugin;
            InitializeSettings();
        }

        public override void Dispose()
        {
            Connection.OnSendToPlugin -= Connection_OnSendToPlugin;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");
        }

        public override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Key Pressed {this.GetType()}");
        }

        public override void KeyReleased(KeyPayload payload) 
        {
        }

        [DllImport("kernel32")]
        extern static UInt64 GetTickCount64();
        public async override void OnTick()
        {
            string timeString = GetTickCount64().ToHumanReadableTickCount();
            SaveUpTime(timeString);
            await Connection.SetTitleAsync(timeString);
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

        private void SaveUpTime(string uptime)
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

        #endregion
    }
}