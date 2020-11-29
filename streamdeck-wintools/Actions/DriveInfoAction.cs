using BarRaider.SdTools;
using BarRaider.SdTools.Wrappers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    // Subscriber: Jeffkang2
    //---------------------------------------------------
    [PluginActionId("com.barraider.wintools.driveinfo")]
    public class DriveInfoAction : PluginBase
    {
        private enum DisplayMode
        {
            SingleDrive = 0,
            MultipleDrives = 1
        }
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
                    BackgroundImage = String.Empty,
                    ShowLabel = false,
                    DisplayMode = DisplayMode.SingleDrive,
                    RotationSpeed = DEFAULT_ROTATION_SPEED_SECONDS.ToString(),
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

            [JsonProperty(PropertyName = "showLabel")]
            public bool ShowLabel { get; set; }

            [JsonProperty(PropertyName = "displayMode")]
            public DisplayMode DisplayMode { get; set; }

            [JsonProperty(PropertyName = "rotationSpeed")]
            public string RotationSpeed { get; set; }

        }

        #region Private Members
        private const string DEFAULT_BACKGROUND_IMAGE = @"images\driveAction@2x.png";
        private const int DRAW_KEY_COOLDOWN_MS = 10000;
        private const string DEFAULT_LOW_COLOR = "#FFFF00";
        private const int DEFAULT_LOW_THRESHOLD = 20;
        private const string DEFAULT_CRITICAL_COLOR = "#FF0000";
        private const int DEFAULT_CRITICAL_THRESHOLD = 10;
        private const int DEFAULT_ROTATION_SPEED_SECONDS = 5;

        private readonly PluginSettings settings;
        private DriveInfo driveInfo = null;
        private TitleParameters titleParameters;
        private int lowThreshold;
        private int criticalThreshold;
        private DateTime lastKeyDraw = DateTime.MinValue;
        private Image backgroundImage = null;
        private int rotationSpeed = DEFAULT_ROTATION_SPEED_SECONDS;
        private int currentDrive = 0;
        private DateTime lastSwitchedDrive = DateTime.MinValue;
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

        public async override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Key Pressed {this.GetType()}");
            string drivePath;

            if (settings.DisplayMode == DisplayMode.SingleDrive)
            {
                if (driveInfo == null)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, "Key Pressed but driveInfo is null!");
                    await Connection.ShowAlert();
                    return;
                }
                else
                {
                    drivePath = driveInfo?.Name;
                }
            }
            else // Multiple Drives, find the current selected one
            {
                drivePath = settings.DiskDrives[currentDrive]?.Name;
            }

            await OpenFolder(drivePath);
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
            settings.DiskDrives = DriveInfo.GetDrives().Where(d => d.IsReady).Select(d => new DiskDrive(d)).ToList();
            SaveSettings();
        }

        private void InitializeSettings()
        {
            lastKeyDraw = DateTime.MinValue;
            lastSwitchedDrive = DateTime.Now;
            currentDrive = 0;
            driveInfo = null;
            bool shouldSaveSettings = false;
            if (!String.IsNullOrEmpty(settings.SelectedDrive))
            {
                driveInfo = new DriveInfo(settings.SelectedDrive);
            }

            if (!Int32.TryParse(settings.LowThreshold, out lowThreshold))
            {
                settings.LowThreshold = DEFAULT_LOW_THRESHOLD.ToString();
                shouldSaveSettings = true;
            }

            if (!Int32.TryParse(settings.CriticalThreshold, out criticalThreshold))
            {
                settings.CriticalThreshold = DEFAULT_CRITICAL_THRESHOLD.ToString();
                shouldSaveSettings = true;
            }

            if (String.IsNullOrEmpty(settings.LowColor))
            {
                settings.LowColor = DEFAULT_LOW_COLOR;
                shouldSaveSettings = true;
            }

            if (String.IsNullOrEmpty(settings.CriticalColor))
            {
                settings.CriticalColor = DEFAULT_CRITICAL_COLOR;
                shouldSaveSettings = true;
            }

            if (!Int32.TryParse(settings.RotationSpeed, out rotationSpeed))
            {
                settings.RotationSpeed = DEFAULT_ROTATION_SPEED_SECONDS.ToString();
                shouldSaveSettings = true;
            }

            if (shouldSaveSettings)
            {
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

            DriveInfo currentDriveInfo = driveInfo;
            if (titleParameters == null)
            {
                return;
            }

            if (settings.DisplayMode == DisplayMode.SingleDrive && driveInfo == null)
            {
                return;
            }
            if (settings.DisplayMode == DisplayMode.SingleDrive && (DateTime.Now - lastKeyDraw).TotalMilliseconds < DRAW_KEY_COOLDOWN_MS)
            {
                return;
            }
            else if (settings.DisplayMode == DisplayMode.MultipleDrives && (DateTime.Now - lastSwitchedDrive).TotalSeconds >= rotationSpeed)
            {
                lastSwitchedDrive = DateTime.Now;
                currentDrive = (currentDrive + 1) % settings.DiskDrives.Count;
            }
            else if (settings.DisplayMode == DisplayMode.MultipleDrives && lastKeyDraw > lastSwitchedDrive) // We drew AFTER we switched drives
            {
                return;
            }

            if (settings.DisplayMode == DisplayMode.MultipleDrives)
            {
                currentDriveInfo = new DriveInfo(settings.DiskDrives[currentDrive]?.Name);
            }

            lastKeyDraw = DateTime.Now;
            if (currentDriveInfo == null)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"DrawKey currentDriveInfo is null!");
                return;
            }
            
            double percentageFree = (int)(((double)currentDriveInfo.AvailableFreeSpace / currentDriveInfo.TotalSize) * 100);
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
                string title = currentDriveInfo.RootDirectory.ToString();
                if (settings.ShowLabel && !String.IsNullOrEmpty(currentDriveInfo.VolumeLabel))
                {
                    title = $"{currentDriveInfo.VolumeLabel} ({currentDriveInfo.Name.Substring(0, currentDriveInfo.Name.Length - 1)})";
                }
                float stringHeight = STARTING_TEXT_Y;
                float stringWidth = 0;
                float fontSize = graphics.GetFontSizeWhereTextFitsImage(title, width, fontDetails);
                using (Font fontTitleDetails = new Font(fontDetails.FontFamily, fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
                {
                    stringWidth = graphics.GetTextCenter(title, width, fontTitleDetails);
                    stringHeight = graphics.DrawAndMeasureString(title, fontTitleDetails, fgBrush, new PointF(stringWidth, stringHeight)) + TEXT_PADDING_Y;
                }

                // Percentage free
                title = $"{percentageFree}% free";
                fontSize = graphics.GetFontSizeWhereTextFitsImage(title, width, fontDetails);
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
                title = Tools.FormatBytes(currentDriveInfo.AvailableFreeSpace);
                stringWidth = graphics.GetTextCenter(title, width, fontDetails);
                stringHeight = graphics.DrawAndMeasureString(title, fontDetails, fgBrush, new PointF(stringWidth, stringHeight)) ;

                await Connection.SetImageAsync(bmp);
                graphics.Dispose();
                fontDetails.Dispose();
            }
        }

        private async Task OpenFolder(string path)
        {
            if (String.IsNullOrEmpty(path))
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, "OpenFolder: path is null!");
                await Connection.ShowAlert();
                return;
            }

            try
            {
                ProcessStartInfo start = new ProcessStartInfo
                {

                    // Enter the executable to run, including the complete path
                    FileName = path.Replace('/', '\\'),
                    WindowStyle = ProcessWindowStyle.Normal
                };

                // Launch the folder
                var p = Process.Start(start);
                await Connection.ShowOk();
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"OpenFolder exception: {ex}");
                await Connection.ShowAlert();
            }
        }

        #endregion
    }



}
