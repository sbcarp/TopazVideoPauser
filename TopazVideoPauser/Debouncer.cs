using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.AxHost;
using Timer = System.Threading.Timer;

namespace TopazVideoPauser
{
	internal class TimerCallBackObject<T>
	{
        public required TaskCompletionSource<T> TaskCompletionSource { get; set; }
		public required Func<Task<T>> Callback { get; set; }
	}
	internal class Debouncer<T>(TimeSpan interval, bool leading = true, bool trailing = true, TimeSpan maxWait = default) : IDisposable
	{
		private TimerCallBackObject<T>? lastTimerCallbackObject;
		private Timer? timer;
		private DateTime lastInvokeTime = DateTime.MinValue;
		private DateTime lastCallTime = DateTime.MinValue;
		private readonly TimeSpan interval = interval;
		private readonly bool leading = leading;
		private readonly bool trailing = trailing;
		private readonly TimeSpan maxWait = maxWait == default ? TimeSpan.MaxValue : maxWait;
		private readonly object syncObj = new();
		private bool trailingScheduled = false;

		public Task<T> Debounce(Func<Task<T>> action)
		{
			return DebounceInternal(action);
		}

		public Task<T> Debounce(Func<T> action)
		{
			return DebounceInternal(() => Task.FromResult(action()));
		}

		private Task<T> DebounceInternal(Func<Task<T>> action)
		{
			lock (syncObj)
			{
				var now = DateTime.UtcNow;

				lastTimerCallbackObject?.TaskCompletionSource.TrySetCanceled();

				lastTimerCallbackObject = new TimerCallBackObject<T>
				{
					TaskCompletionSource = new TaskCompletionSource<T>(),
					Callback = action
				};
				var timeSinceLastCall = now - lastCallTime;
				var timeSinceLastInvoke = now - lastInvokeTime;
				var timeUntilNextInterval = interval - timeSinceLastInvoke;
				var timeUntilMaxWait = maxWait - timeSinceLastInvoke;
				// immediate invoke when leading is true and lastInvocation time exceeds interval
				timer?.Dispose();
				if (leading && timeSinceLastInvoke > interval)
				{
					trailingScheduled = false;
					InvokeAction(lastTimerCallbackObject);
					lastInvokeTime = now;
				}
				// immediate invoke when lastCallTime is within interval but lastInvoke exceeds maxWait
				else if (trailingScheduled && timeSinceLastInvoke > maxWait)
				{
					trailingScheduled = false;
					InvokeAction(lastTimerCallbackObject);
					lastInvokeTime = now;
				}
				// schedule invoke for trailing
				else if (trailing)
				{
					if (!trailingScheduled)
					{
						if (!leading) lastInvokeTime = now;
						trailingScheduled = true;
					}
					timer = new Timer(state =>
					{
						trailingScheduled = false;
						if (state == null) return;
						InvokeAction((TimerCallBackObject<T>)state);
						lastInvokeTime = DateTime.UtcNow;
					}, lastTimerCallbackObject, (int)interval.TotalMilliseconds, Timeout.Infinite);
				}

				lastCallTime = now;
				return lastTimerCallbackObject.TaskCompletionSource.Task;
			}
		}

		public void Dispose()
		{
			timer?.Dispose();
			lastTimerCallbackObject?.TaskCompletionSource.TrySetCanceled();
		}

		private static async void InvokeAction(TimerCallBackObject<T> timerCallbackObject)
		{
			try
			{
				var result = await timerCallbackObject.Callback();
				timerCallbackObject.TaskCompletionSource.TrySetResult(result);
			}
			catch (Exception ex)
			{
				timerCallbackObject.TaskCompletionSource.TrySetException(ex);
			}
		}
	}
}
