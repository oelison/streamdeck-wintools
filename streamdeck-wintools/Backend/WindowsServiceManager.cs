using BarRaider.SdTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WinTools.Backend
{
    internal class WindowsServiceManager
    {
        public enum ServiceActionEnum
        {
            Restart = 0,
            Start = 1,
            Stop = 2
        }

        public bool HandleServiceAction(ServiceController service, ServiceActionEnum action, bool tryAdmin)
        {
            try
            {
                if (service == null)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"HandleServiceAction called but service is null!");
                    return false;
                }
                switch (action)
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
                    default:
                        Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} HandleServiceOperation: Invalid Action {action}");
                        return false;
                }
                return true;
            }
            catch (InvalidOperationException ioe)
            {
                if (tryAdmin)
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} HandleServiceOperation Invalid Operation exception, trying to run as admin {ioe}");
                    WinToolsRunnerHandler.LaunchWintoolsRunner(this, $"{service.ServiceName} {(int)action}");
                    return true;
                }

                throw ioe;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} HandleServiceOperation Exception: {ex}");
            }
            return false;
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

    }
}
