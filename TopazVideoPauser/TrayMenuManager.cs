using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Management;
using System.Reflection;
using TopazVideoPauser.Properties;
using static TopazVideoPauser.AppContext;

namespace TopazVideoPauser
{
	internal class TrayMenuManager : IDisposable
	{
		private readonly AppConfig appConfig;
		private readonly NotifyIcon trayIcon;
		private readonly ToolStripMenuItem startOnSystemStartsMenuItem;
		private readonly ToolStripMenuItem pauseMenuItem;
		private readonly ToolStripMenuItem resumeMenuItem;
		private readonly ToolStripSeparator pauseResumeSeparator;
		private readonly ToolStripMenuItem tasksFinishedMenuItem;
		private readonly ToolStripMenuItem throttleCPUMenuItem;
		private readonly RadioMenuItemManager<TasksFinishedAction> taskFinishedRadioGroup = new();
		private readonly RadioMenuItemManager<ThrottleMode> modeRadioGroup = new();
		private readonly RadioMenuItemManager<ThrottleOption> throttleOptionFullScreenGroup = new();
		private readonly RadioMenuItemManager<ThrottleOption> throttleOptionUserActivityGroup = new();
		private readonly RadioMenuItemManager<ThrottleOption> throttleOptionIdleGroup = new();
		private readonly RadioMenuItemManager<int> throttleOptionFullScreenCPUGroup = new();
		private readonly RadioMenuItemManager<int> throttleOptionUserActivityCPUGroup = new();
		private readonly RadioMenuItemManager<int> throttleOptionIdleCPUGroup = new();
		private readonly RadioMenuItemManager<int> throttleCPUGroup = new();
		private readonly Dictionary<Icon, Icon> iconCache = [];
		private Icon? currentIcon;
		private Icon? currentAutomaticIcon;
		public event EventHandler? OnTrayIconDoubleClicked;
		public event EventHandler? OnStartOnSystemStartsClicked;
		public event EventHandler<ThrottleMode>? OnModeClicked;
		public event EventHandler? OnPauseClicked;
		public event EventHandler? OnResumeClicked;
		public event EventHandler<int>? OnLimitCPUClicked;
		public event EventHandler? OnExitClicked;
		public event EventHandler<TasksFinishedAction>? OnTaskFinishedOptionClicked;
		public event EventHandler? OnTrayHandleCreated;
		public TrayMenuManager(AppConfig appConfig)
		{
			this.appConfig = appConfig;
			this.appConfig.PropertyChanged += AppConfig_PropertyChanged;
			var assemblyName = Assembly.GetExecutingAssembly().GetName();
			string versionText = $"{assemblyName.Name} v{assemblyName.Version?.Major ?? 0}.{assemblyName.Version?.Minor ?? 0}.{assemblyName.Version?.Build ?? 0}";
			startOnSystemStartsMenuItem = new ToolStripMenuItem("Start on System Starts", null, (s, e) => OnStartOnSystemStartsClicked?.Invoke(s, e));
			pauseMenuItem = new ToolStripMenuItem("Pause", null, (s, e) => OnPauseClicked?.Invoke(s, e));
			resumeMenuItem = new ToolStripMenuItem("Resume", null, (s, e) => OnResumeClicked?.Invoke(s, e));
			pauseResumeSeparator = new ToolStripSeparator();
			var taskFinishedRatioMenuItems = taskFinishedRadioGroup.AddRadioMenuItems([
				new RadioMenuItemOption<TasksFinishedAction> { Text = "Do Nothing", Value = TasksFinishedAction.DoNothing, Checked = true },
				new RadioMenuItemOption<TasksFinishedAction> { Text = "Shutdown", Value = TasksFinishedAction.Shutdown },
				new RadioMenuItemOption<TasksFinishedAction> { Text = "Sleep", Value = TasksFinishedAction.Sleep },
				new RadioMenuItemOption<TasksFinishedAction> { Text = "Hibernate", Value = TasksFinishedAction.Hibernate },
			]);
			taskFinishedRadioGroup.OnOptionClicked += TaskFinishedRadioGroup_OnOptionClicked;
			tasksFinishedMenuItem = new ToolStripMenuItem("When Tasks Finished");
			tasksFinishedMenuItem.DropDownItems.AddRange(taskFinishedRatioMenuItems.ToArray());
			var processorCount = Environment.ProcessorCount;
			List<RadioMenuItemOption<int>> cpuOptions = [
				new RadioMenuItemOption<int> { Text = "Use 1 Core", Value = 1 },
			];
			var step = Math.Max(processorCount / 4, 1);

			for (int numCores = Math.Max(step, 2); numCores <= processorCount; numCores += step)
			{
				cpuOptions.Add(new RadioMenuItemOption<int> { Text = $"Use {numCores} Cores", Value = numCores });
			}
			if (cpuOptions.Last().Value < processorCount)
			{
				cpuOptions.Add(new RadioMenuItemOption<int> { Text = $"Use {processorCount} Cores", Value = processorCount });
			}
			var cpuCoresLimit = appConfig.GetCpuCoresLimit(SystemState.Fullscreen) ?? 1;
			var closestOption = cpuOptions.OrderBy(option => Math.Abs(option.Value - cpuCoresLimit)).First();
			closestOption.Checked = true;
			List<RadioMenuItemOption<ThrottleOption>> throttleOptionsFullScreen = [
				new RadioMenuItemOption<ThrottleOption> { Text = "No Limit", Value = ThrottleOption.NoLimit },
				new RadioMenuItemOption<ThrottleOption> { Text = "Limit CPU", Value = ThrottleOption.LimitCPU, DropDownItems = throttleOptionFullScreenCPUGroup.AddRadioMenuItems(cpuOptions) },
				new RadioMenuItemOption<ThrottleOption> { Text = "Pause", Value = ThrottleOption.Pause, Checked = true },
			];
			closestOption.Checked = false;
			cpuCoresLimit = appConfig.GetCpuCoresLimit(SystemState.UserActivity) ?? 1;
			closestOption = cpuOptions.OrderBy(option => Math.Abs(option.Value - cpuCoresLimit)).First();
			closestOption.Checked = true;
			List<RadioMenuItemOption<ThrottleOption>> throttleOptionsUserActivity = [
				new RadioMenuItemOption<ThrottleOption> { Text = "No Limit", Value = ThrottleOption.NoLimit },
				new RadioMenuItemOption<ThrottleOption> { Text = "Limit CPU", Value = ThrottleOption.LimitCPU, DropDownItems = throttleOptionUserActivityCPUGroup.AddRadioMenuItems(cpuOptions), Checked = true },
				new RadioMenuItemOption<ThrottleOption> { Text = "Pause", Value = ThrottleOption.Pause },
			];
			closestOption.Checked = false;
			cpuCoresLimit = appConfig.GetCpuCoresLimit(SystemState.Idle) ?? 1;
			closestOption = cpuOptions.OrderBy(option => Math.Abs(option.Value - cpuCoresLimit)).First();
			closestOption.Checked = true;
			List<RadioMenuItemOption<ThrottleOption>> throttleOptionsIdle = [
				new RadioMenuItemOption<ThrottleOption> { Text = "No Limit", Value = ThrottleOption.NoLimit, Checked = true },
				new RadioMenuItemOption<ThrottleOption> { Text = "Limit CPU", Value = ThrottleOption.LimitCPU, DropDownItems = throttleOptionIdleCPUGroup.AddRadioMenuItems(cpuOptions) },
				new RadioMenuItemOption<ThrottleOption> { Text = "Pause", Value = ThrottleOption.Pause },
			];
			closestOption.Checked = false;
			var onFullscreenMenuItem = new ToolStripMenuItem("On Fullscreen");
			var onUserActivityMenuItem = new ToolStripMenuItem("On User Activity");
			var onIdleMenuItem = new ToolStripMenuItem("On Idle");
			onFullscreenMenuItem.DropDownItems.AddRange(throttleOptionFullScreenGroup.AddRadioMenuItems(throttleOptionsFullScreen).ToArray());
			onUserActivityMenuItem.DropDownItems.AddRange(throttleOptionUserActivityGroup.AddRadioMenuItems(throttleOptionsUserActivity).ToArray());
			onIdleMenuItem.DropDownItems.AddRange(throttleOptionIdleGroup.AddRadioMenuItems(throttleOptionsIdle).ToArray());

			throttleOptionFullScreenGroup.UpdateCheckedOption(appConfig.GetThrottleOption(SystemState.Fullscreen) ?? ThrottleOption.Pause);
			throttleOptionFullScreenGroup.OnOptionClicked += (s, e) => appConfig.SetThrottleOption(SystemState.Fullscreen, e.Value);
			throttleOptionFullScreenCPUGroup.OnOptionClicked += (s, e) => appConfig.SetCpuCoresLimit(SystemState.Fullscreen, e.Value);

			throttleOptionUserActivityGroup.UpdateCheckedOption(appConfig.GetThrottleOption(SystemState.UserActivity) ?? ThrottleOption.LimitCPU);
			throttleOptionUserActivityGroup.OnOptionClicked += (s, e) => appConfig.SetThrottleOption(SystemState.UserActivity, e.Value);
			throttleOptionUserActivityCPUGroup.OnOptionClicked += (s, e) => appConfig.SetCpuCoresLimit(SystemState.UserActivity, e.Value);

			throttleOptionIdleGroup.UpdateCheckedOption(appConfig.GetThrottleOption(SystemState.Idle) ?? ThrottleOption.NoLimit);
			throttleOptionIdleGroup.OnOptionClicked += (s, e) => appConfig.SetThrottleOption(SystemState.Idle, e.Value);
			throttleOptionIdleCPUGroup.OnOptionClicked += (s, e) => appConfig.SetCpuCoresLimit(SystemState.Idle, e.Value);

			var modeRatioMenuItems = modeRadioGroup.AddRadioMenuItems([
				new RadioMenuItemOption<ThrottleMode>
				{
					Text = "Automatic (Experimental)",
					Value = ThrottleMode.Automatic,
					Checked = appConfig.ThrottleMode == ThrottleMode.Automatic,
					DropDownItems = [
						onFullscreenMenuItem,
						onUserActivityMenuItem,
						onIdleMenuItem
					]
				},
				new RadioMenuItemOption<ThrottleMode> { Text = "Manual", Value = ThrottleMode.Manual, Checked = appConfig.ThrottleMode == ThrottleMode.Manual },
			]);
			modeRadioGroup.OnOptionClicked += (s, e) => appConfig.ThrottleMode = e.Value;


			throttleCPUMenuItem = new ToolStripMenuItem("Limit CPU");
			throttleCPUMenuItem.DropDownItems.AddRange(throttleCPUGroup.AddRadioMenuItems(cpuOptions).ToArray());
			throttleCPUGroup.OnOptionClicked += (s, e) => OnLimitCPUClicked?.Invoke(s, e.Value);
			trayIcon = new NotifyIcon()
			{
				Icon = Resources.unknown,
				ContextMenuStrip = new ContextMenuStrip()
			};
			var trayItems = trayIcon.ContextMenuStrip.Items;
			trayIcon.ContextMenuStrip.HandleCreated += (s, e) => OnTrayHandleCreated?.Invoke(s, e);
			trayItems.Add(new ToolStripMenuItem(versionText) { Enabled = false });

			trayItems.Add(startOnSystemStartsMenuItem);
			trayItems.Add(new ToolStripSeparator());
			trayItems.Add(tasksFinishedMenuItem);
			trayItems.Add(new ToolStripSeparator());
			trayItems.Add(new ToolStripMenuItem("Pause Mode") { Enabled = false });
			trayItems.AddRange(modeRatioMenuItems.ToArray());
			trayItems.Add(new ToolStripSeparator());
			trayItems.Add(throttleCPUMenuItem);
			trayItems.Add(pauseMenuItem);
			trayItems.Add(resumeMenuItem);
			trayItems.Add(pauseResumeSeparator);
			trayItems.Add(new ToolStripMenuItem("Check for Updates", null, (s, e) => Process.Start(new ProcessStartInfo("https://github.com/sbcarp/TopazVideoPauser/releases") { UseShellExecute = true })));
			trayItems.Add(new ToolStripMenuItem("Help", null, (s, e) => Process.Start(new ProcessStartInfo("https://github.com/sbcarp/TopazVideoPauser/tree/master?tab=readme-ov-file#faq") { UseShellExecute = true })));
			trayItems.Add(new ToolStripMenuItem("Exit", null, (s, e) => OnExitClicked?.Invoke(s, e)));
			trayIcon.DoubleClick += (s, e) => OnTrayIconDoubleClicked?.Invoke(s, e);
			trayIcon.Visible = true;
		}

		private void AppConfig_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(appConfig.ThrottleMode))
			{
				modeRadioGroup.UpdateCheckedOption(appConfig.ThrottleMode);
				try
				{
					if (currentIcon != null && currentAutomaticIcon != null) {
						trayIcon.Icon = appConfig.ThrottleMode == ThrottleMode.Manual ? currentIcon : currentAutomaticIcon;
					}
				}
				catch (Exception)
				{

				}
			}
		}

		private void TaskFinishedRadioGroup_OnOptionClicked(object? sender, RadioMenuItemEventArgs<TasksFinishedAction> e)
		{
			OnTaskFinishedOptionClicked?.Invoke(sender, e.Value);
		}

		public IntPtr? TrayHandle { get => trayIcon.ContextMenuStrip?.Handle; }

		public void UpdateStartOnSystemStartsOption(bool enabled)
		{
			startOnSystemStartsMenuItem.Checked = enabled;
		}
		public void UpdateCheckedTasksFinishedOption(TasksFinishedAction action)
		{
			DelegateUpdateMenuItems(() =>
			{
				taskFinishedRadioGroup.UpdateCheckedOption(action);
			});

		}
		public void UpdateModeOption(ThrottleMode mode)
		{
			DelegateUpdateMenuItems(() =>
			{
				modeRadioGroup.UpdateCheckedOption(mode);
			});
		}
		public void UpdateMenuItemsVisibility(Icon icon, bool pauseVisible, bool resumeVisible, int averageCoresUsed)
		{
			try
			{
				DelegateUpdateMenuItems(() =>
				{
					if (!iconCache.ContainsKey(icon))
					{
						iconCache[icon] = ComposeIcons(icon, Resources.automatic);
					}
					currentIcon = icon;
					currentAutomaticIcon = iconCache[icon];
					if (appConfig.ThrottleMode == ThrottleMode.Automatic)
					{
						icon = iconCache[icon];
					}
					trayIcon.Icon = icon;
					pauseMenuItem.Visible = pauseVisible;
					resumeMenuItem.Visible = resumeVisible;
					throttleCPUMenuItem.Visible = averageCoresUsed != 0;
					if (averageCoresUsed > 0)
					{
						throttleCPUGroup.UpdateCheckedOption(averageCoresUsed);
					}
					if (pauseVisible || resumeVisible)
					{
						pauseResumeSeparator.Visible = true;
					}
					else
					{
						pauseResumeSeparator.Visible = false;
					}
				});
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
			}
		}

		private void DelegateUpdateMenuItems(Action action)
		{
			if (trayIcon.ContextMenuStrip?.InvokeRequired == true)
			{
				trayIcon.ContextMenuStrip.Invoke((System.Windows.Forms.MethodInvoker)delegate
				{
					action.Invoke();
				});
			}
			else
			{
				action.Invoke();
			}
		}

		private static Icon ComposeIcons(Icon background, Icon overlay)
		{
			try
			{
				int width = Math.Max(background.Width, overlay.Width);
				int height = Math.Max(background.Height, overlay.Height);
				var composedBitmap = new Bitmap(width, height);
				using (Graphics g = Graphics.FromImage(composedBitmap))
				{
					g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
					g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
					g.DrawIcon(background, 0, 0);
					g.DrawIcon(overlay, width / 2, height / 2);

				}
				Icon composedIcon = Icon.FromHandle(composedBitmap.GetHicon());
				return composedIcon;
			}
			catch (Exception)
			{
				return background;
			}
		}


		public void Dispose()
		{
			trayIcon.Dispose();
		}
	}
}
