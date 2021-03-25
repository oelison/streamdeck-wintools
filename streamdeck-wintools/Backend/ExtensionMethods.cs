using BarRaider.SdTools;
using BarRaider.SdTools.Wrappers;
using System;
using System.Collections.Generic;
using System.Globalization;
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

        internal static string Truncate(this string str, int maxSize)
        {
            if (string.IsNullOrEmpty(str))
            {
                return str;
            }

            if (maxSize == 0)
            {
                return String.Empty;
            }

            return str.Substring(0, Math.Min(Math.Max(0, maxSize), str.Length));
        }

        internal static string StreamDeckFormat(this CultureInfo culture, TitleParameters titleParameters, int textLengthLimit)
        {
            if (culture == null)
            {
                return String.Empty;
            }

            if (titleParameters == null)
            {
                return culture.DisplayName;
            }

            // Truncate size
            string displayName = culture.DisplayName ?? String.Empty;
            if (textLengthLimit > 0)
            {
                displayName = displayName.Truncate(textLengthLimit);
            }
            string[] parts = displayName.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length == 1)
            {
                return Tools.SplitStringToFit(parts[0], titleParameters);
            }
            else if (parts.Length == 2)
            {
                return $"{parts[0]}\n{parts[1]}";
            }
            else if (parts.Length > 2)
            {
                return $"{parts[0]}\n{Tools.SplitStringToFit($"{parts[1]} {parts[2]}", titleParameters)}";
            }

            return String.Empty;
        }

    }
}
