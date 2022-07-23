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
using System.Windows.Forms;
using WinTools.Backend;
using WinTools.Wrappers;


namespace WinTools.Actions
{

    [PluginActionId("com.barraider.wintools.keyboardkeytoggle")]
    public class KeyboardKeyToggleAction : PluginBase
    {
        private enum KeyType
        {
            Caps_Lock = 0,
            Num_Lock = 1,
            Scroll_Lock = 2
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    Key = KeyType.Caps_Lock
                };
                return instance;
            }

            [JsonProperty(PropertyName = "keyType")]
            public KeyType Key { get; set; }
        }

        #region Private Members
        private const string LOCKED_IMAGE_FILE = @"images\keyLocked.png";
        private Image prefetchedLockedImage;

        private readonly PluginSettings settings;
        #endregion

        public KeyboardKeyToggleAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            InitializeSettings();
        }


        #region Public Methods

        public override void Dispose()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} Destructor called");
        }

        public override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Key Pressed {this.GetType()} - Changing state for key: {settings.Key}");
            KeyboardManager.Instance.ToggleLockKeyPress(KeyTypeToKey(settings.Key));
        }

        public override void KeyReleased(KeyPayload payload) { }

        public async override void OnTick()
        {
            var keyStatus = KeyboardManager.Instance.GetLockKeysStatus().FirstOrDefault(k => k.Key == KeyTypeToKey(settings.Key));
            if (keyStatus == null)
            {

                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} Could not get status of key {settings.Key}");
                await Connection.SetImageAsync((String)null);
                await Connection.SetTitleAsync(null);
                return;
            }

            await Connection.SetTitleAsync(settings.Key.ToString().Split('_').FirstOrDefault());
            if (keyStatus.IsKeyLocked)
            {
                await Connection.SetImageAsync(GetLockedImage());
            }
            else
            {
                await Connection.SetImageAsync((String)null);
            }
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


        private void InitializeSettings()
        {

        }

        private Keys KeyTypeToKey(KeyType key)
        {
            switch (key)
            {
                case KeyType.Caps_Lock:
                    return Keys.CapsLock;
                case KeyType.Num_Lock:
                    return Keys.NumLock;
                case KeyType.Scroll_Lock:
                    return Keys.Scroll;
                default:
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} KeyTypeToKey invalid key {key}");
                    return Keys.None;
            }
        }

        private Image GetLockedImage()
        {
            if (prefetchedLockedImage == null)
            {
                prefetchedLockedImage = Image.FromFile(LOCKED_IMAGE_FILE);
            }
            return prefetchedLockedImage;
        }
    }

    #endregion
}

