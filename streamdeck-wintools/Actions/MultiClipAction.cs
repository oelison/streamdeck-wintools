using BarRaider.SdTools;
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
                    PlaySoundOnSetFile = String.Empty
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


        }

        #region Private Members
        private const int LONG_KEYPRESS_LENGTH_MS = 600;
        private const int STRING_SPLIT_SIZE = 7;

        private readonly PluginSettings settings;
        private bool longKeyPressed = false;
        private int longKeypressTime = LONG_KEYPRESS_LENGTH_MS;
        private readonly System.Timers.Timer tmrRunLongPress = new System.Timers.Timer();
        private readonly InputSimulator iis = new InputSimulator();
        private string previousText = String.Empty;


        #endregion
        public MultiClipAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
            }
            else
            {
                this.settings = payload.Settings.ToObject<PluginSettings>();
            }
            Connection.OnSendToPlugin += Connection_OnSendToPlugin;
            tmrRunLongPress.Interval = longKeypressTime;
            tmrRunLongPress.Elapsed += TmrRunLongPress_Elapsed;
            InitializeSettings();
        }

        public override void Dispose()
        {
            Connection.OnSendToPlugin -= Connection_OnSendToPlugin;
            tmrRunLongPress.Stop();
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");         
        }

        public override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Key Pressed {this.GetType()}");
            longKeyPressed = false;

            tmrRunLongPress.Interval = longKeypressTime > 0 ? longKeypressTime : LONG_KEYPRESS_LENGTH_MS;
            tmrRunLongPress.Start();
        }

        public override void KeyReleased(KeyPayload payload)
        {
            tmrRunLongPress.Stop();
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Key Released {this.GetType()}");
            if (!longKeyPressed)
            {
                HandleShortKeyPress();
            }
        }

        public async override void OnTick()
        {
            string currentValue = String.Empty;
            if (!String.IsNullOrEmpty(CacheManager.Instance.GetValue(Connection.ContextId)))
            {
                currentValue = SplitLongWord(CacheManager.Instance.GetValue(Connection.ContextId));
            }

            if (currentValue != previousText)
            {
                previousText = currentValue;
                await Connection.SetTitleAsync(currentValue); ;
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

        private void TmrRunLongPress_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            tmrRunLongPress.Stop(); // Should only run once
            HandleLongKeyPress();
        }

        private void HandleShortKeyPress()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Short Keypress");
            string clipboardEntry = CacheManager.Instance.GetValue(Connection.ContextId);
            if (!String.IsNullOrEmpty(clipboardEntry)) // Short Key Press
            {
                SetClipboard(clipboardEntry);
                iis.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.CONTROL, WindowsInput.Native.VirtualKeyCode.VK_V); // Send a Ctrl-V to paste selection into the clipboard
            }
        }

        private void HandleLongKeyPress()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Long Keypress");
            longKeyPressed = true;
            iis.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.CONTROL, WindowsInput.Native.VirtualKeyCode.VK_C); // Send a Ctrl-C to copy selection into the clipboard
            ReadFromClipboard(); // Fetch it from the clipboard and store it internally
            PlaySoundOnSet();
        }

        private void InitializeSettings()
        {
            if (!Int32.TryParse(settings.LongKeypressTime, out longKeypressTime))
            {
                settings.LongKeypressTime = LONG_KEYPRESS_LENGTH_MS.ToString();
                SaveSettings();
            }
            PropagatePlaybackDevices();
        }

        private string SplitLongWord(string word)
        {
            if (String.IsNullOrEmpty(word))
            {
                return word;
            }

            // Split up to 4 lines
            for (int idx = 0; idx < 3; idx++)
            {
                int cutSize = STRING_SPLIT_SIZE * (idx + 1);
                if (word.Length > cutSize)
                {
                    word = $"{word.Substring(0, cutSize)}\n{word.Substring(cutSize)}";
                }
            }
            return word;
        }

        #region Clipboard

        private void ReadFromClipboard()
        {
            Thread staThread = new Thread(
                delegate ()
                {
                    try
                    {
                        CacheManager.Instance.SetValue(Connection.ContextId, Clipboard.GetText(System.Windows.Forms.TextDataFormat.Text));
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
                        Logger.Instance.LogMessage(TracingLevel.ERROR, $"ReadFromClipboard exception: {ex}");
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
                    for (int idx = -1; idx < WaveOut.DeviceCount; idx++)
                    {
                        var currDevice = WaveOut.GetCapabilities(idx);
                        settings.PlaybackDevices.Add(new PlaybackDevice() { ProductName = currDevice.ProductName });
                    }

                    settings.PlaybackDevices = settings.PlaybackDevices.OrderBy(p => p.ProductName).ToList();
                    SaveSettings();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error propagating playback devices {ex}");
            }
        }

        private void PlaySoundOnSet()
        {
            Task.Run(() =>
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
                var deviceNumber = GetPlaybackDeviceFromDeviceName(settings.PlaybackDevice);
                using (var audioFile = new AudioFileReader(settings.PlaySoundOnSetFile))
                {
                    using (var outputDevice = new WaveOutEvent())
                    {
                        outputDevice.DeviceNumber = deviceNumber;
                        outputDevice.Init(audioFile);
                        outputDevice.Play();
                        while (outputDevice.PlaybackState == PlaybackState.Playing)
                        {
                            System.Threading.Thread.Sleep(1000);
                        }
                        outputDevice.Stop();
                    }
                }
            });
        }

        private int GetPlaybackDeviceFromDeviceName(string deviceName)
        {
            for (int idx = -1; idx < WaveOut.DeviceCount; idx++)
            {
                var currDevice = WaveOut.GetCapabilities(idx);
                if (deviceName == currDevice.ProductName)
                {
                    return idx;
                }
            }
            return -1;
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


        #endregion
    }
}