using BarRaider.SdTools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinTools.Backend
{
    internal static class WinToolsRunnerHandler
    {
        private const string WINTOOLS_ADMIN_RUNNER_EXENAME = "WinToolsRunner.exe";

        internal static void LaunchWintoolsRunner(object sender, string args)
        {
            try
            {
                // Prepare the process to run
                ProcessStartInfo start = new ProcessStartInfo();

                // Enter the executable to run, including the complete path
                start.FileName = WINTOOLS_ADMIN_RUNNER_EXENAME;
                // Do you want to show a console window?
                start.Verb = "runas";

                start.Arguments = $"{sender?.GetType()} {args}";

                // Launch the app
                Process.Start(start);
            }
            catch (Exception ex)
            {

                Logger.Instance.LogMessage(TracingLevel.ERROR, $"LaunchWintoolsRunner Exception: {ex}");
            }
        }

    }
}
