using BarRaider.SdTools;
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

    //---------------------------------------------------
    //          BarRaider's Hall Of Fame
    // Subscriber: warbeforepeace
    // nubby_ninja - 10 Gifted Subs
    // Subscriber: WiredJeep
    //---------------------------------------------------
    [PluginActionId("com.barraider.wintools.appaudiomixer")]
    public class AppAudioMixerAction : PluginBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    VolumeStep = DEFAULT_VOLUME_STEP.ToString(),
                    ShowVolume = false,
                    ShowAppName = false
                };
                return instance;
            }

            [JsonProperty(PropertyName = "volumeStep")]
            public String VolumeStep { get; set; }

            [JsonProperty(PropertyName = "showVolume")]
            public bool ShowVolume { get; set; }

            [JsonProperty(PropertyName = "showAppName")]
            public bool ShowAppName { get; set; }

        }

        #region Private Members
        private const int DEFAULT_VOLUME_STEP = 15;

        private PluginSettings settings;
        private int volumeStep = DEFAULT_VOLUME_STEP;

        #endregion
        public AppAudioMixerAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
            }
            else
            {
                this.settings = payload.Settings.ToObject<PluginSettings>();
            }

            InitializeSettings();
        }

        public override void Dispose()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{GetType()} Destructor called");
        }

        public async override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{GetType()} Key Pressed");

            if (Connection.DeviceInfo().Type == StreamDeckDeviceType.StreamDeckClassic)
            {
                await Connection.SwitchProfileAsync("WinTools");
            }
            else
            {
                await Connection.SwitchProfileAsync("WinToolsXL");
            }

            await AudioMixerManager.Instance.ShowMixer(Connection, new MixerSettings(volumeStep, settings.ShowAppName, settings.ShowVolume));
        }

        public override void KeyReleased(KeyPayload payload) { }

        public override void OnTick() 
        {
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            bool showTitle = settings.ShowVolume || settings.ShowAppName;
            Tools.AutoPopulateSettings(settings, payload.Settings);
            InitializeSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #region Private Methods

        private void InitializeSettings()
        {
            if (!Int32.TryParse(settings.VolumeStep, out volumeStep))
            {
                settings.VolumeStep = DEFAULT_VOLUME_STEP.ToString();
                SaveSettings();
            }
        }

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        #endregion
    }
}