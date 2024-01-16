using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TopazVideoPauser
{
	internal enum ThrottleMode
	{
		Manual,
		Automatic
	}
	internal enum ThrottleOption
	{
		NoLimit,
		LimitCPU,
		Pause
	}
	internal enum SystemState
	{
		Fullscreen,
		UserActivity,
		Idle
	}
	[Serializable]
	internal class AppConfig : INotifyPropertyChanged
	{
		[JsonIgnore]
		private readonly Debouncer<bool> saveDebouncer = new(TimeSpan.FromSeconds(3), false, true, TimeSpan.FromSeconds(10));
		[JsonProperty]
		private ThrottleMode throttleMode = ThrottleMode.Manual;
		[JsonIgnore]
		public ThrottleMode ThrottleMode 
		{ 
			get { return throttleMode; } 
			set { 
				if (throttleMode != value)
				{
					throttleMode = value;
					OnPropertyChanged(nameof(ThrottleMode));
				}
			} 
		}
		[JsonProperty]
		private readonly Dictionary<SystemState, ThrottleOption> selectedThrottleOption = new() {
			{ SystemState.Fullscreen, ThrottleOption.Pause },
			{ SystemState.UserActivity, ThrottleOption.LimitCPU },
			{ SystemState.Idle, ThrottleOption.NoLimit },
		};
		[JsonProperty]
		private readonly Dictionary<SystemState, int> selectedCPUCoresLimit = new() {
			{ SystemState.Fullscreen, 1 },
			{ SystemState.UserActivity, Math.Max(Environment.ProcessorCount / 2, 1) },
			{ SystemState.Idle, Environment.ProcessorCount },
		};

		public event PropertyChangedEventHandler? PropertyChanged;
		public int? GetCpuCoresLimit(SystemState state)
		{
			return selectedCPUCoresLimit.TryGetValue(state, out var cores) ? cores : null;
		}
		public void SetCpuCoresLimit(SystemState state, int cores)
		{
			if (selectedCPUCoresLimit.TryGetValue(state, out int value) && value == cores) return;
			selectedCPUCoresLimit[state] = cores;
			OnPropertyChanged();
		}
		public ThrottleOption? GetThrottleOption(SystemState state)
		{
			return selectedThrottleOption.TryGetValue(state, out var option) ? option : null;
		}
		public void SetThrottleOption(SystemState state, ThrottleOption throttleOption)
		{
			if (selectedThrottleOption.TryGetValue(state, out ThrottleOption value) && value == throttleOption) return;
			selectedThrottleOption[state] = throttleOption;
			OnPropertyChanged();
		}
		protected void OnPropertyChanged([CallerMemberName] string? name = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
			SaveAsync().ConfigureAwait(false);
		}
		public static async Task<AppConfig> LoadAsync(string? filePath = default, TimeSpan timeout = default)
		{
			if (filePath == default) filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppConfig.json");
			if (timeout == default) timeout = TimeSpan.FromSeconds(5);
			try
			{
				using var cts = new CancellationTokenSource(timeout);
				string json = await File.ReadAllTextAsync(filePath, cts.Token);
				return JsonConvert.DeserializeObject<AppConfig>(json, new JsonSerializerSettings
				{
					DefaultValueHandling = DefaultValueHandling.Populate,
					NullValueHandling = NullValueHandling.Ignore,
				}) ?? new AppConfig();
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"An error occurred: {ex.Message}");
				return new AppConfig();
			}
		}
		public async Task<bool> SaveAsync(string? filePath = default, TimeSpan timeout = default)
		{
			try
			{
				if (filePath == default) filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppConfig.json");

				return await saveDebouncer.Debounce(async () =>
				{
					if (timeout == default) timeout = TimeSpan.FromSeconds(5);
					try
					{
						using var cts = new CancellationTokenSource(timeout);
						string json = JsonConvert.SerializeObject(this, Formatting.Indented);
						await File.WriteAllTextAsync(filePath, json, cts.Token);
						Debug.WriteLine($"AppConfig saved");
						return true;
					}
					catch (Exception ex)
					{
						Debug.WriteLine($"Failed to save AppConfig: {ex.Message}");
						return false;
					}
				});
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Failed to debounce save: {ex.Message}");
				return false;
			}
		}
	}
}
