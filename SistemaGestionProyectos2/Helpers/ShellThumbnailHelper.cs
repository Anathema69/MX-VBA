using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace SistemaGestionProyectos2.Helpers
{
    /// <summary>
    /// MEJORA-5: Extract thumbnails from local files using Windows Shell.
    /// Works for CAD files (.ipt, .sldprt, .dwg, etc.) when the appropriate
    /// software (Inventor, SolidWorks, AutoCAD) is installed on the system,
    /// because those programs register Shell Extensions that generate previews.
    /// </summary>
    public static class ShellThumbnailHelper
    {
        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        private interface IShellItemImageFactory
        {
            [PreserveSig]
            int GetImage([In] SIZE size, [In] SIIGBF flags, out IntPtr phbm);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int cx;
            public int cy;
        }

        [Flags]
        private enum SIIGBF
        {
            ResizeToFit = 0x00,
            BiggerSizeOk = 0x01,
            MemoryOnly = 0x02,
            IconOnly = 0x04,
            ThumbnailOnly = 0x08,
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        /// <summary>
        /// Extract thumbnail from a local file using Windows Shell (synchronous, must run on STA thread).
        /// Returns null if no thumbnail handler is available (software not installed).
        /// </summary>
        private static BitmapSource? GetThumbnail(string filePath, int size)
        {
            if (!System.IO.File.Exists(filePath)) return null;

            var iidFactory = new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b");
            int hr = SHCreateItemFromParsingName(filePath, IntPtr.Zero, ref iidFactory, out var factory);
            if (hr != 0 || factory == null) return null;

            IntPtr hbm = IntPtr.Zero;
            try
            {
                var sz = new SIZE { cx = size, cy = size };

                // Try thumbnail first (rendered preview from shell extension)
                hr = factory.GetImage(sz, SIIGBF.ThumbnailOnly, out hbm);
                if (hr != 0 || hbm == IntPtr.Zero)
                {
                    // Fallback: resize to fit (may generate from shell extension or use icon)
                    hr = factory.GetImage(sz, SIIGBF.ResizeToFit, out hbm);
                    if (hr != 0 || hbm == IntPtr.Zero) return null;
                }

                var bmpSource = Imaging.CreateBitmapSourceFromHBitmap(
                    hbm, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bmpSource.Freeze();
                return bmpSource;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (hbm != IntPtr.Zero) DeleteObject(hbm);
                if (factory != null) Marshal.ReleaseComObject(factory);
            }
        }

        /// <summary>
        /// Extract thumbnail asynchronously on a dedicated STA thread (required for COM Shell APIs).
        /// Returns null if no shell extension is available for this file type.
        /// </summary>
        public static Task<BitmapSource?> GetThumbnailAsync(string filePath, int size = 200)
        {
            var tcs = new TaskCompletionSource<BitmapSource?>();
            var thread = new Thread(() =>
            {
                try { tcs.SetResult(GetThumbnail(filePath, size)); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            return tcs.Task;
        }
    }
}
