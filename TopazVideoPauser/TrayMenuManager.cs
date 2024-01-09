using System.Diagnostics;
using System.Reflection;
using TopazVideoPauser.Properties;
using static TopazVideoPauser.AppContext;

namespace TopazVideoPauser
{
	internal class TrayMenuManager : IDisposable
	{
		private readonly NotifyIcon trayIcon;
		private readonly ToolStripMenuItem startOnSystemStartsMenuItem;
		private readonly ToolStripMenuItem pauseMenuItem;
		private readonly ToolStripMenuItem resumeMenuItem;
		private readonly ToolStripSeparator pauseResumeSeparator;
		private readonly ToolStripMenuItem tasksFinishedMenuItem;
		public event EventHandler? OnTrayIconDoubleClicked;
		public event EventHandler? OnStartOnSystemStartsClicked;
		public event EventHandler? OnPauseClicked;
		public event EventHandler? OnResumeClicked;
		public event EventHandler? OnExitClicked;
		public event EventHandler<TasksFinishedAction>? OnTaskFinishedOptionClicked;
		public TrayMenuManager()
		{
			var assemblyName = Assembly.GetExecutingAssembly().GetName();
			string versionText = $"{assemblyName.Name} v{assemblyName.Version?.Major ?? 0}.{assemblyName.Version?.Minor ?? 0}.{assemblyName.Version?.Build ?? 0}";
			startOnSystemStartsMenuItem = new ToolStripMenuItem("Start on System Starts", null, (s, e) => OnStartOnSystemStartsClicked?.Invoke(s, e));
			pauseMenuItem = new ToolStripMenuItem("Pause", null, (s, e) => OnPauseClicked?.Invoke(s, e));
			resumeMenuItem = new ToolStripMenuItem("Resume", null, (s, e) => OnResumeClicked?.Invoke(s, e));
			pauseResumeSeparator = new ToolStripSeparator();
			tasksFinishedMenuItem = new ToolStripMenuItem("When Tasks Finished")
			{
				DropDownItems =
							{
								new ToolStripMenuItem("Do Nothing", null, WhenTasksFinishedOption_Clicked)
								{
									Checked = true,
									CheckOnClick = true,
									Tag = TasksFinishedAction.DoNothing
								},
								new ToolStripMenuItem("Shutdown", null, WhenTasksFinishedOption_Clicked){ CheckOnClick = true, Tag = TasksFinishedAction.Shutdown},
								new ToolStripMenuItem("Sleep", null, WhenTasksFinishedOption_Clicked) { CheckOnClick = true, Tag = TasksFinishedAction.Sleep},
								new ToolStripMenuItem("Hibernate", null, WhenTasksFinishedOption_Clicked) { CheckOnClick = true, Tag = TasksFinishedAction.Hibernate},
							}
			};
			trayIcon = new NotifyIcon()
			{
				Icon = Resources.unknown,
				ContextMenuStrip = new ContextMenuStrip
				{
					Items =
					{
						new ToolStripMenuItem(versionText) { Enabled = false },
						startOnSystemStartsMenuItem,
						new ToolStripSeparator(),
						tasksFinishedMenuItem,
						new ToolStripSeparator(),
						pauseMenuItem,
						resumeMenuItem,
						pauseResumeSeparator,
						new ToolStripMenuItem("Check for Updates", null, (s, e) => Process.Start(new ProcessStartInfo("https://github.com/sbcarp/TopazVideoPauser/releases") { UseShellExecute = true })),
						new ToolStripMenuItem("Exit", null, (s, e) => OnExitClicked ?.Invoke(s, e))
					}
				}
			};
			trayIcon.DoubleClick += (s, e) => OnTrayIconDoubleClicked?.Invoke(s, e);
			trayIcon.Visible = true;
		}

		private void WhenTasksFinishedOption_Clicked(object? sender, EventArgs e)
		{
			if (sender is ToolStripMenuItem clickedItem && clickedItem.GetCurrentParent() is ToolStrip parent)
			{
				foreach (ToolStripMenuItem item in parent.Items)
				{
					item.Checked = item == clickedItem;
				}
				if (clickedItem.Tag is TasksFinishedAction action)
				{
					OnTaskFinishedOptionClicked?.Invoke(clickedItem, action);
				}
			}
		}
		public void UpdateStartOnSystemStartsOption(bool enabled)
		{
			startOnSystemStartsMenuItem.Checked = enabled;
		}
		public void UpdateCheckedTasksFinishedOption(TasksFinishedAction action)
		{
			void updateUI()
			{
				foreach (var item in tasksFinishedMenuItem.DropDownItems)
				{
					if (item is ToolStripMenuItem stripMenuItem && stripMenuItem.Tag is TasksFinishedAction tag)
					{
						stripMenuItem.Checked = tag == action;
					}
				}
			}
			if (trayIcon.ContextMenuStrip?.InvokeRequired == true)
			{
				trayIcon.ContextMenuStrip.Invoke((System.Windows.Forms.MethodInvoker)delegate
				{
					updateUI();
				});
			}
			else
			{
				updateUI();
			}

		}
		public void UpdateMenuItemsVisibility(Icon icon, bool pauseVisible, bool resumeVisible)
		{
			try
			{
				void updateUI()
				{
					trayIcon.Icon = icon;
					pauseMenuItem.Visible = pauseVisible;
					resumeMenuItem.Visible = resumeVisible;
					if (pauseVisible ||  resumeVisible)
					{
						pauseResumeSeparator.Visible = true;
					}
					else
					{
						pauseResumeSeparator.Visible = false;
					}
				}
				if (trayIcon.ContextMenuStrip?.InvokeRequired == true)
				{
					trayIcon.ContextMenuStrip.Invoke((System.Windows.Forms.MethodInvoker)delegate
					{
						updateUI();
					});
				}
				else
				{
					updateUI();
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
			}
		}

		public void Dispose()
		{
			trayIcon.Dispose();
		}
	}
}
