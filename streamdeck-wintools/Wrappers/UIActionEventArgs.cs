using BarRaider.SdTools;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinTools.Wrappers
{
    public class UIActionEventArgs : EventArgs
    {
        public UIActionSettings[] Settings { get; set; }
        public bool AllKeysAction { get; set; }


        public UIActionEventArgs(UIActionSettings[] uiActionSettings, bool allKeysAction = false)
        {
            Settings = uiActionSettings;
            AllKeysAction = allKeysAction;
        }
    }
}
