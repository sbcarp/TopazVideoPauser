using System;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;


namespace TopazVideoPauser
{
	public static partial class ProcessExtensions
	{
		[LibraryImport("ntdll.dll")]
		private static partial int NtSuspendProcess(IntPtr processHandle);

		[LibraryImport("ntdll.dll")]
		private static partial int NtResumeProcess(IntPtr processHandle);

		public static bool Suspend(this Process process)
		{
			if (process.HasExited) return false;
			if (process.IsSuspended()) return true;
			return NtSuspendProcess(process.Handle) == 0;
		}

		public static bool Resume(this Process process)
		{
			if (process.HasExited) return false;
			if (!process.IsSuspended()) return true;
			return NtResumeProcess(process.Handle) == 0;
		}

		public static bool IsSuspended(this Process process)
		{
			process.Refresh();
			if (process.HasExited) return false;
			return process.Threads.Cast<ProcessThread>().All(thread => thread.ThreadState == System.Diagnostics.ThreadState.Wait && thread.WaitReason == ThreadWaitReason.Suspended);
		}

		public static int GetParentProcessId(this Process process)
		{
			try
			{
				using ManagementObject mo = new($"win32_process.handle='{process.Id}'");
				mo.Get();
				return Convert.ToInt32(mo["ParentProcessId"]);
			}
			catch (Exception)
			{
				return 0;
			}
		}
	}

	public static partial class ProcessIEnumerableExtensions
	{
		public static bool Suspend(this IEnumerable<Process> processes)
		{
			foreach (var process in processes)
			{
				if (!process.Suspend())
				{
					return false;
				}
			}

			return true;
		}

		public static bool Resume(this IEnumerable<Process> processes)
		{
			foreach (var process in processes)
			{
				if (!process.Resume())
				{
					return false;
				}
			}

			return true;
		}

		public static bool AllSuspended(this IEnumerable<Process> processes)
		{
			return processes.All(p => p.IsSuspended());
		}
		public static bool AnySuspended(this IEnumerable<Process> processes)
		{
			return processes.Any(p => p.IsSuspended());
		}
		public static bool AllActive(this IEnumerable<Process> processes)
		{
			return processes.All(p => !p.HasExited);
		}
		public static bool AnyActive(this IEnumerable<Process> processes)
		{
			return processes.Any(p => !p.HasExited);
		}
	}
}
