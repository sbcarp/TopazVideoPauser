using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;

namespace TopazVideoPauser
{
	internal class IdleDetector : IDisposable
	{
		[DllImport("user32.dll")]
		static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

		[StructLayout(LayoutKind.Sequential)]
		struct LASTINPUTINFO
		{
			public uint cbSize;
			public uint dwTime;
		}

		private readonly Timer idleTimer;
		private readonly int idleThreshold; 
		private bool isIdle = false; 
		public event Action<bool>? OnIdleStateChanged;

		public IdleDetector(int threshold)
		{
			idleThreshold = threshold;
			idleTimer = new (idleThreshold);
			idleTimer.Elapsed += IdleTimer_Elapsed;
		}

		private void IdleTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
		{
			uint idleTime = GetIdleTime();
			if (idleTime > idleThreshold && !isIdle)
			{
				isIdle = true;
				idleTimer.Interval = 1000;
				idleTimer.Stop();
				idleTimer.Start();
				OnIdleStateChanged?.Invoke(isIdle);
			}
			else if (idleTime <= idleThreshold && isIdle)
			{
				isIdle = false;
				idleTimer.Interval = idleThreshold;
				idleTimer.Stop();
				idleTimer.Start();
				OnIdleStateChanged?.Invoke(isIdle); 
			}
		}

		public void Start()
		{
			isIdle = false;
			idleTimer.Start();
		}

		public void Stop()
		{
			idleTimer.Stop();
		}

		private static uint GetIdleTime()
		{
			LASTINPUTINFO lastInputInfo = new()
			{
				cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO))
			};
			if (GetLastInputInfo(ref lastInputInfo))
			{
				return (uint)Environment.TickCount - lastInputInfo.dwTime;
			}

			return 0;
		}

		public void Dispose()
		{
			idleTimer.Close();
			idleTimer.Dispose();
		}
	}
}
