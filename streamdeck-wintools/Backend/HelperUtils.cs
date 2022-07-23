using BarRaider.SdTools;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;

namespace WinTools.Backend
{
    internal static class HelperUtils
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        internal static Process GetForegroundWindowProcess()
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
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"GetForegroundWindowProcess Exception: {ex}");
            }
            return null;
        }
    }
}
