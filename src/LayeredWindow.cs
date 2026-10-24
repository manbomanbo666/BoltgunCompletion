using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace BoltgunCompletion {
internal sealed class LayeredWindow:IDisposable {
    [StructLayout(LayoutKind.Sequential)] struct Point {public int x,y;public Point(int x,int y){this.x=x;this.y=y;}}
    [StructLayout(LayoutKind.Sequential)] struct Size {public int x,y;public Size(int x,int y){this.x=x;this.y=y;}}
    [StructLayout(LayoutKind.Sequential,Pack=1)] struct Blend {public byte operation,flags,alpha,format;}
    [StructLayout(LayoutKind.Sequential)] struct BitmapInfo {
        public uint size;public int width,height;public ushort planes,bits;public uint compression,imageSize;
        public int xDensity,yDensity;public uint used,important;
    }
    [DllImport("user32.dll",SetLastError=true)] static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr window,IntPtr dc);
    [DllImport("gdi32.dll",SetLastError=true)] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr value);
    [DllImport("gdi32.dll",SetLastError=true)] static extern IntPtr SelectObject(IntPtr dc,IntPtr value);
    [DllImport("gdi32.dll",SetLastError=true)] static extern IntPtr CreateDIBSection(IntPtr dc,ref BitmapInfo info,uint usage,out IntPtr bits,IntPtr section,uint offset);
    [DllImport("user32.dll",SetLastError=true)] static extern bool UpdateLayeredWindow(IntPtr window,IntPtr destDC,ref Point dest,
        ref Size size,IntPtr sourceDC,ref Point source,int colorKey,ref Blend blend,int flags);
    IntPtr screen,memory,image,previous,pixels;int width,height;byte[] transfer;
    internal int Allocations {get;private set;}
    void Ensure(int w,int h) {
        if(image!=IntPtr.Zero&&width==w&&height==h)return;
        Dispose();
        try {
            screen=GetDC(IntPtr.Zero);if(screen==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
            memory=CreateCompatibleDC(screen);if(memory==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
            var info=new BitmapInfo {size=40,width=w,height=-h,planes=1,bits=32,compression=0,imageSize=(uint)(w*h*4)};
            image=CreateDIBSection(screen,ref info,0,out pixels,IntPtr.Zero,0);if(image==IntPtr.Zero||pixels==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
            previous=SelectObject(memory,image);if(previous==IntPtr.Zero||previous==new IntPtr(-1))throw new Win32Exception(Marshal.GetLastWin32Error());
            width=w;height=h;transfer=new byte[w*h*4];Allocations++;
        }catch{Dispose();throw;}
    }
    public void Present(IntPtr window,Bitmap bitmap,int left,int top,int opacity) {
        Ensure(bitmap.Width,bitmap.Height);
        var data=bitmap.LockBits(new Rectangle(0,0,width,height),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
        try {
            int rowBytes=width*4;
            if(data.Stride==rowBytes)Marshal.Copy(data.Scan0,transfer,0,transfer.Length);
            else for(int y=0;y<height;y++)Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),transfer,y*rowBytes,rowBytes);
            Marshal.Copy(transfer,0,pixels,transfer.Length);
        }finally{bitmap.UnlockBits(data);}
        var dest=new Point(left,top);var source=new Point(0,0);var size=new Size(width,height);
        var blend=new Blend {operation=0,flags=0,alpha=RadarAppearance.GlobalAlpha(opacity),format=1};
        if(!UpdateLayeredWindow(window,screen,ref dest,ref size,memory,ref source,0,ref blend,2))throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    public void Dispose() {
        if(previous!=IntPtr.Zero&&previous!=new IntPtr(-1)&&memory!=IntPtr.Zero)SelectObject(memory,previous);
        if(image!=IntPtr.Zero)DeleteObject(image);if(memory!=IntPtr.Zero)DeleteDC(memory);if(screen!=IntPtr.Zero)ReleaseDC(IntPtr.Zero,screen);
        screen=memory=image=previous=pixels=IntPtr.Zero;transfer=null;width=height=0;
    }
}
}
