using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;


namespace WinTools
{
    [PluginActionId("com.barraider.wintools.keylock")]
    class KeyLockAction : PluginBase
    {
        private enum KeyType
        {
            CapsLock = 0,
            NumLock = 1,
            ScrollLock = 2
        }
        private enum ShowState
        {
            Unset = 0,
            UnLocked = 1,
            Locked = 2
        }
        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true, CallingConvention = CallingConvention.Winapi)]
        private static extern short GetKeyState(int keyCode);
        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true, CallingConvention = CallingConvention.Winapi)]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    keyType = KeyType.CapsLock
                };
                return instance;
            }

            [FilenameProperty]
            [JsonProperty(PropertyName = "keyType")]
            public KeyType keyType { get; set; }
        }

        #region Private Members

        private PluginSettings settings;
        private Image backgroundImage = null;
        private ShowState numLock = ShowState.Unset;
        private ShowState capsLock = ShowState.Unset;
        private ShowState scrollLock = ShowState.Unset;
        private KeyType shownKeyType = KeyType.CapsLock;
        private const string NUMLOCK_ON_IMAGE = @"images\numLockOn@2x.png";
        private const string NUMLOCK_OFF_IMAGE = @"images\numLockOff@2x.png";
        private const string CAPSLOCK_ON_IMAGE = @"images\capsLockOn@2x.png";
        private const string CAPSLOCK_OFF_IMAGE = @"images\capsLockOff@2x.png";
        private const string SCROLLLOCK_ON_IMAGE = @"images\scrollLockOn@2x.png";
        private const string SCROLLLOCK_OFF_IMAGE = @"images\scrollLockOff@2x.png";
        private const byte VK_CAPSLOCK = 0x14;
        private const byte VK_NUMLOCK = 0x90;
        private const byte VK_SCROLLLOCK = 0x91;

        #endregion
        public KeyLockAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
        }

        public override void Dispose()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");
        }

        public override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Pressed");
        }

        public override void KeyReleased(KeyPayload payload)
        {
            const int KEYEVENTF_EXTENDEDKEY = 0x1;
            const int KEYEVENTF_KEYUP = 0x2;
            const byte SCANCODE_NULL = 0x0;
            if (settings.keyType == KeyType.CapsLock)
            {
                keybd_event(VK_CAPSLOCK, SCANCODE_NULL, KEYEVENTF_EXTENDEDKEY, (UIntPtr)0);
                keybd_event(VK_CAPSLOCK, SCANCODE_NULL, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, (UIntPtr)0);
            }
            else if (settings.keyType == KeyType.NumLock)
            {
                keybd_event(VK_NUMLOCK, SCANCODE_NULL, KEYEVENTF_EXTENDEDKEY, (UIntPtr)0);
                keybd_event(VK_NUMLOCK, SCANCODE_NULL, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, (UIntPtr)0);
            }
            else if (settings.keyType == KeyType.ScrollLock)
            {
                keybd_event(VK_SCROLLLOCK, SCANCODE_NULL, KEYEVENTF_EXTENDEDKEY, (UIntPtr)0);
                keybd_event(VK_SCROLLLOCK, SCANCODE_NULL, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, (UIntPtr)0);
            }
        }

        public async override void OnTick()
        {
            if (shownKeyType != settings.keyType)
            {
                shownKeyType = settings.keyType;
                numLock = ShowState.Unset;
                capsLock = ShowState.Unset;
                scrollLock = ShowState.Unset;
            }
            if (shownKeyType == KeyType.CapsLock)
            {
                if ((((ushort)GetKeyState(VK_CAPSLOCK)) & 0xffff) != 0)
                {
                    if (capsLock != ShowState.Locked)
                    {
                        capsLock = ShowState.Locked;
                        backgroundImage = Image.FromFile(CAPSLOCK_ON_IMAGE);
                        await DrawKey();
                    }
                }
                else
                {
                    if (capsLock != ShowState.UnLocked)
                    {
                        capsLock = ShowState.UnLocked;
                        backgroundImage = Image.FromFile(CAPSLOCK_OFF_IMAGE);
                        await DrawKey();
                    }
                }
            }
            else if (shownKeyType == KeyType.NumLock)
            {
                if ((((ushort)GetKeyState(VK_NUMLOCK)) & 0xffff) != 0)
                {
                    if (numLock != ShowState.Locked)
                    {
                        numLock = ShowState.Locked;
                        backgroundImage = Image.FromFile(NUMLOCK_ON_IMAGE);
                        await DrawKey();
                    }
                }
                else
                {
                    if (numLock != ShowState.UnLocked)
                    {
                        numLock = ShowState.UnLocked;
                        backgroundImage = Image.FromFile(NUMLOCK_OFF_IMAGE);
                        await DrawKey();
                    }
                }
            }
            else if (shownKeyType == KeyType.ScrollLock)
            {
                if ((((ushort)GetKeyState(VK_SCROLLLOCK)) & 0xffff) != 0)
                {
                    if (scrollLock != ShowState.Locked)
                    {
                        scrollLock = ShowState.Locked;
                        backgroundImage = Image.FromFile(SCROLLLOCK_ON_IMAGE);
                        await DrawKey();
                    }
                }
                else
                {
                    if (scrollLock != ShowState.UnLocked)
                    {
                        scrollLock = ShowState.UnLocked;
                        backgroundImage = Image.FromFile(SCROLLLOCK_OFF_IMAGE);
                        await DrawKey();
                    }
                }
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

        private async Task DrawKey()
        {

            using (Bitmap bmp = Tools.GenerateGenericKeyImage(out Graphics graphics))
            {
                int height = bmp.Height;
                int width = bmp.Width;

                // Draw background image
                if (backgroundImage != null)
                {
                    graphics.DrawImage(backgroundImage, 0, 0, width, height);
                }

                await Connection.SetImageAsync(bmp);
                graphics.Dispose();

            }
        }

        #endregion
    }
}
