using BarRaider.SdTools;
using BarRaiderAudio;
using BarRaiderAudio.Wrappers;
using FontAwesome.Sharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinTools.Wrappers;

namespace WinTools.Backend
{

    //---------------------------------------------------
    //          BarRaider's Hall Of Fame
    // 1000 Bits: nubby_ninja
    //---------------------------------------------------
    class AudioMixerManager : IUIHandler
    {
        #region Private Members
        private readonly KeyCoordinates EXIT_KEY_LOCATION = new KeyCoordinates() { Row = 0, Column = 0 };
        private readonly KeyCoordinates NEXT_KEY_LOCATION = new KeyCoordinates() { Row = 1, Column = 0 };
        private readonly KeyCoordinates PREV_KEY_LOCATION = new KeyCoordinates() { Row = 2, Column = 0 };
        private const int PLUS_KEY_ROW = 0;
        private const int APP_KEY_ROW = 1;
        private const int MINUS_KEY_ROW = 2;
        private const int ACTION_KEY_COLUMN_START = 1;


        private static AudioMixerManager instance = null;
        private static readonly object objLock = new object();

        private ISDConnection connection = null;
        private StreamDeckDeviceInfo streamDeckDeviceInfo = null;
        private int currentPage = 0;
        private int appsPerPage = 0;
        private List<AudioApplication> audioApps = null;
        private MixerSettings mixerSettings;
        private readonly System.Timers.Timer tmrRefreshVolume;

        private const int NEXT_IMAGE = 0;
        private const int PREV_IMAGE = 1;
        private const int MINUS_IMAGE = 2;
        private const int PLUS_IMAGE = 3;
        private const int EXIT_IMAGE = 4;
        private readonly string[] imageFiles = { @"images\page_next.png", @"images\page_previous.png", @"images\volume_decrease.png", @"images\volume_increase.png", @"images\exit.png"};
        private readonly Image[] prefectchedImages = null;

        #endregion

        #region Constructors

        public static AudioMixerManager Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

                lock (objLock)
                {
                    if (instance == null)
                    {
                        instance = new AudioMixerManager();
                    }
                    return instance;
                }
            }
        }

        private AudioMixerManager()
        {
            try
            {
                tmrRefreshVolume = new System.Timers.Timer
                {
                    Interval = 3000
                };
                tmrRefreshVolume.Elapsed += TmrRefreshVolume_Elapsed;

                // Prefetch images
                prefectchedImages = new Image[imageFiles.Length];
                for (int currIndex = 0; currIndex < imageFiles.Length; currIndex++)
                {
                    if (!File.Exists(imageFiles[currIndex]))
                    {
                        Logger.Instance.LogMessage(TracingLevel.ERROR, $"AudioMixerManager: Prefetch image does not exist: {imageFiles[currIndex]}");
                        continue;
                    }

                    prefectchedImages[currIndex] = Image.FromFile(imageFiles[currIndex]);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"AudioMixerManager constructor exception: {ex}");
            }
        }

        #endregion

        #region Public Methods

        public async Task<bool> ShowMixer(ISDConnection connection, MixerSettings mixerSettings)
        {
            this.connection = connection;
            this.mixerSettings = mixerSettings;
            if (connection == null)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"AudioMixerManager ShowMixer called with null connection");
                return false;
            }

            streamDeckDeviceInfo = connection.DeviceInfo();
            int keys = streamDeckDeviceInfo.Size.Cols * streamDeckDeviceInfo.Size.Rows;
            currentPage = 0;
            appsPerPage = streamDeckDeviceInfo.Size.Cols - ACTION_KEY_COLUMN_START;
            if (!UIManager.Instance.RegisterUIHandler(this, keys))
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"AudioMixerManager RegisterGameHandler failed");
                return false;
            }

            await FetchAudioApplications();

            // Wait until the GameUI Action keys have subscribed to get events
            int retries = 0;
            while (!UIManager.Instance.IsUIReady && retries < 100)
            {
                Thread.Sleep(100);
                retries++;
            }
            if (!UIManager.Instance.IsUIReady)
            {
                return false;
            }

            // Generate game board
            GenerateMixer();
            tmrRefreshVolume.Start();

            return true;
        }
        private void GenerateMixer()
        {
            InitializeKeys();
            DrawExitKey();
            DrawAppsRow();
        }

        public void ProcessLongKeyPressed(KeyCoordinates coordinates)
        {
            ProcessKeyPressed(coordinates);
        }

        public async void ProcessKeyPressed(KeyCoordinates coordinates)
        {
            // Exit button pressed
            if (coordinates.IsCoordinatesSame(EXIT_KEY_LOCATION))
            {
                tmrRefreshVolume.Stop();
                await connection.SwitchProfileAsync(null);
                return;
            }

            // Next Button pressed
            if (coordinates.IsCoordinatesSame(NEXT_KEY_LOCATION))
            {
                if ((currentPage + 1) * appsPerPage < audioApps.Count) // Are there more apps than the ones we are currently showing
                {
                    currentPage++;
                    GenerateMixer();
                }
                return;
            }

            // Prev Button pressed
            if (coordinates.IsCoordinatesSame(PREV_KEY_LOCATION))
            {
                currentPage--;
                if (currentPage < 0)
                {
                    currentPage = 0;
                }
                GenerateMixer();
                return;
            }

            // Plus/Minus button pressed
            if (coordinates.Row == PLUS_KEY_ROW || coordinates.Row == MINUS_KEY_ROW)
            {
                await HandleVolumeChange(coordinates);
                await HandleAppRowChange();
            }

            // App button pressed (mute/unmute)
            if (coordinates.Row == APP_KEY_ROW)
            {
                await HandleMuteChange(coordinates);
                await HandleAppRowChange();
            }

        }

        #endregion

        #region Private Methods

        private void InitializeKeys()
        {
            UIManager.Instance.ClearAllKeys();
        }

        private void DrawExitKey()
        {
            var action = new UIActionSettings()
            {
                Coordinates = EXIT_KEY_LOCATION,
                Action = UIActions.DrawImage,
                Image = prefectchedImages[EXIT_IMAGE],
                BackgroundColor = Color.Black
            };
            UIManager.Instance.SendUIAction(action);
        }

        private void DrawPlusRow(int numOfCols)
        {
            List<UIActionSettings> actions = new List<UIActionSettings>();
            numOfCols = Math.Min(numOfCols, streamDeckDeviceInfo.Size.Cols);
            for (int column = ACTION_KEY_COLUMN_START; column < numOfCols; column++)
            {
                actions.Add(new UIActionSettings() { Coordinates = new KeyCoordinates() { Row = PLUS_KEY_ROW, Column = column }, Action = UIActions.DrawImage, Image = prefectchedImages[PLUS_IMAGE], BackgroundColor = Color.Black });
            }
            UIManager.Instance.SendUIActions(actions.ToArray());
        }

        private void DrawMinusRow(int numOfCols)
        {
            List<UIActionSettings> actions = new List<UIActionSettings>();
            numOfCols = Math.Min(numOfCols, streamDeckDeviceInfo.Size.Cols);
            for (int column = ACTION_KEY_COLUMN_START; column < numOfCols; column++)
            {
                actions.Add(new UIActionSettings() { Coordinates = new KeyCoordinates() { Row = MINUS_KEY_ROW, Column = column }, Action = UIActions.DrawImage, Image = prefectchedImages[MINUS_IMAGE], BackgroundColor = Color.Black });
            }
            UIManager.Instance.SendUIActions(actions.ToArray());
        }

        private void DrawNextKey()
        {
            var action = new UIActionSettings() { Coordinates = NEXT_KEY_LOCATION, Action = UIActions.DrawImage, Image = prefectchedImages[NEXT_IMAGE], BackgroundColor = Color.Black };
            UIManager.Instance.SendUIAction(action);
        }

        private void DrawPrevKey()
        {
            var action = new UIActionSettings() { Coordinates = PREV_KEY_LOCATION, Action = UIActions.DrawImage, Image = prefectchedImages[PREV_IMAGE], BackgroundColor = Color.Black };
            UIManager.Instance.SendUIAction(action);
        }

        private async Task FetchAudioApplications()
        {
            audioApps = await BRAudio.GetVolumeApplications();
        }

        private void DrawAppsRow()
        {
            // Draw the relevant list of apps, based on which page we are on
            List<UIActionSettings> actions = new List<UIActionSettings>();
            int startingApp = currentPage * appsPerPage;
            int endingApp = Math.Min(startingApp + appsPerPage, audioApps.Count);
            int currentColumn = ACTION_KEY_COLUMN_START;

            // Create actions to show the name of the current list of apps
            for (int currentApp = startingApp; currentApp < endingApp; currentApp++)
            {

                Image image = FetchProcessImage(audioApps[currentApp].ProcessId);

                actions.Add(new UIActionSettings()
                {
                    Coordinates = new KeyCoordinates() { Row = APP_KEY_ROW, Column = currentColumn },
                    Action = UIActions.DrawImage,
                    Title = SetKeyTitle(audioApps[currentApp], image != null),
                    Image = image,
                    BackgroundColor = Color.Black,
                    FontAwesomeIcon = SetKeyIcon(audioApps[currentApp])
                });;
				
                currentColumn++;
            }
            UIManager.Instance.SendUIActions(actions.ToArray());

            // Determine if a next / prev key is needed
            if (startingApp > 0)
            {
                DrawPrevKey();
            }
            if ((currentPage + 1) * appsPerPage < audioApps.Count) // Are there more apps than the ones we are currently showing
            {
                DrawNextKey();
            }

            // Draw Plus and Minus rows based on how many apps are shown
            DrawPlusRow(endingApp - startingApp + 1);
            DrawMinusRow(endingApp - startingApp + 1);
        }

		private string SetKeyTitle(AudioApplication audioApplication, bool hasImage)
		{
            StringBuilder title = new StringBuilder();

            if (mixerSettings.ShowVolume)
            {
                title.Append($"{(int)(audioApplication.Volume * 100)}%\n");
            }

            if (!hasImage || mixerSettings.ShowName)
			{
                if (audioApplication != null)
                {
                    // Add new line if mute icon is already shown
                    title.Append(audioApplication.Name);
                }
			}

            return title.ToString();
		}

        private IconChar? SetKeyIcon(AudioApplication audioApplication)
        {
            if (audioApplication != null && audioApplication.IsMuted)
            {
                return IconChar.VolumeMute;
            }

            return null;
        }

        private async Task HandleVolumeChange(KeyCoordinates coordinates)
        {
            // Get the index of the app in the applications list
            int appIndex = (currentPage * appsPerPage) + coordinates.Column - ACTION_KEY_COLUMN_START;
            if (appIndex >= audioApps.Count)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"HandleVolumeChange: AppIndex is out of range! Index: {appIndex} Apps: {audioApps?.Count}");
                return;
            }

            int absVolumeStep = Math.Abs(mixerSettings.VolumeStep);
            if (coordinates.Row == MINUS_KEY_ROW)
            {
                absVolumeStep *= -1; // Make it DECREASE the amount
            }


            await BRAudio.AdjustAppVolume(audioApps[appIndex].Name, absVolumeStep);
        }

        private async Task HandleMuteChange(KeyCoordinates coordinates)
        {
            // Get the index of the app in the applications list
            int appIndex = (currentPage * appsPerPage) + coordinates.Column - ACTION_KEY_COLUMN_START;
            if (appIndex >= audioApps.Count)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"HandleVolumeChange: AppIndex is out of range! Index: {appIndex} Apps: {audioApps?.Count}");
                return;
            }

            await BRAudio.ToggleAppMute(audioApps[appIndex].Name);
        }

        private Image FetchProcessImage(int pid)
        {
            try
            {
                var process = Process.GetProcessById(pid);
                if (process == null)
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"Failed to fetch image for PID: {pid} - process does not exist");
                    return null;
                }
                if (process != null)
                {
                    return FetchFileImage(process?.MainModule?.FileName);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"FetchProcessImage exception for PID: {pid} {ex}");
            }
            return null;
        }

        private Image FetchFileImage(string fileName)
        {
            Image fileImage = null;
            try
            {
                if (String.IsNullOrEmpty(fileName))
                {
                    return null;
                }

                // Try to extract Icon
                if (!String.IsNullOrEmpty(fileName) && File.Exists(fileName))
                {
                    FileInfo fileInfo = new FileInfo(fileName);
                    var fileIcon = IconExtraction.Shell.OfPath(fileInfo.FullName, small: false);
                    if (fileIcon != null)
                    {
                        using (Bitmap fileIconAsBitmap = fileIcon.ToBitmap())
                        {
                            Logger.Instance.LogMessage(TracingLevel.INFO, $"Bitmap size is: {fileIconAsBitmap.Width}x{fileIconAsBitmap.Height}");
                            fileImage = Tools.GenerateGenericKeyImage(out Graphics graphics);

                            // Check if app icon is smaller than the Stream Deck key
                            if (fileIconAsBitmap.Width < fileImage.Width && fileIconAsBitmap.Height < fileImage.Height)
                            {
                                float position = Math.Min(fileIconAsBitmap.Width / 2, fileIconAsBitmap.Height / 2);
                                graphics.DrawImage(fileIconAsBitmap, position, position, fileImage.Width - position * 2, fileImage.Height - position * 2);
                            }
                            else // App icon is bigger or equals to the size of a stream deck key
                            {
                                graphics.DrawImage(fileIconAsBitmap, 0, 0, fileImage.Width, fileImage.Height);
                            }
                            graphics.Dispose();
                        }
                        fileIcon.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"FetchFileImage exception for {fileName}: {ex}");
            }
            return fileImage;
        }

        private Task HandleAppRowChange()
        {
            return Task.Run(async () =>
            {
                await Task.Delay(100);
                await FetchAudioApplications();
                DrawAppsRow();
            });
        }

        private void TmrRefreshVolume_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            HandleAppRowChange();
        }

        #endregion

    }
}
