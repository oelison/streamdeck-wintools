using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinTools.Wrappers
{
    internal class MixerSettings
    {
        public int VolumeStep { get; private set; }

        public bool ShowName { get; private set; }

        public bool ShowVolume { get; private set; }

        public MixerSettings(int volumeStep, bool showName, bool showVolume)
        {
            VolumeStep = volumeStep;
            ShowName = showName;
            ShowVolume = showVolume;
        }
    }
}
