local Bridge={sequence=0,pose_sequence=0,tick=0,pending=false,session=tostring(os.time())..'-'..tostring({}):gsub('table: ',''),failures=0}
local function emit(state)
    Bridge.sequence=Bridge.sequence+1
    state.v=1; state.session=Bridge.session; state.seq=Bridge.sequence
    state.build=Profile.build; state.pluginVersion='0.1.0'
    state.revision=Profile.revision
    print('[BGC_STATE]'..Json.encode(state)..'\n')
end
local function emit_pose(state)
    if not state or not state.player or type(state.player.x)~='number' or type(state.player.y)~='number' or type(state.player.z)~='number' or type(state.yaw)~='number' then return end
    Bridge.pose_sequence=Bridge.pose_sequence+1
    print('[BGC_POSE]'..Json.encode({v=1,session=Bridge.session,poseSeq=Bridge.pose_sequence,
        build=Profile.build,pluginVersion='0.1.0',revision=Profile.revision,
        world=state.world,level=state.level,generation=state.generation,player=state.player,yaw=state.yaw})..'\n')
end
Adapter.install()
emit({mode='initializing',generation=0,verified=false})
LoopAsync(100,function()
    if Bridge.pending then return false end
    Bridge.pending=true
    local ok,err=pcall(function() ExecuteInGameThread(function()
        local started=os.clock()
        local full=Bridge.tick%5==0
        Bridge.tick=Bridge.tick+1
        local success,state=pcall(full and Adapter.sample or Adapter.pose)
        Bridge.pending=false
        if success then
            Bridge.failures=0
            if full then
                state.sampleMs=(os.clock()-started)*1000
                emit(state)
                if state.mode=='campaign' then emit_pose(state) end
            else emit_pose(state) end
        else
            Bridge.failures=Bridge.failures+1
            if full then emit({mode='error',generation=Adapter.generation,verified=false,error=tostring(state)}) end
        end
    end) end)
    if not ok then Bridge.pending=false end
    return false
end)
return Bridge
