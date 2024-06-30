using Newtonsoft.Json;
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
	internal class ProcessGroupStatusChangedEventArgs(ProcessGroupStatus status, ProcessGroupStatus previousStatus, int averageCoresUsed) : EventArgs
	{
		public ProcessGroupStatus Status { get; } = status;
		public ProcessGroupStatus PreviousStatus { get; } = previousStatus;
		public int AverageCoresUsed { get; } = averageCoresUsed;
	}
	internal class ProcessGroupManager: IDisposable
	{
		private static readonly string[] topazProcessNames = ["Topaz Video AI", "Topaz Video Enhance AI"];
		private static readonly string[] ffmpegProcessNames = ["ffmpeg", "Topaz Video Enhance AI"];
		private static readonly string processExtensionName = ".exe";
		private readonly Dictionary<int, Process> topazProcesses = [];
		private readonly Dictionary<int, Process> ffmpegProcesses = [];
		private readonly Debouncer<bool> processSuspendDebouncer = new(TimeSpan.FromSeconds(3), true, true);
		private readonly Debouncer<bool> processAffinityDebouncer = new(TimeSpan.FromSeconds(3), true, true);
		private readonly ProcessWatcher topazProcessWatcher = new (topazProcessNames.Select((pn) => pn + processExtensionName));
		private readonly ProcessWatcher ffmpegProcessWatcher = new(ffmpegProcessNames.Select((pn) => pn + processExtensionName));
        private readonly object refreshStatusLock = new();
		public ProcessGroupStatus ProcessGroupStatus { get; private set; } = ProcessGroupStatus.Unset;
		public int AverageCoresUsed { get; private set; } = 0;
		public event EventHandler<ProcessGroupStatusChangedEventArgs>? ProcessGroupStatusChanged;
		public event Action? FfmpegProcessSpawned;
		public ProcessGroupManager()
		{
			topazProcessWatcher.OnProcessSpawned += TopazProcessWatcher_OnProcessSpawned;
			topazProcessWatcher.OnProcessExited += TopazProcessWatcher_OnProcessExited;
			ffmpegProcessWatcher.OnProcessSpawned += FfmpegProcessWatcher_OnProcessSpawned;
			ffmpegProcessWatcher.OnProcessExited += FfmpegProcessWatcher_OnProcessExited;

			topazProcesses = GetProcessesByNames(topazProcessNames).ToDictionary(p => p.Id);
			topazProcessWatcher.WatchProcess(topazProcesses.Values);
			ffmpegProcesses = GetProcessesByNames(ffmpegProcessNames).Where(p =>
			{
				var parentId = p.GetParentProcessId();
				return topazProcesses.Values.Any(tp => tp.Id == parentId) || topazProcessNames.Contains(p.ProcessName);
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
				Debug.WriteLine($"OnProcessSpawned {processId}");
				ffmpegProcesses.Add(processId, process);
				FfmpegProcessSpawned?.Invoke();
				RefreshStatus();
			}
		}

		private void FfmpegProcessWatcher_OnProcessExited(int processId, Process? process)
		{
			Debug.WriteLine($"OnProcessExited {processId}");
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

		public async Task<bool> SetAffinity(int cores)
		{
			cores = Math.Max(Math.Min(cores, Environment.ProcessorCount), 1);
			var coresMask = (1 << cores) - 1;
			try
			{
				return await processAffinityDebouncer.Debounce(() =>
				{
					try
					{
						foreach (var process in ffmpegProcesses.Values)
						{
							process.ProcessorAffinity = coresMask;
						}
						return true;
					}
					catch (Exception)
					{
						return false;
					}
				});
			}
			catch (Exception)
			{
				return false;
			}
			finally
			{
				RefreshStatus();
			}
		}

		public async Task<bool> SuspendAsync()
		{
			return await ProcessActionAsync(true);
		}
		public async Task<bool> ResumeAsync()
		{
			return await ProcessActionAsync(false);
		}
		private async Task<bool> ProcessActionAsync(bool suspend)
		{
			try
			{
				
				return await processSuspendDebouncer.Debounce(async () =>
				{
					foreach (var process in ffmpegProcesses.Values)
					{
						if ((DateTime.Now - process.StartTime) < TimeSpan.FromSeconds(15))
						{
							return false;
						}
					}
					try
					{
						var allProcesses = ffmpegProcesses.Values.Concat(topazProcesses.Values);
						var result = suspend ? allProcesses.Suspend() : allProcesses.Resume();
						Debug.WriteLine($"suspend {suspend}, result {result}");
						return result;
					}
					finally
					{
						await Task.Delay(100);
						RefreshStatus();
					}
				});
				
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
				lock(refreshStatusLock)
				{
					if (!topazProcesses.Values.AnyActive())
					{
						ChangeStatus(ProcessGroupStatus.Unkonwn, 0);
						return;
					}

					var areTopazProcessesSuspended = topazProcesses.Values.AllSuspended();
					var isAnyFfmpegProcessActive = ffmpegProcesses.Values.AnyActive();
					var areFfmpegProcessesSuspended = ffmpegProcesses.Values.AllSuspended();
					var averageCoresUsed = GetAverageCpuCoresUsed();

					if (!isAnyFfmpegProcessActive && !areTopazProcessesSuspended)
					{
						ChangeStatus(ProcessGroupStatus.Idle, averageCoresUsed);
					}
					else if (areFfmpegProcessesSuspended)
					{
						ChangeStatus(ProcessGroupStatus.Suspended, averageCoresUsed);
					}
					else
					{
						ChangeStatus(ProcessGroupStatus.Running, averageCoresUsed);
					}
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.ToString());
			}
		}

		public int GetAverageCpuCoresUsed()
		{
			try
			{
				int averageProcessorAffinityMask = ffmpegProcesses.Values.Count != 0
				? (int)ffmpegProcesses.Values.Average(p => {
					return p.ProcessorAffinity; 
				})
				: 0;
				int setBitCount = 0;
				while (averageProcessorAffinityMask > 0)
				{
					setBitCount += averageProcessorAffinityMask & 1;
					averageProcessorAffinityMask >>= 1;
				}
				Debug.WriteLine($"GetAverageCpuCoresUsed {setBitCount}");
				return setBitCount;
			}
			catch (Exception)
			{

				return 0;
			}
		}


		private static IEnumerable<Process> GetProcessesByNames(string[] names)
		{
			try
			{
				
				var processes = names.SelectMany(Process.GetProcessesByName).ToList();
                if (processes != null) return [.. processes];
				return [];
			}
			catch (Exception)
			{

				return [];
			}
			
		}
		private void ChangeStatus(ProcessGroupStatus newStatus, int averageCoresUsed)
		{
			if (newStatus != ProcessGroupStatus || averageCoresUsed != AverageCoresUsed)
			{
				Debug.WriteLine($"ProcessGroupStatus {newStatus}");
				var previousStatus = ProcessGroupStatus;
				ProcessGroupStatus = newStatus;
				AverageCoresUsed = averageCoresUsed;
				ProcessGroupStatusChanged?.Invoke(this, new ProcessGroupStatusChangedEventArgs(newStatus, previousStatus, averageCoresUsed));
			}
		}

		public void Dispose()
		{
			processSuspendDebouncer.Dispose();
			topazProcessWatcher.Dispose();
			ffmpegProcessWatcher.Dispose();
		}
	}
}
