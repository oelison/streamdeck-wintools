using BarRaider.SdTools;
using BarRaider.SdTools.Wrappers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WinTools.MonitorWrapper;

namespace WinTools.Actions
{

    [PluginActionId("com.barraider.wintools.primarymonitor")]
    public class PrimaryActionMonitor : PluginBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    ScreenFriendlyName = true,
                    Screen = String.Empty,
                    Screens = null,
                    ShowScreenName = false
                };
                return instance;
            }

            [JsonProperty(PropertyName = "screenFriendlyName")]
            public bool ScreenFriendlyName { get; set; }

            [JsonProperty(PropertyName = "screens")]
            public List<MonitorInfo> Screens { get; set; }

            [JsonProperty(PropertyName = "screen")]
            public string Screen { get; set; }

            [JsonProperty(PropertyName = "showScreenName")]
            public bool ShowScreenName { get; set; }
        }

        #region Private Members
        private const string PRIMARY_MONITOR_IMAGE_FILE = @"images\monitorSelected.png";

        private readonly PluginSettings settings;

        private TitleParameters titleParameters;
        private Image prefetchedPrimaryMonitorImage;
        private bool previouslySetToPrimary = false;

        #endregion

        public PrimaryActionMonitor(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
            }
            else
            {
                this.settings = payload.Settings.ToObject<PluginSettings>();
            }
            Connection.OnTitleParametersDidChange += Connection_OnTitleParametersDidChange;
            PopulateScreens();
            SaveSettings();
        }

        #region Public Methods

        public override void Dispose()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");
            Connection.OnTitleParametersDidChange -= Connection_OnTitleParametersDidChange;
        }

        public async override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Key Pressed {this.GetType()}");

            if (!SetPrimaryMonitor())
            {
                await Connection.ShowAlert();
                return;
            }
        }

        public override void KeyReleased(KeyPayload payload) { }

        public async override void OnTick()
        {
            if (String.IsNullOrEmpty(settings.Screen))
            {
                return;
            }
            Screen screen = MonitorManager.Instance.GetScreenFromUniqueValue(settings.Screen);
            if (screen == null)
            {
                return;
            }

            if (settings.ShowScreenName)
            {
                string[] values = settings.Screen.Split(Constants.UNIQUE_VALUE_DELIMITER);
                await Connection.SetTitleAsync(Tools.SplitStringToFit(values[0], titleParameters));
            }

            if (screen.Primary)
            {
                previouslySetToPrimary = true;
                await Connection.SetImageAsync(GetIsPrimayMonitorImage());
            }
            else if (previouslySetToPrimary)
            {
                previouslySetToPrimary = false;
                await Connection.SetImageAsync((string)null);
            }
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            bool screenFriendlyName = settings.ScreenFriendlyName;
            Tools.AutoPopulateSettings(settings, payload.Settings);
            if (screenFriendlyName != settings.ScreenFriendlyName)
            {
                PopulateScreens();
            }
            SaveSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #endregion

        #region Private Methods

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        private void PopulateScreens()
        {
            settings.Screens = MonitorManager.Instance.GetAllMonitors();
            bool uniqueFriendly = MonitorManager.Instance.HasUniqueFriendlyName();
            settings.Screens.ForEach(mon =>
            {
                mon.DisplayName = mon.DeviceName;
                if (settings.ScreenFriendlyName)
                {
                    if (uniqueFriendly)
                    {
                        mon.DisplayName = $"{mon.FriendlyName}";
                    }
                    else
                    {
                        mon.DisplayName = $"{mon.FriendlyName} ({mon.WMIInfo.SerialNumber})";
                    }
                }
            });

            if (string.IsNullOrWhiteSpace(settings.Screen) && settings.Screens.Count > 0)
            {
                settings.Screen = settings.Screens[0].UniqueValue;
            }
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Populated {settings.Screens.Count} screens");
        }

        private bool SetPrimaryMonitor()
        {
            if (String.IsNullOrEmpty(settings.Screen))
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Screen not specified!");
                return false;
            }

            Screen screen = MonitorManager.Instance.GetScreenFromUniqueValue(settings.Screen);
            if (screen == null)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Could not find screen {settings.Screen}");
                return false;
            }

            return PrimaryMonitorChanger.SetAsPrimaryMonitor(screen);
        }

        private Image GetIsPrimayMonitorImage()
        {
            if (prefetchedPrimaryMonitorImage == null)
            {
                prefetchedPrimaryMonitorImage = Image.FromFile(PRIMARY_MONITOR_IMAGE_FILE);
            }
            return prefetchedPrimaryMonitorImage;
        }
        private void Connection_OnTitleParametersDidChange(object sender, SDEventReceivedEventArgs<BarRaider.SdTools.Events.TitleParametersDidChange> e)
        {
            titleParameters = e.Event?.Payload?.TitleParameters;
        }
        #endregion
    }


}
