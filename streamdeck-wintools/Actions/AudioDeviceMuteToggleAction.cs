using BarRaider.SdTools;
using BarRaiderAudio;
using BarRaiderAudio.Wrappers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinTools.Backend;
using WinTools.Wrappers;

namespace WinTools.Actions
{
    [PluginActionId("com.barraider.wintools.audiomute")]
    public class AudioDeviceMuteToggleAction : PluginBase
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
                };
                return instance;
            }

            [JsonProperty(PropertyName = "deviceType")]
            public DeviceTypes DeviceType { get; set; }

            [JsonProperty(PropertyName = "devices")]
            public List<DeviceEndpoint> Devices { get; set; }

            [JsonProperty(PropertyName = "device")]
            public String Device { get; set; }
        }

        #region Private Members
        private const string DEFAULT_DEVICE_NAME = "- Default Device -";
        private const string MUTE_IMAGE_FILE = @"images\audioMute.png";

        private Image prefetchedMuteImage;
        private readonly PluginSettings settings;
        bool disableStatusCheck = false;

        #endregion

        public AudioDeviceMuteToggleAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Toggling Mute for {settings.Device}");
            bool result;
            if (settings.DeviceType == DeviceTypes.Playback)
            {
                result = BRAudio.TogglePlaybackDeviceMuteStatus(device);
            }
            else
            {
                result =  BRAudio.ToggleRecordingDeviceMuteStatus(device);
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

        public async override void OnTick()
        {
            if (String.IsNullOrEmpty(settings.Device) || disableStatusCheck)
            {
                return;
            }

            bool? status;
            if (settings.DeviceType == DeviceTypes.Playback)
            {
                status = BRAudio.GetPlaybackDeviceMuteStatus(settings.Device == DEFAULT_DEVICE_NAME ? BRAudio.DEFAULT_ENDPOINT : settings.Device);
            }
            else
            {
                status = BRAudio.GetRecordingDeviceMuteStatus(settings.Device == DEFAULT_DEVICE_NAME ? BRAudio.DEFAULT_ENDPOINT : settings.Device);
            }

            if (!status.HasValue)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} Exception raised for Mute Status for device {settings.Device}, halting updates");
                disableStatusCheck = true;
                return;
            }

            if (status.Value) // Is Muted
            {
                await Connection.SetImageAsync(GetMuteImage());
            }
            else
            {
                await Connection.SetImageAsync((string)null);
            }
        }

        public async override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            var deviceType = settings.DeviceType;
            Tools.AutoPopulateSettings(settings, payload.Settings);
            InitializeSettings();
            if (deviceType != settings.DeviceType)
            {
                await Connection.SetImageAsync((string)null);
                FetchDevices();
            }
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #region Private Methods

        private void InitializeSettings()
        {
            disableStatusCheck = false;
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

        private Image GetMuteImage()
        {
            if (prefetchedMuteImage == null)
            {
                prefetchedMuteImage = Image.FromFile(MUTE_IMAGE_FILE);
            }
            return prefetchedMuteImage;
        }

        #endregion
    }
}