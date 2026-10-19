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
queue={};loops={};samples=0;installs=0
Adapter={generation=0,install=function()installs=installs+1 end,sample=function()samples=samples+1;return {mode='campaign',generation=0} end}
LoopAsync=function(interval,f)loops[#loops+1]={interval=interval,f=f}end
ExecuteInGameThread=function(f)queue[#queue+1]=f end
GenerateSDK=function()error('Forbidden SDK generation')end
ForEachUObject=function()error('Forbidden global scan')end
''');l.execute((ROOT/'src/mod/main.lua').read_text(encoding='utf-8'))
l.execute('''
assert(installs==1 and #loops==1 and loops[1].interval==500 and samples==0)
loops[1].f();loops[1].f();assert(#queue==1)
table.remove(queue,1)();assert(samples==1)
loops[1].f();assert(#queue==1)
''');print('PASS: one 2 Hz game-thread sampler, no piled-up jobs or SDK generation')
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
player=object('Player','player',{bBeginPlayComplete=true,bPlayerFinishedLevelLoad=true}, {IsDead={fn=function()return playerDead end},K2_GetActorLocation={fn=function()return {x=0,y=0,z=0}end},K2_GetActorRotation={fn=function()return {Yaw=0}end}})
meta=object('Meta','meta',{SecretesFoundCount=0},{GetEnemiesKilled={fn=function()return 0 end},GetEnemiesSpawned={fn=function()return 7 end},GetTotalLevelSecrets={params={'ChapterID','LevelID','ReturnValue'},fn=function()return 1 end}})
instance=object('Instance','instance',{Metagame=meta},{GetChapterID={fn=function()return 0 end},GetLevelID={fn=function()return 3 end}})
statics=object('Statics','statics',{}, {GetPlayerPawn={params={'WorldContextObject','PlayerIndex','ReturnValue'},fn=function()return player end}})
secret=object('Secret','secret_one',{bSecretFound=false},{K2_GetActorLocation={fn=function()return {x=10,y=20,z=30}end}})
enemy=object('Enemy','enemy',{}, {IsDead={fn=function()return false end},K2_GetActorLocation={fn=function()return {x=40,y=0,z=0}end}})
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
c=json.loads((ROOT/'src/data/levels.json').read_text(encoding='utf-8'))
assert len(c)==29 and sum(v['dlc'] for v in c.values())==5
assert sum(v['secretsTotal'] for v in c.values())==185
assert all(v['completeIndex'] and len(v['secrets'])==v['secretsTotal'] for v in c.values())
assert c['C2M5']['chapterIndex']==2 and 'C3M5'not in c and 'C3M6'not in c
print('PASS: 24 base + 5 DLC playable levels, 185 secrets, test maps excluded')
print('RUNTIME_TESTS_PASSED')
