using Microsoft.Win32;
using System.Diagnostics;
using System.Timers;
using TopazVideoPauser.Properties;

namespace TopazVideoPauser
{
	internal static class Program
	{
		private static readonly Mutex mutex = new (true, "01aa8337-1691-4775-a0ca-b58044e70370");
		[STAThread]
		static void Main()
		{
			if (mutex.WaitOne(TimeSpan.Zero, true))
			{
				ApplicationConfiguration.Initialize();
				Application.EnableVisualStyles();
				Application.SetCompatibleTextRenderingDefault(false);
				Application.Run(new AppContext());
				mutex.ReleaseMutex();
			}
		}
	}
	

	public class AppContext : ApplicationContext
	{
		private readonly AppConfig appConfig = Task.Run(async () => await AppConfig.LoadAsync()).Result;
		private readonly ProcessGroupManager processGroupManager = new();
		private readonly TrayMenuManager trayMenuManager;
		private readonly FullScreenDetector fullScreenDetector;
		private readonly IdleDetector idleDetector = new(30000);
		private readonly ModeManager modeManager;
		public enum TasksFinishedAction
		{
			DoNothing,
			Sleep,
			Hibernate,
			Shutdown
		}
		
		private TasksFinishedAction taskFinishedAction = TasksFinishedAction.DoNothing;
		private readonly System.Timers.Timer taskTimer = new(TimeSpan.FromMinutes(1)) { AutoReset = false };

		public AppContext()
		{
			trayMenuManager = new(appConfig);
			fullScreenDetector = new(trayMenuManager.TrayHandle);
			trayMenuManager.OnTrayHandleCreated += (s, e) => {
				if (trayMenuManager.TrayHandle != null)
				{
					fullScreenDetector.UpdateHandle((nint)trayMenuManager.TrayHandle);
				}
			};
			processGroupManager.ProcessGroupStatusChanged += ProcessGroupManager_ProcessGroupStatusChanged;
			modeManager = new ModeManager(appConfig, processGroupManager, idleDetector, fullScreenDetector);
			
			RefreshProcessGroupStatus();

			trayMenuManager.OnTrayIconDoubleClicked += async (s, e) => await modeManager.TogglePauseResumeStateAsync(true);
			trayMenuManager.OnStartOnSystemStartsClicked += TrayMenuManager_OnStartOnSystemStartsClicked;
			trayMenuManager.OnLimitCPUClicked += async (s, e) => await modeManager.LimitCpuAsync(e);
			trayMenuManager.OnPauseClicked += async (s, e) => await modeManager.PauseAsync(true);
			trayMenuManager.OnResumeClicked += async (s, e) => await modeManager.ResumeAsync(true);
			trayMenuManager.OnExitClicked += TrayMenuManager_OnExitClicked;
			trayMenuManager.OnTaskFinishedOptionClicked += TrayMenuManager_OnTaskFinishedOptionClicked;
			taskTimer.Elapsed += TaskTimer_Elapsed;

			trayMenuManager.UpdateStartOnSystemStartsOption(ReadStartUpStatus());
		}
		private void TrayMenuManager_OnStartOnSystemStartsClicked(object? sender, EventArgs e)
		{
			var isStartUpEnabled = ReadStartUpStatus();
			if (WriteStartUpStatus(!isStartUpEnabled))
			{
				trayMenuManager.UpdateStartOnSystemStartsOption(!isStartUpEnabled);
			}
		}

		private void TrayMenuManager_OnExitClicked(object? sender, EventArgs e)
		{
			processGroupManager.Dispose();
			fullScreenDetector.Dispose();
			idleDetector.Dispose();
			trayMenuManager.Dispose();
			Application.Exit();
		}

		private void TrayMenuManager_OnTaskFinishedOptionClicked(object? sender, TasksFinishedAction action)
		{
			taskFinishedAction = action;
			if (action == TasksFinishedAction.DoNothing)
			{
				RefreshProcessGroupStatus();
			}
		}

		private void TaskTimer_Elapsed(object? sender, ElapsedEventArgs e)
		{
			var previousTasksFinishedAction = taskFinishedAction;
			taskFinishedAction = TasksFinishedAction.DoNothing;
			trayMenuManager.UpdateCheckedTasksFinishedOption(taskFinishedAction);
			RefreshProcessGroupStatus();
			if (previousTasksFinishedAction == TasksFinishedAction.Sleep)
			{
				Application.SetSuspendState(PowerState.Suspend, true, false);

			}
			else if (previousTasksFinishedAction == TasksFinishedAction.Hibernate)
			{
				Application.SetSuspendState(PowerState.Hibernate, true, false);
			}
			else if (previousTasksFinishedAction == TasksFinishedAction.Shutdown)
			{
				Process.Start("shutdown", "/s /f /t 0");
			}
		}
		private void RefreshProcessGroupStatus()
		{
			ProcessGroupManager_ProcessGroupStatusChanged(null, new(processGroupManager.ProcessGroupStatus, processGroupManager.ProcessGroupStatus, processGroupManager.GetAverageCpuCoresUsed()));
		}
		private void ProcessGroupManager_ProcessGroupStatusChanged(object? sender, ProcessGroupStatusChangedEventArgs e)
		{
			var isTaskTimerStarted = false;
			if (taskFinishedAction != TasksFinishedAction.DoNothing && e.PreviousStatus == ProcessGroupStatus.Running && e.Status == ProcessGroupStatus.Idle)
			{
				taskTimer.Start();
				isTaskTimerStarted = true;
			}
			else
			{
				taskTimer.Stop();
			}
			UpdateMenuItemsVisibility(e.Status, isTaskTimerStarted, e.AverageCoresUsed);
		}
		private void UpdateMenuItemsVisibility(ProcessGroupStatus status, bool isTaskTimerStarted, int averageCoresUsed)
		{
			try
			{
				(Icon icon, bool pauseVisible, bool resumeVisible) = status switch
				{
					ProcessGroupStatus.Running => (Resources.running, true, false),
					ProcessGroupStatus.Suspended => (Resources.suspend, false, true),
					ProcessGroupStatus.Idle => (Resources.idle, false, false),
					_ => (Resources.unknown, true, true) // default case
				};

				icon = isTaskTimerStarted ? Resources.shutdown : icon;
				trayMenuManager.UpdateMenuItemsVisibility(icon, pauseVisible, resumeVisible, averageCoresUsed);
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
			}
		}
		private static bool ReadStartUpStatus()
		{
			try
			{
				var registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
				if (registryKey == null) return false;
				return registryKey.GetValue(Application.ProductName)?.ToString() == Application.ExecutablePath;
			}
			catch (Exception)
			{
				return false;
			}
		}

		private static bool WriteStartUpStatus(bool shouldEnable)
		{
			try
			{
				var registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
				if (registryKey == null || Application.ProductName == null) return false;
				if (shouldEnable)
				{
					registryKey.SetValue(Application.ProductName, Application.ExecutablePath);
				}
				else
				{
					registryKey.DeleteValue(Application.ProductName);
				}
				return true;
			}
			catch (Exception)
			{

				return false;
			}
		}
	}
}