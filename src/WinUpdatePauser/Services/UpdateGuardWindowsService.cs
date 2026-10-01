using System;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace WinUpdatePauser.Services
{
    /// <summary>监听暂停日期注册表，并在系统重置后恢复用户设置的目标日期。</summary>
    internal sealed class UpdateGuardWindowsService : ServiceBase
    {
        private const int RegNotifyChangeLastSet = 0x00000004;
        private readonly ManualResetEvent _stopEvent = new ManualResetEvent(false);
        private readonly GuardMode _mode;
        private Thread _worker;

        public UpdateGuardWindowsService(GuardMode mode)
        {
            _mode = mode;
            ServiceName = UpdateGuardManager.GetServiceName(mode);
            CanStop = true;
            AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            _worker = new Thread(WatchLoop) { IsBackground = true, Name = "WUPauseGuard" };
            _worker.Start();
        }

        protected override void OnStop()
        {
            _stopEvent.Set();
            _worker?.Join(TimeSpan.FromSeconds(5));
        }

        private void WatchLoop()
        {
            while (!_stopEvent.WaitOne(0))
            {
                GuardConfiguration config = UpdateGuardManager.ReadConfiguration(_mode);
                if (!config.Enabled || !HasTarget(config)) return;

                try
                {
                    RestoreTarget(config);
                    using (RegistryKey key = PauseRegistryService.OpenSettingsKeyForMonitoring())
                    using (var changed = new AutoResetEvent(false))
                    {
                        if (key == null)
                        {
                            if (_stopEvent.WaitOne(TimeSpan.FromSeconds(60))) return;
                            continue;
                        }

                        int result = RegNotifyChangeKeyValue(key.Handle, false,
                            RegNotifyChangeLastSet, changed.SafeWaitHandle, true);
                        if (result != 0)
                        {
                            if (_stopEvent.WaitOne(TimeSpan.FromSeconds(60))) return;
                            continue;
                        }

                        int signaled = WaitHandle.WaitAny(
                            new WaitHandle[] { _stopEvent, changed }, TimeSpan.FromSeconds(60));
                        if (signaled == 0) return;
                        if (signaled == 1
                            && _stopEvent.WaitOne(TimeSpan.FromMilliseconds(1500))) return;
                    }
                }
                catch
                {
                    if (_stopEvent.WaitOne(TimeSpan.FromSeconds(60))) return;
                }
            }
        }

        private bool HasTarget(GuardConfiguration config)
        {
            return _mode == GuardMode.Legacy
                ? config.TargetDays.HasValue
                : !string.IsNullOrWhiteSpace(config.TargetUtc);
        }

        private void RestoreTarget(GuardConfiguration config)
        {
            if (_mode == GuardMode.Legacy)
            {
                PauseRegistryService.EnsureLegacyPauseDays(config.TargetDays.Value);
            }
            else
            {
                PauseRegistryService.EnsureEndTime(config.TargetUtc);
            }
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegNotifyChangeKeyValue(
            SafeRegistryHandle hKey,
            bool watchSubtree,
            int notifyFilter,
            SafeWaitHandle eventHandle,
            bool asynchronous);
    }
}
