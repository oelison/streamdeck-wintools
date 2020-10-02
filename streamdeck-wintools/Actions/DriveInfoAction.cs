using BarRaider.SdTools;
using BarRaider.SdTools.Wrappers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls.WebParts;
using WinTools.Wrappers;

namespace WinTools.Actions
{
    //---------------------------------------------------
    //          BarRaider's Hall Of Fame
    // Subscriber: CarstenPet
    // Subscriber: HateYoFace
    // Subscriber: Tek_Soup
    // Subscriber: KTremain
    // Subscriber: Craigs_Cave
    //---------------------------------------------------
    [PluginActionId("com.barraider.wintools.driveinfo")]
    public class DriveInfoAction : PluginBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    DiskDrives = null,
                    SelectedDrive = String.Empty,
                    LowColor = DEFAULT_LOW_COLOR,
                    LowThreshold = DEFAULT_LOW_THRESHOLD.ToString(),
                    CriticalColor = DEFAULT_CRITICAL_COLOR,
                    CriticalThreshold = DEFAULT_CRITICAL_THRESHOLD.ToString(),
                    BackgroundImage = String.Empty
                };
                return instance;
            }

            [JsonProperty(PropertyName = "diskDrives")]
            public List<DiskDrive> DiskDrives { get; set; }

            [JsonProperty(PropertyName = "selectedDrive")]
            public String SelectedDrive { get; set; }

            [JsonProperty(PropertyName = "lowColor")]
            public String LowColor { get; set; }

            [JsonProperty(PropertyName = "lowThreshold")]
            public String LowThreshold { get; set; }

            [JsonProperty(PropertyName = "criticalColor")]
            public String CriticalColor { get; set; }

            [JsonProperty(PropertyName = "criticalThreshold")]
            public String CriticalThreshold { get; set; }

            [FilenameProperty]
            [JsonProperty(PropertyName = "backgroundImage")]
            public String BackgroundImage { get; set; }
        }

        #region Private Members
        private const string DEFAULT_BACKGROUND_IMAGE = @"images\driveAction@2x.png";
        private const int DRAW_KEY_COOLDOWN_MS = 10000;
        private const string DEFAULT_LOW_COLOR = "#FFFF00";
        private const int DEFAULT_LOW_THRESHOLD = 20;
        private const string DEFAULT_CRITICAL_COLOR = "#FF0000";
        private const int DEFAULT_CRITICAL_THRESHOLD = 10;

        private readonly PluginSettings settings;
        private DriveInfo driveInfo = null;
        private TitleParameters titleParameters;
        private int lowThreshold;
        private int criticalThreshold;
        private DateTime lastKeyDraw = DateTime.MinValue;
        private Image backgroundImage = null;
        #endregion

        public DriveInfoAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            FetchDiskDrives();
        }

        #region Public Methods

        public override void Dispose()
        {
            Connection.OnTitleParametersDidChange -= Connection_OnTitleParametersDidChange;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");
        }

        public override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Key Pressed {this.GetType()}");
        }

        public override void KeyReleased(KeyPayload payload) { }

        public async override void OnTick()
        {
            await DrawKey();
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

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        private void FetchDiskDrives()
        {
            settings.DiskDrives = DriveInfo.GetDrives().Select(d => new DiskDrive(d)).ToList();
            SaveSettings();
        }

        private void InitializeSettings()
        {
            lastKeyDraw = DateTime.MinValue;
            driveInfo = null;
            if (!String.IsNullOrEmpty(settings.SelectedDrive))
            {
                driveInfo = new DriveInfo(settings.SelectedDrive);
            }

            if (!Int32.TryParse(settings.LowThreshold, out lowThreshold))
            {
                settings.LowThreshold = DEFAULT_LOW_THRESHOLD.ToString();
                SaveSettings();
            }

            if (!Int32.TryParse(settings.CriticalThreshold, out criticalThreshold))
            {
                settings.CriticalThreshold = DEFAULT_CRITICAL_THRESHOLD.ToString();
                SaveSettings();
            }

            if (String.IsNullOrEmpty(settings.LowColor))
            {
                settings.LowColor = DEFAULT_LOW_COLOR;
                SaveSettings();
            }

            if (String.IsNullOrEmpty(settings.CriticalColor))
            {
                settings.CriticalColor = DEFAULT_CRITICAL_COLOR;
                SaveSettings();
            }

            PrefetchBackgroundImage();
        }

        private void Connection_OnTitleParametersDidChange(object sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Events.TitleParametersDidChange> e)
        {
            titleParameters = e?.Event?.Payload?.TitleParameters;
            lastKeyDraw = DateTime.MinValue;
        }

        private void PrefetchBackgroundImage()
        {
            if (backgroundImage != null)
            {
                backgroundImage.Dispose();
                backgroundImage = null;
            }

            if (!String.IsNullOrEmpty(settings.BackgroundImage))
            {
                if (!File.Exists(settings.BackgroundImage))
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"Background image file not found {settings.BackgroundImage}");
                }
                else
                {
                    backgroundImage = Image.FromFile(settings.BackgroundImage);
                }
            }
            else // No user defined background image, use ours
            {
                backgroundImage = Image.FromFile(DEFAULT_BACKGROUND_IMAGE);
            }
        }

        private async Task DrawKey()
        {
            const int STARTING_TEXT_Y = 5;
            const int TEXT_PADDING_Y = 5;
            const int BAR_STARTING_X = 21;
            const int BAR_HEIGHT = 12;
            const int BAR_WIDTH = 102; // 1 Pixel on each side for the border
            const string BAR_FILL_COLOR = "#26a0da";
            if (driveInfo == null)
            {
                return;
            }

            if (titleParameters == null)
            {
                return;
            }

            if ((DateTime.Now - lastKeyDraw).TotalMilliseconds < DRAW_KEY_COOLDOWN_MS)
            {
                return;
            }

            lastKeyDraw = DateTime.Now;
            double percentageFree = (int)(((double)driveInfo.AvailableFreeSpace / driveInfo.TotalSize) * 100);
            using (Bitmap bmp = Tools.GenerateGenericKeyImage(out Graphics graphics))
            {
                int height = bmp.Height;
                int width = bmp.Width;

                // Draw background image
                if (backgroundImage != null)
                {
                    graphics.DrawImage(backgroundImage, 0, 0, width, height);
                }

                Color textColor = titleParameters.TitleColor;
                if (percentageFree <= criticalThreshold)
                {
                    textColor = ColorTranslator.FromHtml(settings.CriticalColor);
                }
                else if (percentageFree <= lowThreshold)
                {
                    textColor = ColorTranslator.FromHtml(settings.LowColor);
                }

                Font fontDetails = new Font(titleParameters.FontFamily, 24, FontStyle.Bold, GraphicsUnit.Pixel);
                SolidBrush fgBrush = new SolidBrush(textColor);

                // Drive Letter
                string title = driveInfo.RootDirectory.ToString();
                float stringHeight = STARTING_TEXT_Y;
                float stringWidth = graphics.GetTextCenter(title, width, fontDetails);
                stringHeight = graphics.DrawAndMeasureString(title, fontDetails, fgBrush, new PointF(stringWidth, stringHeight)) + TEXT_PADDING_Y;

                // Percentage free
                title = $"{percentageFree}% free";
                float fontSize = graphics.GetFontSizeWhereTextFitsImage(title, width, fontDetails);
                using (Font fontPercentage = new Font(titleParameters.FontFamily, fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
                {
                    stringWidth = graphics.GetTextCenter(title, width, fontPercentage);
                    stringHeight = graphics.DrawAndMeasureString(title, fontPercentage, fgBrush, new PointF(stringWidth, stringHeight)) + TEXT_PADDING_Y;
                }

                // Free space bar
                graphics.DrawRectangle(new Pen(Color.White), new Rectangle(BAR_STARTING_X, (int)stringHeight, BAR_WIDTH, BAR_HEIGHT));
                graphics.FillRectangle(new SolidBrush(ColorTranslator.FromHtml(BAR_FILL_COLOR)), new Rectangle(BAR_STARTING_X + 1, (int)stringHeight + 1, (int)percentageFree, BAR_HEIGHT - 2));
                stringHeight += BAR_HEIGHT + TEXT_PADDING_Y;

                // Free space left
                title = Tools.FormatBytes(driveInfo.AvailableFreeSpace);
                stringWidth = graphics.GetTextCenter(title, width, fontDetails);
                stringHeight = graphics.DrawAndMeasureString(title, fontDetails, fgBrush, new PointF(stringWidth, stringHeight)) ;

                await Connection.SetImageAsync(bmp);
                graphics.Dispose();
                fontDetails.Dispose();
            }
        }

        #endregion
    }



}
