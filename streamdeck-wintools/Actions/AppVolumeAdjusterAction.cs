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
    // Subscriber: Tek_Soup
    // Subscriber: icessassin
    // icessassin - 2 Gifted Subs
    // Subscriber: stea1e
    // Subscriber: Vedeksu
    //---------------------------------------------------
    [PluginActionId("com.barraider.wintools.appvolumeadjust")]
    public class AppVolumeAdjusterAction : PluginBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    Applications = null,
                    Application = String.Empty,
                    VolumeStep = DEFAULT_VOLUME_STEP.ToString(),
                    ShowVolume = false,
                    ShowAppName = false
                };
                return instance;
            }

            [JsonProperty(PropertyName = "applications")]
            public List<AudioApplication> Applications { get; set; }

            [JsonProperty(PropertyName = "application")]
            public String Application { get; set; }

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
        public AppVolumeAdjusterAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
            }
            else
            {
                this.settings = payload.Settings.ToObject<PluginSettings>();
            }

            Connection.OnSendToPlugin += Connection_OnSendToPlugin;
            InitializeSettings();
            FetchApplications();
        }

        public override void Dispose()
        {
            Connection.OnSendToPlugin -= Connection_OnSendToPlugin;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{GetType()} Destructor called");
        }

        public async override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{GetType()} Key Pressed");
            if (String.IsNullOrEmpty(settings.Application))
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"{GetType()} Key Pressed but no application is set");
                return;
            }

            Logger.Instance.LogMessage(TracingLevel.INFO, $"Adjusting {settings.Application}'s volume by {volumeStep}");
            if (await AppVolume.AdjustAppVolume(settings.Application, volumeStep))
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
            string title = String.Empty;
            if (String.IsNullOrEmpty(settings.Application) || (!settings.ShowVolume && !settings.ShowAppName))
            {
                return;
            }

            if (settings.ShowAppName)
            {
                title = settings.Application;
            }

            if (settings.ShowVolume)
            {
                var appInfo = (await AppVolume.GetVolumeApplicationsStatus()).Where(app => app.Name == settings.Application).FirstOrDefault();
                if (appInfo != null)
                {
                    // Append volume on new line if app name is also selected
                    title = title + (String.IsNullOrEmpty(title) ? "" : "\n") + Math.Round(appInfo.Volume * 100).ToString();
                }
            }

            await Connection.SetTitleAsync(title);
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            bool showTitle = settings.ShowVolume || settings.ShowAppName;
            Tools.AutoPopulateSettings(settings, payload.Settings);
            InitializeSettings();

            // Clear title if setting changed
            if (showTitle != (settings.ShowVolume || settings.ShowAppName))
            {
                Connection.SetTitleAsync((string)null);
            }
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

        private async void FetchApplications()
        {
            // Get all the applications in the Volume Mixer
            settings.Applications = await AppVolume.GetVolumeApplicationsStatus();

            if (settings.Applications == null)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"{GetType()} GetVolumeApplicationsNames called but returned null");
                return;
            }
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{GetType()} FetchApplication returned {settings.Applications.Count} volume apps");
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
                        FetchApplications();
                        break;
                }
            }
        }


        #endregion
    }
}