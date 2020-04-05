using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinTools.Backend
{
    class GlobalSettings
    {
        [JsonProperty(PropertyName = "playSoundOnSet")]
        public bool PlaySoundOnSet { get; set; }

        [JsonProperty(PropertyName = "playbackDevice")]
        public string PlaybackDevice { get; set; }

        [JsonProperty(PropertyName = "playSoundOnSetFile")]
        public string PlaySoundOnSetFile { get; set; }
    }
}
