using System.Diagnostics;
using System.Management;

namespace TopazVideoPauser
{
	internal class ProcessWatcher : IDisposable
	{
		public event Action<int, Process?>? OnProcessSpawned;
		public event Action<int, Process?>? OnProcessExited;

		private readonly string processName;
		private readonly ManagementEventWatcher eventWatcher;

		public ProcessWatcher(string processName)
		{
			this.processName = processName;
			var startQuery = "SELECT * FROM Win32_ProcessStartTrace WHERE ProcessName = '" + this.processName + "'";
			eventWatcher = new ManagementEventWatcher(startQuery);
			eventWatcher.EventArrived += OnProcessStarted;
			eventWatcher.Start();
		}

		public void WatchProcess(IEnumerable<Process> processs)
		{
			foreach (var process in processs)
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
