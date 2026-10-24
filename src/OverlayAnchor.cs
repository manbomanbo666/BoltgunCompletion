using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace BoltgunCompletion {
internal static class OverlayAnchor {
    [StructLayout(LayoutKind.Sequential)] struct Rect {public int left,top,right,bottom;}
    [StructLayout(LayoutKind.Sequential)] struct Point {public int x,y;}
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr window,out Rect rectangle);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr window,ref Point point);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
    internal static System.Drawing.Point Center(Rectangle target,Rectangle radar) {
        return new System.Drawing.Point(target.Left+target.Width/2-radar.Left-radar.Width/2,target.Top+target.Height/2-radar.Top-radar.Height/2);
    }
    internal static System.Drawing.Point SettingsPosition(Rectangle area,Rectangle overlay,System.Drawing.Size size) {
        var candidates=new[]{new System.Drawing.Point(overlay.Right+12,overlay.Top),new System.Drawing.Point(overlay.Left-size.Width-12,overlay.Top),
            new System.Drawing.Point(overlay.Left,overlay.Top-size.Height-12),new System.Drawing.Point(overlay.Left,overlay.Bottom+12)};
        foreach(var point in candidates)if(area.Contains(new Rectangle(point,size)))return point;
        return new System.Drawing.Point(area.Left+Math.Max(0,Math.Min(12,area.Width-size.Width)),area.Top+Math.Max(0,Math.Min(12,area.Height-size.Height)));
    }
    internal static bool ClientBounds(IntPtr window,out Rectangle bounds) {
        bounds=Rectangle.Empty;Rect rect;var origin=new Point();
        if(window==IntPtr.Zero||IsIconic(window)||!GetClientRect(window,out rect)||rect.right<=rect.left||rect.bottom<=rect.top||!ClientToScreen(window,ref origin))return false;
        bounds=new Rectangle(origin.x,origin.y,rect.right-rect.left,rect.bottom-rect.top);return true;
    }
}
}
