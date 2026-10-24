using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Web.Script.Serialization;

namespace BoltgunCompletion {
public sealed class Point3 { public double x; public double y; public double z; }
public sealed class TargetInfo { public string id; public string label; public Point3 position; public bool known; }
public sealed class FinalResult { public bool confirmed; public string source; public double? killPercent; public int? secretsFound; public int? secretsTotal; public string world; public string level; public int? generation; }
public sealed class GameSnapshot {
    public int v; public string session; public long seq; public int generation; public string build; public string pluginVersion;
    public string mode; public string world; public string level; public string gameMode; public bool verified; public string error; public string revision; public double sampleMs;
    public int? secretsFound; public int? secretsTotal; public int? killed; public int? spawned; public double? killPercent;
    public Point3 player; public double? yaw; public TargetInfo[] secrets; public TargetInfo[] enemies;
    public string[] warnings; public FinalResult finalResult;
    public bool RadarActive { get { return mode=="campaign" || mode=="horde"; } }
    public bool Perfect { get { return mode=="results" && verified && finalResult!=null && finalResult.confirmed &&
        finalResult.source=="game_settlement" && !String.IsNullOrEmpty(world) && finalResult.world==world && finalResult.level==level && finalResult.generation==generation && finalResult.killPercent==100 && finalResult.secretsFound.HasValue &&
        finalResult.secretsTotal.HasValue && finalResult.secretsTotal>=0 && finalResult.secretsFound==finalResult.secretsTotal; } }
}
public sealed class SnapshotStore {
    public readonly PoseStream Poses=new PoseStream();
    public GameSnapshot Current { get; private set; }
    public long ReceivedAt { get; private set; }
    public int Rejected { get; private set; }
    public bool RevisionMismatch { get; private set; }
    static readonly HashSet<string> Modes=new HashSet<string>(new[]{"initializing","main_menu","loading","dead","campaign","horde","unsupported","results","error"});
    public bool Accept(string json,long now) {
        try {
            var s=new JavaScriptSerializer {MaxJsonLength=1048576}.Deserialize<GameSnapshot>(json);
            if(s!=null && s.revision!=AppPaths.Revision) {RevisionMismatch=true;return Reject();}
            if(s==null || s.v!=1 || s.build!="14893097" || s.pluginVersion!="0.1.0" || String.IsNullOrEmpty(s.session) || s.session.Length>128 || s.seq<1 || s.generation<0 || !Modes.Contains(s.mode)) return Reject();
            if(!Count(s.secretsFound)||!Count(s.secretsTotal)||!Count(s.killed)||!Count(s.spawned)||!ProgressNumber(s.killPercent)) return Reject();
            if(s.mode=="horde" && (s.finalResult!=null || s.verified)) return Reject();
            if(Current!=null && (s.session!=Current.session || s.seq<=Current.seq || s.generation<Current.generation)) return Reject();
            if(!ValidPoint(s.player) || (s.yaw.HasValue && (Double.IsNaN(s.yaw.Value)||Double.IsInfinity(s.yaw.Value)))) return Reject();
            foreach(var t in (s.secrets??new TargetInfo[0]).Concat(s.enemies??new TargetInfo[0])) if(t==null || !ValidPoint(t.position)) return Reject();
            if(s.finalResult!=null && (!Count(s.finalResult.secretsFound)||!Count(s.finalResult.secretsTotal)||!Percent(s.finalResult.killPercent))) return Reject();
            if(s.finalResult!=null && s.finalResult.confirmed && (s.finalResult.world!=s.world || s.finalResult.level!=s.level || s.finalResult.generation!=s.generation)) return Reject();
            // Live killed/spawned counters can diverge (observed in C1M8).
            // Discard only the estimate, not otherwise valid target telemetry.
            // Final settlement percentages retain the strict 0..100 check above.
            if(s.killPercent>100) {
                s.killPercent=null;
                s.warnings=(s.warnings??new string[0]).Concat(new[]{"击杀计数与生成计数不一致，参考百分比待核实"}).ToArray();
            }
            Poses.ResetFor(s);Current=s; ReceivedAt=now; RevisionMismatch=false; return true;
        } catch { return Reject(); }
    }
    bool Reject() { Rejected++; return false; }
    static bool Count(int? v) { return !v.HasValue || (v>=0 && v<10000000); }
    static bool ProgressNumber(double? v) { return !v.HasValue || (!Double.IsNaN(v.Value)&&!Double.IsInfinity(v.Value)&&v>=0); }
    static bool Percent(double? v) { return !v.HasValue || (!Double.IsNaN(v.Value)&&!Double.IsInfinity(v.Value)&&v>=0&&v<=100); }
    static bool ValidPoint(Point3 p) { return p==null || new[]{p.x,p.y,p.z}.All(n=>!Double.IsNaN(n)&&!Double.IsInfinity(n)&&Math.Abs(n)<1e12); }
    public bool Fresh(long now) { return Current!=null && now>=ReceivedAt && now-ReceivedAt<=2000; }
    public void Clear() { Current=null; ReceivedAt=0; Rejected=0; RevisionMismatch=false;Poses.Clear(); }
}
// Byte-oriented incremental reader: preserves split UTF-8 characters and lines.
// Starts at the end of the pre-existing log, so stale data is never a handshake.
public sealed class LogTail {
    readonly string path;
    long offset;
    readonly Decoder decoder=new UTF8Encoding(false,false).GetDecoder();
    readonly StringBuilder pending=new StringBuilder();
    readonly byte[] bytes=new byte[65536];
    readonly char[] characters=new char[Encoding.UTF8.GetMaxCharCount(65536)];
    public LogTail(string path) { this.path=path; offset=File.Exists(path)?new FileInfo(path).Length:0; }
    public int Poll(Action<string> state,Action<string> schema,Action<string> pose=null) {
        if(!File.Exists(path)) return 0;
        using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)) {
            if(file.Length<offset) { offset=0; pending.Clear(); decoder.Reset(); }
            file.Position=offset;
            int read=file.Read(bytes,0,bytes.Length);
            if(read==0) return 0;
            offset+=read;
            int count=decoder.GetChars(bytes,0,read,characters,0,false);
            for(int i=0;i<count;i++) {
                if(characters[i]=='\n') {
                    string line=pending.ToString().TrimEnd('\r'); pending.Clear();
                    int pos=line.IndexOf("[BGC_STATE]",StringComparison.Ordinal);
                    if(pos>=0) state(line.Substring(pos+11));
                    pos=line.IndexOf("[BGC_SCHEMA]",StringComparison.Ordinal);
                    if(pos>=0 && schema!=null) schema(line.Substring(pos+12));
                    pos=line.IndexOf("[BGC_POSE]",StringComparison.Ordinal);
                    if(pos>=0&&pose!=null)pose(line.Substring(pos+10));
                } else if(pending.Length<1048576) pending.Append(characters[i]);
                else { pending.Clear(); } // bounded even if another component writes malformed output
            }
            return read;
        }
    }
}
public static class DisplayText {
    public static string Count(int? n) { return n.HasValue?n.Value.ToString(CultureInfo.InvariantCulture):"待核实"; }
    static string HordeProgress(GameSnapshot s) {
        return "围攻模式 · 已确认存活敌人 "+(s.enemies??new TargetInfo[0]).Count(t=>t!=null&&t.known)+"\r\n仅追踪当前已加载目标，未刷出波次不计入；不判定剧情完美通关。";
    }
    public static string CompactProgress(GameSnapshot s) {
        if(s.mode=="horde")return HordeProgress(s).Replace("\r\n"," · ");
        if(s.mode=="results"&&s.finalResult!=null&&s.finalResult.confirmed) {
            string result=s.Perfect?"最终结算：完美通关":"最终结算击杀率 "+(s.finalResult.killPercent.HasValue?s.finalResult.killPercent.Value.ToString("0.##",CultureInfo.InvariantCulture)+"%":"待核实");
            return result+" · 秘密 "+Count(s.finalResult.secretsFound)+" / "+Count(s.finalResult.secretsTotal)+(s.verified?"":" · 判定待核实");
        }
        return "本次击杀 "+Count(s.killed)+" · 秘密 "+Count(s.secretsFound)+" / "+Count(s.secretsTotal)+" · 参考击杀 "+
            (s.killPercent.HasValue?s.killPercent.Value.ToString("0.##",CultureInfo.InvariantCulture)+"%":"待核实")+" · 以最终结算为准";
    }
    public static string Progress(GameSnapshot s) {
        if(s.mode=="horde")return HordeProgress(s);
        string percent=s.killPercent.HasValue?s.killPercent.Value.ToString("0.##",CultureInfo.InvariantCulture)+"%":"待核实";
        string result="当前击杀进度  "+percent+"（参考）  已击杀 "+Count(s.killed)+"\r\n秘密    "+Count(s.secretsFound)+" / "+Count(s.secretsTotal);
        if(s.mode=="results" && s.finalResult!=null && s.finalResult.confirmed)
            result="最终结算击杀率  "+(s.finalResult.killPercent.HasValue?s.finalResult.killPercent.Value.ToString("0.##",CultureInfo.InvariantCulture)+"%":"待核实")+"\r\n秘密    "+Count(s.finalResult.secretsFound)+" / "+Count(s.finalResult.secretsTotal);
        if(!s.verified) result+="\r\n统计口径核对中";
        return result;
    }
    public static string Nearest(GameSnapshot s,TargetInfo[] targets,string kind,bool compact=false) {
        if(s.player==null) return kind+"：等待玩家位置";
        var located=(targets??new TargetInfo[0]).Where(t=>t.position!=null).OrderBy(t=>Distance(s.player,t.position)).Take(3).ToArray();
        if(located.Length==0) return kind+"：当前没有可确认的位置";
        return String.Join("\r\n",located.Select((t,i)=> {
            double dx=t.position.x-s.player.x,dy=t.position.y-s.player.y,dz=t.position.z-s.player.z;
            double bearing=Math.Atan2(dy,dx)*180/Math.PI-(s.yaw??0);
            bearing=(bearing%360+540)%360-180;
            string direction=!s.yaw.HasValue?"方向待核实":Math.Abs(bearing)<22.5?"前方":bearing>=22.5&&bearing<67.5?"右前方":bearing>=67.5&&bearing<112.5?"右侧":bearing>=112.5&&bearing<157.5?"右后方":bearing<=-22.5&&bearing>-67.5?"左前方":bearing<=-67.5&&bearing>-112.5?"左侧":bearing<=-112.5&&bearing>-157.5?"左后方":"后方";
            string distance=(Distance(s.player,t.position)/100).ToString("0.0",CultureInfo.InvariantCulture);
            if(compact)return (i+1)+"  "+direction+" · "+distance+" m\r\n"+(dz>200?"较高处":dz<-200?"较低处":"相近高度")+" · "+(t.known?"已确认":"状态待核实")+(i<located.Length-1?"\r\n":"");
            return kind+(i+1)+"  "+direction+"  "+distance+"m"+(dz>200?"  较高处":dz<-200?"  较低处":"")+(t.known?"":"  状态待核实");
        }));
    }
    static double Distance(Point3 a,Point3 b) { return Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)+(a.z-b.z)*(a.z-b.z)); }
}
}
