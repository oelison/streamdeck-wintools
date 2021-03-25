using System.Windows.Forms;

namespace WinTools.Wrappers
{
    public class KeyStatus
    {
        public Keys Key { get; private set; }
        public bool IsKeyLocked { get; private set; }

        public KeyStatus(Keys key, bool locked)
        {
            Key = key;
            IsKeyLocked = locked;
        }
    }
}
