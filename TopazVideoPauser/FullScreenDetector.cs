using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace TopazVideoPauser
{
	[StructLayout(LayoutKind.Sequential)]
	internal struct APPBARDATA
	{
		internal int cbSize;
		internal IntPtr hWnd;
		internal int uCallbackMessage;
		internal int uEdge;
		internal RECT rc;
		internal IntPtr lParam;
	}

	internal enum ABMsg
	{
		ABM_NEW = 0,
		ABM_REMOVE,
		ABM_QUERYPOS,
		ABM_SETPOS,
		ABM_GETSTATE,
		ABM_GETTASKBARPOS,
		ABM_ACTIVATE,
		ABM_GETAUTOHIDEBAR,
		ABM_SETAUTOHIDEBAR,
		ABM_WINDOWPOSCHANGED,
		ABM_SETSTATE
	}

	internal enum ABNotify
	{
		ABN_STATECHANGE = 0,
		ABN_POSCHANGED,
		ABN_FULLSCREENAPP,
		ABN_WINDOWARRANGE
	}

	internal enum ABEdge
	{
		ABE_LEFT = 0,
		ABE_TOP,
		ABE_RIGHT,
		ABE_BOTTOM
	}
	internal struct RECT
	{
		public int left, top, right, bottom;
	}
	internal partial class FullScreenDetector : NativeWindow, IDisposable
	{

		[LibraryImport("SHELL32")]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
		public static partial uint SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

		[LibraryImport("User32.dll", EntryPoint = "RegisterWindowMessageA", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
		public static partial int RegisterWindowMessage(string msg);

		public event Action<bool>? OnFullScreenStateChanged;


		private int windowMessageId;
		private APPBARDATA appBarData;
		private bool running = false;
		public FullScreenDetector(IntPtr? hwnd)
		{
			if (hwnd != null)
			{
				UpdateHandle((IntPtr)hwnd);
			}
		}

		public void UpdateHandle(IntPtr hwnd)
		{
			if (hwnd == IntPtr.Zero || Handle == hwnd) return;
			AssignHandle(hwnd);
			appBarData = new APPBARDATA
			{
				cbSize = Marshal.SizeOf(typeof(APPBARDATA)),
				hWnd = hwnd
			};
			if (running)
			{
				Stop();
				Start();
			}
		}


		public bool Start()
		{
			if (windowMessageId == 0)
			{
				var assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
				windowMessageId = RegisterWindowMessage(assemblyName ?? "TopazVideoPauser");
				appBarData.uCallbackMessage = windowMessageId;
			}
			if (windowMessageId == 0) return false;
			if (SHAppBarMessage((int)ABMsg.ABM_NEW, ref appBarData) != 0)
			{
				running = true;
				return true;
			}
			return false;
		}

		public bool Stop()
		{
			if (SHAppBarMessage((int)ABMsg.ABM_REMOVE, ref appBarData) != 0)
			{
				running = false;
				return true;
			}
			return false;
		}

		protected override void WndProc(ref Message m)
		{
			try
			{
				if (m.Msg == windowMessageId && (ABNotify)m.WParam.ToInt32() == ABNotify.ABN_FULLSCREENAPP)
				{
					OnFullScreenStateChanged?.Invoke(m.LParam.ToInt32() == 1);
				}
			}
			catch (Exception)
			{

			}
			
			base.WndProc(ref m);
		}

		public void Dispose()
		{
			Stop();
			ReleaseHandle();
		}
	}
}
