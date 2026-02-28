using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SistemaGestionProyectos2.Helpers
{
    /// <summary>
    /// Helper para manejar ventanas en configuraciones multi-monitor.
    /// Resuelve el problema de que SystemParameters.WorkArea solo devuelve
    /// el area de trabajo del monitor primario.
    /// </summary>
    public static class WindowHelper
    {
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        /// <summary>
        /// Maximiza la ventana al area de trabajo del monitor donde se encuentra,
        /// respetando la barra de tareas. Funciona en multi-monitor.
        /// </summary>
        public static void MaximizeToCurrentMonitor(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;

            if (hwnd == IntPtr.Zero)
            {
                // Handle aun no existe, usar fallback del monitor primario
                FallbackToPrimary(window);
                return;
            }

            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

            if (!GetMonitorInfo(monitor, ref monitorInfo))
            {
                FallbackToPrimary(window);
                return;
            }

            // Obtener factor de escala DPI para convertir pixels fisicos a WPF units
            var source = PresentationSource.FromVisual(window);
            double scaleX = source?.CompositionTarget?.TransformFromDevice.M11 ?? (96.0 / GetSystemDpi());
            double scaleY = source?.CompositionTarget?.TransformFromDevice.M22 ?? (96.0 / GetSystemDpi());

            var work = monitorInfo.rcWork;
            window.Left = work.Left * scaleX;
            window.Top = work.Top * scaleY;
            window.Width = (work.Right - work.Left) * scaleX;
            window.Height = (work.Bottom - work.Top) * scaleY;
        }

        private static void FallbackToPrimary(Window window)
        {
            var workArea = SystemParameters.WorkArea;
            window.Left = workArea.Left;
            window.Top = workArea.Top;
            window.Width = workArea.Width;
            window.Height = workArea.Height;
        }

        [DllImport("user32.dll")]
        private static extern int GetDpiForSystem();

        private static double GetSystemDpi()
        {
            try { return GetDpiForSystem(); }
            catch { return 96.0; }
        }
    }
}
