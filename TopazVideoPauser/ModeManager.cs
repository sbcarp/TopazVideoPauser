using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Timer = System.Threading.Timer;

namespace TopazVideoPauser
{
	internal class ModeManager
	{
		private readonly AppConfig appConfig;
		private readonly ProcessGroupManager processGroupManager;
		private readonly FullScreenDetector? fullScreenDetector;
		private readonly IdleDetector idleDetector;
		private bool isFullScreen = false;
		private bool hasUserActivity = true;
		private Timer? delayThrottleTimer;

		public ModeManager(AppConfig appConfig, ProcessGroupManager processGroupManager, IdleDetector idleDetector, FullScreenDetector? fullScreenDetector) {
			this.appConfig = appConfig;
			this.appConfig.PropertyChanged += AppConfig_PropertyChanged;
			this.processGroupManager = processGroupManager;
			this.fullScreenDetector = fullScreenDetector;
			this.processGroupManager.ProcessGroupStatusChanged += ProcessGroupManager_ProcessGroupStatusChanged;
			this.processGroupManager.FfmpegProcessSpawned += ProcessGroupManager_FfmpegProcessSpawned;
			if (this.fullScreenDetector != null )
			{
				this.fullScreenDetector.OnFullScreenStateChanged += FullScreenDetector_OnFullScreenStateChangedAsync;
			}
			this.idleDetector = idleDetector;
			idleDetector.OnIdleStateChanged += IdleDetector_OnIdleStateChanged;
			ChangeMode(appConfig.ThrottleMode);
		}

		private async void ProcessGroupManager_ProcessGroupStatusChanged(object? sender, ProcessGroupStatusChangedEventArgs e)
		{
			if (e.Status == ProcessGroupStatus.Running)
			{
				await UpdateSystemState();
			}
		}

		private async void ProcessGroupManager_FfmpegProcessSpawned()
		{
			if (processGroupManager.ProcessGroupStatus == ProcessGroupStatus.Running)
			{
				await UpdateSystemState();
			}
		}

		private async void AppConfig_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(appConfig.ThrottleMode))
			{
				ChangeMode(appConfig.ThrottleMode);
			}
			else
			{
				await UpdateSystemState();
			}
		}

		public void ChangeMode(ThrottleMode mode)
		{
			appConfig.ThrottleMode = mode;
			if (mode == ThrottleMode.Manual)
			{
				fullScreenDetector?.Stop();
				idleDetector.Stop();
				delayThrottleTimer?.Dispose();
			}
			else
			{
				fullScreenDetector?.Start();
				idleDetector.Start();
				UpdateSystemState().ConfigureAwait(true);
			}
		}
		public async Task TogglePauseResumeStateAsync(bool isManual)
		{
			var status = processGroupManager.ProcessGroupStatus;
			if (status == ProcessGroupStatus.Running)
			{
				await PauseAsync(isManual);
			}
			else if (status == ProcessGroupStatus.Suspended)
			{
				await ResumeAsync(isManual);
			}
		}
		public async Task<bool> LimitCpuAsync(int cores)
		{
			ChangeMode(ThrottleMode.Manual);
			return await processGroupManager.SetAffinity(cores);
		}
		public async Task<bool> PauseAsync(bool isManual)
		{
			if (isManual)
			{
				ChangeMode(ThrottleMode.Manual);
			}
			return await processGroupManager.SuspendAsync();
		}
		public async Task<bool> ResumeAsync(bool isManual)
		{
			if (isManual)
			{
				ChangeMode(ThrottleMode.Manual);
			}
			return await processGroupManager.ResumeAsync();
		}
		private async void FullScreenDetector_OnFullScreenStateChangedAsync(bool isFullScreen)
		{
			if (appConfig.ThrottleMode == ThrottleMode.Manual) return;
			this.isFullScreen = isFullScreen;
			await UpdateSystemState();
		}
		private async void IdleDetector_OnIdleStateChanged(bool isIdle)
		{
			if (appConfig.ThrottleMode == ThrottleMode.Manual) return;
			Debug.WriteLine($"isIdle {isIdle}");
			hasUserActivity = !isIdle;
			await UpdateSystemState();
		}
		private async Task UpdateSystemState()
		{
			delayThrottleTimer?.Dispose();
			if (appConfig.ThrottleMode == ThrottleMode.Manual) return;
			var systemState = isFullScreen ? SystemState.Fullscreen : (hasUserActivity ? SystemState.UserActivity : SystemState.Idle);
			var throttleOption = appConfig.GetThrottleOption(systemState) ?? ThrottleOption.NoLimit;
			var cpuCoresLimit = appConfig.GetCpuCoresLimit(systemState) ?? Environment.ProcessorCount;
			if (!await ApplyThrottlePolicy(throttleOption, cpuCoresLimit))
			{
				delayThrottleTimer = new Timer(async (state) => await UpdateSystemState(), null, 15000, Timeout.Infinite);
			}
		}

		private async Task<bool> ApplyThrottlePolicy(ThrottleOption throttleOption, int cpuCoresLimit)
		{
			var status = processGroupManager.ProcessGroupStatus;
			var processorCount = Environment.ProcessorCount;
			if (throttleOption == ThrottleOption.NoLimit || throttleOption == ThrottleOption.LimitCPU)
			{
				if (status == ProcessGroupStatus.Suspended)
				{
					if (!await ResumeAsync(false)) return false;
				}
				if (throttleOption == ThrottleOption.NoLimit)
				{
					if (!await processGroupManager.SetAffinity(processorCount)) return false;
				}
			}
			
			if (throttleOption == ThrottleOption.LimitCPU)
			{
				if (!await processGroupManager.SetAffinity(cpuCoresLimit)) return false;
			}

			if (throttleOption == ThrottleOption.Pause)
			{
				if (status == ProcessGroupStatus.Running)
				{
					if (!await PauseAsync(false)) return false;
				}
			}
			return true;
		}
	}
}
