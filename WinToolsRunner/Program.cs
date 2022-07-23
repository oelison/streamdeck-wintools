using BarRaider.SdTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using static WinTools.Backend.WindowsServiceManager;

namespace WinToolsRunner
{
    internal class Program
    {

        #region Private Members
        private const int MIN_EXPECTED_ARGS = 3;
        private const string ACTION_SERVICES = "WinTools.Backend.WindowsServiceManager";

        #endregion
        static void Main(string[] args)
        {
            if (args.Length < MIN_EXPECTED_ARGS)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"WinToolsRunner missing arguments. Expected {MIN_EXPECTED_ARGS} received {args.Length}");
                return;
            }

            string action = args[0];
            string[] actionParams = args.Skip(1).ToArray();

            switch (action)
            {
                case ACTION_SERVICES:
                    HandleServiceRequest(actionParams);
                    break;
                default:
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"WinToolsRunner unsupported action {action}, exiting");
                    return;
            }
        }

        private static void HandleServiceRequest(string[] args)
        {
            if (args.Length < 2)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"WinToolsRunner HandleServiceRequest missing arguments. Expected {2} received {args.Length}");
                return;
            }

            try
            {
                string serviceName = args[0];
                ServiceController service = ServiceController.GetServices().Where(s => s.ServiceName ==serviceName).FirstOrDefault();

                if (service == null)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"WinToolsRunner HandleServiceRequest invalid service {args[0]}");
                    return;
                }

                if (!Int32.TryParse(args[1], out int actionId))
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"WinToolsRunner HandleServiceRequest invalid action argument {args[1]}");
                    return;
                }
                WinTools.Backend.WindowsServiceManager.ServiceActionEnum serviceAction = (ServiceActionEnum)actionId;

                WinTools.Backend.WindowsServiceManager wsm = new WinTools.Backend.WindowsServiceManager();
                wsm.HandleServiceAction(service, serviceAction, false);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"WinToolsRunner HandleServiceRequest Exception: {ex}");
            }

        }
    }
}
