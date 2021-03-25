using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinTools.Wrappers
{
    class DeviceEndpoint
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; private set; }

        public DeviceEndpoint(string deviceName)
        {
            Name = deviceName;
        }
    }
}
