using BarRaider.SdTools;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WinTools.Wrappers;

namespace WinTools.Backend
{

    public partial class KeyboardManager
    {
        #region Private Members

        private static KeyboardManager instance = null;
        private static readonly object objLock = new object();

        private const int LANGUAGE_CHECK_COOLDOWN_MS = 200;
        private readonly Keys[] LOCK_KEYS = new Keys[] { Keys.CapsLock, Keys.NumLock, Keys.Scroll };


        private DateTime lastLanguageInputCheck = DateTime.MinValue;
        private CultureInfo languageInput;

        #endregion

        #region Constructors

        public static KeyboardManager Instance
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
                        instance = new KeyboardManager();
                    }
                    return instance;
                }
            }
        }

        private KeyboardManager()
        {
        }

        #endregion

        #region Public Methods

        [DllImport("user32.dll")]
        static extern IntPtr GetKeyboardLayout(uint idThread);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

        public CultureInfo GetKeyboardLanguageInput()
        {
            if ((DateTime.Now - lastLanguageInputCheck).TotalMilliseconds < LANGUAGE_CHECK_COOLDOWN_MS)
            {
                return languageInput;
            }

            languageInput = null;
            try
            {
                var culture = GetKeyboardLayout(GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero));
                languageInput = new CultureInfo((short)culture.ToInt64());
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"GetKeyboardLanguageInput Exception: {ex}");
            }
            return languageInput;
        }

        public async void ChangeKeyboardInputLanguage()
        {
            try
            {
                var totalLanaguages = InputLanguage.InstalledInputLanguages.Count;
                for (int idx = 0; idx < totalLanaguages; idx++)
                {
                    if (InputLanguage.InstalledInputLanguages[idx].Culture.LCID == languageInput?.LCID)
                    {
                        CultureInfo culture = null;
                        int nextLanguage = idx;
                        do
                        {
                            nextLanguage = (idx + 1) % totalLanaguages;
                            culture = InputLanguage.InstalledInputLanguages[nextLanguage].Culture;
                        } while (culture != null && culture.LCID == InputLanguage.InstalledInputLanguages[idx].Culture.LCID && nextLanguage != idx);

                        if (culture == null)
                        {
                            Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} ChangeKeyboardInputLanguage: Could not find next language culture");
                            return;
                        }
                        
                        if (culture.LCID == InputLanguage.InstalledInputLanguages[idx].Culture.LCID)
                        {

                            Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} ChangeKeyboardInputLanguage could not find different language");
                            return;
                        }

                        (await KeyboardLayout.Load(culture)).Activate();
                        return;
                    }
                }

            }
            catch (Exception ex)
            {

                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} ChangeKeyboardInputLanguage Exception {ex}");
            }
        }

        public List<KeyStatus> GetLockKeysStatus()
        {
            List<KeyStatus> keysList = new List<KeyStatus>();

            foreach (var key in LOCK_KEYS)
            {
                keysList.Add(new KeyStatus(key, Control.IsKeyLocked(key)));
            }

            return keysList;
        }

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        public void ToggleLockKeyPress(System.Windows.Forms.Keys key)
        {
            try
            {
                const int KEYEVENTF_EXTENDEDKEY = 0x1;
                const int KEYEVENTF_KEYUP = 0x2;
                const byte CAPSLOCK = 0x14;
                const byte NUMLOCK = 0x90;
                const byte SCROLLLOCK = 0x91;

                byte lockKey;
                switch (key)
                {
                    case System.Windows.Forms.Keys.CapsLock:
                        lockKey = CAPSLOCK;
                        break;
                    case System.Windows.Forms.Keys.NumLock:
                        lockKey = NUMLOCK;
                        break;
                    case System.Windows.Forms.Keys.Scroll:
                        lockKey = SCROLLLOCK;
                        break;
                    default:
                        Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} ToggleLockKeyPress: Unsupported key {key}");
                        return;
                }

                keybd_event(lockKey, 0x45, KEYEVENTF_EXTENDEDKEY, (UIntPtr)0);
                keybd_event(lockKey, 0x45, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, (UIntPtr)0);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} ToggleLockKeyPress Exception: {ex}");
            }
        }
        #endregion

        #region Private Classes

        private static class KeyboardLayoutFlags
        {
            public const uint KLF_SETFORPROCESS = 0x00000100;
        }

        private sealed class KeyboardLayout
        {
            private const int WM_INPUTLANGCHANGEREQUEST = 0x0050;

            private readonly uint hkl;

            [DllImport("user32.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, EntryPoint = "LoadKeyboardLayout", SetLastError = true, ThrowOnUnmappableChar = false)]
            static extern uint LoadKeyboardLayout(StringBuilder pwszKLID, uint flags);

            private KeyboardLayout(CultureInfo cultureInfo)
            {
                string layoutName = cultureInfo.LCID.ToString("x8");

                var pwszKlid = new StringBuilder(layoutName);
                this.hkl = LoadKeyboardLayout(pwszKlid, KeyboardLayoutFlags.KLF_SETFORPROCESS);
            }

            private KeyboardLayout(uint hkl)
            {
                this.hkl = hkl;
            }

            public static Task<KeyboardLayout> Load(CultureInfo culture)
            {
                return Task.Run(() => new KeyboardLayout(culture));
            }

            [return: MarshalAs(UnmanagedType.Bool)]
            [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            static extern bool PostMessage(HandleRef hWnd, uint Msg, IntPtr wParam, uint lParam);
            public void Activate()
            {
                Task.Run(() => PostMessage(new HandleRef(this, GetForegroundWindow()), WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, this.hkl));
            }
        }
    }

    #endregion
}
