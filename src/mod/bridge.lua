local Bridge={sequence=0,pending=false,session=tostring(os.time())..'-'..tostring({}):gsub('table: ',''),failures=0}
local function emit(state)
    Bridge.sequence=Bridge.sequence+1
    state.v=1; state.session=Bridge.session; state.seq=Bridge.sequence
    state.build=Profile.build; state.pluginVersion='0.1.0'
    print('[BGC_STATE]'..Json.encode(state)..'\n')
end
Adapter.install()
emit({mode='initializing',generation=0,verified=false})
LoopAsync(500,function()
    if Bridge.pending then return false end
    Bridge.pending=true
    local ok,err=pcall(function() ExecuteInGameThread(function()
        local success,state=pcall(Adapter.sample)
        Bridge.pending=false
        if success then
            Bridge.failures=0
            emit(state)
        else
            Bridge.failures=Bridge.failures+1
            emit({mode='error',generation=Adapter.generation,verified=false,error=tostring(state)})
        end
    end) end)
    if not ok then Bridge.pending=false end
    return false
end)
return Bridge
