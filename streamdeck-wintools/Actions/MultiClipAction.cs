using BarRaider.SdTools;
using BarRaider.SdTools.Wrappers;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Contexts;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsInput;
using WinTools.Backend;
using WinTools.Wrappers;

namespace WinTools
{
    //---------------------------------------------------
    //          BarRaider's Hall Of Fame
    // Subscriber: SenseiHitokiri
    // Subscriber: Tek_Soup
    // nubby_ninja - Tip: 10.10
    // Subscriber: superslycer
    // 1 Bits: Anonymous
    // Subscriber: OneMouseGaming
    //---------------------------------------------------
    [PluginActionId("com.barraider.wintools.multiclip")]
    public class MultiClipAction : PluginBase
    {
        private enum LongPressAction
        {
            StoreSelectedText = 0,
            StoreClipboard = 1
        }
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    LongKeypressTime = LONG_KEYPRESS_LENGTH_MS.ToString(),
                    PlaySoundOnSet = false,
                    PlaybackDevices = null,
                    PlaybackDevice = String.Empty,
                    PlaySoundOnSetFile = String.Empty,
                    SharedId = String.Empty,
                    LongPressAction = LongPressAction.StoreSelectedText
                };
                return instance;
            }

            [JsonProperty(PropertyName = "longKeypressTime")]
            public string LongKeypressTime { get; set; }

            [JsonProperty(PropertyName = "playSoundOnSet")]
            public bool PlaySoundOnSet { get; set; }

            [JsonProperty(PropertyName = "playbackDevices")]
            public List<PlaybackDevice> PlaybackDevices { get; set; }

            [JsonProperty(PropertyName = "playbackDevice")]
            public string PlaybackDevice { get; set; }

            [FilenameProperty]
            [JsonProperty(PropertyName = "playSoundOnSetFile")]
            public string PlaySoundOnSetFile { get; set; }

            [JsonProperty(PropertyName = "sharedId")]
            public string SharedId { get; set; }

            [JsonProperty(PropertyName = "longPressAction")]
            public LongPressAction LongPressAction { get; set; }
        }

        #region Private Members
        private const int LONG_KEYPRESS_LENGTH_MS = 600;
        private const int MAX_TITLE_LENGTH = 50;

        private readonly PluginSettings settings;
        private TitleParameters titleParameters;
        private bool longKeyPressed = false;
        private int longKeypressTime = LONG_KEYPRESS_LENGTH_MS;
        private readonly System.Timers.Timer tmrRunLongPress = new System.Timers.Timer();
        private readonly InputSimulator iis = new InputSimulator();
        private string previousText = String.Empty;
        private string clipboardId = String.Empty;


        #endregion
        public MultiClipAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            Connection.OnSendToPlugin += Connection_OnSendToPlugin;
            Connection.OnTitleParametersDidChange += Connection_OnTitleParametersDidChange;
            tmrRunLongPress.Interval = longKeypressTime;
            tmrRunLongPress.Elapsed += TmrRunLongPress_Elapsed;
            InitializeSettings();
        }

        public override void Dispose()
        {
            tmrRunLongPress.Stop();
            Connection.OnSendToPlugin -= Connection_OnSendToPlugin;
            Connection.OnTitleParametersDidChange -= Connection_OnTitleParametersDidChange;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");
        }

        public async override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Key Pressed {this.GetType()}");

            if (payload.IsInMultiAction)
            {
                await HandleMultiActionKeyPress(payload.UserDesiredState);
            }
            else
            {
                longKeyPressed = false;

                tmrRunLongPress.Interval = longKeypressTime > 0 ? longKeypressTime : LONG_KEYPRESS_LENGTH_MS;
                tmrRunLongPress.Start();
            }
        }

        public override void KeyReleased(KeyPayload payload)
        {
            tmrRunLongPress.Stop();
            if (!payload.IsInMultiAction)
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, $"Key Released {this.GetType()}");
                if (!longKeyPressed)
                {
                    HandleShortKeyPress();
                }
            }
        }

        public async override void OnTick()
        {
            string currentValue = String.Empty;
            if (!String.IsNullOrEmpty(CacheManager.Instance.GetValue(clipboardId)))
            {
                currentValue = Tools.SplitStringToFit(CacheManager.Instance.GetValue(clipboardId), titleParameters);
            }

            if (currentValue != previousText)
            {
                previousText = currentValue;
                await Connection.SetTitleAsync(currentValue.Truncate(MAX_TITLE_LENGTH)?.Trim());
            }
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(settings, payload.Settings);
            InitializeSettings();
            SaveSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #region Private Methods

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        private async void TmrRunLongPress_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            tmrRunLongPress.Stop(); // Should only run once
            await HandleLongKeyPress();
        }

        private void HandleShortKeyPress()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} Short Keypress");
            string clipboardEntry = CacheManager.Instance.GetValue(clipboardId);
            if (!String.IsNullOrEmpty(clipboardEntry)) // Short Key Press
            {
                SetClipboard(clipboardEntry);
                iis.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.CONTROL, WindowsInput.Native.VirtualKeyCode.VK_V); // Send a Ctrl-V to paste selection into the clipboard
            }
        }

        private async Task HandleLongKeyPress()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} Long Keypress");
            longKeyPressed = true;

            if (settings.LongPressAction == LongPressAction.StoreSelectedText)
            {
                iis.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.CONTROL, WindowsInput.Native.VirtualKeyCode.VK_C); // Send a Ctrl-C to copy selection into the clipboard
                Thread.Sleep(50);
            }
            ReadFromClipboard(); // Fetch it from the clipboard and store it internally
            await PlaySoundOnSet();
        }

        private void InitializeSettings()
        {
            if (!Int32.TryParse(settings.LongKeypressTime, out longKeypressTime))
            {
                settings.LongKeypressTime = LONG_KEYPRESS_LENGTH_MS.ToString();
                SaveSettings();
            }

            clipboardId = Connection.ContextId;
            if (!String.IsNullOrEmpty(settings.SharedId))
            {
                clipboardId = settings.SharedId;
            }

            PropagatePlaybackDevices();
        }

        #region Clipboard

        private void ReadFromClipboard()
        {
            Thread staThread = new Thread(
                delegate ()
                {
                    try
                    {
                        CacheManager.Instance.SetValue(clipboardId, Clipboard.GetText(System.Windows.Forms.TextDataFormat.Text));
                    }

                    catch (Exception ex)
                    {
                        Logger.Instance.LogMessage(TracingLevel.ERROR, $"ReadFromClipboard exception: {ex}");
                    }
                });
            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
            staThread.Join();
        }

        private void SetClipboard(string text)
        {
            Thread staThread = new Thread(
                delegate ()
                {
                    try
                    {
                        Clipboard.SetText(text);
                    }

                    catch (Exception ex)
                    {
                        Logger.Instance.LogMessage(TracingLevel.ERROR, $"SetClipboard exception: {ex}");
                    }
                });
            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
            staThread.Join();
        }

        #endregion

        #region Play Sound

        private void PropagatePlaybackDevices()
        {
            settings.PlaybackDevices = new List<PlaybackDevice>();

            try
            {
                if (settings.PlaySoundOnSet)
                {
                    settings.PlaybackDevices = AudioUtils.Common.GetAllPlaybackDevices(true).Select(d => new PlaybackDevice() { ProductName = d }).ToList();
                    SaveSettings();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error propagating playback devices {ex}");
            }
        }

        private async Task PlaySoundOnSet()
        {
            if (!settings.PlaySoundOnSet)
            {
                return;
            }

            if (String.IsNullOrEmpty(settings.PlaySoundOnSetFile) || string.IsNullOrEmpty(settings.PlaybackDevice))
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"PlaySoundOnSet called but File or Playback device are empty. File: {settings.PlaySoundOnSetFile} Device: {settings.PlaybackDevice}");
                return;
            }

            if (!File.Exists(settings.PlaySoundOnSetFile))
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"PlaySoundOnSet called but file does not exist: {settings.PlaySoundOnSetFile}");
                return;
            }

            Logger.Instance.LogMessage(TracingLevel.INFO, $"PlaySoundOnEnd called. Playing {settings.PlaySoundOnSetFile} on device: {settings.PlaybackDevice}");
            await AudioUtils.Common.PlaySound(settings.PlaySoundOnSetFile, settings.PlaybackDevice);
        }

        #endregion

        private void Connection_OnSendToPlugin(object sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Events.SendToPlugin> e)
        {
            var payload = e.Event.Payload;

            if (payload["property_inspector"] != null)
            {
                switch (payload["property_inspector"].ToString().ToLowerInvariant())
                {
                    case "resetmulticlips":
                        CacheManager.Instance.ClearCache();
                        Logger.Instance.LogMessage(TracingLevel.WARN, $"resetMultiClips called, clipboards are cleared");
                        break;
                }
            }
        }

        private void Connection_OnTitleParametersDidChange(object sender, SDEventReceivedEventArgs<BarRaider.SdTools.Events.TitleParametersDidChange> e)
        {
            titleParameters = e.Event?.Payload?.TitleParameters;
        }

        private async Task HandleMultiActionKeyPress(uint state)
        {
            // Multiaction mode, check if desired state is 1 (0==Short Press, 1==Long Press, 2==Clear Multi Clip) 
            switch (state)
            {
                case (0): // Short Keypress
                    HandleShortKeyPress();
                    break;
                case (1): // Long Keypress
                    await HandleLongKeyPress();
                    break;
                case (2): // Clear Multi Clips
                    CacheManager.Instance.ClearCache();
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} ResetMultiClips called, clipboards are cleared");
                    break;
                default:
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} HandleMultiActionKeyPress - Invalid State: {state}");
                    break;
            }
        }

        #endregion
    }
}