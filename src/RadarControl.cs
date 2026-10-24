using System;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BoltgunCompletion {
public sealed class RadarControl:Control {
    TargetView[] targets=new TargetView[0];
    bool active,heading;
    int range=100;
    internal float PlotDiameter {get{return Math.Max(10,Math.Min(Width,Height)-24);}}
    internal int VisibleTargetCount {get{return active&&heading?targets.Length:0;}}
    public RadarControl() {
        DoubleBuffered=true;ResizeRedraw=true;Dock=DockStyle.Fill;Margin=Padding.Empty;
        BackColor=Color.FromArgb(22,26,31);
    }
    public void UpdateTargets(TargetView[] secrets,TargetView[] enemies,int metres,bool show,bool hasHeading) {
        targets=(secrets??new TargetView[0]).Concat(enemies??new TargetView[0]).ToArray();
        range=metres;active=show;heading=hasHeading;Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);Draw(e.Graphics,new Rectangle(Point.Empty,Size),new RadarAppearance());
    }
    internal void Draw(Graphics g,Rectangle bounds,RadarAppearance appearance,bool showPlayer=true,double heightTolerance=2) {
        var state=g.Save();
        try {g.TranslateTransform(bounds.X,bounds.Y);DrawLayers(g,appearance,showPlayer,heightTolerance);}finally{g.Restore(state);}
    }
    void DrawLayers(Graphics g,RadarAppearance appearance,bool showPlayer,double heightTolerance) {
        g.SmoothingMode=SmoothingMode.AntiAlias;
        float cx=Width/2f,cy=Height/2f,r=PlotDiameter/2;
        using(var grid=new Pen(RadarAppearance.Fade(Color.FromArgb(57,72,84),appearance.Rings))) {
            for(int i=1;i<=3;i++){float a=r*i/3;g.DrawEllipse(grid,cx-a,cy-a,a*2,a*2);}
        }
        using(var grid=new Pen(RadarAppearance.Fade(Color.FromArgb(57,72,84),appearance.Axes))) {
            g.DrawLine(grid,cx-r,cy,cx+r,cy);g.DrawLine(grid,cx,cy-r,cx,cy+r);
        }
        // No target, compass, distance, range or connection text on the radar.
        if(!active||!heading)return;
        foreach(var t in targets.Reverse()) {
            double right,forward;bool outside;t.RadarPoint(range,out right,out forward,out outside);
            float x=cx+(float)right*r,y=cy-(float)forward*r;
            Color accent=RadarAppearance.Fade(TargetColor(t),t.Code.StartsWith("秘",StringComparison.Ordinal)?appearance.Secrets:appearance.Enemies);
            using(var pen=new Pen(accent,2))using(var brush=new SolidBrush(accent)) {
                int band=t.BandAt(heightTolerance);
                if(band==0) {
                    if(!outside)g.FillEllipse(brush,x-6,y-6,12,12);else g.DrawEllipse(pen,x-6,y-6,12,12);
                } else {
                    // Triangle orientation encodes elevation only, never the bearing.
                    float up=band>0?-1:1;
                    var triangle=new[]{new PointF(x,y+up*9),new PointF(x+8,y-up*6),new PointF(x-8,y-up*6)};
                    if(!outside)g.FillPolygon(brush,triangle);else g.DrawPolygon(pen,triangle);
                }
            }
        }
        if(showPlayer)using(var player=new SolidBrush(RadarAppearance.Fade(Color.White,appearance.Player)))g.FillPolygon(player,new[]{new PointF(cx,cy-10),new PointF(cx+7,cy+7),new PointF(cx,cy+3),new PointF(cx-7,cy+7)});
    }
    static Color TargetColor(TargetView t) {
        Color color=t.Code.StartsWith("秘",StringComparison.Ordinal)?Color.FromArgb(239,199,105):Color.FromArgb(126,204,226);
        // Uncertainty is dimmed independently of the requested range/fill semantics.
        return t.Target.known?color:Color.FromArgb(155,color);
    }
}
}
