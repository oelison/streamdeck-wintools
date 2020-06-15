using BarRaider.SdTools;
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
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinTools.Backend;
using WinTools.Wrappers;

namespace WinTools
{
    [PluginActionId("com.barraider.wintools.powerplanswitcher")]
    public class PowerPlanSwitcherAction : PluginBase
    {

        //---------------------------------------------------
        //          BarRaider's Hall Of Fame
        // Quote of the day:  "I'm old but not as old as COBOL" -- BarRaider, 2020
        // tobitege - Tip: $15.42
        //---------------------------------------------------
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    PowerPlans = null,
                    PowerPlan = String.Empty,
                    ShowActivePowerPlan = false
                };
                return instance;
            }

            [JsonProperty(PropertyName = "powerPlans")]
            public List<PowerPlanInfo> PowerPlans { get; set; }

            [JsonProperty(PropertyName = "powerPlan")]
            public String PowerPlan { get; set; }

            [JsonProperty(PropertyName = "showActivePowerPlan")]
            public bool ShowActivePowerPlan { get; set; }
        }

        #region Private Members
        private const string ACTIVE_IMAGE_FILE = @"images\powerSelected.png";

        private readonly PluginSettings settings;
        private const int STRING_SPLIT_SIZE = 7;
        private Image prefetchedActiveImage;

        #endregion
        public PowerPlanSwitcherAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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

        public override void Dispose()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");
        }

        public override async void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Key Pressed {this.GetType()}");

            if (String.IsNullOrEmpty(settings.PowerPlan))
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, "Key Pressed but power plan is empty");
                await Connection.ShowAlert();
                return;
            }

            if (!Guid.TryParse(settings.PowerPlan, out Guid guid))
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Could not parse power plan guid {settings.PowerPlan}");
                await Connection.ShowAlert();
                return;
            }

            try
            {
                PowerPlans.SwitchPowerPlan(guid);
                await Connection.ShowOk();
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"SwitchPowerPlan Exception: {ex}");
                await Connection.ShowAlert();
            }
        }

        public override void KeyReleased(KeyPayload payload)
        {
        }

        public async override void OnTick()
        {
            var powerPlan = PowerPlans.GetActivePowerPlan();
            if (powerPlan == null)
            {
                return;
            }

            if (settings.PowerPlan == powerPlan.Guid)
            {
                await Connection.SetImageAsync(GetActivePowerImage());
            }
            else
            {
                await Connection.SetImageAsync((string) null);
            }

            if (settings.ShowActivePowerPlan)
            {
                await Connection.SetTitleAsync(FormatStringToKey(powerPlan.Name));
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

        private void InitializeSettings()
        {
            LoadAllPowerPlans();
            SaveSettings();
        }

        private void LoadAllPowerPlans()
        {
            settings.PowerPlans = PowerPlans.GetAll().ToList();
        }

        private string FormatStringToKey(string str)
        {
            // Split to 3 lines
            for (int idx = 1; idx <= 2; idx++)
            {
                int cutSize = STRING_SPLIT_SIZE * idx;
                if (str.Length > cutSize)
                {
                    str = $"{str.Substring(0, cutSize)}\n{str.Substring(cutSize)}";
                }
            }
            return str;
        }

        private Image GetActivePowerImage()
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