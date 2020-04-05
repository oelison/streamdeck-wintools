using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WinTools
{
    [PluginActionId("com.barraider.wintools.latestfilecopy")]
    public class LatestFileCopyAction : PluginBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    WatchDirectory = String.Empty,
                    CopyPath = String.Empty
                };
                return instance;
            }

            [JsonProperty(PropertyName = "watchDirectory")]
            public String WatchDirectory { get; set; }

            [JsonProperty(PropertyName = "copyPath")]
            public String CopyPath { get; set; }
        }

        #region Private Members

        private readonly PluginSettings settings;
        private readonly FileSystemWatcher fsw = new FileSystemWatcher();
        private string lastChangeFileName = String.Empty;
        private DateTime lastChangedTime = DateTime.MinValue;

        #endregion
        public LatestFileCopyAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            fsw.Changed += Fsw_Changed;
            fsw.Created += Fsw_Created;
            fsw.Renamed += Fsw_Renamed;
            fsw.Error += Fsw_Error;
            InitializeSettings();
        }

        public override void Dispose()
        {
            Connection.OnSendToPlugin -= Connection_OnSendToPlugin;
            fsw.Changed -= Fsw_Changed;
            fsw.Created -= Fsw_Created;
            fsw.Renamed -= Fsw_Renamed;
            fsw.Error -= Fsw_Error;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");
        }

        public override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Pressed");
        }

        public override void KeyReleased(KeyPayload payload) { }

        public async override void OnTick()
        {
            if (!String.IsNullOrEmpty(lastChangeFileName) && lastChangedTime > DateTime.MinValue)
            {
                await Connection.SetTitleAsync($"{lastChangeFileName}\n{lastChangedTime.ToString("HH:mm:ss")}");
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
            ConfigureFileSystemWatcher();
        }

        private void ConfigureFileSystemWatcher()
        {
            fsw.EnableRaisingEvents = false;
            if (String.IsNullOrEmpty(settings.WatchDirectory) || String.IsNullOrEmpty(settings.CopyPath))
            {
                return;
            }

            if (!Directory.Exists(settings.WatchDirectory))
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Directory does not exist: {settings.WatchDirectory}");
                return;
            }

            fsw.Path = settings.WatchDirectory;
            fsw.IncludeSubdirectories = false;
            fsw.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
            fsw.EnableRaisingEvents = true;
        }


        private void Fsw_Error(object sender, ErrorEventArgs e)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"FileSystemWatcher returned an error: {e.GetException()}");
        }

        private void HandleFileChange(string fileName)
        {
            try
            {
                if (String.IsNullOrEmpty(settings.CopyPath))
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"HandleFileChange called but CopyPath is empty");
                    return;
                }

                if (!File.Exists(fileName))
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"HandleFileChange called but fileName does not exist: {fileName}");
                    return;
                }


                // Copy file to the requested directory
                File.Copy(fileName, settings.CopyPath, true);

                FileInfo fi = new FileInfo(fileName);
                lastChangedTime = DateTime.Now;
                lastChangeFileName = fi.Name;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"HandleFileChange Exception when copying from {fileName} to {settings.CopyPath}: {ex}");
            }
        }

        private void Fsw_Renamed(object sender, RenamedEventArgs e)
        {
            HandleFileChange(e.FullPath);
        }

        private void Fsw_Created(object sender, FileSystemEventArgs e)
        {
            HandleFileChange(e.FullPath);
        }

        private void Fsw_Changed(object sender, FileSystemEventArgs e)
        {
            HandleFileChange(e.FullPath);
        }

        private void Connection_OnSendToPlugin(object sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Events.SendToPlugin> e)
        {
            var payload = e.Event.Payload;

            if (payload["property_inspector"] != null)
            {
                switch (payload["property_inspector"].ToString().ToLower())
                {
                    case "loadfolderpicker":
                        string folderPropertyName = (string)payload["property_name"];
                        string folderTitle = (string)payload["picker_title"];
                        string folderName = PickersUtil.Pickers.FolderPicker(folderTitle, null);
                        if (!string.IsNullOrEmpty(folderName))
                        {
                            if (!PickersUtil.Pickers.SetJsonPropertyValue(settings, folderPropertyName, folderName))
                            {
                                Logger.Instance.LogMessage(TracingLevel.ERROR, "Failed to save picker value to settings");
                            }
                            SaveSettings();
                            InitializeSettings();
                        }
                        break;
                    case "loadsavepicker":
                        string propertyName = (string)payload["property_name"];
                        string pickerTitle = (string)payload["picker_title"];
                        string pickerFilter = (string)payload["picker_filter"];
                        string fileName = PickersUtil.Pickers.SaveFilePicker(pickerTitle, null, pickerFilter);
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            if (!PickersUtil.Pickers.SetJsonPropertyValue(settings, propertyName, fileName))
                            {
                                Logger.Instance.LogMessage(TracingLevel.ERROR, "Failed to save picker value to settings");
                            }
                            SaveSettings();
                            InitializeSettings();
                        }
                        break;
                }
            }
        }

        #endregion
    }
}