using BarRaider.SdTools;
using BarRaiderAudio;
using BarRaiderAudio.Wrappers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using WinTools.Backend;
using WinTools.Wrappers;

namespace WinTools.Actions
{
    [PluginActionId("com.barraider.wintools.appplayback")]
    public class AppPlaybackDeviceAction : PluginBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    Applications = null,
                    Application = String.Empty,
                    Devices = null,
                    Device = String.Empty,
                    ShowAppName = false,
                    ShowDeviceName = false,
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

            [JsonProperty(PropertyName = "devices")]
            public List<DeviceEndpoint> Devices { get; set; }

            [JsonProperty(PropertyName = "device")]
            public String Device { get; set; }

            [JsonProperty(PropertyName = "showAppName")]
            public bool ShowAppName { get; set; }

            [JsonProperty(PropertyName = "showDeviceName")]
            public bool ShowDeviceName { get; set; }
        }

        #region Private Members
        private const string DEFAULT_PLAYBACK_DEVICE_NAME = "- Default Playback Device -";

        private readonly PluginSettings settings;

        #endregion
        public AppPlaybackDeviceAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            if (settings.AppSpecific && String.IsNullOrEmpty(settings.Application))
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"{GetType()} Key Pressed but no application is set");
                await Connection.ShowAlert();
                return;
            }

            if (String.IsNullOrEmpty(settings.Device))
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"{GetType()} Key Pressed but no device is set");
                await Connection.ShowAlert();
                return;
            }

            string device = (settings.Device == DEFAULT_PLAYBACK_DEVICE_NAME) ? BRAudio.DEFAULT_ENDPOINT : settings.Device;
            bool isSuccess = false;

            if (settings.AppCurrent)
            {
                Process proc = GetForegroundWindowProcess();
                if (proc == null)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} GetForegroundWindowProcess() returned null!");
                    await Connection.ShowAlert();
                    return;
                }

                Logger.Instance.LogMessage(TracingLevel.INFO, $"Modifying current process {proc.ProcessName} with PID {proc.Id} playback device to be {device}");
                isSuccess = await BRAudio.SetAppPlaybackDeviceByProcessId(proc.Id, device);
            }
            else // App Specific
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, $"Modifying {settings.Application}'s playback device to be {device}");
                isSuccess = await BRAudio.SetAppPlaybackDeviceByDeviceFriendlyName(settings.Application, device);

            }

            if (isSuccess)
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
            if ((settings.AppSpecific && String.IsNullOrEmpty(settings.Application)) || 
                (!settings.ShowDeviceName && !settings.ShowAppName))
            {
                return;
            }

            if (settings.ShowAppName)
            {
                title = settings.Application;
            }

            if (settings.ShowDeviceName && !String.IsNullOrEmpty(settings.Device))
            {
                title = title + (String.IsNullOrEmpty(title) ? "" : "\n") + settings.Device;
            }

            await Connection.SetTitleAsync(title);
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            bool showTitle = settings.ShowDeviceName || settings.ShowAppName;
            bool appCurrent = settings.AppCurrent;
            Tools.AutoPopulateSettings(settings, payload.Settings);
            InitializeSettings();

            // Clear title if setting changed
            if (showTitle != (settings.ShowDeviceName || settings.ShowAppName) ||
                appCurrent != settings.AppCurrent)
            {
                Connection.SetTitleAsync((string)null);
            }

            if (appCurrent != settings.AppCurrent)
            {
                SaveSettings();
            }
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #region Private Methods

        private void InitializeSettings()
        {
            if (!settings.AppCurrent && !settings.AppSpecific)
            {
                settings.AppSpecific = true;
                SaveSettings();
            }

            if (settings.AppSpecific)
            {
                FetchApplicationsAndDevices();
            }
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

        private async void FetchPlaybackDevices()
        {
            // Get all the applications in the Volume Mixer
            settings.Devices = (await BRAudio.GetAllPlaybackDevices()).Select(d => new DeviceEndpoint(d.FriendlyName)).ToList();

            if (settings.Devices == null)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"{GetType()} GetAllPlaybackDevices called but returned null");
                return;
            }
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{GetType()} FetchPlaybackDevices returned {settings.Devices.Count} playback devices");
            settings.Devices.Insert(0, new DeviceEndpoint(DEFAULT_PLAYBACK_DEVICE_NAME));
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
                        FetchApplicationsAndDevices();
                        break;
                }
            }
        }

        private void FetchApplicationsAndDevices()
        {
            FetchApplications();
            FetchPlaybackDevices();
        }

        private void Connection_OnPropertyInspectorDidAppear(object sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Events.PropertyInspectorDidAppear> e)
        {
            FetchApplicationsAndDevices();
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private Process GetForegroundWindowProcess()
        {
            try
            {
                uint processID = 0;
                IntPtr hWnd = GetForegroundWindow(); // Get foreground window handle
                uint threadID = GetWindowThreadProcessId(hWnd, out processID); // Get PID from window handle
                return Process.GetProcessById(Convert.ToInt32(processID)); // Get it as a C# obj.
            }
            catch (Exception ex)
            {

                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} GetForegroundWindowProcess Exception: {ex}");
            }
            return null;
        }


        #endregion
    }
}