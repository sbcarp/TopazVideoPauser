using System.Diagnostics;
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
		private readonly ToolStripMenuItem tasksFinishedMenuItem;
		public event EventHandler? OnTrayIconDoubleClicked;
		public event EventHandler? OnStartOnSystemStartsClicked;
		public event EventHandler? OnPauseClicked;
		public event EventHandler? OnResumeClicked;
		public event EventHandler? OnExitClicked;
		public event EventHandler<TasksFinishedAction>? OnTaskFinishedOptionClicked;
		public TrayMenuManager()
		{
			startOnSystemStartsMenuItem = new ToolStripMenuItem("Start on System Starts", null, (s, e) => OnStartOnSystemStartsClicked?.Invoke(s, e));
			pauseMenuItem = new ToolStripMenuItem("Pause", null, (s, e) => OnPauseClicked?.Invoke(s, e));
			resumeMenuItem = new ToolStripMenuItem("Resume", null, (s, e) => OnResumeClicked?.Invoke(s, e));
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
								new ToolStripMenuItem("Sleep", null, WhenTasksFinishedOption_Clicked) { CheckOnClick = true, Tag = TasksFinishedAction.Sleep},
								new ToolStripMenuItem("Shutdown", null, WhenTasksFinishedOption_Clicked){ CheckOnClick = true, Tag = TasksFinishedAction.Shutdown}
							}
			};
			trayIcon = new NotifyIcon()
			{
				Icon = Resources.unknown,
				ContextMenuStrip = new ContextMenuStrip
				{
					Items =
					{
						startOnSystemStartsMenuItem,
						new ToolStripSeparator(),
						tasksFinishedMenuItem,
						pauseMenuItem,
						resumeMenuItem,
						new ToolStripSeparator(),
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
				trayIcon.ContextMenuStrip.Invoke((MethodInvoker)delegate
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
				}
				if (trayIcon.ContextMenuStrip?.InvokeRequired == true)
				{
					trayIcon.ContextMenuStrip.Invoke((MethodInvoker)delegate
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
