local A = {generation=0, world=nil, level=nil, registry={}, pending={}, schemas={},
           needs_bootstrap=true, hooked={}, diagnostics_done=false, final_result=nil, secret_states={}, secret_positions={}, enemy_events=0}
local P = Profile
local function attempt(fn)
    local ok, value = pcall(fn)
    if ok then return value end
    return nil
end
local function valid(o) return o ~= nil and attempt(function() return o:IsValid() end) == true end
local function name(o) return valid(o) and attempt(function() return o:GetFullName() end) or nil end
local function value(o, key)
    if not valid(o) then return nil end
    return attempt(function() return o[key] end)
end
local function short(o) return valid(o) and attempt(function() return o:GetFName():ToString() end) or nil end
local function number(v) return type(v)=='number' and v==v and v>=0 and v<1000000000 and v or nil end
local function unwrap(o) return attempt(function() return o:get() end) or o end
local function schema(cls)
    local id = name(cls)
    if not id then return {properties={},functions={}} end
    if A.schemas[id] then return A.schemas[id] end
    local s = {properties={},functions={}}
    local current, depth = cls, 0
    while valid(current) and depth<16 do
        attempt(function() current:ForEachProperty(function(p) s.properties[p:GetFName():ToString()]=true end) end)
        attempt(function() current:ForEachFunction(function(f)
            local params, types = {}, {}
            f:ForEachProperty(function(p)
                local key=p:GetFName():ToString()
                params[#params+1]=key
                types[key]=attempt(function() return p:GetClass():GetFName():ToString() end)
            end)
            s.functions[f:GetFName():ToString()]={path=f:GetFullName():gsub('^Function ',''),params=params,types=types}
        end) end)
        current = attempt(function() return current:GetSuperStruct() end)
        depth = depth+1
    end
    A.schemas[id]=s
    return s
end
local function describe(o) return schema(attempt(function() return o:GetClass() end)) end
local function primitive(o,key)
    if not valid(o) or not describe(o).properties[key] then return nil end
    local v = value(o,key)
    if type(v)=='number' or type(v)=='boolean' or type(v)=='string' then return v end
    return nil
end
local function noarg(o, method)
    if not valid(o) then return nil end
    local f = describe(o).functions[method]
    if not f then return nil end
    for _, p in ipairs(f.params) do if p~='ReturnValue' then return nil end end
    return attempt(function() return o[method](o) end)
end
local function call(o,method,expected,...)
    if not valid(o) then return nil end
    local f=describe(o).functions[method]
    if not f or #f.params~=#expected then return nil end
    for i,key in ipairs(expected) do if f.params[i]~=key then return nil end end
    local args={...}
    return attempt(function() return o[method](o,table.unpack(args)) end)
end
local function location(o)
    local pos = noarg(o,'K2_GetActorLocation')
    if not pos then return nil end
    return attempt(function() return {x=pos.X,y=pos.Y,z=pos.Z} end)
end
local function enemy_location(o)
    -- Actor validity and IsDead alone do not establish a usable transform.
    -- A detached/destroyed root can leave GetActorLocation returning zero.
    -- Check the reflected component reference, never blacklist the origin:
    -- a rooted, live actor is allowed to actually stand at (0,0,0).
    if not describe(o).properties.RootComponent then return nil,'rootSchemaMissing' end
    local root=value(o,'RootComponent')
    if not valid(root) then return nil,'rootMissing' end
    local pos=location(o)
    if not pos then return nil,'positionInvalid' end
    for _,axis in ipairs({'x','y','z'}) do
        local n=pos[axis]
        if type(n)~='number' or n~=n or math.abs(n)>=1e12 then return nil,'positionInvalid' end
    end
    return pos
end
local function widget_text(widget,field)
    local block=value(widget,field)
    local text=noarg(block,'GetText')
    if type(text)=='string' then return text end
    return attempt(function() return text:ToString() end)
end
local function progress_value(killed,spawned)
    if not killed or not spawned or spawned<=0 then return nil end
    local function f32(n) return (string.unpack('f',string.pack('f',n))) end
    return f32(f32(f32(killed)/f32(spawned))*100)
end
local function progress_percent(killed,spawned)
    local percentage=progress_value(killed,spawned)
    return percentage and math.floor(percentage) or nil
end
local function find(class)
    local o = attempt(function() return FindFirstOf(class) end)
    return valid(o) and o or nil
end
function A.reset()
    A.generation=A.generation+1
    A.registry={}; A.world=nil; A.level=nil; A.secret_states={}; A.secret_positions={}
    -- Construction notifications for the next world may arrive before reset.
    -- Keep the bounded pending queue; each item is filtered by its actual world.
    A.needs_bootstrap=true; A.final_result=nil; A.meta=nil; A.player=nil; A.ended=false
    A.result_widget=nil; A.results_inspected=false; A.result_widget_generation=nil
    A.last_kills_text=nil; A.last_secrets_text=nil
end
local function in_world(o)
    return valid(o) and name(attempt(function() return o:GetWorld() end)) == A.world
end
local function add(o,kind)
    if not valid(o) or not in_world(o) then return end
    local id = name(o)
    if not id or id:find('Default__',1,true) then return end
    A.registry[id]={object=o,kind=kind}
end
function A.inspect()
    if not P.diagnostics_enabled or A.diagnostics_done then return end
    A.diagnostics_done=true
    for _, cname in ipairs(P.inspect_classes) do
        local cls=attempt(function() return StaticFindObject('/Script/Boltgun.'..cname) end)
        if valid(cls) then
            local s=schema(cls)
            local props, funcs = Json.array(), Json.array()
            for key in pairs(s.properties) do props[#props+1]=key end
            for key,f in pairs(s.functions) do
                if not f.path:find('/Script/Engine.',1,true) then
                    funcs[#funcs+1]={name=key,path=f.path,params=Json.array(f.params),types=f.types}
                end
            end
            print('[BGC_SCHEMA]'..Json.encode({class=cname,properties=props,functions=funcs})..'\n')
        end
    end
    local en=attempt(function() return StaticFindObject('/Script/Boltgun.EBGStatTrackingLevel') end)
    if valid(en) then
        local values=Json.array()
        attempt(function() en:ForEachName(function(n,v) values[#values+1]={name=n:ToString(),value=v} end) end)
        print('[BGC_SCHEMA]'..Json.encode({enum='EBGStatTrackingLevel',values=values})..'\n')
    end
    local result_class=attempt(function() return StaticFindObject(P.results_asset) end)
    if valid(result_class) then
        local fields=Json.array()
        for key in pairs(schema(result_class).properties) do
            if key:lower():find('kill') or key:lower():find('secret') or key:lower():find('stage') then fields[#fields+1]=key end
        end
        print('[BGC_SCHEMA]'..Json.encode({class=P.results_class,properties=fields})..'\n')
    end
end
function A.install()
    attempt(function() RegisterLoadMapPreHook(function() A.reset() end) end)
    attempt(function() NotifyOnNewObject('/Script/Boltgun.BGGameInstance',function(o) A.instance=o end) end)
    for _, pair in ipairs({{P.secret_class,'secret'},{P.enemy_class,'enemy'}}) do
        local cname,kind=pair[1],pair[2]
        attempt(function() NotifyOnNewObject('/Script/Boltgun.'..cname,function(o)
            -- Do not inspect an object while its constructor is still running.
            if #A.pending<4096 then A.pending[#A.pending+1]={object=o,kind=kind} end
        end) end)
    end
    local function hook(path,pre,post)
        local ok,err=pcall(function() RegisterHook(path,pre or function() end,post) end)
        if not ok then print('[BGC_EVENT]'..Json.encode({hook=path,error=tostring(err)})..'\n') end
    end
    hook(P.hooks.restart,function() A.reset() end)
    hook(P.hooks.restored,nil,function()
        A.registry={}; A.secret_states={}; A.secret_positions={}; A.needs_bootstrap=true; A.final_result=nil; A.ended=false
        A.result_widget=nil; A.result_widget_generation=nil; A.results_inspected=false
        A.last_kills_text=nil; A.last_secrets_text=nil
        A.generation=A.generation+1
    end)
    hook(P.hooks.secret,nil,function()
        print('[BGC_EVENT]'..Json.encode({event='secret_found',generation=A.generation,world=A.world})..'\n')
    end)
    hook(P.hooks.enemy_killed,nil,function(_,enemy_type)
        A.enemy_events=A.enemy_events+1
        local kind=attempt(function() return enemy_type:get() end)
        print('[BGC_EVENT]'..Json.encode({event='enemy_killed',generation=A.generation,type=type(kind)=='number' and kind or tostring(kind)})..'\n')
    end)
    hook(P.hooks.end_level,function()
        if not A.world or not A.level or not Catalog[A.level] then return end
        local meta=A.meta
        local killed=number(noarg(meta,'GetEnemiesKilled'))
        local spawned=number(noarg(meta,'GetEnemiesSpawned'))
        local found=number(primitive(meta,'SecretesFoundCount'))
        A.final_result={source='game_settlement',confirmed=false,killed=killed,spawned=spawned,
            secretsFound=found,secretsTotal=Catalog[A.level].secretsTotal,world=A.world,level=A.level,generation=A.generation}
        print('[BGC_EVENT]'..Json.encode({event='end_level',result=A.final_result})..'\n')
        A.ended=true
    end)
    -- UMG is a native base class that exists before the results blueprint loads.
    -- Only class metadata is inspected in the constructor callback.
    attempt(function() NotifyOnNewObject('/Script/UMG.UserWidget',function(o)
        local object_name=short(o)
        if object_name and not object_name:find('Default__',1,true) and short(attempt(function() return o:GetClass() end))==P.results_class then
            A.result_widget=o; A.result_widget_generation=A.generation
        end
    end) end)
end
-- Lightweight pose path: only the already validated player getters, no actor
-- discovery, enemy iteration, statistics, or resource loading.
function A.pose()
    local player=A.player
    if A.ended or not A.world or not A.level or not Catalog[A.level] or not in_world(player) then return nil end
    if primitive(player,'bBeginPlayComplete')==false or primitive(player,'bPlayerFinishedLevelLoad')==false or noarg(player,'IsDead')==true then return nil end
    local pos=location(player)
    local rot=noarg(player,'K2_GetActorRotation')
    local yaw=rot and attempt(function() return rot.Yaw end)
    if not pos or type(pos.x)~='number' or type(pos.y)~='number' or type(pos.z)~='number' or type(yaw)~='number' then return nil end
    return {world=A.world,level=A.level,generation=A.generation,player=pos,yaw=yaw}
end
function A.sample()
    A.inspect()
    local out={mode='main_menu',verified=P.statistics_verified,generation=A.generation,
               secrets=Json.array(),enemies=Json.array(),warnings=Json.array()}
    if not A.references_initialized then
        A.references_initialized=true
        A.instance=find('BGGameInstance')
        A.gameplay_statics=attempt(function() return StaticFindObject('/Script/Engine.Default__GameplayStatics') end)
    end
    local instance=A.instance
    local player=call(A.gameplay_statics,'GetPlayerPawn',{'WorldContextObject','PlayerIndex','ReturnValue'},instance,0)
    A.player=player
    if not valid(player) then return out end
    local world=name(attempt(function() return player:GetWorld() end))
    if not world then out.mode='loading'; return out end
    if primitive(player,'bBeginPlayComplete')==false or primitive(player,'bPlayerFinishedLevelLoad')==false then
        out.mode='loading'; return out
    end
    if not A.ended and noarg(player,'IsDead')==true then out.mode='dead';return out end
    if A.world and A.world~=world then A.reset(); A.player=player end
    A.world=world
    A.level = world:match('%.([^%.:]+)$') or world
    out.world=world; out.generation=A.generation
    out.level=A.level
    local level_data=Catalog[A.level]
    if not level_data then A.pending={};out.mode='unsupported'; return out end
    out.mode='campaign'
    out.secretsTotal=level_data.secretsTotal
    A.meta=value(instance,'Metagame')
    local meta=A.meta
    local chapter=noarg(instance,'GetChapterID')
    local level_id=noarg(instance,'GetLevelID')
    local runtime_total
    if type(chapter)=='number' and type(level_id)=='number' then
        runtime_total=call(meta,'GetTotalLevelSecrets',{'ChapterID','LevelID','ReturnValue'},chapter,level_id)
    end
    if type(runtime_total)=='number' and runtime_total~=out.secretsTotal then
        out.warnings[#out.warnings+1]='关卡秘密总数与目录不一致，判定已停止'
        out.verified=false
    end
    out.secretsFound=number(primitive(meta,'SecretesFoundCount'))
    out.killed=number(noarg(meta,'GetEnemiesKilled'))
    out.spawned=number(noarg(meta,'GetEnemiesSpawned'))
    -- The base-game results blueprint uses int->float, division, *100 and FFloor.
    -- Keep this as a progress estimate until a real results widget confirms it.
    out.killPercent=progress_percent(out.killed,out.spawned)
    out.player=location(player)
    local rot=noarg(player,'K2_GetActorRotation')
    if rot then out.yaw=attempt(function() return rot.Yaw end) end
    -- Bootstrap only once per world, never in a recurring all-object scan.
    if A.needs_bootstrap then
        A.needs_bootstrap=false
        for _,pair in ipairs({{P.secret_class,'secret'},{P.enemy_class,'enemy'}}) do
            local objects=attempt(function() return FindAllOf(pair[1]) end) or {}
            for _,o in ipairs(objects) do A.pending[#A.pending+1]={object=o,kind=pair[2]} end
        end
        A.inspect()
    end
    local pending=A.pending; A.pending={}
    for _,p in ipairs(pending) do add(p.object,p.kind) end
    local loaded_secrets={}
    local unlocated_enemies=0
    local spatial_checks={rootSchemaMissing=0,rootMissing=0,positionInvalid=0}
    for id,entry in pairs(A.registry) do
        local o=entry.object
        if not valid(o) or not in_world(o) then A.registry[id]=nil
        elseif entry.kind=='secret' then
            local found=primitive(o,'bSecretFound')
            local key=short(o)
            loaded_secrets[key]=true
            if type(found)=='boolean' then A.secret_states[key]=found end
            local current_position=location(o)
            if current_position then A.secret_positions[key]=current_position end
        elseif entry.kind=='enemy' then
            -- Only a reflected, validated death getter can classify a live target.
            local dead=noarg(o,'IsDead')
            if type(dead)=='boolean' and not dead then
                local pos,reason=enemy_location(o)
                if pos then
                    out.enemies[#out.enemies+1]={id=id,label=short(o),position=pos,known=true}
                else
                    -- Retain the registry entry for possible reactivation.
                    -- Missing spatial state never means a confirmed death.
                    unlocated_enemies=unlocated_enemies+1
                    spatial_checks[reason]=spatial_checks[reason]+1
                end
            end
        end
    end
    if unlocated_enemies>0 then
        out.enemySpatialChecks=spatial_checks
        out.warnings[#out.warnings+1]=tostring(unlocated_enemies)..' 个敌人对象缺少有效空间状态，未绘制，未计为死亡'
    end
    for _,secret in ipairs(level_data.secrets) do
        local found=A.secret_states[secret.name]
        if found~=true then
            out.secrets[#out.secrets+1]={id=secret.guid or secret.name,label=secret.name,position=A.secret_positions[secret.name] or secret.position,
                known=loaded_secrets[secret.name]==true and type(found)=='boolean'}
        end
    end
    if A.ended and A.final_result and A.final_result.world==A.world then
        out.mode='results'
        local widget=A.result_widget
        if valid(widget) and A.result_widget_generation==A.generation then
            local ws=describe(widget)
            if not A.results_inspected then
                A.results_inspected=true
                local properties={}
                for key in pairs(ws.properties) do
                    if key:lower():find('kill') or key:lower():find('secret') or key:lower():find('stage') then
                        properties[key]=primitive(widget,key) or attempt(function() return tostring(widget[key]) end)
                    end
                end
                print('[BGC_SCHEMA]'..Json.encode({class=P.results_class,values=properties})..'\n')
            end
            -- Read the actual displayed result, not its animated intermediate
            -- percentage or campaign-history maxima. Wait until both displayed
            -- values match this EndLevel event's captured target counts.
            local kills_text=widget_text(widget,P.results_kills_text)
            local secrets_text=widget_text(widget,P.results_secrets_text)
            if kills_text~=A.last_kills_text or secrets_text~=A.last_secrets_text then
                A.last_kills_text=kills_text; A.last_secrets_text=secrets_text
                print('[BGC_EVENT]'..Json.encode({event='result_display',generation=A.generation,
                    killsText=kills_text,secretsText=secrets_text})..'\n')
            end
            local percent=type(kills_text)=='string' and tonumber(kills_text:match('(%d+)%s*%%')) or nil
            local found,total
            if type(secrets_text)=='string' then
                local a,b=secrets_text:match('(%d+)%s*/%s*(%d+)')
                found,total=tonumber(a),tonumber(b)
            end
            local expected_value=progress_value(A.final_result.killed,A.final_result.spawned)
            local target_value=number(primitive(widget,'KillPercentage'))
            -- The live C1M2 widget stores 98.26589966 for 170/173 kills,
            -- while its text displays 98%. Check both representations against
            -- this run's captured counts, allowing one float32 ULP near 100.
            local targets_match=expected_value and target_value and target_value<=100
                and math.abs(target_value-expected_value)<=0.00001
                and math.floor(target_value)==percent and primitive(widget,'SecretsFound')==found
                and primitive(widget,'TotalSecrets')==total
            if percent and percent<=100 and targets_match and found==A.final_result.secretsFound and total==A.final_result.secretsTotal then
                A.final_result.killPercent=percent; A.final_result.secretsFound=found; A.final_result.secretsTotal=total
                if not A.final_result.confirmed then
                    print('[BGC_EVENT]'..Json.encode({event='result_confirmed',killsText=kills_text,secretsText=secrets_text,generation=A.generation})..'\n')
                end
                A.final_result.confirmed=true
            end
        end
        out.finalResult=A.final_result
        -- A live, same-run result can be verified independently of estimated
        -- campaign progress. Require a runtime/catalog total cross-check too.
        out.verified=P.settlement_fields_verified==true and A.final_result.confirmed==true
            and runtime_total==out.secretsTotal
    end
    if out.mode=='campaign' then out.warnings[#out.warnings+1]='未激活波次及未加载区域待核实' end
    if not out.verified then out.warnings[#out.warnings+1]='统计口径尚待游戏结算核对' end
    return out
end
return A
