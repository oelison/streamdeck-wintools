using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinTools.Wrappers;

namespace WinTools
{
    public enum ServiceActionEnum
    {
        Restart = 0,
        Start = 1,
        Stop = 2
    }
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
            public ServiceActionEnum Action { get; set; }
        }

        #region Private Members

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

        public override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Pressed");

            if (String.IsNullOrEmpty(settings.ServiceName))
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, "Key Pressed but no service is selected");
                return;
            }

            HandleServiceOperation();
        }

        public override void KeyReleased(KeyPayload payload) { }

        public async override void OnTick()
        {
            await Connection.SetTitleAsync($"{settings.ServiceName ?? ""}\n{settings.Action}");
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

        private void HandleServiceOperation()
        {
            ServiceController service = GetService();
            if (service == null)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, "HandleServiceOperation on null service");
                return;
            }

            switch (settings.Action)
            {
                case ServiceActionEnum.Stop:
                    StopService(service);
                    break;
                case ServiceActionEnum.Start:
                    StartService(service);
                    break;
                case ServiceActionEnum.Restart:
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"Restarting {service.DisplayName}");
                    StopService(service);
                    int attempts = 120; // 120 attempts = 2 minutes
                    while (service.Status != ServiceControllerStatus.Stopped && attempts > 0)
                    {
                        attempts--;
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"Waiting for {service.DisplayName} to stop...");
                        Thread.Sleep(1000);
                        service.Refresh();
                    }
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"{service.DisplayName} stopped, attempting restart");
                    StartService(service);
                    break;
            }
        }

        private void StopService(ServiceController service)
        {
            if (service.Status != ServiceControllerStatus.Running)
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, $"Not stopping {service.DisplayName} as status is: {service.Status}");
                return;
            }
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Stopping {service.DisplayName}");
            service.Stop();
        }

        private void StartService(ServiceController service)
        {
            if (service.Status != ServiceControllerStatus.Stopped)
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, $"Not starting {service.DisplayName} as status is: {service.Status}");
                return;
            }
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Starting {service.DisplayName}");
            service.Start();
        }

        #endregion
    }
}