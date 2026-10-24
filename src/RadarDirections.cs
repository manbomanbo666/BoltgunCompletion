using System;
using System.Linq;

namespace BoltgunCompletion {
// Continuous bearings, not compass sectors. Keep one real enemy bearing for
// near-coincident directions, using stable target order to avoid distance swaps.
internal sealed class RadarDirections {
    internal const double MergeDegrees=12;
    readonly TargetView[] enemies;
    internal readonly double[] Bearings;
    internal int Count {get;private set;}
    internal RadarDirections(TargetView[] targets) {
        enemies=targets.Where(t=>!RadarLayout.IsSecret(t)).OrderBy(t=>t.Number)
            .ThenBy(t=>t.Target.id??t.Code,StringComparer.Ordinal).ToArray();
        Bearings=new double[enemies.Length];
    }
    internal static double Gap(double a,double b){return Math.Abs(((a-b)%360+540)%360-180);}
    internal void Arrange(double range,bool available) {
        Count=0;if(!available)return;
        foreach(var enemy in enemies) {
            if(!enemy.HasHeading||enemy.InRange(range))continue;
            double bearing=(enemy.Bearing%360+540)%360-180;
            bool covered=false;
            for(int i=0;i<Count;i++)if(Gap(bearing,Bearings[i])<MergeDegrees){covered=true;break;}
            if(!covered)Bearings[Count++]=bearing;
        }
    }
}
}
