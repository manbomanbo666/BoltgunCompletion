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
