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
    public static int Run() {
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
