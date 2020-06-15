using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinTools.Backend
{
    internal class CacheManager
    {
        #region Private Members
        private static CacheManager instance = null;
        private static readonly object objLock = new object();

        private readonly Dictionary<string, string> dictValues = new Dictionary<string, string>();

        #endregion

        #region Constructors

        public static CacheManager Instance
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
                        instance = new CacheManager();
                    }
                    return instance;
                }
            }
        }

        private CacheManager()
        {
        }

        #endregion

        #region Public Methods

        public string GetValue(string key)
        {
            if (!dictValues.ContainsKey(key))
            {
                return null;
            }

            return dictValues[key];
        }

        public void SetValue(string key, string value)
        {
            dictValues[key] = value;
        }

        public void ClearCache()
        {
            dictValues.Clear();
        }

        #endregion
    }
}
