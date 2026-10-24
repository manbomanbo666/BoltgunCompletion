using System;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace BoltgunCompletion {
public sealed class PlayerPose {
    public int v,generation;public long poseSeq;public string session,world,level,build,pluginVersion,revision;
    public Point3 player;public double? yaw;
}
public sealed class PoseStream {
    PlayerPose previous,latest;long previousAt,latestAt;
    public int Rejected {get;private set;}
    static bool Same(PlayerPose p,GameSnapshot s){return p!=null&&s!=null&&p.session==s.session&&p.world==s.world&&p.level==s.level&&p.generation==s.generation;}
    public void ResetFor(GameSnapshot s){if(s==null||s.mode!="campaign"||(latest!=null&&!Same(latest,s)))Clear();}
    public bool Accept(string json,GameSnapshot current,long now) {
        try {
            var serializer=new JavaScriptSerializer();var data=serializer.Deserialize<Dictionary<string,object>>(json);
            object point;var coordinates=data!=null&&data.TryGetValue("player",out point)?point as Dictionary<string,object>:null;
            if(coordinates==null||!new[]{"x","y","z"}.All(k=>coordinates.ContainsKey(k)&&(coordinates[k] is int||coordinates[k] is long||coordinates[k] is double||coordinates[k] is decimal))){Rejected++;return false;}
            var pose=serializer.ConvertToType<PlayerPose>(data);
            if(current==null||current.mode!="campaign"||pose==null||pose.v!=1||pose.poseSeq<1||pose.build!="14893097"||pose.pluginVersion!="0.1.0"||pose.revision!=AppPaths.Revision||String.IsNullOrEmpty(pose.world)||String.IsNullOrEmpty(pose.level)||!Same(pose,current)||pose.player==null||!pose.yaw.HasValue||
                new[]{pose.player.x,pose.player.y,pose.player.z,pose.yaw.Value}.Any(n=>Double.IsNaN(n)||Double.IsInfinity(n)||Math.Abs(n)>=1e12)||
                latest!=null&&(pose.poseSeq<=latest.poseSeq||now<latestAt)) {Rejected++;return false;}
            bool continuous=latest!=null&&now-latestAt<=300&&DistanceSquared(latest.player,pose.player)<25000000;
            previous=continuous?latest:null;previousAt=latestAt;latest=pose;latestAt=now;return true;
        }catch{Rejected++;return false;}
    }
    static double DistanceSquared(Point3 a,Point3 b){double x=a.x-b.x,y=a.y-b.y,z=a.z-b.z;return x*x+y*y+z*z;}
    public PlayerPose Sample(long now,GameSnapshot current) {
        if(current==null||current.mode!="campaign"||!Same(latest,current)||now<latestAt||now-latestAt>350)return null;
        if(previous==null||latestAt<=previousAt)return latest;
        double t=Math.Max(0,Math.Min(1,(now-100.0-previousAt)/(latestAt-previousAt)));
        double delta=((latest.yaw.Value-previous.yaw.Value)%360+540)%360-180;double yaw=previous.yaw.Value+delta*t;
        return new PlayerPose {v=1,session=latest.session,world=latest.world,level=latest.level,generation=latest.generation,poseSeq=latest.poseSeq,
            player=new Point3 {x=previous.player.x+(latest.player.x-previous.player.x)*t,y=previous.player.y+(latest.player.y-previous.player.y)*t,z=previous.player.z+(latest.player.z-previous.player.z)*t},yaw=yaw};
    }
    public void Clear(){previous=null;latest=null;previousAt=latestAt=0;Rejected=0;}
}
}
