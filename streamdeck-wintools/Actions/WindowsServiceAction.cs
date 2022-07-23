using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinTools.Backend;
using WinTools.Wrappers;

namespace WinTools
{
    [PluginActionId("com.barraider.wintools.service")]
    public class WindowsServiceAction : PluginBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    ServiceName = String.Empty,
                    Services = null
                };
                return instance;
            }

            [JsonProperty(PropertyName = "services")]
            public WindowsService[] Services { get; set; }

            [JsonProperty(PropertyName = "serviceName")]
            public string ServiceName { get; set; }

            [JsonProperty(PropertyName = "action")]
            public WindowsServiceManager.ServiceActionEnum Action { get; set; }
        }

        #region Private Members
        private const string RUNNING_IMAGE_FILE = @"images\serviceRunning.png";
        private const string STOPPED_IMAGE_FILE = @"images\serviceStopped.png";

        private Image prefetchedRunningImage;
        private Image prefetchedStoppedImage;

        private readonly PluginSettings settings;

        #endregion
        public WindowsServiceAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            LoadWindowsServices();
        }

        public override void Dispose()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");
        }

        public async override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Pressed");

            if (String.IsNullOrEmpty(settings.ServiceName))
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, "Key Pressed but no service is selected");
                return;
            }

            if (HandleServiceOperation())
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
            await Connection.SetTitleAsync($"{settings.ServiceName ?? ""}\n{settings.Action}");

            var service = GetService();
            if (service == null)
            {
                await Connection.SetImageAsync((string)null);
            }
            else if (service.Status == ServiceControllerStatus.Running)
            {
                await Connection.SetImageAsync(GetRunningImage());
            }
            else
            {
                await Connection.SetImageAsync(GetStoppedImage());
            }
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(settings, payload.Settings);
            SaveSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #region Private Methods

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        private void LoadWindowsServices()
        {
            settings.Services = ServiceController.GetServices().Select(s => new WindowsService() { DisplayName = s.DisplayName, ServiceName = s.ServiceName }).OrderBy(s => s.DisplayName).ToArray();
            SaveSettings();
        }

        private ServiceController GetService()
        {
            if (String.IsNullOrEmpty(settings.ServiceName))
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, "GetService called with empty ServiceName");
                return null;
            }
            return ServiceController.GetServices().Where(s => s.ServiceName == settings.ServiceName).FirstOrDefault();
        }

        private bool HandleServiceOperation()
        {
            try
            {
                ServiceController service = GetService();
                if (service == null)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"HandleServiceOperation for {settings.ServiceName} returned null!");
                    return false;
                }

                WindowsServiceManager wsm = new WindowsServiceManager();
                return wsm.HandleServiceAction(service, settings.Action, true);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} HandleServiceOperation Exception: {ex}");
            }

            return false;
        }
            
        private Image GetRunningImage()
        {
            if (prefetchedRunningImage == null)
            {
                prefetchedRunningImage = Image.FromFile(RUNNING_IMAGE_FILE);
            }
            return prefetchedRunningImage;
        }


        private Image GetStoppedImage()
        {
            if (prefetchedStoppedImage == null)
            {
                prefetchedStoppedImage = Image.FromFile(STOPPED_IMAGE_FILE);
            }
            return prefetchedStoppedImage;
        }

        #endregion
    }
}