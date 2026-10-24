using System;
using System.Drawing;

namespace BoltgunCompletion {
public sealed class RadarAppearance {
    public int Background=100,Rings=100,Axes=100,Secrets=100,Enemies=100,Player=100,Counter=100;
    public static int Clamp(int value) {return Math.Max(0,Math.Min(100,value));}
    public void Normalize() {
        Background=Clamp(Background);Rings=Clamp(Rings);Axes=Clamp(Axes);Secrets=Clamp(Secrets);
        Enemies=Clamp(Enemies);Player=Clamp(Player);Counter=Clamp(Counter);
    }
    public static Color Fade(Color color,int percent) {return Color.FromArgb((color.A*Clamp(percent)+50)/100,color);}
    public static byte GlobalAlpha(int percent) {return (byte)((255*Clamp(percent)+50)/100);}
}
}
