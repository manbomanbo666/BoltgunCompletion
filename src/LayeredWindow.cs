using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;

namespace BoltgunCompletion {
internal static class LayeredWindow {
    [StructLayout(LayoutKind.Sequential)] struct Point {public int x,y;public Point(int x,int y){this.x=x;this.y=y;}}
    [StructLayout(LayoutKind.Sequential)] struct Size {public int x,y;public Size(int x,int y){this.x=x;this.y=y;}}
    [StructLayout(LayoutKind.Sequential,Pack=1)] struct Blend {public byte operation,flags,alpha,format;}
    [DllImport("user32.dll",SetLastError=true)] static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr window,IntPtr dc);
    [DllImport("gdi32.dll",SetLastError=true)] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr value);
    [DllImport("gdi32.dll",SetLastError=true)] static extern IntPtr SelectObject(IntPtr dc,IntPtr value);
    [DllImport("user32.dll",SetLastError=true)] static extern bool UpdateLayeredWindow(IntPtr window,IntPtr destDC,ref Point dest,
        ref Size size,IntPtr sourceDC,ref Point source,int colorKey,ref Blend blend,int flags);
    public static void Present(IntPtr window,Bitmap bitmap,int left,int top,int opacity) {
        IntPtr screen=IntPtr.Zero,memory=IntPtr.Zero,image=IntPtr.Zero,previous=IntPtr.Zero;
        try {
            screen=GetDC(IntPtr.Zero);if(screen==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
            memory=CreateCompatibleDC(screen);if(memory==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
            // The source bitmap is premultiplied ARGB; a transparent matte preserves its alpha.
            image=bitmap.GetHbitmap(Color.FromArgb(0));previous=SelectObject(memory,image);
            if(previous==IntPtr.Zero||previous==new IntPtr(-1))throw new Win32Exception(Marshal.GetLastWin32Error());
            var dest=new Point(left,top);var source=new Point(0,0);var size=new Size(bitmap.Width,bitmap.Height);
            var blend=new Blend {operation=0,flags=0,alpha=RadarAppearance.GlobalAlpha(opacity),format=1};
            if(!UpdateLayeredWindow(window,screen,ref dest,ref size,memory,ref source,0,ref blend,2))throw new Win32Exception(Marshal.GetLastWin32Error());
        } finally {
            if(previous!=IntPtr.Zero&&previous!=new IntPtr(-1)&&memory!=IntPtr.Zero)SelectObject(memory,previous);
            if(image!=IntPtr.Zero)DeleteObject(image);if(memory!=IntPtr.Zero)DeleteDC(memory);if(screen!=IntPtr.Zero)ReleaseDC(IntPtr.Zero,screen);
        }
    }
}
}
