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
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using WinTools.Backend;
using WinTools.Wrappers;

namespace WinTools
{
    [PluginActionId("com.barraider.wintools.notification")]
    public class NotificationToastAction : PluginBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    Title = String.Empty,
                    MessageFromFile = false,
                    MessageText = String.Empty,
                    MessageFile = null,
                    ImageFile = null
                };
                return instance;
            }

            [JsonProperty(PropertyName = "title")]
            public string Title { get; set; }

            [JsonProperty(PropertyName = "messageText")]
            public string MessageText { get; set; }

            [JsonProperty(PropertyName = "messageFromFile")]
            public bool MessageFromFile { get; set; }

            [FilenameProperty]
            [JsonProperty(PropertyName = "messageFile")]
            public string MessageFile { get; set; }

            [FilenameProperty]
            [JsonProperty(PropertyName = "imageFile")]
            public string ImageFile { get; set; }
        }

        #region Private Members
        private const string DEFAULT_IMAGE_FILENAME = @"images\brlogo.png";
        private readonly PluginSettings settings;

        #endregion
        public NotificationToastAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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

        public async override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Key Pressed {this.GetType()}");

            if (settings.MessageFromFile)
            {
                if (String.IsNullOrEmpty(settings.MessageFile))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} 'Message from file' selected but filename is empty!");
                    await Connection.ShowAlert();
                    return;
                }
                if (!File.Exists(settings.MessageFile))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} 'Message from file' selected but file not found {settings.MessageFile}!");
                    await Connection.ShowAlert();
                    return;
                }
            }

            if (!String.IsNullOrEmpty(settings.ImageFile) && !File.Exists(settings.ImageFile))
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} Image file not found {settings.ImageFile}!");
                await Connection.ShowAlert();
                return;
            }

            if (ShowNotification())
            {
                await Connection.ShowOk();
            }
            else
            {
                await Connection.ShowAlert();
            }
        }

        public override void KeyReleased(KeyPayload payload)
        {
        }

        public override void OnTick() { }

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
        }

        private string GetMessageText()
        {
            if (!settings.MessageFromFile)
            {
                return settings.MessageText;
            }

            if (String.IsNullOrEmpty(settings.MessageFile))
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} Message from file selected but filename is empty!");
                return String.Empty;
            }
            if (!File.Exists(settings.MessageFile))
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} Message from file selected but filename is empty!");
                return String.Empty;
            }

            return File.ReadAllText(settings.MessageFile);
        }

        private bool ShowNotification()
        {
            try
            {
                string title = settings.Title;
                string message = GetMessageText();

                string assetsImageFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DEFAULT_IMAGE_FILENAME);
                if (!String.IsNullOrEmpty(settings.ImageFile))
                {
                    assetsImageFileName = settings.ImageFile;
                }

                // 1. create element
                ToastTemplateType toastTemplate = ToastTemplateType.ToastImageAndText02;
                XmlDocument toastXml = ToastNotificationManager.GetTemplateContent(toastTemplate);

                // 2. provide text
                XmlNodeList toastTextElements = toastXml.GetElementsByTagName("text");
                toastTextElements[0].AppendChild(toastXml.CreateTextNode(title));
                toastTextElements[1].AppendChild(toastXml.CreateTextNode(message));

                // 3. provide image
                XmlNodeList toastImageAttributes = toastXml.GetElementsByTagName("image");
                ((XmlElement)toastImageAttributes[0]).SetAttribute("src", assetsImageFileName);
                ((XmlElement)toastImageAttributes[0]).SetAttribute("alt", "logo");

                // Send toast
                ToastNotification toast = new ToastNotification(toastXml);
                ToastNotificationManager.CreateToastNotifier("Win Tools by BarRaider").Show(toast);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} Exception: ShowNotification{ex}");
            }
            return false;
        }
        #endregion
    }
}