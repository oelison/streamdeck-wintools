using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinTools.Backend
{
    internal static class ExtensionMethods
    {
        internal static string ToHumanReadableTickCount(this UInt64 tickCount)
        {
            UInt64 total = tickCount / 1000;
            UInt64 minutes = total / 60;
            UInt64 seconds = total % 60;
            UInt64 hours = minutes / 60;
            minutes %= 60;

            UInt64 days = hours / 24;
            hours %= 24;

            StringBuilder sb = new StringBuilder();
            if (days > 0)
            {
                sb.Append($"{days} days\n");
            }

            if (days > 0 || hours > 0)
            {
                sb.Append($"{hours.ToString("00")}:");
            }
            sb.Append($"{minutes.ToString("00")}:{seconds.ToString("00")}");
            return sb.ToString();
        }

    }
}
