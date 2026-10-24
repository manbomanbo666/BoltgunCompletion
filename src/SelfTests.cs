using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Drawing;
using System.Windows.Forms;

namespace BoltgunCompletion {
public static class SelfTests {
    public static int Render(string path) {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        using(var form=new MainForm(GameLocator.Find())) {
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
        MarkerVisualTest();
    }
    static void MarkerVisualTest() {
        var markers=new TargetView[6];
        for(int i=0;i<markers.Length;i++)markers[i]=new TargetView {Code=i%2==0?"秘":"敌",Target=new TargetInfo {known=i!=0},
            HasHeading=true,Bearing=30+(i%3)*120,HorizontalMetres=i<3?50:150,HeightMetres=new[]{3.0,0,-3.0}[i%3]};
        using(var radar=new RadarControl {Size=new Size(400,400)})using(var bitmap=new Bitmap(400,400)) {
            radar.UpdateTargets(markers,null,100,true,true);radar.CreateControl();radar.DrawToBitmap(bitmap,new Rectangle(0,0,400,400));
            foreach(var marker in markers) {
                double right,forward;bool outside;marker.RadarPoint(100,out right,out forward,out outside);
                int x=(int)Math.Round(200+right*radar.PlotDiameter/2),y=(int)Math.Round(200-forward*radar.PlotDiameter/2);
                bool filled=bitmap.GetPixel(x,y).G>100;
                Check(filled==!outside,"rendered "+marker.HeightBand+" elevation marker is "+(outside?"hollow outside range":"filled inside range"));
            }
        }
    }
    public static int RenderRadars(string directory) {
        Directory.CreateDirectory(directory);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        var s=new GameSnapshot {session="preview",world="world",level="C1M8",generation=1,mode="campaign",player=new Point3(),yaw=25,killed=119,spawned=117,secretsFound=2,secretsTotal=6,
            secrets=new[]{Target("one",3200,1200,1500),Target("two",9000,7000,-1400,false),Target("three",-12000,-3000,0),Target("four",24000,25000,500)},
            enemies=new[]{Target("e1",2000,-4000,-600),Target("e2",10000,800,2000),Target("e3",-7000,-3000,1000),Target("e4",50000,0,0),Target("e5",2000,-4100,0)},
            warnings=new[]{"未激活波次及未加载区域待核实","击杀计数与生成计数不一致，参考百分比待核实"}};
        foreach(string mode in new[]{"campaign","compact","interrupted","results","unknown-heading","missing-position","critical-warning"}) {
            s.mode=mode=="results"?"results":"campaign";s.yaw=mode=="unknown-heading"?(double?)null:25;
            s.player=mode=="missing-position"?null:new Point3();
            s.warnings=mode=="critical-warning"?new[]{"关卡秘密总数与目录不一致，判定已停止"}:new[]{"未激活波次及未加载区域待核实","击杀计数与生成计数不一致，参考百分比待核实"};
            s.finalResult=mode=="results"?new FinalResult {confirmed=true,killPercent=98,secretsFound=2,secretsTotal=6}:null;
            using(var form=new OverlayForm(new Settings {OverlayX=-32000,OverlayY=-32000})) {
                if(mode=="compact")form.Height=420;
                form.Opacity=1;form.Location=new Point(-32000,-32000);form.Show();
                form.UpdateState(s.level,mode=="interrupted"?"数据中断 · 旧数据已停止作为实时提示":"插件数据桥接就绪 · "+s.level,DisplayText.Progress(s),s,mode!="interrupted");
                Application.DoEvents();
                CheckLabels(form);
                var radar=FindRadar(form);
                if(radar==null||radar.PlotDiameter<(mode=="compact"?340:375))throw new Exception("Radar was reduced by layout whitespace");
                if(form.Width*form.Height>=540*520)throw new Exception("Radar layout occupies more area than the old compact layout");
                bool campaign=mode!="interrupted"&&mode!="results";
                if(form.EnemyCountText!=(campaign?"当前区域敌人：5":"当前区域敌人：待核实"))throw new Exception("Regional enemy count was stale or included unknown enemies");
                if((!campaign||s.player==null||!s.yaw.HasValue)&&radar.VisibleTargetCount!=0)throw new Exception("Unavailable radar still shows target positions");
                if(CountLabels(form)!=1)throw new Exception("Radar-only overlay contains unwanted text controls");
                using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(directory,"radar-"+mode+".png"));}
                form.Close();
            }
        }
        Console.WriteLine("RADAR_PREVIEWS_RENDERED");return 0;
    }
    static RadarControl FindRadar(Control root) {
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
        RadarTests();
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
        } finally {File.Delete(path);Directory.Delete(dir);}
        Console.WriteLine("HOST_TESTS_PASSED");return 0;
    }
}
}
