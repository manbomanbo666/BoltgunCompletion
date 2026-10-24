from pathlib import Path
import json,sys
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'work/pythonlibs'))
from lupa.lua54 import LuaRuntime

def runtime():
    l=LuaRuntime(unpack_returned_tuples=True)
    l.globals().Json=l.execute((ROOT/'src/mod/json.lua').read_text(encoding='utf-8'))
    l.globals().Profile=l.execute((ROOT/'src/mod/profile.lua').read_text(encoding='utf-8'))
    l.globals().Profile.revision='test-revision'
    l.execute('messages={};print=function(s) messages[#messages+1]=s end')
    return l

l=runtime();encoded=l.eval('Json.encode({text="秘密\\n\\\"😀",empty=Json.array(),no=false})')
assert encoded.isascii() and json.loads(encoded)=={'text':'秘密\n"😀','empty':[],'no':False}
print('PASS: ASCII protocol preserves Chinese, escaping, emoji and booleans')
l=runtime();l.execute('''
queue={};loops={};samples=0;poses=0;installs=0
Adapter={generation=0,install=function()installs=installs+1 end,sample=function()samples=samples+1;return {mode='campaign',world='world',level='C1M8',generation=0,player={x=0,y=0,z=0},yaw=0} end,pose=function()poses=poses+1;return {world='world',level='C1M8',generation=0,player={x=0,y=0,z=0},yaw=0}end}
LoopAsync=function(interval,f)loops[#loops+1]={interval=interval,f=f}end
ExecuteInGameThread=function(f)queue[#queue+1]=f end
GenerateSDK=function()error('Forbidden SDK generation')end
ForEachUObject=function()error('Forbidden global scan')end
''');l.execute((ROOT/'src/mod/main.lua').read_text(encoding='utf-8'))
l.execute('''
assert(installs==1 and #loops==1 and loops[1].interval==100 and samples==0)
loops[1].f();loops[1].f();assert(#queue==1)
table.remove(queue,1)();assert(samples==1)
for i=1,4 do loops[1].f();assert(#queue==1);table.remove(queue,1)() end
assert(samples==1 and poses==4)
loops[1].f();table.remove(queue,1)();assert(samples==2 and poses==4)
local stateCount,poseCount=0,0
for _,message in ipairs(messages)do
 if message:find('[BGC_STATE]',1,true)then stateCount=stateCount+1 end
 if message:find('[BGC_POSE]',1,true)then poseCount=poseCount+1 end
end
assert(stateCount==3 and poseCount==6)
Adapter.sample=function()samples=samples+1;return {mode='horde',world='horde',level='Horde_1',generation=0,player={x=1,y=2,z=3},yaw=10}end
Adapter.pose=function()poses=poses+1;return {world='horde',level='Horde_1',generation=0,player={x=1,y=2,z=3},yaw=10}end
local before=#messages
for i=1,5 do loops[1].f();table.remove(queue,1)()end
assert(samples==3 and poses==8 and #messages==before+6)
assert(messages[#messages]:find('[BGC_POSE]',1,true), 'Full horde samples also send a pose')
''');print('PASS: 10 Hz lightweight pose, 2 Hz full state, one pending job and no SDK generation')
l=runtime();l.execute('''
Catalog={C1M4={secretsTotal=1,secrets={{name='secret_one',guid='guid',position={x=10,y=20,z=30}}}}}
calls={findAll=0};hooks={};notify={}
local function fname(n)return {ToString=function()return n end}end
invalid={IsValid=function()return false end}
function object(classname,objname,props,methods)
 props=props or {};methods=methods or {};local cls={}
 cls.IsValid=function()return true end
 cls.GetFullName=function()return 'Class /Script/Test.'..classname end
 cls.GetFName=function()return fname(classname)end
 cls.GetSuperStruct=function()return invalid end
 cls.ForEachProperty=function(_,f)for key in pairs(props)do local k=key;f({GetFName=function()return fname(k)end})end end
 cls.ForEachFunction=function(_,f)for key,d in pairs(methods)do local k,definition=key,d
  f({GetFName=function()return fname(k)end,GetFullName=function()return 'Function /Script/Test.'..classname..':'..k end,
  ForEachProperty=function(_,g)for _,param in ipairs(definition.params or {'ReturnValue'})do local n=param;g({GetFName=function()return fname(n)end})end end})
 end end
 local o={alive=true};o.IsValid=function(self)return self.alive end
 o.GetFullName=function()return classname..' '..objname end;o.GetFName=function()return fname(objname)end
 o.GetClass=function()return cls end;o.GetWorld=function()return world end
 for k,v in pairs(props)do o[k]=v end;for k,d in pairs(methods)do o[k]=d.fn end
 return o
end
world=object('World','C1M4.C1M4')
playerDead=false
player=object('Player','player',{bBeginPlayComplete=true,bPlayerFinishedLevelLoad=true}, {IsDead={fn=function()return playerDead end},K2_GetActorLocation={fn=function()return {X=0,Y=0,Z=0}end},K2_GetActorRotation={fn=function()return {Yaw=0}end}})
meta=object('Meta','meta',{SecretesFoundCount=0},{GetEnemiesKilled={fn=function()return 0 end},GetEnemiesSpawned={fn=function()return 7 end},GetTotalLevelSecrets={params={'ChapterID','LevelID','ReturnValue'},fn=function()return 1 end}})
instance=object('Instance','instance',{Metagame=meta},{GetChapterID={fn=function()return 0 end},GetLevelID={fn=function()return 3 end}})
statics=object('Statics','statics',{}, {GetPlayerPawn={params={'WorldContextObject','PlayerIndex','ReturnValue'},fn=function()return player end}})
secret=object('Secret','secret_one',{bSecretFound=false},{K2_GetActorLocation={fn=function()return {x=10,y=20,z=30}end}})
enemyRoot=object('SceneComponent','enemy_root')
enemyPosition={X=40,Y=0,Z=0};enemyDead=false
enemy=object('Enemy','enemy',{RootComponent=enemyRoot}, {IsDead={fn=function()return enemyDead end},K2_GetActorLocation={fn=function()return enemyPosition end}})
StaticFindObject=function(path)if path=='/Script/Engine.Default__GameplayStatics'then return statics end;return invalid end
FindFirstOf=function(class)if class=='BGGameInstance'then return instance end;return invalid end
FindAllOf=function(class)calls.findAll=calls.findAll+1;if class==Profile.secret_class then return {secret}end;if class==Profile.enemy_class then return {enemy}end;return {}end
NotifyOnNewObject=function(class,f)assert(not notify[class]);notify[class]=f end
RegisterLoadMapPreHook=function(f)beforeMap=f end
RegisterHook=function(path,pre,post)hooks[path]={pre=pre,post=post};return 1,2 end
''');l.globals().Adapter=l.execute((ROOT/'src/mod/adapter.lua').read_text(encoding='utf-8'))
l.execute('''
Adapter.install();local s=Adapter.sample()
assert(s.level=='C1M4' and s.secretsFound==0 and s.secretsTotal==1)
assert(#s.secrets==1 and s.secrets[1].known==true and #s.enemies==1)
assert(s.killed==0 and s.spawned==7 and calls.findAll==2)
Adapter.sample();assert(calls.findAll==2)
-- A live UObject without a root may return the origin despite IsDead=false.
-- It must not become a phantom target, but stays registered for reactivation.
enemyPosition={X=0,Y=0,Z=0};enemy.RootComponent=invalid
s=Adapter.sample();assert(#s.enemies==0 and #s.warnings>0 and s.enemySpatialChecks.rootMissing==1)
assert(s.killed==0 and s.spawned==7 and calls.findAll==2)
enemy.RootComponent=nil;s=Adapter.sample();assert(#s.enemies==0)
enemy.RootComponent=enemyRoot;s=Adapter.sample()
assert(#s.enemies==1 and s.enemies[1].position.x==0, 'A genuine rooted actor at the origin must stay visible')
enemyRoot.alive=false;s=Adapter.sample();assert(#s.enemies==0)
enemyRoot.alive=true;enemyPosition={X=410,Y=200,Z=-30};s=Adapter.sample()
assert(#s.enemies==1 and s.enemies[1].position.x==410 and calls.findAll==2)
enemyDead=true;s=Adapter.sample();assert(#s.enemies==0)
enemyDead=false;s=Adapter.sample();assert(#s.enemies==1, 'Reactivated rooted enemy returns without another object scan')
for _,position in ipairs({{X=0/0,Y=0,Z=0},{X=math.huge,Y=0,Z=0},{X=1,Y=2},{X='bad',Y=0,Z=0}}) do
 enemyPosition=position;s=Adapter.sample();assert(#s.enemies==0)
end
enemyPosition={X=40,Y=0,Z=0}
local unresolved=object('EnemyWithoutRootSchema','unresolved',{}, {IsDead={fn=function()return false end},K2_GetActorLocation={fn=function()error('Unvalidated location getter must not run')end}})
notify['/Script/Boltgun.'..Profile.enemy_class](unresolved)
s=Adapter.sample();assert(#s.enemies==1 and calls.findAll==2 and s.enemySpatialChecks.rootSchemaMissing==1)
print('PASS: rootless zero-origin actors, invalid roots/transforms, real origin targets and reactivation without scans')
local pose=Adapter.pose();assert(pose and pose.player.x==0 and pose.yaw==0 and pose.generation==Adapter.generation)
for i=1,20 do assert(Adapter.pose())end;assert(calls.findAll==2)
player.bPlayerFinishedLevelLoad=false;assert(Adapter.pose()==nil);player.bPlayerFinishedLevelLoad=true
playerDead=true;assert(Adapter.pose()==nil);playerDead=false
player.bPlayerFinishedLevelLoad=false;s=Adapter.sample();assert(s.mode=='loading' and s.secretsFound==nil)
player.bPlayerFinishedLevelLoad=true;playerDead=true;s=Adapter.sample();assert(s.mode=='dead' and s.secretsFound==nil)
playerDead=false
secret.bSecretFound=true;meta.SecretesFoundCount=1;s=Adapter.sample();assert(#s.secrets==0 and s.secretsFound==1)
secret.bSecretFound=false;Adapter.sample();secret.alive=false;s=Adapter.sample();assert(#s.secrets==1 and s.secrets[1].known==false)
hooks[Profile.hooks.end_level].pre();s=Adapter.sample();assert(s.mode=='results' and not s.finalResult.confirmed and not s.verified)
meta.GetEnemiesKilled=function()return 7 end
hooks[Profile.hooks.end_level].pre()
killTextValue='50%';secretTextValue='1 / 1'
local kills=object('TextBlock','kills',{}, {GetText={fn=function()return {ToString=function()return killTextValue end}end}})
local secrets=object('TextBlock','secrets',{}, {GetText={fn=function()return {ToString=function()return secretTextValue end}end}})
local result=object(Profile.results_class,'results',{TextBlock_Kills_Stat=kills,TextBlock_Secrets_Stat=secrets,KillPercentage=100,SecretsFound=1,TotalSecrets=1},{})
Adapter.result_widget=result;Adapter.result_widget_generation=Adapter.generation-1
s=Adapter.sample();assert(not s.finalResult.confirmed)
notify['/Script/UMG.UserWidget'](result);s=Adapter.sample();assert(not s.finalResult.confirmed)
killTextValue='100%';result.SecretsFound=0;s=Adapter.sample();assert(not s.finalResult.confirmed)
result.SecretsFound=1;s=Adapter.sample();assert(s.finalResult.confirmed and s.finalResult.killPercent==100 and s.finalResult.secretsFound==1 and s.verified)
local default=object(Profile.results_class,'Default__results',{},{});notify['/Script/UMG.UserWidget'](default)
assert(Adapter.result_widget==result)
meta.SecretesFoundCount=0;hooks[Profile.hooks.end_level].pre()
result.SecretsFound=0;secretTextValue='0 / 1';s=Adapter.sample()
assert(s.finalResult.confirmed and s.finalResult.secretsFound==0 and s.verified)
meta.GetEnemiesKilled=function()return 6 end;meta.SecretesFoundCount=1;hooks[Profile.hooks.end_level].pre()
result.SecretsFound=1;result.KillPercentage=85.714286804199;secretTextValue='1 / 1';killTextValue='85%';s=Adapter.sample()
assert(s.finalResult.confirmed and s.finalResult.killPercent==85 and s.verified)
beforeMap();assert(Adapter.final_result==nil and Adapter.result_widget==nil and Adapter.generation>0)
observedResult=result
''');print('PASS: false flags, unloaded actors, one-time discovery and final-state reset')
print('PASS: rootless zero-origin actors, invalid roots/transforms, real origin targets and reactivation without scans')
origin=json.loads((ROOT/'tests/fixtures/enemy-origin-c2m9.json').read_text(encoding='utf-8'))
for actor in origin['actors']:
    l.globals().OriginBefore=l.table_from(dict(zip(('X','Y','Z'),actor['before'])))
    l.execute('''
    Adapter.final_result=nil;Adapter.ended=false;Adapter.result_widget=nil
    enemy.RootComponent=enemyRoot;enemyPosition=OriginBefore
    local s=Adapter.sample();assert(#s.enemies==1)
    -- The real log supplies the coordinate jump. Root loss is the explicitly
    -- simulated failure condition; it was not present in the old telemetry.
    enemy.RootComponent=invalid;enemyPosition={X=0,Y=0,Z=0}
    for i=1,100 do s=Adapter.sample();assert(#s.enemies==0) end
    enemy.RootComponent=enemyRoot;enemyPosition=OriginBefore
    s=Adapter.sample();assert(#s.enemies==1 and s.enemies[1].position.x==OriginBefore.X)
    enemyPosition={X=0,Y=0,Z=0};s=Adapter.sample()
    assert(#s.enemies==1, 'Origin alone is not a reason to suppress a target')
    ''')
print('PASS: real C2M9 coordinate jumps replayed with simulated root loss, genuine origin and recovery')
observed=json.loads((ROOT/'tests/fixtures/settlement-c1m2.json').read_text(encoding='utf-8'))
l.globals().Observed=l.table_from(observed)
l.execute('''
local result=observedResult
Catalog[Observed.level]={secretsTotal=Observed.secretsTotal,secrets={}}
world=object('World',Observed.level..'.'..Observed.level)
meta.GetEnemiesKilled=function()return Observed.killed end
meta.GetEnemiesSpawned=function()return Observed.spawned end
meta.GetTotalLevelSecrets=function()return Observed.secretsTotal end
meta.SecretesFoundCount=Observed.secretsFound
Adapter.sample();hooks[Profile.hooks.end_level].pre()
notify['/Script/UMG.UserWidget'](result)
result.KillPercentage=98.5;result.SecretsFound=Observed.secretsFound;result.TotalSecrets=Observed.secretsTotal
killTextValue=Observed.killsText;secretTextValue=Observed.secretsText
local s=Adapter.sample();assert(not s.finalResult.confirmed, 'Different raw percentage with same display must be rejected')
result.KillPercentage=Observed.widgetKillPercentage;killTextValue='99%'
s=Adapter.sample();assert(not s.finalResult.confirmed, 'Mismatched display must be rejected')
killTextValue=Observed.killsText;secretTextValue='0'
s=Adapter.sample();assert(not s.finalResult.confirmed, 'Animated secrets must be rejected')
secretTextValue=Observed.secretsText;s=Adapter.sample()
assert(s.mode=='results' and s.verified and s.finalResult.confirmed)
assert(s.finalResult.killPercent==98 and s.finalResult.secretsFound==6 and s.finalResult.secretsTotal==6)
beforeMap();assert(Adapter.final_result==nil)
''');print('PASS: real C1M2 170/173 kills, raw 98.26589966, displayed 98%, secrets 6/6')
l.execute('''
-- An arena is admitted by exact world AND reflected game-mode class, never
-- by a Horde prefix, a streaming sublevel name or a campaign fallback.
local fullWorld='World /Game/Maps/_DLC/Horde/Horde_1.Horde_1'
world=object('World','/Game/Maps/_DLC/Horde/Horde_1.Horde_1')
gameMode=object('HordeMode','mode')
gameMode:GetClass().GetFullName=function()return Profile.horde.mode_class end
statics=object('HordeStatics','statics',{}, {
 GetPlayerPawn={params={'WorldContextObject','PlayerIndex','ReturnValue'},fn=function()return player end},
 GetGameMode={params={'WorldContextObject','ReturnValue'},fn=function()return gameMode end}})
Adapter.gameplay_statics=statics
-- Horde must never consult these campaign counters or chapter getters.
meta.GetEnemiesKilled=function()error('Campaign statistics used in horde')end
meta.GetEnemiesSpawned=function()error('Campaign statistics used in horde')end
instance.GetChapterID=function()error('Campaign chapter used in horde')end
secret.alive=true;secret.bSecretFound=false
secret.K2_GetActorLocation=function()return {X=10,Y=20,Z=30}end
enemyDead=false;enemyRoot.alive=true;enemy.RootComponent=enemyRoot
enemyPosition={X=400,Y=100,Z=-500}
local scans=calls.findAll
local s=Adapter.sample()
assert(s.mode=='horde' and s.world==fullWorld and s.gameMode==Profile.horde.mode_class)
assert(#s.enemies==1 and #s.secrets==1 and s.secrets[1].position.x==10)
assert(not s.verified and not s.finalResult and not s.killed and not s.spawned and not s.killPercent)
assert(s.secretsFound==nil and s.secretsTotal==nil and calls.findAll==scans+2)
for i=1,20 do assert(Adapter.pose());assert(Adapter.sample().mode=='horde')end
assert(calls.findAll==scans+2, 'No periodic arena scans')
local waveEnemyDead=false
local waveEnemy=object('WaveEnemy','wave_enemy',{RootComponent=enemyRoot},{
 IsDead={fn=function()return waveEnemyDead end},
 K2_GetActorLocation={fn=function()return {X=1000,Y=2000,Z=300}end}})
notify['/Script/Boltgun.'..Profile.enemy_class](waveEnemy)
s=Adapter.sample();assert(#s.enemies==2 and calls.findAll==scans+2)
enemyDead=true;secret.bSecretFound=true;s=Adapter.sample();assert(#s.enemies==1 and #s.secrets==0)
waveEnemyDead=true;s=Adapter.sample();assert(#s.enemies==0 and not s.killPercent and s.mode=='horde')
hooks[Profile.hooks.end_level].pre();s=Adapter.sample()
assert(s.mode=='horde' and not Adapter.ended and not s.finalResult, 'Campaign EndLevel is ignored in horde')
playerDead=true;s=Adapter.sample();assert(s.mode=='dead' and #s.enemies==0 and Adapter.pose()==nil)
playerDead=false;hooks[Profile.hooks.restart].pre();assert(Adapter.pose()==nil)
enemyDead=false;secret.bSecretFound=false;s=Adapter.sample()
assert(s.mode=='horde' and #s.enemies==1 and #s.secrets==1 and calls.findAll==scans+4)
-- Same-world retries can construct the next wave before the reset hook.
waveEnemyDead=false;notify['/Script/Boltgun.'..Profile.enemy_class](waveEnemy)
hooks[Profile.hooks.restored].post();assert(Adapter.pose()==nil)
s=Adapter.sample();assert(s.mode=='horde' and #s.enemies==2)
gameMode:GetClass().GetFullName=function()return 'BlueprintGeneratedClass /Game/TestMode.TestMode_C' end
s=Adapter.sample();assert(s.mode=='unsupported' and #s.enemies==0 and Adapter.pose()==nil)
gameMode:GetClass().GetFullName=function()return Profile.horde.mode_class end
world=object('World','/Game/Maps/_DLC/Horde/Horde_2.Horde_2')
s=Adapter.sample();assert(s.mode=='unsupported' and #s.enemies==0 and Adapter.pose()==nil)
world=object('World','/Game/Maps/_DLC/Horde/Horde_1_SubLevels/Horde_1_Walkable.Horde_1_Walkable')
s=Adapter.sample();assert(s.mode=='unsupported')
world=object('World','/Game/Maps/_DLC/Horde/Horde_1.Horde_1')
s=Adapter.sample();assert(s.mode=='horde' and #s.enemies==1)
-- Returning to a supported campaign restores its original statistics and
-- metadata; the arena's actor registry and final-state exclusions do not leak.
world=object('World','C1M4.C1M4')
meta.GetEnemiesKilled=function()return 0 end
meta.GetEnemiesSpawned=function()return 7 end
meta.GetTotalLevelSecrets=function()return 1 end
meta.SecretesFoundCount=0
instance.GetChapterID=function()return 0 end
instance.GetLevelID=function()return 3 end
s=Adapter.sample()
assert(s.mode=='campaign' and s.killed==0 and s.spawned==7 and s.secretsTotal==1 and #s.secrets==1 and Adapter.pose())
''');print('PASS: exact horde admission, pose, secret state, wave spawn/death, empty arena, retry, transition and campaign-stat isolation')
c=json.loads((ROOT/'src/data/levels.json').read_text(encoding='utf-8'))
assert len(c)==29 and sum(v['dlc'] for v in c.values())==5
assert sum(v['secretsTotal'] for v in c.values())==185
assert all(v['completeIndex'] and len(v['secrets'])==v['secretsTotal'] for v in c.values())
assert c['C2M5']['chapterIndex']==2 and 'C3M5'not in c and 'C3M6'not in c
print('PASS: 24 base + 5 DLC playable levels, 185 secrets, test maps excluded')
print('RUNTIME_TESTS_PASSED')
