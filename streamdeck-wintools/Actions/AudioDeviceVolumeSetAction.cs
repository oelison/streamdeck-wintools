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
    [PluginActionId("com.barraider.wintools.audiovolumeset")]
    public class AudioDeviceVolumeSetAction : PluginBase
    {
        private enum DeviceTypes
        {
            Playback = 0,
            Recording = 1,
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
                    ShowVolume = false,
                    Volume = DEFAULT_VOLUME_LEVEL.ToString(),
                    FadeVolume = false,
                    FadeLength = DEFAULT_FADE_LENGTH_MS.ToString(),
                };
                return instance;
            }

            [JsonProperty(PropertyName = "deviceType")]
            public DeviceTypes DeviceType { get; set; }

            [JsonProperty(PropertyName = "devices")]
            public List<DeviceEndpoint> Devices { get; set; }

            [JsonProperty(PropertyName = "device")]
            public String Device { get; set; }

            [JsonProperty(PropertyName = "showVolume")]
            public bool ShowVolume { get; set; }

            [JsonProperty(PropertyName = "volume")]
            public String Volume { get; set; }

            [JsonProperty(PropertyName = "fadeVolume")]
            public bool FadeVolume { get; set; }

            [JsonProperty(PropertyName = "fadeLength")]
            public String FadeLength { get; set; }
        }

        #region Private Members
        private const string DEFAULT_DEVICE_NAME = "- Default Device -";
        private const int DEFAULT_VOLUME_LEVEL = 100;
        private const int DEFAULT_FADE_LENGTH_MS = 1000;

        private readonly PluginSettings settings;
        private int volume = DEFAULT_VOLUME_LEVEL;
        private int fadeLength = DEFAULT_FADE_LENGTH_MS;

        #endregion

        public AudioDeviceVolumeSetAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
                await Connection.ShowAlert();
                return;
            }

            string device = settings.Device == DEFAULT_DEVICE_NAME ? BRAudio.DEFAULT_ENDPOINT : settings.Device;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Setting {settings.Device}'s volume to {volume}");
            if (settings.DeviceType == DeviceTypes.Playback)
            {
                BRAudio.SetPlaybackDeviceVolume(volume, device, fadeLength);
            }
            else
            {
                BRAudio.SetRecordingDeviceVolume(volume, device, fadeLength);
            }
        }

        public override void KeyReleased(KeyPayload payload) { }

        public async override void OnTick() 
        {
            if (String.IsNullOrEmpty(settings.Device))
            {
                return;
            }

            if (settings.ShowVolume)
            {
                if (settings.DeviceType == DeviceTypes.Playback)
                {
                    await Connection.SetTitleAsync(BRAudio.GetPlaybackDeviceVolume(settings.Device == DEFAULT_DEVICE_NAME ? BRAudio.DEFAULT_ENDPOINT : settings.Device).ToString());
                }
                else
                {
                    await Connection.SetTitleAsync(BRAudio.GetRecordingDeviceVolume(settings.Device == DEFAULT_DEVICE_NAME ? BRAudio.DEFAULT_ENDPOINT : settings.Device).ToString());
                }
            }
        }
        
        public async override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            bool showVolume = settings.ShowVolume;
            var deviceType = settings.DeviceType;
            var device = settings.Device;
            Tools.AutoPopulateSettings(settings, payload.Settings);
            InitializeSettings();
            if (deviceType != settings.DeviceType)
            {
                FetchDevices();
            }

            if (showVolume != settings.ShowVolume || device != settings.Device)
            {
                await Connection.SetTitleAsync(null);
            }
            
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #region Private Methods

        private void InitializeSettings()
        {
            if (!Int32.TryParse(settings.Volume, out volume))
            {
                settings.Volume = DEFAULT_VOLUME_LEVEL.ToString();
                volume = DEFAULT_VOLUME_LEVEL;
            }

            if (!Int32.TryParse(settings.FadeLength, out fadeLength))
            {
                settings.FadeLength = DEFAULT_FADE_LENGTH_MS.ToString();
                fadeLength = DEFAULT_FADE_LENGTH_MS;
            }

            SaveSettings();
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
            settings.Devices.Insert(0, new DeviceEndpoint(DEFAULT_DEVICE_NAME));
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
            settings.Devices.Insert(0, new DeviceEndpoint(DEFAULT_DEVICE_NAME));
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