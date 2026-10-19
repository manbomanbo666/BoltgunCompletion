local J = {}
function J.array(t) return setmetatable(t or {}, {__json_array=true}) end
local function quote(s)
    local escaped = s:gsub('[%z\1-\31\\"]', function(c)
        local escapes = {['"']='\\"', ['\\']='\\\\', ['\n']='\\n', ['\r']='\\r', ['\t']='\\t'}
        return escapes[c] or string.format('\\u%04x', string.byte(c))
    end)
    -- UE4SS console/log encoding depends on the Windows code page. Keep the
    -- protocol ASCII so Chinese warnings and paths survive every code page.
    escaped = escaped:gsub('[\194-\244][\128-\191]+', function(c)
        local n = utf8.codepoint(c)
        if n <= 65535 then return string.format('\\u%04x', n) end
        n=n-65536
        return string.format('\\u%04x\\u%04x',55296+(n>>10),56320+(n&1023))
    end)
    return '"' .. escaped .. '"'
end
function J.encode(value)
    local t = type(value)
    if t == 'nil' then return 'null' end
    if t == 'boolean' then return value and 'true' or 'false' end
    if t == 'number' then
        if value ~= value or value == math.huge or value == -math.huge then return 'null' end
        return string.format('%.10g', value)
    end
    if t == 'string' then return quote(value) end
    if t ~= 'table' then error('Cannot serialize ' .. t) end
    local parts = {}
    if getmetatable(value) and getmetatable(value).__json_array then
        for _, v in ipairs(value) do parts[#parts+1] = J.encode(v) end
        return '[' .. table.concat(parts, ',') .. ']'
    end
    for k, v in pairs(value) do parts[#parts+1] = quote(tostring(k)) .. ':' .. J.encode(v) end
    table.sort(parts)
    return '{' .. table.concat(parts, ',') .. '}'
end
return J
