using System;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BoltgunCompletion {
public sealed class RadarControl:Control {
    TargetView[] targets=new TargetView[0];
    RadarLayout layout=new RadarLayout(new TargetView[0]);
    RadarDirections directions=new RadarDirections(new TargetView[0]);
    readonly PointF[] directionPoints=new PointF[6];
    bool active,heading;
    int range=100;
    internal float PlotDiameter {get{return Math.Max(10,Math.Min(Width,Height)-24);}}
    internal int VisibleTargetCount {get{return active&&heading?targets.Count(t=>RadarLayout.IsVisible(t,range)):0;}}
    internal RadarDirections OutsideEnemyDirections {get{directions.Arrange(range,active&&heading);return directions;}}
    internal void SetRange(int metres){range=metres;}
    internal void SetHeading(bool value){heading=value;}
    public RadarControl() {
        DoubleBuffered=true;ResizeRedraw=true;Dock=DockStyle.Fill;Margin=Padding.Empty;
        BackColor=Color.FromArgb(22,26,31);
    }
    public void UpdateTargets(TargetView[] secrets,TargetView[] enemies,int metres,bool show,bool hasHeading) {
        targets=(secrets??new TargetView[0]).Concat(enemies??new TargetView[0]).ToArray();
        layout=new RadarLayout(targets);
        directions=new RadarDirections(targets);
        range=metres;active=show;heading=hasHeading;Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);Draw(e.Graphics,new Rectangle(Point.Empty,Size),new RadarAppearance());
    }
    internal void Draw(Graphics g,Rectangle bounds,RadarAppearance appearance,bool showPlayer=true,double heightTolerance=2) {
        var state=g.Save();
        try {g.TranslateTransform(bounds.X,bounds.Y);DrawGrid(g,appearance);DrawMarkers(g,appearance,showPlayer,heightTolerance);}finally{g.Restore(state);}
    }
    internal void DrawBackdrop(Graphics g,Rectangle bounds,RadarAppearance appearance){var state=g.Save();try{g.TranslateTransform(bounds.X,bounds.Y);DrawGrid(g,appearance);}finally{g.Restore(state);}}
    internal void DrawTargets(Graphics g,Rectangle bounds,RadarAppearance appearance,bool showPlayer,double heightTolerance){var state=g.Save();try{g.TranslateTransform(bounds.X,bounds.Y);DrawMarkers(g,appearance,showPlayer,heightTolerance);}finally{g.Restore(state);}}
    void DrawGrid(Graphics g,RadarAppearance appearance) {
        g.SmoothingMode=SmoothingMode.AntiAlias;
        float cx=Width/2f,cy=Height/2f,r=PlotDiameter/2;
        using(var grid=new Pen(RadarAppearance.Fade(Color.FromArgb(57,72,84),appearance.Rings))) {
            for(int i=1;i<=3;i++){float a=r*i/3;g.DrawEllipse(grid,cx-a,cy-a,a*2,a*2);}
        }
        using(var grid=new Pen(RadarAppearance.Fade(Color.FromArgb(57,72,84),appearance.Axes))) {
            g.DrawLine(grid,cx-r,cy,cx+r,cy);g.DrawLine(grid,cx,cy-r,cx,cy+r);
        }
    }
    void DrawMarkers(Graphics g,RadarAppearance appearance,bool showPlayer,double heightTolerance) {
        g.SmoothingMode=SmoothingMode.AntiAlias;
        float cx=Width/2f,cy=Height/2f,r=PlotDiameter/2;
        // No target, compass, distance, range or connection text on the radar.
        if(!active||!heading)return;
        var arranged=layout.Arrange(range,r);
        // Keep secret symbols above the larger direction chevrons at the rim.
        DrawEnemyDirections(g,cx,cy,r,appearance.Enemies);
        // Leaders are beneath every symbol. The endpoint on the outer circle is
        // the true target bearing, including when dense symbols have fanned out.
        // Keep connectors faint and below symbols, grouped by category and
        // verification state. Component opacity also applies to connectors.
        for(int group=0;group<4;group++)using(var path=new GraphicsPath()) {
            RadarMarker example=null;
            foreach(var marker in arranged)if(marker.Leader) {
                var t=marker.View;int key=(RadarLayout.IsSecret(t)?0:2)+(t.Target.known?0:1);if(key!=group)continue;
                example=marker;
                float ax=cx+(float)marker.AnchorRight,ay=cy-(float)marker.AnchorForward;
                path.StartFigure();path.AddLine(cx+(float)marker.Right,cy-(float)marker.Forward,ax,ay);
                float tx=(float)(marker.AnchorForward/r*2),ty=(float)(marker.AnchorRight/r*2);
                path.StartFigure();path.AddLine(ax-tx,ay-ty,ax+tx,ay+ty);
            }
            if(example!=null) {
                bool secret=RadarLayout.IsSecret(example.View);int opacity=secret?appearance.Secrets:appearance.Enemies;
                using(var pen=new Pen(RadarAppearance.Fade(TargetColor(example.View),(opacity*(secret?35:14)+50)/100),1))g.DrawPath(pen,path);
            }
        }
        foreach(var marker in arranged) {
            var t=marker.View;bool outside=marker.Outside;
            float x=cx+(float)marker.Right,y=cy-(float)marker.Forward;
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
    void DrawEnemyDirections(Graphics g,float cx,float cy,float radius,int opacity) {
        if(opacity==0)return;
        var hints=OutsideEnemyDirections;if(hints.Count==0)return;
        // Broad filled chevrons straddle the rim instead of growing the panel.
        // The notch distinguishes them from the triangles encoding elevation.
        using(var pen=new Pen(RadarAppearance.Fade(Color.FromArgb(28,12,12),opacity),3))
        using(var brush=new SolidBrush(RadarAppearance.Fade(Color.FromArgb(245,86,86),opacity))) {
            pen.LineJoin=LineJoin.Round;
            for(int i=0;i<hints.Count;i++) {
                double angle=hints.Bearings[i]*Math.PI/180;
                float x=(float)Math.Sin(angle),y=-(float)Math.Cos(angle);
                directionPoints[0]=new PointF(cx+x*(radius+14),cy+y*(radius+14));
                directionPoints[1]=new PointF(cx+x*(radius-3)-y*17,cy+y*(radius-3)+x*17);
                directionPoints[2]=new PointF(cx+x*(radius-8)-y*12,cy+y*(radius-8)+x*12);
                directionPoints[3]=new PointF(cx+x*(radius+4),cy+y*(radius+4));
                directionPoints[4]=new PointF(cx+x*(radius-8)+y*12,cy+y*(radius-8)-x*12);
                directionPoints[5]=new PointF(cx+x*(radius-3)+y*17,cy+y*(radius-3)-x*17);
                g.DrawPolygon(pen,directionPoints);
                g.FillPolygon(brush,directionPoints);
            }
        }
    }
    static Color TargetColor(TargetView t) {
        Color color=t.Code.StartsWith("秘",StringComparison.Ordinal)?Color.FromArgb(239,199,105):Color.FromArgb(245,86,86);
        // Uncertainty is dimmed independently of the requested range/fill semantics.
        return t.Target.known?color:Color.FromArgb(155,color);
    }
}
}
