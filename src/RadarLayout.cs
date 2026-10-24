using System;
using System.Linq;
using System.Collections.Generic;

namespace BoltgunCompletion {
internal sealed class RadarMarker {
    internal TargetView View;
    internal double Right,Forward;
    internal double AnchorRight,AnchorForward;
    internal double AngleOffset,TrackRadius;
    internal bool Outside;
    internal bool Visible {get{return !Outside||RadarLayout.IsSecret(View);}}
    internal bool Leader {get{return Outside&&(Math.Abs(AngleOffset)>0.000001||Math.Abs(TrackRadius-AnchorRadius)>0.01);}}
    internal double AnchorRadius;
}

// Only outside symbols may move for legibility. Their anchors retain the exact
// bearing; the renderer draws a leader from displaced symbols to that anchor.
internal sealed class RadarLayout {
    internal const double Separation=22;
    const int Tracks=6;
    readonly RadarMarker[] markers;
    RadarMarker[] paintOrder;
    bool placed;
    double lastRange,lastRadius;
    struct Candidate {internal double Radius,Angle,Cost;}
    internal RadarLayout(TargetView[] targets) {
        markers=targets.OrderBy(t=>IsSecret(t)?0:1).ThenBy(t=>t.Number)
            .ThenBy(t=>t.Target.id??t.Code,StringComparer.Ordinal)
            .Select(t=>new RadarMarker {View=t}).ToArray();
        paintOrder=new RadarMarker[markers.Length];
    }
    internal static bool IsSecret(TargetView view){return view.Code.StartsWith("秘",StringComparison.Ordinal);}
    internal static bool IsVisible(TargetView view,double range){return IsSecret(view)||view.InRange(range);}
    internal RadarMarker[] Arrange(double range,double radius) {
        bool changed=!placed||range!=lastRange||radius!=lastRadius;
        foreach(var marker in markers) {
            double right,forward;bool outside;marker.View.RadarPoint(range,out right,out forward,out outside);
            changed|=outside!=marker.Outside;
            marker.Right=right*radius;marker.Forward=forward*radius;marker.Outside=outside;
            double angle=marker.View.Bearing*Math.PI/180;
            marker.AnchorRight=Math.Sin(angle)*radius;marker.AnchorForward=Math.Cos(angle)*radius;marker.AnchorRadius=radius;
        }
        if(changed)Place(radius);
        // Reuse the full-snapshot layout while animating position/yaw. No dense
        // collision search on the 30 FPS drawing path. Range crossings reflow.
        foreach(var marker in markers)if(marker.Visible&&marker.Outside) {
            double angle=(marker.View.Bearing+marker.AngleOffset)*Math.PI/180;
            marker.Right=Math.Sin(angle)*marker.TrackRadius;marker.Forward=Math.Cos(angle)*marker.TrackRadius;
        }
        placed=true;lastRange=range;lastRadius=radius;
        int count=markers.Count(m=>m.Visible);
        if(paintOrder.Length!=count)paintOrder=new RadarMarker[count];
        int index=0;
        for(int i=markers.Length-1;i>=0;i--)if(markers[i].Visible&&markers[i].Outside)paintOrder[index++]=markers[i];
        for(int i=markers.Length-1;i>=0;i--)if(!markers[i].Outside)paintOrder[index++]=markers[i];
        return paintOrder;
    }
    void Place(double radius) {
        var candidates=new List<Candidate>();
        for(int track=0;track<Tracks;track++) {
            double r=Math.Max(radius*0.4,radius-track*Separation),inset=radius-r;
            for(int degrees=0;degrees<=90;degrees+=3) {
                double arc=r*degrees*Math.PI/180,cost=inset*inset+arc*arc;
                candidates.Add(new Candidate {Radius=r,Angle=degrees,Cost=cost});
                if(degrees>0)candidates.Add(new Candidate {Radius=r,Angle=-degrees,Cost=cost});
            }
        }
        // Prefer the smallest displacement. On equal cost preserve the bearing
        // before moving sideways. Stable target order is independent of distance.
        var slots=candidates.OrderBy(c=>c.Cost).ThenBy(c=>Math.Abs(c.Angle)).ThenBy(c=>c.Angle).ToArray();
        for(int i=0;i<markers.Length;i++) {
            var marker=markers[i];if(!marker.Visible||!marker.Outside)continue;
            Candidate best=slots[0];double bestClearance=-1;
            foreach(var candidate in slots) {
                double angle=(marker.View.Bearing+candidate.Angle)*Math.PI/180;
                double x=Math.Sin(angle)*candidate.Radius,y=Math.Cos(angle)*candidate.Radius,clearance=Double.MaxValue;
                for(int j=0;j<markers.Length;j++) {
                    var other=markers[j];if(j==i||!other.Visible||(other.Outside&&j>i))continue;
                    double dx=x-other.Right,dy=y-other.Forward;
                    clearance=Math.Min(clearance,dx*dx+dy*dy);
                    if(clearance<Separation*Separation&&clearance<=bestClearance)break;
                }
                if(clearance>bestClearance+0.000001){bestClearance=clearance;best=candidate;}
                if(clearance>=Separation*Separation-0.000001){best=candidate;break;}
            }
            marker.TrackRadius=best.Radius;marker.AngleOffset=best.Angle;
            double chosen=(marker.View.Bearing+best.Angle)*Math.PI/180;
            marker.Right=Math.Sin(chosen)*best.Radius;marker.Forward=Math.Cos(chosen)*best.Radius;
        }
    }
}
}
