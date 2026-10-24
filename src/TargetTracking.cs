using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

namespace BoltgunCompletion {
public sealed class TargetView {
    public TargetInfo Target;
    public int Number;
    public double HorizontalMetres, HeightMetres, Bearing;
    public bool HasHeading;
    public string Code;
    // Relative elevation only; exact floor topology is not known.
    public int HeightBand {get{return HeightMetres>=2?1:HeightMetres<=-2?-1:0;}}
    public bool InRange(double range) {return HorizontalMetres<=range;}
    public string Direction {
        get {
            if(!HasHeading)return "方向待核实";
            string[] names={"前方","右前方","右侧","右后方","后方","左后方","左侧","左前方"};
            return names[Sector];
        }
    }
    int Sector {get{return (int)Math.Floor((Bearing+360+22.5)/45)%8;}}
    public string Arrow {get{return HasHeading?new[]{"↑","↗","→","↘","↓","↙","←","↖"}[Sector]:"?";}}
    public string HeightText {
        get {
            if(Math.Abs(HeightMetres)<2)return "同高附近 ("+(HeightMetres>=0?"+":"")+HeightMetres.ToString("0.0",CultureInfo.InvariantCulture)+"m)";
            return (HeightMetres>0?"高 ":"低 ")+Math.Abs(HeightMetres).ToString("0.0",CultureInfo.InvariantCulture)+"m";
        }
    }
    // Radar uses player-relative horizontal distance; vertical separation is textual.
    public void RadarPoint(double range,out double right,out double forward,out bool outside) {
        outside=!InRange(range);
        double radius=Math.Min(1,HorizontalMetres/range),angle=Bearing*Math.PI/180;
        right=Math.Sin(angle)*radius;forward=Math.Cos(angle)*radius;
    }
}
public sealed class TargetTracker {
    string scene;
    readonly Dictionary<string,int> secrets=new Dictionary<string,int>(),enemies=new Dictionary<string,int>();
    public TargetView[] Secrets=new TargetView[0],Enemies=new TargetView[0];
    public void Update(GameSnapshot snapshot) {
        string key=new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new object[]{snapshot.session,snapshot.world,snapshot.level,snapshot.generation});
        if(scene!=key) {scene=key;secrets.Clear();enemies.Clear();}
        Secrets=Views(snapshot,snapshot.secrets,secrets,"秘");Enemies=Views(snapshot,snapshot.enemies,enemies,"敌");
    }
    static TargetView[] Views(GameSnapshot snapshot,TargetInfo[] targets,Dictionary<string,int> numbers,string prefix) {
        if(snapshot.player==null)return new TargetView[0];
        var located=(targets??new TargetInfo[0]).Where(t=>t!=null&&t.position!=null).ToArray();
        foreach(var t in located.OrderBy(t=>Identity(t),StringComparer.Ordinal)) {
            string id=Identity(t);if(!numbers.ContainsKey(id))numbers[id]=numbers.Count+1;
        }
        return located.Select(t=> {
            double dx=t.position.x-snapshot.player.x,dy=t.position.y-snapshot.player.y,dz=t.position.z-snapshot.player.z;
            double bearing=Math.Atan2(dy,dx)*180/Math.PI-(snapshot.yaw??0);
            int number=numbers[Identity(t)];
            return new TargetView {Target=t,Number=number,Code=prefix+number,HorizontalMetres=Math.Sqrt(dx*dx+dy*dy)/100,
                HeightMetres=dz/100,Bearing=(bearing%360+540)%360-180,HasHeading=snapshot.yaw.HasValue};
        }).OrderBy(t=>Math.Sqrt(t.HorizontalMetres*t.HorizontalMetres+t.HeightMetres*t.HeightMetres)).ThenBy(t=>t.Number).ToArray();
    }
    static string Identity(TargetInfo t) {
        // Runtime IDs are actor paths (enemies) or catalog GUIDs (secrets).
        // Position is a fallback for older messages without either identifier.
        return !String.IsNullOrEmpty(t.id)?t.id:!String.IsNullOrEmpty(t.label)?t.label:
            String.Join("/",new[]{t.position.x,t.position.y,t.position.z}.Select(n=>n.ToString("R",CultureInfo.InvariantCulture)));
    }
    public static string Details(TargetView[] targets,string kind,bool narrow=false) {
        if(targets.Length==0)return kind=="秘密"?"当前没有可定位的秘密\r\n请结合上方剩余计数检查":"当前未确认存活敌人\r\n未加载区域及波次仍待核实";
        return String.Join(narrow?"\r\n\r\n":"\r\n",targets.Take(3).Select(t=>t.Code+"  "+t.Arrow+" "+(narrow?(t.HasHeading?t.Direction.Replace("方","").Replace("侧",""):"方向未知"):t.Direction)+" "+t.HorizontalMetres.ToString("0.0",CultureInfo.InvariantCulture)+"m\r\n"+
            t.HeightText+" · "+(t.Target.known?"已确认":narrow?"待核实":"状态待核实")));
    }
    public static string Summary(GameSnapshot snapshot,TargetView[] targets,bool secret) {
        string lead=secret?"未找到 "+(snapshot.secretsFound.HasValue&&snapshot.secretsTotal.HasValue&&snapshot.secretsFound<=snapshot.secretsTotal?
            (snapshot.secretsTotal.Value-snapshot.secretsFound.Value).ToString(CultureInfo.InvariantCulture):"待核实")+" 个秘密":"已确认存活 "+(snapshot.enemies??new TargetInfo[0]).Count(t=>t!=null&&t.known)+" 个敌人";
        return lead+"\r\n雷达 "+targets.Length+" 个 · 列表最近 "+Math.Min(3,targets.Length)+" 个";
    }
}
}
