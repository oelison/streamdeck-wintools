using BarRaider.SdTools;
using BarRaiderVirtualDesktop.VirtualDesktop;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinTools.Wrappers;

namespace WinTools.Actions
{

    [PluginActionId("com.barraider.wintools.vdshow")]
    public class VirtualDesktopShowAction : PluginBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                };
                return instance;
            }

        }

        #region Private Members
        private const string DEFAULT_DESKTOP_NAME = "Desktop 1";

        private readonly PluginSettings settings;
        bool nameFeatureSupported = false;

        #endregion

        public VirtualDesktopShowAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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

            InitializeSettings();
        }

        #region Public Methods

        public override void Dispose()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");
        }

        public async override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Key Pressed {this.GetType()}");
            await Connection.ShowAlert();
        }

        public override void KeyReleased(KeyPayload payload) { }

        public async override void OnTick() 
        {
            if (!nameFeatureSupported)
            {
                return;
            }

            string currentDesktopName = VirtualDesktopManager.Instance.CurrentDesktop().GetName();
            if (String.IsNullOrEmpty(currentDesktopName))
            {
                currentDesktopName = DEFAULT_DESKTOP_NAME;
            }
            await Connection.SetTitleAsync(currentDesktopName);
        }


        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(settings, payload.Settings);
            InitializeSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #endregion

        #region Private Methods

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        private void InitializeSettings()
        {
            if (!VirtualDesktopManager.Instance.IsWin11Version())
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} unsupported on this Virtual Desktop verison");
                Connection.SetTitleAsync("Unsupported");
                nameFeatureSupported = false;
                return;
            }
            else
            {
                nameFeatureSupported = true;
            }
        }

        #endregion
    }

}
