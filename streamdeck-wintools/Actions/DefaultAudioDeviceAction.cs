using BarRaider.SdTools;
using BarRaiderAudio;
using BarRaiderAudio.Wrappers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinTools.Backend;
using WinTools.Wrappers;

namespace WinTools.Actions
{
    [PluginActionId("com.barraider.wintools.defaultaudiodevice")]
    public class DefaultAudioDeviceAction : PluginBase
    {
        private enum DeviceTypes
        {
            Playback = 0,
            Recording = 1
        }

        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    DeviceType = DeviceTypes.Playback,
                    Devices = null,
                    Device = String.Empty,
                    SetDefaultCommunication = false
                };
                return instance;
            }

            [JsonProperty(PropertyName = "deviceType")]
            public DeviceTypes DeviceType { get; set; }

            [JsonProperty(PropertyName = "devices")]
            public List<DeviceEndpoint> Devices { get; set; }

            [JsonProperty(PropertyName = "device")]
            public String Device { get; set; }

            [JsonProperty(PropertyName = "commDevice")]
            public bool SetDefaultCommunication { get; set; }
        }

        #region Private Members
        private readonly PluginSettings settings;

        #endregion
        public DefaultAudioDeviceAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            Connection.OnPropertyInspectorDidAppear += Connection_OnPropertyInspectorDidAppear;
            InitializeSettings();
        }

        public override void Dispose()
        {
            Connection.OnSendToPlugin -= Connection_OnSendToPlugin;
            Connection.OnPropertyInspectorDidAppear -= Connection_OnPropertyInspectorDidAppear;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{GetType()} Destructor called");
        }

        public async override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{GetType()} Key Pressed");

            if (String.IsNullOrEmpty(settings.Device))
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"{GetType()} Key Pressed but no device is set");
                return;
            }

            Logger.Instance.LogMessage(TracingLevel.INFO, $"Modifying default {settings.DeviceType} device to be {settings.Device}");
            bool result = false;
            if (settings.DeviceType == DeviceTypes.Playback)
            {
                result = await BRAudio.SetDefaultPlaybackDeviceByDeviceFriendlyName(settings.Device);
                if (result && settings.SetDefaultCommunication)
                {
                    result = await BRAudio.SetDefaultPlaybackCommunicationDeviceFriendlyName(settings.Device);
                }
            }
            else // Recording Device
            {
                result = await BRAudio.SetDefaultRecordingDeviceByDeviceFriendlyName(settings.Device);
                if (result && settings.SetDefaultCommunication)
                {
                    result = await BRAudio.SetDefaultRecordingCommunicationDeviceFriendlyName(settings.Device);
                }
            }
            
            if (result)
            {
                await Connection.ShowOk();
            }
            else
            {
                await Connection.ShowAlert();
            }
        }

        public override void KeyReleased(KeyPayload payload) { }

        public override void OnTick() { }
        
        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            var deviceType = settings.DeviceType;
            Tools.AutoPopulateSettings(settings, payload.Settings);
            if (deviceType != settings.DeviceType)
            {
                FetchDevices();
            }
            InitializeSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #region Private Methods

        private void InitializeSettings()
        {
        }

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        private async void FetchPlaybackDevices()
        {
            // Get all the applications in the Volume Mixer
            settings.Devices = (await BRAudio.GetAllPlaybackDevices()).Select(d => new DeviceEndpoint(d.FriendlyName)).ToList();

            if (settings.Devices == null)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"{GetType()} GetAllPlaybackDevices called but returned null");
                return;
            }
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{GetType()} FetchPlaybackDevices returned {settings.Devices.Count} devices");
            await SaveSettings();
        }

        private async void FetchRecordingDevices()
        {
            // Get all the applications in the Volume Mixer
            settings.Devices = (await BRAudio.GetAllRecordingDevices()).Select(d => new DeviceEndpoint(d.FriendlyName)).ToList();

            if (settings.Devices == null)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"{GetType()} GetAllRecordingDevices called but returned null");
                return;
            }
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{GetType()} FetchRecordingDevices returned {settings.Devices.Count} devices");
            await SaveSettings();
        }


        private void Connection_OnSendToPlugin(object sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Events.SendToPlugin> e)
        {
            var payload = e.Event.Payload;

            if (payload["property_inspector"] != null)
            {
                switch (payload["property_inspector"].ToString().ToLowerInvariant())
                {
                    case "refreshapplications":
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"{GetType()} refreshApplications called");
                        FetchDevices();
                        break;
                }
            }
        }

        private void FetchDevices()
        {
            if (settings.DeviceType == DeviceTypes.Playback)
            {
                FetchPlaybackDevices();
            }
            else
            {
                FetchRecordingDevices();
            }
        }

        private void Connection_OnPropertyInspectorDidAppear(object sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Events.PropertyInspectorDidAppear> e)
        {
            FetchDevices();
        }



        #endregion
    }
}