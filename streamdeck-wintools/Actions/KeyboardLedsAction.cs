using BarRaider.SdTools;
using BarRaider.SdTools.Wrappers;
using FontAwesome.Sharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinTools.Backend;
using WinTools.Wrappers;


namespace WinTools.Actions
{

    [PluginActionId("com.barraider.wintools.keyboardleds")]
    public class KeyboardLedsAction : PluginBase
    {
        private enum KeypressActions
        {
            Unset = 0,
            LockStatus = 1
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    KeyPressAction = KeypressActions.Unset
                };
                return instance;
            }

            [JsonProperty(PropertyName = "keyPress")]
            public KeypressActions KeyPressAction { get; set; }
        }

        #region Private Members
        private const int DEFAULT_TEXT_LIMIT = 0;
        private const string BACKGROUND_IMAGE = @"images\keyAction@2x.png";


        private readonly PluginSettings settings;
        private TitleParameters titleParameters;
        private Image backgroundImage = null;
        private bool isLocked = false;
        private Dictionary<System.Windows.Forms.Keys, bool> dicLockStatus = new Dictionary<System.Windows.Forms.Keys, bool>();

        #endregion

        public KeyboardLedsAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            Connection.OnTitleParametersDidChange += Connection_OnTitleParametersDidChange;
            PrefetchImages();
        }

        private void PrefetchImages()
        {
            if (backgroundImage != null)
            {
                backgroundImage.Dispose();
                backgroundImage = null;
            }

            if (!File.Exists(BACKGROUND_IMAGE))
            {

                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} PrefetchIamges: File not found {BACKGROUND_IMAGE}");
                return;
            }

            backgroundImage = Image.FromFile(BACKGROUND_IMAGE);
        }

        private void Connection_OnTitleParametersDidChange(object sender, SDEventReceivedEventArgs<BarRaider.SdTools.Events.TitleParametersDidChange> e)
        {
            titleParameters = e?.Event?.Payload?.TitleParameters;
        }

        #region Public Methods

        public override void Dispose()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} Destructor called");
            Connection.OnTitleParametersDidChange -= Connection_OnTitleParametersDidChange;
        }

        public async override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Key Pressed {this.GetType()}");
            if (settings.KeyPressAction == KeypressActions.Unset)
            {

                Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} No action set for keypress");
                await Connection.ShowAlert();
                return;
            }

            isLocked = !isLocked;
            if (isLocked)
            {
                dicLockStatus = KeyboardManager.Instance.GetLockKeysStatus().ToDictionary(item => item.Key, item => item.IsKeyLocked);
            }

        }

        public override void KeyReleased(KeyPayload payload) { }

        public async override void OnTick()
        {
            var keys = KeyboardManager.Instance.GetLockKeysStatus();

            if (keys == null || HandleLockMode(keys))
            {
                return;
            }

            await DrawKey(keys);
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(settings, payload.Settings);
            SaveSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #endregion

        #region Private Methods

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        private async Task DrawKey(List<KeyStatus> keysList)
        {
            if (keysList == null || keysList.Count == 0)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} DrawKey: keysList is null!");
                return;
            }

            if (titleParameters == null)
            {

                Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} DrawKey: TitleParameters is null!");
                return;
            }

            const int KEY_POSITION_Y = 25;
            const int KEY_PADDING_X = 15;
            const int KEY_TEXT_SIZE_INCREASE = 5;
            const int ICON_SIZE_PIXELS = 55;
            

            using (Bitmap bmp = Tools.GenerateGenericKeyImage(out Graphics graphics))
            {
                int height = bmp.Height;
                int width = bmp.Width;

                // Draw background image
                if (backgroundImage != null)
                {
                    graphics.DrawImage(backgroundImage, 0, 0, width, height);
                }

                if (isLocked)
                {
                    using (Bitmap icon = FormsIconHelper.ToBitmap(IconChar.Lock, Color.Red, ICON_SIZE_PIXELS))
                    {
                        graphics.DrawImage(icon, new PointF(width / 2 - ICON_SIZE_PIXELS / 2 , (int)height - ICON_SIZE_PIXELS));
                    }
                }

                int partWidth = width / keysList.Count;
                using (Font font = new Font(titleParameters.FontFamily, (float)titleParameters.FontSizeInPixels + KEY_TEXT_SIZE_INCREASE, FontStyle.Bold, GraphicsUnit.Pixel))
                {
                    for (int currKey = 0; currKey < keysList.Count; currKey++)
                    {
                        int startPos = (partWidth * currKey) + KEY_PADDING_X;
                        if (keysList[currKey] == null)
                        {

                            Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} DrawKey: Invalid KeyStatus");
                            continue;
                        }

                        Color color = keysList[currKey].IsKeyLocked ? Color.Green : Color.White;
                        graphics.DrawString(keysList[currKey].Key.ToString().Substring(0, 1), font, new SolidBrush(color), new PointF(startPos, KEY_POSITION_Y));
                    }

                    await Connection.SetImageAsync(bmp);
                    graphics.Dispose();
                }
            }
        }

        /// <summary>
        /// Checks if any of the lock key's status needs to change. Returns if any modification was made
        /// </summary>
        /// <param name="keysList"></param>
        /// <returns>Did we modify any of the lock keys</returns>
        private bool HandleLockMode(List<KeyStatus> keysList)
        {
            if (!isLocked)
            {
                return false;
            }

            if (keysList == null || dicLockStatus == null)
            {
                return false;
            }

            try
            {
                bool lockChangeMade = false;
                foreach (var key in keysList)
                {
                    if (dicLockStatus.ContainsKey(key.Key) && dicLockStatus[key.Key] != key.IsKeyLocked)
                    {
                        lockChangeMade = true;
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} Found change in key {key.Key}, changing status back");

                        ToggleLockKeyPress(key.Key);
                    }
                }

                return lockChangeMade;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} HandleLockMode Exception : {ex}");
                return false;
            }
        }

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        private void ToggleLockKeyPress(System.Windows.Forms.Keys key)
        {
            const int KEYEVENTF_EXTENDEDKEY = 0x1;
            const int KEYEVENTF_KEYUP = 0x2;
            const byte CAPSLOCK = 0x14;
            const byte NUMLOCK = 0x90;
            const byte SCROLLLOCK = 0x91;

            byte lockKey;
            switch (key)
            {
                case System.Windows.Forms.Keys.CapsLock:
                    lockKey = CAPSLOCK;
                    break;
                case System.Windows.Forms.Keys.NumLock:
                    lockKey = NUMLOCK;
                    break;
                case System.Windows.Forms.Keys.Scroll:
                    lockKey = SCROLLLOCK;
                    break;
                default:
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} ToggleLockKeyPress: Unsupported key {key}");
                    return;
            }

            keybd_event(lockKey, 0x45, KEYEVENTF_EXTENDEDKEY, (UIntPtr)0);
            keybd_event(lockKey, 0x45, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, (UIntPtr)0);
        }
    }

    #endregion
}

