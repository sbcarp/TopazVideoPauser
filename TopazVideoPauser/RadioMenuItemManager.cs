using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static TopazVideoPauser.AppContext;

namespace TopazVideoPauser
{
	internal class RadioMenuItemManager<T>
	{
		public event EventHandler<RadioMenuItemEventArgs<T>>? OnOptionClicked;
		private readonly List<ToolStripMenuItem> radioMenuItems = [];
		public List<ToolStripMenuItem> AddRadioMenuItems(List<RadioMenuItemOption<T>> options)
		{
			var menuItems = new List<ToolStripMenuItem>();
			foreach (var option in options)
			{
				var item = new ToolStripMenuItem(option.Text, null, OnItemClick)
				{
					Checked = option.Checked,
					CheckOnClick = true,
					Tag = option.Value,
				};
				if (option.DropDownItems != null)
				{
					item.DropDownItems.AddRange(option.DropDownItems.ToArray());
				}
				radioMenuItems.Add(item);
				menuItems.Add(item);
			}
			return menuItems;
		}
		public void UpdateCheckedOption(T tag)
		{
			foreach (ToolStripMenuItem item in radioMenuItems)
			{
				item.Checked = EqualityComparer<T>.Default.Equals((T)item.Tag!, tag);
			}
		}
		private void OnItemClick(object? sender, EventArgs e)
		{
			if (sender is ToolStripMenuItem clickedItem)
			{
				foreach (ToolStripMenuItem item in radioMenuItems)
				{
					item.Checked = item == clickedItem;
				}

				OnOptionClicked?.Invoke(this, new RadioMenuItemEventArgs<T>((T)clickedItem.Tag!));
			}
		}
	}

	public class RadioMenuItemEventArgs <T>(T value) : EventArgs
	{
		public T Value { get; } = value;
	}

	public class RadioMenuItemOption<T>
	{
		public bool Checked { get; set; } = false;
		public required string Text { get; set; }
		public required T Value { get; set; }
		public List<ToolStripMenuItem>? DropDownItems { get; set; }
	}

}
