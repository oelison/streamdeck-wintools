using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinTools.Wrappers
{
    internal class PowerPlanInfo
    {
        [JsonProperty(PropertyName = "guid")]
        public string Guid { get; private set; }

        [JsonProperty(PropertyName = "name")]
        public string Name { get; private set; }

        public PowerPlanInfo(string name, string guid)
        {
            Name = name;
            Guid = guid;
        }
    }
}
