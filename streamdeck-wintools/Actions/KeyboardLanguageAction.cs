using BarRaider.SdTools;
using BarRaider.SdTools.Wrappers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinTools.Backend;

namespace WinTools.Actions
{

    [PluginActionId("com.barraider.wintools.keyboardlang")]
    public class KeyboardLanguageAction : PluginBase
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    TextLimit = DEFAULT_TEXT_LIMIT.ToString()
                };
                return instance;
            }

            [JsonProperty(PropertyName = "textLimit")]
            public String TextLimit { get; set; }
        }

        #region Private Members
        private const int DEFAULT_TEXT_LIMIT = 0;
        
        private readonly PluginSettings settings;
        private TitleParameters titleParameters;
        private int textLimit = DEFAULT_TEXT_LIMIT;

        #endregion

        public KeyboardLanguageAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            InitializeSettings();
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

        public override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Key Pressed {this.GetType()}");

            // Hack: Check if StreamDeck is in foreground and close if it is (otherwise it crashes ¯\_(ツ)_/¯ )
            HandleStreamDeckAsForeground();

            KeyboardManager.Instance.ChangeKeyboardInputLanguage();
        }

        public override void KeyReleased(KeyPayload payload) { }

        public async override void OnTick() 
        {
            await Connection.SetTitleAsync(KeyboardManager.Instance.GetKeyboardLanguageInput()?.StreamDeckFormat(titleParameters, textLimit));
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(settings, payload.Settings);
            InitializeSettings();
            SaveSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #endregion

        #region Private Methods

        private void InitializeSettings()
        {
            if (!Int32.TryParse(settings.TextLimit, out textLimit))
            {
                settings.TextLimit = DEFAULT_TEXT_LIMIT.ToString();
                textLimit = DEFAULT_TEXT_LIMIT;
            }
        }
        
        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        private void HandleStreamDeckAsForeground()
        {
            var activeHandle = GetForegroundWindow();
            foreach (var process in Process.GetProcessesByName("StreamDeck"))
            {
                if (process.MainWindowHandle == activeHandle)
                {
                    process.CloseMainWindow();
                    Thread.Sleep(300);
                    return;
                }
            }
        }

        #endregion
    }
}
