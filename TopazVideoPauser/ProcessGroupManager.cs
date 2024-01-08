using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TopazVideoPauser
{
	internal enum ProcessGroupStatus
	{
		Unset,
		Unkonwn,
		Idle,
		Running,
		Suspended
	}
	internal class ProcessGroupStatusChangedEventArgs(ProcessGroupStatus status, ProcessGroupStatus previousStatus) : EventArgs
	{
		public ProcessGroupStatus Status { get; } = status;
		public ProcessGroupStatus PreviousStatus { get; } = previousStatus;
	}
	internal class ProcessGroupManager: IDisposable
	{
		private static readonly string topazProcessName = "Topaz Video AI";
		private static readonly string ffmpegProcessName = "ffmpeg";
		private static readonly string processExtensionName = ".exe";
		private readonly Dictionary<int, Process> topazProcesses = [];
		private readonly Dictionary<int, Process> ffmpegProcesses = [];
		private DateTime lastProcessActionTime = DateTime.MinValue;
		private readonly TimeSpan processActionThrottlePeriod = TimeSpan.FromSeconds(3);
		private readonly ProcessWatcher topazProcessWatcher = new (topazProcessName + processExtensionName);
		private readonly ProcessWatcher ffmpegProcessWatcher = new (ffmpegProcessName + processExtensionName);
		public ProcessGroupStatus ProcessGroupStatus { get; private set; } = ProcessGroupStatus.Unset;
		public event EventHandler<ProcessGroupStatusChangedEventArgs>? ProcessGroupStatusChanged;

		public ProcessGroupManager()
		{
			topazProcessWatcher.OnProcessSpawned += TopazProcessWatcher_OnProcessSpawned;
			topazProcessWatcher.OnProcessExited += TopazProcessWatcher_OnProcessExited;
			ffmpegProcessWatcher.OnProcessSpawned += FfmpegProcessWatcher_OnProcessSpawned;
			ffmpegProcessWatcher.OnProcessExited += FfmpegProcessWatcher_OnProcessExited;

			topazProcesses = GetProcessesByName(topazProcessName).ToDictionary(p => p.Id);
			topazProcessWatcher.WatchProcess(topazProcesses.Values);
			ffmpegProcesses = GetProcessesByName(ffmpegProcessName).Where(p =>
			{
				var parentId = p.GetParentProcessId();
				return topazProcesses.Values.Any(tp => tp.Id == parentId);
			}).ToDictionary(p => p.Id);
			ffmpegProcessWatcher.WatchProcess(ffmpegProcesses.Values);

			RefreshStatus();
		}

		private void FfmpegProcessWatcher_OnProcessSpawned(int processId, Process? process)
		{
			if (process == null) return;

			var parentProcessId = process.GetParentProcessId();
			if (topazProcesses.ContainsKey(parentProcessId))
			{
				ffmpegProcesses.Add(processId, process);
				RefreshStatus();
			}
		}

		private void FfmpegProcessWatcher_OnProcessExited(int processId, Process? process)
		{
#pragma warning disable CA1853 // Unnecessary call to 'Dictionary.ContainsKey(key)'
			if (ffmpegProcesses.ContainsKey(processId))
			{
				ffmpegProcesses.Remove(processId);
				RefreshStatus();
			}
#pragma warning restore CA1853 // Unnecessary call to 'Dictionary.ContainsKey(key)'
		}

		private void TopazProcessWatcher_OnProcessSpawned(int processId, Process? process)
		{
			if (process == null) return;

			topazProcesses.Add(processId, process);
			RefreshStatus();
		}

		private void TopazProcessWatcher_OnProcessExited(int processId, Process? process)
		{
			topazProcesses.Remove(processId);
			RefreshStatus();
		}



		public bool Suspend()
		{
			return ProcessAction(true);
		}
		public bool Resume()
		{
			return ProcessAction(false);
		}
		private bool ProcessAction(bool suspend)
		{
			try
			{
				if (DateTime.Now - lastProcessActionTime < processActionThrottlePeriod)
				{
					return false;
				}
				lastProcessActionTime = DateTime.Now;
				var allProcesses = topazProcesses.Values.Concat(ffmpegProcesses.Values);

				if (suspend ? !allProcesses.Suspend() : !allProcesses.Resume())
				{
					return false;
				}
				RefreshStatus();
				return true;
			}
			catch (Exception)
			{

				return false;
			}
			
		}
		public void RefreshStatus()
		{
			try
			{
				if (!topazProcesses.Values.AnyActive())
				{
					ChangeStatus(ProcessGroupStatus.Unkonwn);
					return;
				}

				var areTopazProcessesSuspended = topazProcesses.Values.AllSuspended();
				var isAnyFfmpegProcessActive = ffmpegProcesses.Values.AnyActive();
				var areFfmpegProcessesSuspended = ffmpegProcesses.Values.AllSuspended();

				if (!isAnyFfmpegProcessActive && !areTopazProcessesSuspended)
				{
					ChangeStatus(ProcessGroupStatus.Idle);
				}
				else if (areFfmpegProcessesSuspended)
				{
					ChangeStatus(ProcessGroupStatus.Suspended);
				}
				else
				{
					ChangeStatus(ProcessGroupStatus.Running);
				}
			}
			catch (Exception)
			{

			}
		}

		private static IEnumerable<Process> GetProcessesByName(string name)
		{
			try
			{
				var processes = Process.GetProcessesByName(name);
				if (processes != null) return [.. processes];
				return [];
			}
			catch (Exception)
			{

				return [];
			}
			
		}
		private void ChangeStatus(ProcessGroupStatus newStatus)
		{
			if (ProcessGroupStatus != newStatus)
			{
				var previousStatus = ProcessGroupStatus;
				ProcessGroupStatus = newStatus;
				ProcessGroupStatusChanged?.Invoke(this, new ProcessGroupStatusChangedEventArgs(newStatus, previousStatus));
			}
		}

		public void Dispose()
		{
			topazProcessWatcher.Dispose();
			ffmpegProcessWatcher.Dispose();
		}
	}
}
