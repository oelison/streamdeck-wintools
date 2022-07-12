using BarRaider.SdTools;
using BarRaiderAudio;
using BarRaiderAudio.Wrappers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Radios;
using WinTools.Backend;
using WinTools.Wrappers;

namespace WinTools.Actions
{
    [PluginActionId("com.barraider.wintools.bluetooth")]
    public class BluetoothToggleAction : PluginBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    Radios = null,
                    Radio = String.Empty,

                };
                return instance;
            }

            [JsonProperty(PropertyName = "radios")]
            public List<RadioDevice> Radios { get; set; }

            [JsonProperty(PropertyName = "radio")]
            public String Radio { get; set; }
        }

        #region Private Members
        private const string ACTIVE_IMAGE_FILE = @"images\bluetoothEnabled.png";

        private Image prefetchedActiveImage;
        private readonly PluginSettings settings;
        private Radio radio;

        #endregion
        public BluetoothToggleAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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

            Connection.OnPropertyInspectorDidAppear += Connection_OnPropertyInspectorDidAppear;
            Connection.OnSendToPlugin += Connection_OnSendToPlugin;
            InitializeSettings();
        }

        public override void Dispose()
        {
            Connection.OnPropertyInspectorDidAppear -= Connection_OnPropertyInspectorDidAppear;
            Connection.OnSendToPlugin -= Connection_OnSendToPlugin;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{GetType()} Destructor called");

        }

        public async override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{GetType()} Key Pressed");
            if (String.IsNullOrEmpty(settings.Radio))
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"{GetType()} Key Pressed but no radio is set");
                return;
            }

            if (radio == null)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"{GetType()} Key Pressed but radio is null");
                return;
            }

            if (payload.IsInMultiAction)
            {
                await HandleMultiActionKeypress(payload.UserDesiredState);
                return;
            }

            await ToggleRadioState();
        }

        public override void KeyReleased(KeyPayload payload) { }

        public async override void OnTick()
        {
            if (radio == null)
            {
                return;
            }

            if (radio.State == RadioState.On)
            {
                await Connection.SetImageAsync(GetActiveRadioImage());
            }
            else
            {
                await Connection.SetImageAsync((string)null);
            }
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(settings, payload.Settings);
            InitializeSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #region Private Methods

        private async void InitializeSettings()
        {
            if (String.IsNullOrEmpty(settings.Radio))
            {
                radio = null;
            }
            else if (radio == null || radio.Name != settings.Radio)
            {
                radio = (await Radio.GetRadiosAsync()).FirstOrDefault(r => r.Name == settings.Radio);
            }
        }

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        private async void FetchRadios()
        {

            settings.Radios = (await Radio.GetRadiosAsync())?.Select(r => new RadioDevice() { Name = r.Name }).ToList() ?? null;
            if (settings.Radios == null)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"{GetType()} FetchRadios called but returned null");
                return;
            }

            Logger.Instance.LogMessage(TracingLevel.INFO, $"{GetType()} FetchRadios returned {settings.Radios.Count} devices");
            await SaveSettings();
        }

        private void Connection_OnSendToPlugin(object sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Events.SendToPlugin> e)
        {
            var payload = e.Event.Payload;

            if (payload["property_inspector"] != null)
            {
                switch (payload["property_inspector"].ToString().ToLowerInvariant())
                {
                    case "refreshradios":
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"{GetType()} refreshApplications called");
                        FetchRadios();
                        break;
                }
            }
        }

        private void Connection_OnPropertyInspectorDidAppear(object sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Events.PropertyInspectorDidAppear> e)
        {
            FetchRadios();
        }

        private async Task ToggleRadioState()
        {
            if (radio == null)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} ToggleRadioState called but radio is null {settings.Radio}");
                return;

            }

            RadioState radioState = radio.State == RadioState.On ? RadioState.Off : RadioState.On;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Toggling {radio.Name} state to {radioState}. Current state is: {radio.State}");

            var res = await radio.SetStateAsync(radioState);
            if (res == RadioAccessStatus.Allowed)
            {
                await Connection.ShowOk();
            }
            else
            {
                await Connection.ShowAlert();
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{GetType()} Access denied when toggling radio state for {radio?.Name}: {res}");
            }
        }
        private async Task HandleMultiActionKeypress(uint state)
        {
            if (radio == null)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} Multi-Action pressed but radio is null {settings.Radio}");
                return;

            }

            RadioState radioState = radio.State;
            switch (state) // 0 = Toggle, 1 = On, 2 = Off
            {
                case 0:
                    await ToggleRadioState();
                    break;
                case 1: // On
                    if (radioState == RadioState.Off)
                    {
                        await ToggleRadioState();
                    }
                    break;
                case 2: // Off
                    if (radioState == RadioState.On)
                    {
                        await ToggleRadioState();
                    }
                    break;
                default:
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"HandleMultiActionKeypress Invalid state: {state}");
                    return;
            }
        }

        private Image GetActiveRadioImage()
        {
            if (prefetchedActiveImage == null)
            {
                prefetchedActiveImage = Image.FromFile(ACTIVE_IMAGE_FILE);
            }
            return prefetchedActiveImage;
        }

        #endregion
    }
}