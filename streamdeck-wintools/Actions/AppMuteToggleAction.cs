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
    [PluginActionId("com.barraider.wintools.appmute")]
    public class AppMuteToggleAction : PluginBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    Applications = null,
                    Application = String.Empty,
                    ShowAppName = false,
                    AppCurrent = false,
                    AppSpecific = true

                };
                return instance;
            }

            [JsonProperty(PropertyName = "appCurrent")]
            public bool AppCurrent { get; set; }

            [JsonProperty(PropertyName = "appSpecific")]
            public bool AppSpecific { get; set; }

            [JsonProperty(PropertyName = "applications")]
            public List<AudioApplication> Applications { get; set; }

            [JsonProperty(PropertyName = "application")]
            public String Application { get; set; }

            [JsonProperty(PropertyName = "showAppName")]
            public bool ShowAppName { get; set; }
        }

        #region Private Members
        private const string MUTE_IMAGE_FILE = @"images\appMute.png";

        private Image prefetchedMuteImage;
        private readonly PluginSettings settings;

        #endregion
        public AppMuteToggleAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            if (settings.AppSpecific && String.IsNullOrEmpty(settings.Application))
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"{GetType()} Key Pressed but no application is set");
                return;
            }

            string appName = settings.Application;
            if (settings.AppCurrent)
            {
                appName = HelperUtils.GetForegroundWindowProcess().ProcessName;
            }

            var appInfo = (await BRAudio.GetVolumeApplications()).Where(app => app.Name == appName).FirstOrDefault();
            if (appInfo == null)
            {

                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} No valid volume application found for {appName}");
                return;
            }


            Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} Toggling Mute for {appName}");
            if (await BRAudio.ToggleAppMute(appName))
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
            if (settings.AppSpecific && String.IsNullOrEmpty(settings.Application))
            {
                return;
            }

            string appName = settings.Application;
            if (settings.AppCurrent)
            {
                appName = HelperUtils.GetForegroundWindowProcess().ProcessName;
            }

            var appInfo = (await BRAudio.GetVolumeApplications()).Where(app => app.Name == appName).FirstOrDefault();
            if (appInfo == null)
            {
                await Connection.SetImageAsync((string)null);
                await Connection.SetTitleAsync(null);
                return;
            }

            if (appInfo.IsMuted)
            {
                await Connection.SetImageAsync(GetMuteImage());
            }
            else
            {
                await Connection.SetImageAsync((string)null);
            }

            if (settings.ShowAppName)
            {
                await Connection.SetTitleAsync(appName);
            }

        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            bool showTitle = settings.ShowAppName;
            Tools.AutoPopulateSettings(settings, payload.Settings);
            InitializeSettings();

            // Clear title if setting changed
            if (settings.AppCurrent || showTitle != settings.ShowAppName)
            {
                Connection.SetTitleAsync((string)null);
            }
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #region Private Methods

        private void InitializeSettings()
        {
            if (!settings.AppSpecific && !settings.AppCurrent) // Backward compatibility
            {
                settings.AppSpecific = true;
            }

            SaveSettings();
        }

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        private async void FetchApplications()
        {
            // Get all the applications in the Volume Mixer
            settings.Applications = await BRAudio.GetVolumeApplications();

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