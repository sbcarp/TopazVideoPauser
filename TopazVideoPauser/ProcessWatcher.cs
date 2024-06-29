using System.Diagnostics;
using System.Management;

namespace TopazVideoPauser
{
    internal class ProcessWatcher : IDisposable
    {
        public event Action<int, Process?>? OnProcessSpawned;
        public event Action<int, Process?>? OnProcessExited;

        private readonly List<string> processNames;
        private readonly ManagementEventWatcher eventWatcher;

        public ProcessWatcher(IEnumerable<string> processNames)
        {
            this.processNames = processNames.ToList();
            var startQuery = "SELECT * FROM Win32_ProcessStartTrace WHERE " +
                string.Join(" OR ", this.processNames.Select(name => $"ProcessName = '{name}'"));

            eventWatcher = new ManagementEventWatcher(startQuery);
            eventWatcher.EventArrived += OnProcessStarted;
            eventWatcher.Start();
        }

        public void WatchProcess(IEnumerable<Process> processes)
        {
            foreach (var process in processes)
            {
                WatchProcess(process);
            }
        }

        public void WatchProcess(Process process)
        {
            MonitorProcessExit(process);
        }

        private void OnProcessStarted(object sender, EventArrivedEventArgs e)
        {
            var processId = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value);
            Process? process = null;
            try
            {
                process = Process.GetProcessById(processId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            OnProcessSpawned?.Invoke(processId, process);

            if (process != null)
            {
                MonitorProcessExit(process);
            }
            else
            {
                OnProcessExited?.Invoke(processId, process);
            }
        }

        private async void MonitorProcessExit(Process process)
        {
            await process.WaitForExitAsync();
            OnProcessExited?.Invoke(process.Id, process);
        }

        public void Dispose()
        {
            eventWatcher.Stop();
            eventWatcher.Dispose();
        }
    }
}
