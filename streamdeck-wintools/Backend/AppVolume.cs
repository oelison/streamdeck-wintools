using BarRaider.SdTools;
using CSCore.CoreAudioAPI;
using CSCore.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinTools.Wrappers;

namespace WinTools.Backend
{

    //---------------------------------------------------
    //          BarRaider's Hall Of Fame
    // Subscriber: nubby_ninja
    //---------------------------------------------------
    internal static class AppVolume
    {
        private const int VOLUME_APPLICATION_FETCH_COOLDOWN_MS = 1000;
        private static readonly Guid GUID_IAudioSessionManager2 = new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
        private static readonly Object lockGetVolumeApplicationsStatus = new object();

        private static List<AudioApplication> cachedVolumeApplicationsList = null;
        private static DateTime lastVolumeApplicationFetch = DateTime.MinValue;

        private enum ModifyVolumeType
        {
            SetVolume,
            AdjustVolume
        }


        internal static Task<List<AudioApplication>> GetVolumeApplicationsStatus()
        {
            return Task.Run<List<AudioApplication>>(() =>
            {
                if (cachedVolumeApplicationsList != null && (DateTime.Now - lastVolumeApplicationFetch).TotalMilliseconds < VOLUME_APPLICATION_FETCH_COOLDOWN_MS)
                {
                    //Logger.Instance.LogMessage(TracingLevel.DEBUG, "GetVolumeApplicationsStatus: Using cached VolumeApplications list");
                    return cachedVolumeApplicationsList;
                }

                lock (lockGetVolumeApplicationsStatus)
                {
                    if (cachedVolumeApplicationsList != null && (DateTime.Now - lastVolumeApplicationFetch).TotalMilliseconds < VOLUME_APPLICATION_FETCH_COOLDOWN_MS)
                    {
                        //Logger.Instance.LogMessage(TracingLevel.DEBUG, "GetVolumeApplicationsStatus: Using cooldown cached VolumeApplications list");
                        return cachedVolumeApplicationsList;
                    }

                    List<AudioApplication> applications = new List<AudioApplication>();
                    var volumeObjects = GetAllVolumeObjects();
                    var dictVolumeObjects = volumeObjects.ToLookup(x => x.PID);
                    foreach (var lookup in dictVolumeObjects)
                    {
                        if (lookup.Key == 0) // Ignore the PID: 0 idle process;
                        {
                            continue;
                        }
                        var process = Process.GetProcessById(lookup.Key);
                        if (process != null)
                        {
                            var obj = lookup.First();
                            applications.Add(new AudioApplication(process.ProcessName, GetIsMuted(obj?.AudioSession), GetVolume(obj?.AudioSession), lookup.Key));
                        }
                        else
                        {
                            Logger.Instance.LogMessage(TracingLevel.WARN, $"GetVolumeApplicationsStatus: Could not match process to PID {lookup.Key}");
                        }
                    }

                    // Cleanup Volume Objects
                    DisposeVolumeObjects(volumeObjects);

                    cachedVolumeApplicationsList = applications.OrderBy(app => app.Name).ToList();
                    //Logger.Instance.LogMessage(TracingLevel.DEBUG, $"GetVolumeApplicationsStatus: Retrieved {cachedVolumeApplicationsList.Count} apps");
                    lastVolumeApplicationFetch = DateTime.Now;
                    return cachedVolumeApplicationsList;
                }
            });
        }


        internal static Task<bool> AdjustAppVolume(string applicationName, int volumeStep)
        {
            return Task.Run(() =>
            {
                return ModifyAppVolume(applicationName, ModifyVolumeType.AdjustVolume, volumeStep);
            });
        }

        internal static Task<bool> SetAppVolume(string applicationName, int volumeStep)
        {
            return Task.Run(() =>
            {
                return ModifyAppVolume(applicationName, ModifyVolumeType.SetVolume, volumeStep);
            });
        }

        internal static Task<bool> ToggleAppMute(string applicationName)
        {
            return Task.Run(() =>
            {
                return AppMuteToggle(applicationName);
            });
        }

        internal static Task<bool> SetAppMute(string applicationName, bool shouldMute)
        {
            return Task.Run(() =>
            {
                return ModifyAppMute(applicationName, shouldMute);
            });
        }

        private static List<VolumeObject> GetAllVolumeObjects()
        {
            List<VolumeObject> volumeObjects = new List<VolumeObject>();
            // get the speakers (1st render + multimedia) device
            using (MMDeviceEnumerator deviceEnumerator = new MMDeviceEnumerator())
            {

                // Find all the different "devices"/endpoints on PC
                using (var endpoints = deviceEnumerator.EnumAudioEndpoints(DataFlow.Render, DeviceState.Active))
                {
                    foreach (var endpoint in endpoints)
                    {
                        using (AudioSessionManager2 mgr = new AudioSessionManager2(endpoint.Activate(GUID_IAudioSessionManager2, CLSCTX.CLSCTX_ALL, IntPtr.Zero)))
                        {
                            using (var sessionEnumerator = mgr.GetSessionEnumerator())
                            {
                                foreach (var session in sessionEnumerator)
                                {
                                    // Note: This means audioSession must be disposed of
                                    AudioSessionControl2 audioSession = session.QueryInterface<AudioSessionControl2>();
                                    volumeObjects.Add(new VolumeObject(audioSession.ProcessID, audioSession));
                                }
                            }
                        }
                    }
                }
            }

            return volumeObjects;
        }

        private static bool ModifyAppVolume(string applicationName, ModifyVolumeType modifyType, int modifyLevel)
        {
            bool foundApplication = false;
            try
            {
                var volumeObjects = GetAllVolumeObjects();
                float volumeModifyLevelPercentage = (float)modifyLevel / 100f;

                // Create a dictionary out of the volumeObjects 
                var dictVolumeObjects = volumeObjects.ToLookup(x => x.PID);

                // Find the volume object that matches the name of the application we want to modify the volume for
                var appProcesses = Process.GetProcessesByName(applicationName);
                foreach (var proc in appProcesses)
                {
                    if (proc == null)
                    {
                        continue;
                    }

                    // Does this process have an associated volumeObject?
                    if (!dictVolumeObjects.Contains(proc.Id))
                    {
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"Could not find volume object for {applicationName} PID {proc.Id}");
                        continue;
                    }

                    foreach (var volumeObject in dictVolumeObjects[proc.Id])
                    {
                        foundApplication = true;

                        // Unmute the app if it's currently muted
                        SetIsMuted(volumeObject?.AudioSession, false);
                        var currentVolume = GetVolume(volumeObject?.AudioSession);

                        switch (modifyType)
                        {
                            case ModifyVolumeType.AdjustVolume:
                                Logger.Instance.LogMessage(TracingLevel.INFO, $"ModifyAppVolume modifying volume from {currentVolume} by {volumeModifyLevelPercentage} for Process {proc.ProcessName} PID {proc.Id}");
                                currentVolume += volumeModifyLevelPercentage;
                                break;
                            case ModifyVolumeType.SetVolume:
                                Logger.Instance.LogMessage(TracingLevel.INFO, $"ModifyAppVolume setting volume from {currentVolume} to {volumeModifyLevelPercentage} for Process {proc.ProcessName} PID {proc.Id}");
                                currentVolume = volumeModifyLevelPercentage;
                                break;
                            default:
                                Logger.Instance.LogMessage(TracingLevel.ERROR, $"ModifyAppVolume received invalid ModifyVolumeType!");
                                return false;
                        }

                        // Make sure we don't go out of bounds
                        if (currentVolume < 0)
                        {
                            currentVolume = 0;
                        }
                        else if (currentVolume > 1)
                        {
                            currentVolume = 1;
                        }
                        SetVolume(volumeObject?.AudioSession, currentVolume);
                    }
                }

                // Cleanup Volume Objects
                DisposeVolumeObjects(volumeObjects);

                return foundApplication;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"AdjustAppVolume exception for {applicationName}: {ex}");
                return false;
            }
        }

        private static bool AppMuteToggle(string applicationName)
        {
            bool foundApplication = false;
            try
            {
                var volumeObjects = GetAllVolumeObjects();

                // Create a dictionary out of the volumeObjects 
                var dictVolumeObjects = volumeObjects.ToLookup(x => x.PID);

                // Find the volume object that matches the name of the application we want to modify the volume for
                var appProcesses = Process.GetProcessesByName(applicationName);
                foreach (var proc in appProcesses)
                {
                    if (proc == null)
                    {
                        continue;
                    }

                    // Does this process have an associated volumeObject?
                    if (!dictVolumeObjects.Contains(proc.Id))
                    {
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"Could not find volume object for {applicationName} PID {proc.Id}");
                        continue;
                    }

                    // Try and get the current mute state of this app
                    bool isMuted = false;
                    var currentVolumeObject = dictVolumeObjects[proc.Id].FirstOrDefault();
                    if (currentVolumeObject != null)
                    {
                        isMuted = GetIsMuted(currentVolumeObject?.AudioSession);
                    }

                    foundApplication = true;
                    ModifyAppMute(applicationName, !isMuted);
                    break;
                }

                // Cleanup Volume Objects
                DisposeVolumeObjects(volumeObjects);

                return foundApplication;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"ToggleAppMute exception for {applicationName}: {ex}");
                return false;
            }
        }

        private static bool ModifyAppMute(string applicationName, bool shouldMute)
        {
            bool foundApplication = false;
            try
            {
                var volumeObjects = GetAllVolumeObjects();

                // Create a dictionary out of the volumeObjects 
                var dictVolumeObjects = volumeObjects.ToLookup(x => x.PID);

                // Find the volume object that matches the name of the application we want to modify the volume for
                var appProcesses = Process.GetProcessesByName(applicationName);
                foreach (var proc in appProcesses)
                {
                    if (proc == null)
                    {
                        continue;
                    }

                    // Does this process have an associated volumeObject?
                    if (!dictVolumeObjects.Contains(proc.Id))
                    {
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"Could not find volume object for {applicationName} PID {proc.Id}");
                        continue;
                    }

                    foreach (var volumeObject in dictVolumeObjects[proc.Id])
                    {
                        foundApplication = true;

                        Logger.Instance.LogMessage(TracingLevel.INFO, $"ModifyAppMute modifying mute status to {shouldMute} for Process {proc.ProcessName} PID {proc.Id}");
                        if (!SetIsMuted(volumeObject?.AudioSession, shouldMute))
                        {
                            Logger.Instance.LogMessage(TracingLevel.ERROR, $"ModifyAppMute failed to modify mute status to {shouldMute} for Process {proc.ProcessName} PID {proc.Id}");
                        }
                    }
                }

                // Cleanup Volume Objects
                DisposeVolumeObjects(volumeObjects);

                return foundApplication;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"ModifyAppMute exception for {applicationName}: {ex}");
                return false;
            }
        }


        private static float GetVolume(AudioSessionControl2 audioSession)
        {
            try
            {
                if (audioSession == null)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, "GetVolume failed - audioSession is null");
                    return -1;
                }

                var volumeObj = audioSession.QueryInterface<SimpleAudioVolume>();
                if (volumeObj == null)
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"QueryInterface for GetVolume failed for {audioSession?.DisplayName}");
                    return -1;
                }

                return volumeObj.MasterVolume;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"GetVolume exception {ex}");
                return -1;
            }
        }

        private static bool SetVolume(AudioSessionControl2 audioSession, float volume)
        {
            try
            {
                if (audioSession == null)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, "SetVolume failed - audioSession is null");
                    return false;
                }

                var volumeObj = audioSession.QueryInterface<SimpleAudioVolume>();
                if (volumeObj == null)
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"QueryInterface for SetVolume failed for {audioSession?.DisplayName}");
                    return false;
                }

                volumeObj.MasterVolume = volume;
                return true;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"SetVolume exception {ex}");
                return false;
            }
        }

        private static bool GetIsMuted(AudioSessionControl2 audioSession)
        {
            try
            {
                if (audioSession == null)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, "IsMuted failed - audioSession is null");
                    return false;
                }

                var volumeObj = audioSession.QueryInterface<SimpleAudioVolume>();
                if (volumeObj == null)
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"QueryInterface for IsMuted failed for {audioSession?.DisplayName}");
                    return false;
                }

                return volumeObj.IsMuted;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"IsMuted exception {ex}");
                return false;
            }
        }

        private static bool SetIsMuted(AudioSessionControl2 audioSession, bool shouldMute)
        {
            try
            {
                if (audioSession == null)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, "IsMuted failed - audioSession is null");
                    return false;
                }

                var volumeObj = audioSession.QueryInterface<SimpleAudioVolume>();
                if (volumeObj == null)
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"QueryInterface for IsMuted failed for {audioSession?.DisplayName}");
                    return false;
                }

                volumeObj.IsMuted = shouldMute;
                return true;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"IsMuted exception {ex}");
                return false;
            }
        }






        private static void DisposeVolumeObjects(List<VolumeObject> volumeObjects)
        {
            foreach (var volumeObject in volumeObjects)
            {
                if (volumeObject != null)
                {
                    volumeObject.Dispose();
                }
            }
        }
    }
}
