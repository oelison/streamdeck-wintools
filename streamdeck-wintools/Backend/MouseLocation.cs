using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace WinTools.Backend
{
    internal static class MouseLocation
    {
        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(SystemMetric smIndex);

        const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
        const uint MOUSEEVENTF_MOVE = 0x0001;
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy,
                                            uint dwData, UIntPtr dwExtraInfo);

        internal static Point ConvertScreenPointToAbsolutePoint(Point currentPoint)
        {
            // Get current desktop maximum screen resolution.
            int screenMaxWidth = GetSystemMetrics(SystemMetric.SM_CXMAXTRACK) - 1;
            int screenMaxHeight = GetSystemMetrics(SystemMetric.SM_CYMAXTRACK) - 1;

            double convertedPointX = (currentPoint.X * (65535.0f / screenMaxWidth));
            double convertedPointY = (currentPoint.Y * (65535.0f / screenMaxHeight));
            return new Point((int)convertedPointX, (int)convertedPointY);
        }
    }
}