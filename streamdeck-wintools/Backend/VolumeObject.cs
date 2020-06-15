using CSCore.CoreAudioAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinTools.Backend
{
    internal class VolumeObject : IDisposable
    {
        internal int PID { get; private set; }
        internal AudioSessionControl2 AudioSession { get; private set; }

        public VolumeObject(int pid, AudioSessionControl2 audioSession)
        {
            PID = pid;
            AudioSession = audioSession;
        }

        public void Dispose()
        {
            if (AudioSession != null)
            {
                AudioSession.Dispose();
            }
        }
    }
}
