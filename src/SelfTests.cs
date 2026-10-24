using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using System.Drawing;
using System.Windows.Forms;

namespace BoltgunCompletion {
public static class SelfTests {
    public static int Render(string path) {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        return RenderMain(path);
    }
    static int RenderMain(string path) {
        using(var form=new MainForm(GameLocator.Find(),false)) {
            form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-32000,-32000);
            form.Show();Application.DoEvents();
            using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(path);}
            form.Close();
        }
        return 0;
    }
    static void Check(bool condition,string name) {if(!condition)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);}
    static string Row(long seq,string mode,int generation) {return new JavaScriptSerializer().Serialize(new GameSnapshot {v=1,session="test",seq=seq,generation=generation,build="14893097",pluginVersion="0.1.0",revision=AppPaths.Revision,mode=mode});}
    static TargetInfo Target(string id,double x,double y,double z,bool known=true) {return new TargetInfo {id=id,known=known,position=new Point3 {x=x,y=y,z=z}};}
    static void RadarTests() {
        var tracker=new TargetTracker();
        var s=new GameSnapshot {session="radar",world="world",level="C1M8",generation=1,mode="campaign",player=new Point3(),yaw=0,secretsFound=1,secretsTotal=6,
            secrets=new[]{Target("a",1000,0,1200),Target("b",0,2000,-500,false),Target("c",-3000,0,0),Target("d",0,-4000,0)},enemies=new[]{Target("e",1000,1000,0)}};
        tracker.Update(s);var a=Array.Find(tracker.Secrets,t=>t.Target.id=="a");int stable=a.Number;
        double right,forward;bool outside;a.RadarPoint(100,out right,out forward,out outside);
        Check(a.Direction=="前方"&&a.Arrow=="↑"&&Math.Abs(right)<0.00001&&Math.Abs(forward-0.1)<0.00001&&!outside,"radar front and horizontal distance ignore vertical separation");
        Check(a.HeightText=="高 12.0m"&&Array.Find(tracker.Secrets,t=>t.Target.id=="b").HeightText=="低 5.0m","explicit height above and below player");
        Check(tracker.Secrets.Length==4&&TargetTracker.Details(tracker.Secrets,"秘密").Contains("状态待核实")&&TargetTracker.Summary(s,tracker.Secrets,true).Contains("雷达 4 个"),"all located targets retained beyond three detail rows, uncertainty preserved");
        Check(tracker.Enemies[0].Code=="敌1"&&a.Code=="秘1","combined radar uses distinct category prefixes");
        s.player.x=1800;s.yaw=90;Array.Reverse(s.secrets);tracker.Update(s);
        a=Array.Find(tracker.Secrets,t=>t.Target.id=="a");
        Check(a.Number==stable,"target numbers survive movement and array reordering");
        var turn=new TargetView {Bearing=-90,HasHeading=true,HorizontalMetres=200};turn.RadarPoint(100,out right,out forward,out outside);
        Check(turn.Arrow=="←"&&Math.Abs(right+1)<0.00001&&outside,"out-of-range target stays on correct radar edge");
        var east=new TargetTracker();s.player=new Point3();s.yaw=90;east.Update(s);
        Check(Array.Find(east.Secrets,t=>t.Target.id=="a").Arrow=="←"&&Array.Find(east.Secrets,t=>t.Target.id=="b").Direction=="前方","player yaw rotates world targets into relative directions");
        s.yaw=450;east.Update(s);Check(Array.Find(east.Secrets,t=>t.Target.id=="b").Direction=="前方","yaw wrap is consistent");
        s.yaw=null;east.Update(s);Check(east.Secrets[0].Direction=="方向待核实"&&east.Secrets[0].Arrow=="?","unknown heading never fabricates a direction");
        s.secrets=new[]{Target("new",0,0,0)};tracker.Update(s);Check(tracker.Secrets[0].Number==5,"disappeared IDs are not reassigned within the same scene");
        s.secrets=new[]{Target("a",1000,0,1200)};tracker.Update(s);Check(tracker.Secrets[0].Number==stable,"returning target keeps its original number");
        s.generation++;tracker.Update(s);Check(tracker.Secrets[0].Number==1,"retry clears target numbering");
        s.session="new-session";s.secrets=new[]{Target("other",0,0,0)};tracker.Update(s);Check(tracker.Secrets[0].Number==1,"new runtime session clears numbering");
        s.enemies=new TargetInfo[0];tracker.Update(s);Check(TargetTracker.Details(tracker.Enemies,"怪物").Contains("波次仍待核实"),"no visible enemies never implies all waves cleared");
        Check(OverlayForm.ValidRange(0)==100&&OverlayForm.ValidRange(500)==500,"saved radar range is validated");
        s.player=null;tracker.Update(s);Check(tracker.Secrets.Length==0&&tracker.Enemies.Length==0,"missing player position never plots guessed target bearings");
        var progress=new GameSnapshot {mode="campaign",killed=119,killPercent=null,secretsFound=4,secretsTotal=6};
        Check(DisplayText.CompactProgress(progress).Contains("参考击杀 待核实")&&!DisplayText.CompactProgress(progress).Contains("完美通关"),"compact progress preserves unknown estimate without declaring perfection");
        Check(new TargetView {HeightMetres=1.99}.HeightBand==0&&new TargetView {HeightMetres=2}.HeightBand==1&&new TargetView {HeightMetres=-2}.HeightBand==-1,"relative elevation uses the two-metre boundary");
        Check(new TargetView {HorizontalMetres=100}.InRange(100)&&!new TargetView {HorizontalMetres=100.01}.InRange(100),"range boundary belongs to filled in-range markers");
        MarkerVisualTest();AppearanceTests();
    }
    static RadarAppearance Invisible() {return new RadarAppearance {Background=0,Rings=0,Axes=0,Secrets=0,Enemies=0,Player=0,Counter=0};}
    static int VisiblePixels(Bitmap bitmap,Rectangle area) {int count=0;for(int y=area.Top;y<area.Bottom;y++)for(int x=area.Left;x<area.Right;x++)if(bitmap.GetPixel(x,y).A>0)count++;return count;}
    static void AppearanceTests() {
        var legacy=Settings.FromJson("{\"Opacity\":88,\"OverlayX\":123}");
        Check(legacy.Opacity==88&&legacy.OverlayX==123&&legacy.Appearance.Background==100&&legacy.Appearance.Counter==100&&legacy.HeightTolerance==5&&legacy.ShowPlayerArrow&&!legacy.PinCenter,"old settings retain opacity and position with independent-layer defaults");
        var invalid=Settings.FromJson("{\"Opacity\":-1,\"Appearance\":{\"Background\":-2,\"Secrets\":101,\"Enemies\":0}}");
        Check(invalid.Opacity==0&&invalid.Appearance.Background==0&&invalid.Appearance.Secrets==100&&invalid.Appearance.Enemies==0&&invalid.Appearance.Rings==100,"component percentages are clamped while absent fields retain defaults");
        Check(Settings.FromJson("{\"Appearance\":null}").Appearance.Player==100,"null legacy appearance is restored");
        Check(RadarAppearance.GlobalAlpha(0)==0&&RadarAppearance.GlobalAlpha(100)==255&&RadarAppearance.GlobalAlpha(50)==128,"overall opacity is a separate window-wide multiplier");
        Check(Settings.FromJson("{\"HeightTolerance\":0}").HeightTolerance==0.5&&Settings.FromJson("{\"HeightTolerance\":100}").HeightTolerance==30,"height tolerance is limited to the adjustable range");
        Check(new TargetView {HeightMetres=3}.BandAt(5)==0&&new TargetView {HeightMetres=5}.BandAt(5)==1&&new TargetView {HeightMetres=-5}.BandAt(5)==-1,"five-metre tolerance reduces premature upper and lower floor markers");
        var centered=OverlayAnchor.Center(new Rectangle(100,200,1280,720),new Rectangle(8,36,404,416));
        Check(centered.X+210==740&&centered.Y+244==560,"pinning aligns radar centre rather than the enclosing window centre");
        centered=OverlayAnchor.Center(new Rectangle(-1920,0,1920,1080),new Rectangle(8,36,404,416));
        Check(centered.X+210==-960&&centered.Y+244==540,"centering handles a monitor left of the primary display");
        var radarBounds=new Rectangle(750,296,420,460);var dialogSize=new Size(486,449);
        var dialogPoint=OverlayAnchor.SettingsPosition(new Rectangle(0,0,1920,1040),radarBounds,dialogSize);
        Check(!new Rectangle(dialogPoint,dialogSize).IntersectsWith(radarBounds),"settings window stays beside a centred radar when space permits");
        var settings=new Settings {Opacity=100,Appearance=Invisible()};
        var state=new GameSnapshot {session="alpha",world="world",level="C1M8",mode="campaign",player=new Point3(),yaw=0,
            secrets=new[]{Target("secret",1000,0,0)},enemies=new[]{Target("enemy",0,1000,0)}};
        using(var form=new OverlayForm(settings)) {
            form.UpdateState("C1M8","ready","",state,true);
            using(var image=form.RenderBitmap())Check(VisiblePixels(image,new Rectangle(Point.Empty,image.Size))==0,"all components at zero produce fully transparent pixels");
            settings.Appearance.Background=50;
            using(var image=form.RenderBitmap())Check(image.GetPixel(0,0).A==128,"background alpha is genuine partial transparency");
            settings.Appearance.Background=0;settings.Appearance.Secrets=100;
            using(var image=form.RenderBitmap())Check(image.GetPixel(210,225).A==255&&image.GetPixel(229,244).A==0&&image.GetPixel(0,0).A==0,"secret remains opaque with invisible background and enemies");
            settings.Appearance.Secrets=50;
            using(var image=form.RenderBitmap())Check(image.GetPixel(210,225).A==128,"secret opacity does not depend on background opacity");
            settings.Appearance.Secrets=0;settings.Appearance.Enemies=100;
            using(var image=form.RenderBitmap())Check(image.GetPixel(229,244).A==255&&image.GetPixel(210,225).A==0,"enemy opacity is independent of secret opacity");
            settings.Appearance.Enemies=0;settings.Appearance.Player=100;
            using(var image=form.RenderBitmap())Check(image.GetPixel(210,244).A==255,"player arrow can be the only visible radar component");
            settings.ShowPlayerArrow=false;
            using(var image=form.RenderBitmap())Check(image.GetPixel(210,244).A==0,"centre arrow can be disabled independently");
            settings.ShowPlayerArrow=true;var freePosition=form.Location;form.SetPinned(true);
            using(var image=form.RenderBitmap())Check(image.GetPixel(210,244).A==0,"pinning suppresses the centre arrow even when selected");
            form.SetPinned(false);
            using(var image=form.RenderBitmap())Check(image.GetPixel(210,244).A==255&&form.Location==freePosition&&settings.ShowPlayerArrow,"unpinning restores the free position and previous arrow preference");
            settings.Appearance.Player=0;settings.Appearance.Counter=100;
            using(var image=form.RenderBitmap())Check(VisiblePixels(image,new Rectangle(8,8,404,28))>0&&image.GetPixel(210,244).A==0,"enemy count can be visible independently of player arrow");
            settings.Appearance.Counter=0;settings.Appearance.Rings=100;
            using(var image=form.RenderBitmap())Check(VisiblePixels(image,new Rectangle(8,36,404,416))>0&&image.GetPixel(210,244).A==0,"rings can remain visible with axes hidden");
            settings.Appearance.Rings=0;settings.Appearance.Axes=100;
            using(var image=form.RenderBitmap())Check(VisiblePixels(image,new Rectangle(8,36,404,416))>0,"crosshair axes are independently visible");
            var restored=Settings.FromJson(new JavaScriptSerializer().Serialize(settings));
            Check(restored.Appearance.Axes==100&&restored.Appearance.Rings==0&&restored.Appearance.Background==0,"component settings survive serialization");
        }
    }
    static void MarkerVisualTest() {
        var markers=new TargetView[6];
        for(int i=0;i<markers.Length;i++)markers[i]=new TargetView {Code=i%2==0?"秘":"敌",Target=new TargetInfo {known=i!=0},
            HasHeading=true,Bearing=30+(i%3)*120,HorizontalMetres=i<3?50:150,HeightMetres=new[]{3.0,0,-3.0}[i%3]};
        foreach(var marker in markers.Where(m=>!m.InRange(100)))marker.Code="秘";
        using(var radar=new RadarControl {Size=new Size(400,400)})using(var bitmap=new Bitmap(400,400)) {
            radar.UpdateTargets(markers,null,100,true,true);radar.CreateControl();radar.DrawToBitmap(bitmap,new Rectangle(0,0,400,400));
            foreach(var marker in markers) {
                double right,forward;bool outside;marker.RadarPoint(100,out right,out forward,out outside);
                int x=(int)Math.Round(200+right*radar.PlotDiameter/2),y=(int)Math.Round(200-forward*radar.PlotDiameter/2);
                var pixel=bitmap.GetPixel(x,y);bool filled=Math.Max(pixel.R,Math.Max(pixel.G,pixel.B))>100;
                Check(filled==!outside,"rendered "+marker.HeightBand+" elevation marker is "+(outside?"hollow outside range":"filled inside range"));
            }
        }
    }
    static void LayoutTests() {
        var tracker=new TargetTracker();
        var state=new GameSnapshot {session="edge",world="world",level="C2M4",mode="campaign",player=new Point3(),yaw=0,
            secrets=new[]{Target("secret",25000,0,0),Target("secret-3",30000,0,-900),Target("secret-1",40000,0,900),Target("secret-2",50000,0,0)},
            enemies=new TargetInfo[0]};
        tracker.Update(state);
        var views=tracker.Secrets.Concat(tracker.Enemies).ToArray();var layout=new RadarLayout(views);
        var markers=layout.Arrange(100,190);var positions=new Dictionary<string,PointF>();
        foreach(var marker in markers)positions[marker.View.Target.id]=new PointF((float)marker.Right,(float)marker.Forward);
        bool separated=true;
        for(int i=0;i<markers.Length;i++)for(int j=0;j<i;j++) {
            double dx=markers[i].Right-markers[j].Right,dy=markers[i].Forward-markers[j].Forward;
            separated&=dx*dx+dy*dy>=RadarLayout.Separation*RadarLayout.Separation-0.000001;
        }
        Check(separated&&markers.All(m=>m.Outside),"four co-bearing outside targets occupy separate hollow positions");
        Check(markers.All(m=>Math.Abs(m.AnchorRight)<0.00001&&Math.Abs(m.AnchorForward-190)<0.00001&&Math.Sqrt(m.Right*m.Right+m.Forward*m.Forward)>=76),"displaced symbols retain an exact bearing anchor in a bounded band");
        Check(Math.Abs(positions["secret"].Y-190)<0.00001&&markers[markers.Length-1].View.Target.id=="secret","first secret keeps the outer track in a dense secret cluster");
        Array.Reverse(views);var reordered=new RadarLayout(views).Arrange(100,190);
        Check(reordered.All(m=>Math.Abs(m.Right-positions[m.View.Target.id].X)<0.00001&&Math.Abs(m.Forward-positions[m.View.Target.id].Y)<0.00001),"snapshot array reordering cannot swap edge tracks");
        tracker.UpdatePose(new Point3(),73);var rotated=layout.Arrange(100,190);
        Check(rotated.All(m=>{var p=positions[m.View.Target.id];return Math.Abs(Math.Sqrt(m.Right*m.Right+m.Forward*m.Forward)-Math.Sqrt(p.X*p.X+p.Y*p.Y))<0.0001;}),"turning in place rotates markers without changing collision tracks");
        Check(Object.ReferenceEquals(markers,rotated),"animated layout reuses its marker and paint-order buffers");
        state.secrets=new[]{state.secrets[0]};state.enemies=new[]{Target("inside",10000,0,0)};tracker.Update(state);
        markers=new RadarLayout(tracker.Secrets.Concat(tracker.Enemies).ToArray()).Arrange(100,190);
        var inside=Array.Find(markers,m=>!m.Outside);var outside=Array.Find(markers,m=>m.Outside);
        Check(inside.View.Target.id=="inside"&&Math.Abs(inside.Right)<0.00001&&Math.Abs(inside.Forward-190)<0.00001&&Object.ReferenceEquals(markers[markers.Length-1],inside),"in-range target retains its exact distance and paints after edge markers");
        Check(Math.Abs(outside.Forward-168)<0.00001,"outside marker leaves room for an in-range target on the boundary");
        state.enemies=new TargetInfo[0];tracker.Update(state);
        markers=new RadarLayout(tracker.Secrets).Arrange(100,190);
        Check(Math.Abs(markers[0].Forward-190)<0.00001,"isolated outside target stays on the original edge");
        state.secrets=state.secrets.Concat(Enumerable.Range(1,30).Select(i=>Target("dense-"+i,30000+i*100,0,0))).ToArray();tracker.Update(state);
        markers=new RadarLayout(tracker.Secrets.Concat(tracker.Enemies).ToArray()).Arrange(100,190);
        Check(markers.Length==31&&markers.All(m=>m.Outside&&Math.Abs(m.AnchorRight)<0.00001&&Math.Abs(m.AnchorForward-190)<0.00001)&&markers[markers.Length-1].View.Target.id=="secret","dense bearing retains all targets with truthful leader anchors");
        Check(NoOutsideOverlap(markers),"thirty-one co-bearing symbols no longer collapse onto four tracks");
        Check(markers.Any(m=>m.Leader&&Math.Abs(m.AngleOffset)>0),"dense targets have a leader when their symbol is displaced sideways");
        var denseLayout=new RadarLayout(tracker.Secrets.Concat(tracker.Enemies).ToArray());denseLayout.Arrange(100,190);
        tracker.UpdatePose(new Point3(),179);var turned=denseLayout.Arrange(100,190);
        double anchorAngle=-179*Math.PI/180;
        Check(turned.All(m=>Math.Abs(m.AnchorRight-Math.Sin(anchorAngle)*190)<0.00001&&Math.Abs(m.AnchorForward-Math.Cos(anchorAngle)*190)<0.00001),"turning rotates exact bearing anchors with the player");
        tracker.UpdatePose(new Point3(),0);
        using(var radar=new RadarControl {Size=new Size(404,416)})using(var image=new Bitmap(404,416))using(var g=Graphics.FromImage(image)) {
            radar.UpdateTargets(tracker.Secrets,tracker.Enemies,100,true,true);
            var appearance=Invisible();appearance.Secrets=100;appearance.Enemies=100;radar.Draw(g,new Rectangle(Point.Empty,radar.Size),appearance,false,5);
            Color secretEdge=image.GetPixel(208,18);
            Check(secretEdge.R>secretEdge.B&&secretEdge.A>0,"gold secret outline remains visible in a dense secret cluster");
        }
        RangeVisibilityTests();
    }
    static void RangeVisibilityTests() {
        var state=new GameSnapshot {session="range",world="world",level="C2M7",mode="campaign",player=new Point3(),yaw=0,
            secrets=new[]{Target("distant-secret",30000,0,0)},enemies=new[]{Target("enemy",15000,0,0)}};
        var tracker=new TargetTracker();tracker.Update(state);int number=tracker.Enemies[0].Number;
        var layout=new RadarLayout(tracker.Secrets.Concat(tracker.Enemies).ToArray());
        var markers=layout.Arrange(100,190);
        Check(markers.Length==1&&markers[0].View.Target.id=="distant-secret"&&markers[0].Outside,"outside enemy is hidden while a distant secret remains hollow and visible");
        Check(layout.Arrange(250,190).Length==2&&layout.Arrange(100,190).Length==1,"changing range immediately shows and hides the cached enemy");
        tracker.UpdatePose(new Point3 {x=5000},0);
        Check(layout.Arrange(100,190).Length==2&&tracker.Enemies[0].Number==number,"movement to the exact range boundary reveals the same enemy without a new snapshot");
        tracker.UpdatePose(new Point3 {x=4999},0);
        Check(layout.Arrange(100,190).Length==1&&state.enemies.Length==1&&state.enemies[0].known,"moving outside range hides the marker without removing the confirmed enemy");
        var settings=new Settings {Appearance=Invisible(),ShowPlayerArrow=false};settings.Appearance.Enemies=100;settings.Appearance.Counter=100;
        using(var form=new OverlayForm(settings)) {
            form.UpdateState(state.level,"ready","",state,true);
            using(var image=form.RenderBitmap())Check(VisiblePixels(image,new Rectangle(8,36,404,416))>0&&form.Radar.OutsideEnemyDirections.Count==1&&form.TrackingText.Contains("存活敌人：1"),"outside enemy has a coarse direction while remaining in the area count");
        }
        EnemyDirectionTests();
    }
    static void EnemyDirectionTests() {
        var enemies=Enumerable.Range(0,80).Select(i=>new TargetView {Code="敌"+i,Number=i,Target=new TargetInfo {known=true},HasHeading=true,HorizontalMetres=150,Bearing=(i%8)*45}).ToArray();
        using(var radar=new RadarControl {Size=new Size(404,416)}) {
            radar.UpdateTargets(null,enemies,100,true,true);
            Check(radar.VisibleTargetCount==0&&radar.OutsideEnemyDirections.Count==8,"coincident distant enemies share hints without individual symbols");
            var appearance=Invisible();appearance.Enemies=100;
            var bounds=new Rectangle(8,36,404,416);
            using(var image=new Bitmap(420,460))using(var g=Graphics.FromImage(image)) {
                radar.DrawTargets(g,bounds,appearance,false,3);
                bool allDirections=true,noInterior=true,noClipping=true;
                for(int sector=0;sector<8;sector++) {
                    double angle=sector*Math.PI/4;int x=(int)Math.Round(210+Math.Sin(angle)*201),y=(int)Math.Round(244-Math.Cos(angle)*201);
                    allDirections&=VisiblePixels(image,Rectangle.Intersect(new Rectangle(x-6,y-6,13,13),new Rectangle(Point.Empty,image.Size)))>0;
                }
                for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++) {
                    if((x-210)*(x-210)+(y-244)*(y-244)<=180*180&&image.GetPixel(x,y).A!=0)noInterior=false;
                    if((x==0||x==image.Width-1||y==0||y==image.Height-1)&&image.GetPixel(x,y).A!=0)noClipping=false;
                }
                Check(allDirections&&noInterior&&noClipping,"large chevrons fit the overlay and stay within the rim band");
                Check(VisiblePixels(image,new Rectangle(Point.Empty,image.Size))>1600,"large filled direction hints have a substantial visible area");
                radar.UpdateTargets(null,enemies.Take(8).ToArray(),100,true,true);
                using(var single=new Bitmap(420,460))using(var sg=Graphics.FromImage(single)) {
                    radar.DrawTargets(sg,bounds,appearance,false,3);
                    Check(SamePixels(image,single),"coincident enemies draw one hint with consistent opacity");
                }
                Color red=image.GetPixel(210,45);Check(red.R>220&&red.G<110&&red.B<110,"outside direction hints use red");
                appearance.Enemies=50;g.Clear(Color.Transparent);radar.DrawTargets(g,bounds,appearance,false,3);
                Check(image.GetPixel(210,45).A>=125&&image.GetPixel(210,45).A<=130,"direction hints follow enemy component opacity");
                appearance.Enemies=0;g.Clear(Color.Transparent);radar.DrawTargets(g,bounds,appearance,false,3);
                Check(VisiblePixels(image,new Rectangle(Point.Empty,image.Size))==0,"zero enemy opacity also hides directional hints");
            }
            radar.UpdateTargets(null,enemies,100,true,true);
            radar.SetRange(250);Check(radar.OutsideEnemyDirections.Count==0&&radar.VisibleTargetCount==80,"increasing range replaces distant direction hints with regular target symbols");
            radar.SetRange(100);radar.SetHeading(false);Check(radar.OutsideEnemyDirections.Count==0,"missing heading clears directional hints");
            radar.UpdateTargets(null,enemies,100,false,true);Check(radar.OutsideEnemyDirections.Count==0,"inactive and interrupted states clear directional hints");
        }
        var scattered=Enumerable.Range(0,24).Select(i=>new TargetView {Code="敌"+i,Number=i,Target=new TargetInfo {known=true},HasHeading=true,HorizontalMetres=150,Bearing=i*15+7.3}).ToArray();
        var hints=new RadarDirections(scattered);hints.Arrange(100,true);var buffer=hints.Bearings;
        Check(hints.Count==24&&RadarDirections.Gap(hints.Bearings[0],7.3)<0.000001,"continuous bearings retain more than eight distinct directions without snapping");
        foreach(var enemy in scattered)enemy.Bearing+=0.7;hints.Arrange(100,true);
        Check(hints.Count==24&&RadarDirections.Gap(hints.Bearings[0],8)<0.000001&&Object.ReferenceEquals(buffer,hints.Bearings),"small rotations move hints continuously while reusing their buffer");
        foreach(var bearing in new[]{-179.9,-22.501,-22.5,22.499,22.5,337.5,382.5,180.0,-180.0}) {
            var single=new RadarDirections(new[]{new TargetView {Code="敌1",Target=new TargetInfo(),HasHeading=true,HorizontalMetres=150,Bearing=bearing}});single.Arrange(100,true);
            Check(single.Count==1&&RadarDirections.Gap(single.Bearings[0],bearing)<0.000001,"continuous bearing and wrap "+bearing);
        }
        var close=new[]{179.0,-179.0,160.0,22.4,22.6}.Select((a,i)=>new TargetView {Code="敌"+i,Number=i,Target=new TargetInfo(),HasHeading=true,HorizontalMetres=150,Bearing=a}).ToArray();
        var merged=new RadarDirections(close);merged.Arrange(100,true);
        Check(merged.Count==3&&close.All(t=>merged.Bearings.Take(merged.Count).Any(b=>RadarDirections.Gap(b,t.Bearing)<RadarDirections.MergeDegrees)),"nearby directions merge across the rear seam while every enemy direction remains covered");
        var reversed=new RadarDirections(close.Reverse().ToArray());reversed.Arrange(100,true);
        Check(reversed.Bearings.Take(reversed.Count).SequenceEqual(merged.Bearings.Take(merged.Count)),"snapshot ordering cannot change the representative enemy direction");
        var state=new GameSnapshot {session="directions",world="world",level="C2M7",mode="campaign",player=new Point3(),yaw=0,
            enemies=new[]{Target("enemy",15000,0,0)},secrets=new[]{Target("secret",0,30000,0)}};
        var tracker=new TargetTracker();tracker.Update(state);
        using(var radar=new RadarControl {Size=new Size(404,416)}) {
            radar.UpdateTargets(tracker.Secrets,tracker.Enemies,100,true,true);
            Check(radar.OutsideEnemyDirections.Count==1&&radar.VisibleTargetCount==1,"distant secrets stay separate and never create enemy directions");
            tracker.UpdatePose(new Point3(),31.7);Check(RadarDirections.Gap(radar.OutsideEnemyDirections.Bearings[0],-31.7)<0.000001,"turning updates continuous directions without a full snapshot");
            tracker.UpdatePose(new Point3 {x=5000},0);Check(radar.OutsideEnemyDirections.Count==0&&radar.VisibleTargetCount==2,"moving to the range boundary removes the hint and restores the enemy symbol");
            using(var image=new Bitmap(420,460))using(var g=Graphics.FromImage(image)) {
                var appearance=Invisible();appearance.Enemies=100;radar.DrawTargets(g,new Rectangle(8,36,404,416),appearance,false,3);
                Color red=image.GetPixel(210,54);Check(red.R>220&&red.G<110&&red.B<110,"in-range enemy symbols also use red");
            }
        }
    }
    static bool NoOutsideOverlap(RadarMarker[] markers) {
        for(int i=0;i<markers.Length;i++)for(int j=0;j<i;j++)if(markers[i].Outside||markers[j].Outside) {
            double dx=markers[i].Right-markers[j].Right,dy=markers[i].Forward-markers[j].Forward;
            if(dx*dx+dy*dy<RadarLayout.Separation*RadarLayout.Separation-0.000001)return false;
        }
        return true;
    }
    public static int RenderRadars(string directory) {
        Directory.CreateDirectory(directory);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        var s=new GameSnapshot {session="preview",world="world",level="C1M8",generation=1,mode="campaign",player=new Point3(),yaw=25,killed=119,spawned=117,secretsFound=2,secretsTotal=6,
            secrets=new[]{Target("one",3200,1200,1500),Target("two",9000,7000,-1400,false),Target("three",-12000,-3000,0),Target("four",24000,25000,500)},
            enemies=new[]{Target("e1",2000,-4000,-600),Target("e2",10000,800,2000),Target("e3",-7000,-3000,1000),Target("e4",50000,0,0),Target("e5",2000,-4100,0)},
            warnings=new[]{"未激活波次及未加载区域待核实","击杀计数与生成计数不一致，参考百分比待核实"}};
        foreach(string mode in new[]{"campaign","compact","interrupted","results","unknown-heading","missing-position","critical-warning","background-hidden","markers-only","half-components","pinned"}) {
            s.mode=mode=="results"?"results":"campaign";s.yaw=mode=="unknown-heading"?(double?)null:25;
            s.player=mode=="missing-position"?null:new Point3();
            s.warnings=mode=="critical-warning"?new[]{"关卡秘密总数与目录不一致，判定已停止"}:new[]{"未激活波次及未加载区域待核实","击杀计数与生成计数不一致，参考百分比待核实"};
            s.finalResult=mode=="results"?new FinalResult {confirmed=true,killPercent=98,secretsFound=2,secretsTotal=6}:null;
            var settings=new Settings {OverlayX=-32000,OverlayY=-32000,Opacity=100};
            if(mode=="background-hidden")settings.Appearance.Background=0;
            if(mode=="markers-only"){settings.Appearance=Invisible();settings.Appearance.Secrets=100;settings.Appearance.Enemies=100;settings.Appearance.Player=100;}
            if(mode=="half-components"){settings.Appearance.Background=30;settings.Appearance.Rings=50;settings.Appearance.Axes=20;settings.Appearance.Enemies=25;settings.Appearance.Player=75;}
            if(mode=="pinned")settings.PinCenter=true;
            using(var form=new OverlayForm(settings)) {
                if(mode=="compact")form.Height=420;
                form.Location=new Point(-32000,-32000);form.Show();
                form.UpdateState(s.level,mode=="interrupted"?"数据中断 · 旧数据已停止作为实时提示":"插件数据桥接就绪 · "+s.level,DisplayText.Progress(s),s,mode!="interrupted");
                Application.DoEvents();
                CheckLabels(form);
                var radar=FindRadar(form);
                if(radar==null||radar.PlotDiameter<(mode=="compact"?340:375))throw new Exception("Radar was reduced by layout whitespace");
                if(form.Width*form.Height>=540*520)throw new Exception("Radar layout occupies more area than the old compact layout");
                bool campaign=mode!="interrupted"&&mode!="results";
                if(form.EnemyCountText!=(campaign?"当前区域敌人：5":"当前区域敌人：待核实"))throw new Exception("Regional enemy count was stale or included unknown enemies");
                if((!campaign||s.player==null||!s.yaw.HasValue)&&radar.VisibleTargetCount!=0)throw new Exception("Unavailable radar still shows target positions");
                if(CountLabels(form)!=0)throw new Exception("Layered overlay contains legacy child labels");
                if(form.PresentationCount==0)throw new Exception("Native layered presentation did not run");
                var priorBounds=form.Bounds;form.SetEdit(true);form.SetEdit(false);form.ApplyAppearance();
                if(form.Bounds!=priorBounds||radar.PlotDiameter<(mode=="compact"?340:375))throw new Exception("Editing changed overlay size or position");
                if(form.PresentationError!=null)throw new Exception("Layered rendering failed after editing: "+form.PresentationError);
                using(var bitmap=form.RenderBitmap())bitmap.Save(Path.Combine(directory,"radar-"+mode+".png"));
                if(mode=="background-hidden")using(var bitmap=form.RenderBitmap())SaveCheckerPreview(bitmap,Path.Combine(directory,"radar-background-checker.png"));
                form.Close();
            }
        }
        s.mode="campaign";s.finalResult=null;s.player=new Point3();s.yaw=0;
        s.secrets=new[]{Target("edge-secret",25000,0,0),Target("edge-up",-25000,500,900),Target("inside-secret",3500,2500,0)};
        s.enemies=Enumerable.Range(1,18).Select(i=> {
            double angle=(i<=6?(i-3)*0.4:i*25)*Math.PI/180;
            return Target("edge-enemy-"+i,30000*Math.Cos(angle),30000*Math.Sin(angle),(i%3-1)*900);
        }).Concat(new[]{Target("inside-enemy",10000,0,0)}).ToArray();
        using(var form=new OverlayForm(new Settings {Opacity=100,ShowPlayerArrow=false})) {
            form.UpdateState(s.level,"ready","",s,true);
            using(var bitmap=form.RenderBitmap())bitmap.Save(Path.Combine(directory,"radar-crowded-edge.png"));
        }
        using(var dialog=new AppearanceDialog(new Settings(),()=>{},false)) {
            dialog.Location=new Point(-32000,-32000);dialog.StartPosition=FormStartPosition.Manual;dialog.Show();Application.DoEvents();CheckLabels(dialog);
            using(var bitmap=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(directory,"radar-settings.png"));}dialog.Close();
        }
        RenderMain(Path.Combine(directory,"assistant-settings-main.png"));
        Console.WriteLine("RADAR_PREVIEWS_RENDERED");return 0;
    }
    static void SaveCheckerPreview(Bitmap source,string path) {
        using(var bitmap=new Bitmap(source.Width,source.Height))using(var g=Graphics.FromImage(bitmap)) {
            using(var light=new SolidBrush(Color.FromArgb(74,78,84)))using(var dark=new SolidBrush(Color.FromArgb(48,52,58)))
                for(int y=0;y<bitmap.Height;y+=20)for(int x=0;x<bitmap.Width;x+=20)g.FillRectangle((x/20+y/20)%2==0?light:dark,x,y,20,20);
            g.DrawImageUnscaled(source,0,0);bitmap.Save(path);
        }
    }
    static RadarControl FindRadar(Control root) {
        var overlay=root as OverlayForm;if(overlay!=null)return overlay.Radar;
        var radar=root as RadarControl;if(radar!=null)return radar;
        foreach(Control child in root.Controls){radar=FindRadar(child);if(radar!=null)return radar;}return null;
    }
    static int CountLabels(Control root) {int count=root is Label?1:0;foreach(Control child in root.Controls)count+=CountLabels(child);return count;}
    static void CheckLabels(Control root) {
        foreach(Control child in root.Controls) {
            var label=child as Label;
            if(label!=null&&!String.IsNullOrEmpty(label.Text)) {
                Size needed=TextRenderer.MeasureText(label.Text,label.Font,new Size(label.Width,Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);
                if(needed.Height>label.Height+2)throw new Exception("Clipped overlay label: "+label.Text+" ["+needed.Height+">"+label.Height+"]");
            }
            CheckLabels(child);
        }
    }
    public static int Run() {
        RadarTests();LayoutTests();PoseTests();CacheTests();
        var store=new SnapshotStore();
        Check(store.Accept(Row(1,"campaign",1),1000),"valid state accepted");
        Check(store.Fresh(3000)&&!store.Fresh(3001),"stale after two seconds");
        Check(!store.Accept(Row(1,"campaign",1),2000),"duplicate sequence rejected");
        Check(!store.Accept(Row(2,"campaign",0),2000),"old scene generation rejected");
        Check(!store.Accept(Row(2,"campaign",1).Replace("test","old-session"),2000),"other runtime session rejected");
        Check(!store.Accept(Row(2,"campaign",1).Replace(AppPaths.Revision,"previous-revision"),2000)&&store.RevisionMismatch,"older installed code requires restart");
        Check(store.Accept(Row(2,"loading",2),2000)&&store.Current.secretsFound==null,"loading clears old progress");
        var s=new GameSnapshot {mode="campaign",world="current-world",level="C1M4",generation=3,verified=true,finalResult=new FinalResult {confirmed=true,source="game_settlement",killPercent=100,secretsFound=6,secretsTotal=6,world="current-world",level="C1M4",generation=3}};
        Check(!s.Perfect,"current progress never confirms final result");s.mode="results";
        Check(s.Perfect,"verified same-run final result can confirm perfection");s.finalResult.secretsFound=5;
        Check(!s.Perfect,"missing secret fails perfection");s.finalResult.secretsFound=6;s.finalResult.killPercent=99;
        Check(!s.Perfect,"missing kill percentage fails perfection");s.finalResult.killPercent=100;s.verified=false;
        Check(!s.Perfect,"unverified semantics cannot confirm perfection");
        s.verified=true;s.finalResult.generation=2;Check(!s.Perfect,"previous run final result cannot mark current run perfect");
        var divergent=new GameSnapshot {v=1,session="c1m8-regression",seq=1,generation=8,build="14893097",pluginVersion="0.1.0",revision=AppPaths.Revision,
            mode="campaign",world="C1M8",level="C1M8",killed=119,spawned=117,killPercent=101,secretsFound=4,secretsTotal=6,
            player=new Point3 {x=100,y=200,z=300},secrets=new[]{new TargetInfo {id="secret",known=true,position=new Point3 {x=500}}}};
        var serializer=new JavaScriptSerializer();var live=new SnapshotStore();
        Check(live.Accept(serializer.Serialize(divergent),1000),"real C1M8 119/117 counters do not drop the snapshot");
        Check(live.Current.killPercent==null&&live.Current.killed==119&&live.Current.spawned==117&&live.Current.secretsFound==4&&live.Current.secrets.Length==1&&live.Current.player.y==200,
            "invalid estimate is unknown while counters, secrets and position survive");
        Check(live.Current.warnings.Length==1&&!live.Current.Perfect&&DisplayText.Progress(live.Current).Contains("待核实"),"out-of-range estimate is warned, never clamped to perfection");
        divergent.seq=2;Check(live.Accept(serializer.Serialize(divergent),2500)&&live.Fresh(4000),"continued above-100 telemetry stays fresh");
        divergent.seq=3;divergent.killPercent=100;
        Check(live.Accept(serializer.Serialize(divergent),3000)&&live.Current.killPercent==100&&live.Current.warnings==null,"normal estimate recovers without sticky warning");
        divergent.seq=4;divergent.killPercent=-1;
        Check(!live.Accept(serializer.Serialize(divergent),3500)&&live.ReceivedAt==3000,"negative estimate remains invalid and does not renew freshness");
        divergent.killPercent=101;divergent.mode="results";divergent.verified=true;
        divergent.finalResult=new FinalResult {confirmed=true,source="game_settlement",killPercent=101,secretsFound=6,secretsTotal=6,world="C1M8",level="C1M8",generation=8};
        Check(!live.Accept(serializer.Serialize(divergent),3500),"above-100 final settlement remains rejected");
        divergent.finalResult.killPercent=98;
        Check(live.Accept(serializer.Serialize(divergent),3500)&&!live.Current.Perfect,"valid non-perfect settlement is independent of invalid live estimate");
        divergent.seq=5;divergent.finalResult.killPercent=100;
        Check(live.Accept(serializer.Serialize(divergent),4000)&&live.Current.Perfect,"confirmed same-run perfect settlement remains supported");
        string dir=Path.Combine(AppPaths.Root,"data","test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        string path=Path.Combine(dir,"中文日志.log");
        try {
            File.WriteAllText(path,"[BGC_STATE]"+Row(1,"campaign",1)+"\n",new UTF8Encoding(false));
            var tail=new LogTail(path);var received=new List<string>();tail.Poll(received.Add,null);
            Check(received.Count==0,"existing log cannot become a fresh handshake");
            byte[] bytes=Encoding.UTF8.GetBytes("prefix[BGC_STATE]"+Row(2,"campaign",1).Replace("test","中文会话")+"\n");
            int split=Array.IndexOf(bytes,(byte)0xe4)+1;
            using(var f=new FileStream(path,FileMode.Append))f.Write(bytes,0,split);
            tail.Poll(received.Add,null);Check(received.Count==0,"partial line buffered");
            using(var f=new FileStream(path,FileMode.Append))f.Write(bytes,split,bytes.Length-split);
            tail.Poll(received.Add,null);Check(received.Count==1&&received[0].Contains("中文会话"),"split UTF-8 preserved");
            File.WriteAllText(path,"[BGC_STATE]"+Row(3,"main_menu",2)+"\n",new UTF8Encoding(false));
            tail.Poll(received.Add,null);Check(received.Count==2,"log truncation handled");
            var poses=new List<string>();File.AppendAllText(path,"[BGC_POSE]{\"poseSeq\":1}\n",new UTF8Encoding(false));tail.Poll(received.Add,null,poses.Add);
            Check(poses.Count==1&&poses[0]=="{\"poseSeq\":1}"&&received.Count==2,"pose messages are routed separately from full-state messages");
        } finally {File.Delete(path);Directory.Delete(dir);}
        Console.WriteLine("HOST_TESTS_PASSED");return 0;
    }
    static void PoseTests() {
        var serializer=new JavaScriptSerializer();var store=new SnapshotStore();
        var state=new GameSnapshot {v=1,build="14893097",pluginVersion="0.1.0",revision=AppPaths.Revision,session="pose",seq=1,generation=1,world="world",level="C1M8",mode="campaign",player=new Point3(),yaw=0};
        Check(store.Accept(serializer.Serialize(state),1000),"pose test full state accepted");
        var pose=new PlayerPose {v=1,build="14893097",pluginVersion="0.1.0",revision=AppPaths.Revision,session="pose",poseSeq=1,generation=1,world="world",level="C1M8",player=new Point3(),yaw=179};
        Check(store.Poses.Accept(serializer.Serialize(pose),store.Current,1000),"matching lightweight pose accepted");
        pose.poseSeq=2;pose.player.x=100;pose.yaw=-179;Check(store.Poses.Accept(serializer.Serialize(pose),store.Current,1100),"next lightweight pose accepted");
        var smooth=store.Poses.Sample(1150,store.Current);
        Check(Math.Abs(smooth.player.x-50)<0.00001&&Math.Abs(Math.Abs(smooth.yaw.Value)-180)<0.00001,"position interpolation and shortest yaw arc preserve wrap-around");
        Check(!store.Poses.Accept(serializer.Serialize(pose),store.Current,1200),"duplicate pose sequence rejected");
        pose.poseSeq=3;pose.generation=2;Check(!store.Poses.Accept(serializer.Serialize(pose),store.Current,1200),"old or future scene poses cannot mix into current scene");
        pose.generation=1;pose.player.x=10000;Check(store.Poses.Accept(serializer.Serialize(pose),store.Current,1200)&&store.Poses.Sample(1200,store.Current).player.x==10000,"teleport-sized change snaps rather than crossing intervening space");
        var malformed=serializer.Deserialize<Dictionary<string,object>>(serializer.Serialize(pose));((Dictionary<string,object>)malformed["player"]).Remove("x");
        Check(!store.Poses.Accept(serializer.Serialize(malformed),store.Current,1250),"missing pose coordinates cannot silently become zero");
        Check(store.Poses.Sample(1600,store.Current)==null,"late poses expire without extrapolating");
        pose.poseSeq=4;Check(store.Poses.Accept(serializer.Serialize(pose),store.Current,3500)&&!store.Fresh(3500),"pose heartbeat never renews old full statistics");
        state.seq=2;state.generation=2;store.Accept(serializer.Serialize(state),3600);
        Check(store.Poses.Sample(3600,store.Current)==null,"scene transition clears interpolation history");
        pose.generation=2;pose.poseSeq=5;store.Poses.Accept(serializer.Serialize(pose),store.Current,3600);
        state.seq=3;state.mode="results";store.Accept(serializer.Serialize(state),3700);
        Check(store.Poses.Sample(3700,store.Current)==null,"results clear the lightweight pose stream");
    }
    static void CacheTests() {
        var state=new GameSnapshot {session="cache",world="world",level="C1M8",mode="campaign",player=new Point3(),yaw=0,
            secrets=new[]{Target("secret",1000,0,0)},enemies=new[]{Target("enemy",0,1000,0)}};
        var settings=new Settings {Opacity=100,OverlayX=-32000,OverlayY=-32000};
        using(var form=new OverlayForm(settings)) {
            form.Location=new Point(-32000,-32000);form.Show();form.UpdateState("C1M8","ready","",state,true);form.Animate(null);
            int buffers=form.BufferAllocations,native=form.NativeSurfaceAllocations,backgrounds=form.BackgroundBuildCount;
            for(int i=1;i<=60;i++)form.Animate(new PlayerPose {player=new Point3 {x=i},yaw=(double)i});
            Check(form.PresentationError==null&&form.BufferAllocations==buffers&&form.NativeSurfaceAllocations==native,"animation reuses bitmap buffers and native DIB surface");
            Check(form.BackgroundBuildCount==backgrounds,"moving player does not redraw static background, grid or count text");
            int presentations=form.PresentationCount;form.Animate(new PlayerPose {player=new Point3 {x=60},yaw=60});
            Check(form.PresentationCount==presentations,"unchanged pose does not generate a redundant frame");
            using(var direct=form.RenderBitmap())using(var cached=form.CachedFrame())Check(SamePixels(direct,cached),"cached frame matches the full renderer pixel for pixel");
            settings.Appearance.Background=0;settings.Appearance.Counter=50;settings.Appearance.Rings=30;settings.Appearance.Axes=70;settings.Appearance.Enemies=50;form.ApplyAppearance();
            using(var direct=form.RenderBitmap())using(var cached=form.CachedFrame())Check(SamePixels(direct,cached),"cached translucent layers preserve all component alpha values");
            form.Close();
        }
    }
    static bool SamePixels(Bitmap a,Bitmap b){
        if(a.Size!=b.Size)return false;
        for(int y=0;y<a.Height;y++)for(int x=0;x<a.Width;x++)if(a.GetPixel(x,y)!=b.GetPixel(x,y))return false;
        return true;
    }
}
}
