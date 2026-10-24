using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using BoltgunCompletion;

// Compile with the production sources; no game connection or input is used.
internal static class RadarReplay {
    static void Require(bool condition,string message) {if(!condition)throw new Exception(message);}
    [STAThread] static int Main(string[] args) {
        try {
            var states=new JavaScriptSerializer().Deserialize<GameSnapshot[]>(File.ReadAllText(args[0]));
            string output=args[1];Directory.CreateDirectory(output);
            var samples=new List<double>();int index=0;
            foreach(var state in states) {
                var tracker=new TargetTracker();tracker.Update(state);
                var views=tracker.Secrets.Concat(tracker.Enemies).ToArray();
                var watch=Stopwatch.StartNew();var layout=new RadarLayout(views);var markers=layout.Arrange(100,190);watch.Stop();
                double placementMs=watch.Elapsed.TotalMilliseconds;
                var expected=tracker.Secrets.Concat(tracker.Enemies.Where(t=>t.HorizontalMetres<=100)).Select(t=>t.Target.id).OrderBy(id=>id,StringComparer.Ordinal).ToArray();
                Require(markers.Select(m=>m.View.Target.id).OrderBy(id=>id,StringComparer.Ordinal).SequenceEqual(expected),"Visible targets differ from all secrets plus in-range enemies");
                Require(markers.All(m=>!m.Outside||RadarLayout.IsSecret(m.View)),"An outside enemy is visible");
                int conflicts=0;double closest=Double.MaxValue;
                for(int i=0;i<markers.Length;i++) {
                    var m=markers[i];double angle=m.View.Bearing*Math.PI/180;
                    Require(Math.Abs(m.AnchorRight-Math.Sin(angle)*190)<0.000001&&Math.Abs(m.AnchorForward-Math.Cos(angle)*190)<0.000001,"Leader does not point to the real bearing");
                    if(!m.Outside) {
                        double x,y;bool outside;m.View.RadarPoint(100,out x,out y,out outside);
                        Require(Math.Abs(m.Right-x*190)<0.000001&&Math.Abs(m.Forward-y*190)<0.000001,"An in-range target was displaced");
                    }
                    for(int j=0;j<i;j++)if(m.Outside||markers[j].Outside) {
                        double dx=m.Right-markers[j].Right,dy=m.Forward-markers[j].Forward,dist=Math.Sqrt(dx*dx+dy*dy);
                        closest=Math.Min(closest,dist);if(dist<22-0.000001)conflicts++;
                    }
                }
                Console.WriteLine(state.level+" case "+index+": "+markers.Length+" visible / "+views.Length+" tracked, "+conflicts+" outside-symbol conflicts; closest "+closest.ToString("0.00")+"px; placement "+placementMs.ToString("0.00")+"ms");
                Require(conflicts==0,"Captured scene still has overlapping outside symbols");
                var settings=new Settings {Opacity=100,PinCenter=true,ShowPlayerArrow=false};settings.Appearance.Background=0;
                using(var form=new OverlayForm(settings)) {
                    form.UpdateState(state.level,"ready","",state,true);
                    using(var image=form.RenderBitmap())image.Save(Path.Combine(output,"scene-"+index+".png"));
                }
                using(var radar=new RadarControl {Size=new Size(404,416)})using(var image=new Bitmap(404,416))using(var g=Graphics.FromImage(image)) {
                    radar.UpdateTargets(tracker.Secrets,tracker.Enemies,100,true,true);
                    var expectedDirections=new List<double>();
                    foreach(var enemy in state.enemies.Where(t=>t.position!=null)) {
                        double dx=enemy.position.x-state.player.x,dy=enemy.position.y-state.player.y;
                        if(Math.Sqrt(dx*dx+dy*dy)<=10000)continue;
                        double bearing=Math.Atan2(dy,dx)*180/Math.PI-(state.yaw??0);
                        expectedDirections.Add(bearing);
                    }
                    var directions=radar.OutsideEnemyDirections;
                    var bearings=directions.Bearings.Take(directions.Count).ToArray();
                    Require(expectedDirections.All(a=>bearings.Any(b=>RadarDirections.Gap(a,b)<RadarDirections.MergeDegrees)),"A distant enemy direction is missing from the continuous hints");
                    Require(bearings.All(a=>expectedDirections.Any(b=>RadarDirections.Gap(a,b)<0.000001)),"A hint no longer follows an actual enemy bearing");
                    for(int i=0;i<bearings.Length;i++)for(int j=0;j<i;j++)Require(RadarDirections.Gap(bearings[i],bearings[j])>=RadarDirections.MergeDegrees-0.000001,"Near-coincident directional hints did not merge");
                    for(int i=-20;i<180;i++) {
                        tracker.UpdatePose(state.player,(state.yaw??0)+i*2);
                        watch.Restart();g.Clear(Color.Transparent);radar.Draw(g,new Rectangle(Point.Empty,radar.Size),settings.Appearance,false,3);watch.Stop();
                        if(i>=0)samples.Add(watch.Elapsed.TotalMilliseconds);
                    }
                }
                index++;
            }
            samples.Sort();Console.WriteLine("DRAW_MEAN_MS="+samples.Average().ToString("0.00")+" DRAW_P95_MS="+samples[(int)(samples.Count*.95)].ToString("0.00"));
            Console.WriteLine("RADAR_REPLAY_PASSED");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
