using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using VirtualDesktop;

namespace WinTools.Actions
{
    //---------------------------------------------------
    //          BarRaider's Hall Of Fame
    // Subscriber: ElectricHavoc
    // Subscriber: TF_JonesTown
    // Subscriber: justgiggz
    //---------------------------------------------------

    [PluginActionId("com.barraider.wintools.vdcreate")]
    public class VirtualDesktopCreateAction : PluginBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    Name = String.Empty
                };
                return instance;
            }

            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; }
        }

        #region Private Members
        private readonly PluginSettings settings;

        #endregion

        public VirtualDesktopCreateAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
        }

        #region Public Methods

        public override void Dispose()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");
        }

        public async override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Key Pressed {this.GetType()}");

            if (!WindowsHelpers.IsSupportedVirtualDesktopVersion())
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, $"Key Pressed but invalid Virtual Desktop Version");
                await Connection.SetTitleAsync("Update\nWindows");
                await Connection.ShowAlert();
                return;
            }

            if (CreateNewVirtualDesktop())
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
            Tools.AutoPopulateSettings(settings, payload.Settings);
            SaveSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #endregion

        #region Private Methods

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        private bool CreateNewVirtualDesktop()
        { 
            if (String.IsNullOrEmpty(settings.Name))
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"CreateNewVirtualDesktop: Desktop name is null!");
                return false;
            }

            try
            {
                // Check if there already is a desktop with that name
                int id = Desktop.SearchDesktop(settings.Name);
                if (id >= 0)
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"Virtual desktop with name {settings.Name} already exists");
                    return true;
                }

                var newDesktop = Desktop.Create();
                newDesktop.SetName(settings.Name);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"CreateNewVirtualDesktop Exception: {ex}");
            }
            return false;
        }

        #endregion
    }

}
