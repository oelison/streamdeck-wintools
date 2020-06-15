using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinTools.Wrappers
{
    class AudioApplication
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; private set; }

        [JsonProperty(PropertyName = "volume")]
        public float Volume { get; private set; }

        [JsonProperty(PropertyName = "isMuted")]
        public bool IsMuted { get; private set; }

        [JsonProperty(PropertyName = "processId")]
        public int ProcessId { get; private set; }

        public AudioApplication(string applicationName, bool isMuted, float volume, int processId)
        {
            Name = applicationName;
            Volume = volume;
            IsMuted = isMuted;
            ProcessId = processId;
        }
    }
}
