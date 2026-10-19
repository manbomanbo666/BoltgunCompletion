from pathlib import Path
import argparse
import hashlib,json,shutil,subprocess
ROOT=Path(__file__).resolve().parents[1]
def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--output',type=Path,default=ROOT/'dist'/'BoltgunCompletion')
    options=parser.parse_args()
    dist=options.output.resolve()
    if not dist.is_relative_to((ROOT/'dist').resolve()):raise ValueError('Build output must remain inside project dist/')
    runtime=dist/'runtime'
    runtime.mkdir(parents=True,exist_ok=True)
    upstream=ROOT/'work'/'ue4ss-pinned'/'ue4ss'
    pinned=json.loads((ROOT/'work'/'pinned-runtime.json').read_text(encoding='utf-8'))
    if pinned['version']!='3.0.1-1181-g83330635': raise ValueError('Unexpected loader version')
    if not (runtime/'UE4SS.dll').exists() or hashlib.sha256((runtime/'UE4SS.dll').read_bytes()).digest()!=hashlib.sha256((upstream/'UE4SS.dll').read_bytes()).digest():
        shutil.copy2(upstream/'UE4SS.dll',runtime/'UE4SS.dll')
    settings=(upstream/'UE4SS-settings.ini').read_text(encoding='utf-8-sig')
    settings=settings.replace('GuiConsoleEnabled = 1','GuiConsoleEnabled = 0').replace('bUseUObjectArrayCache = true','bUseUObjectArrayCache = false').replace('UseCache = 1','UseCache = 0')
    (runtime/'UE4SS-settings.ini').write_text(settings,encoding='utf-8')
    mods=runtime/'Mods'
    script=mods/'BoltgunCompletion'/'Scripts'
    script.mkdir(parents=True,exist_ok=True)
    source=ROOT/'src'/'mod'/'main.lua'
    bundle=[]
    catalog=json.loads((ROOT/'src'/'data'/'levels.json').read_text(encoding='utf-8'))
    revision=hashlib.sha256(b''.join(p.read_bytes() for p in sorted((ROOT/'src'/'mod').glob('*.lua')))+(ROOT/'src'/'data'/'levels.json').read_bytes()).hexdigest()
    (dist/'plugin-release.json').write_text(json.dumps({'version':'0.1.0','revision':revision},indent=2),encoding='utf-8')
    def lua(value):
        if value is None:return 'nil'
        if isinstance(value,bool):return 'true' if value else 'false'
        if isinstance(value,(int,float)):return repr(value)
        if isinstance(value,str):return json.dumps(value,ensure_ascii=True)
        if isinstance(value,list):return '{'+','.join(lua(v) for v in value)+'}'
        return '{'+','.join('['+lua(k)+']='+lua(v) for k,v in value.items())+'}'
    bundle.append('local Catalog='+lua(catalog)+'\n')
    for variable,filename in [('Json','json.lua'),('Profile','profile.lua'),('Adapter','adapter.lua')]:
        bundle.append('local '+variable+' = (function()\n'+(ROOT/'src'/'mod'/filename).read_text(encoding='utf-8')+'\nend)()\n')
        if variable=='Profile':bundle.append('Profile.revision='+json.dumps(revision)+'\n')
    bundle.append(source.read_text(encoding='utf-8'))
    (script/'main.lua').write_text('\n'.join(bundle),encoding='utf-8')
    (mods/'mods.txt').write_text('BoltgunCompletion : 1\n',encoding='utf-8')
    (script.parent/'enabled.txt').write_text('',encoding='utf-8')
    license_dir=dist/'licenses'
    license_dir.mkdir(exist_ok=True)
    shutil.copy2(ROOT/'licenses'/'UE4SS-MIT.txt',license_dir/'UE4SS-MIT.txt')
    shutil.copy2(ROOT/'work'/'pinned-runtime.json',dist/'loader-version.json')
    shutil.copy2(ROOT/'README.md',dist/'使用说明.md')
    csc=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
    for target,name in [('exe','BoltgunCompletion-cli.exe'),('winexe','BoltgunCompletion.exe')]:
        subprocess.run([str(csc),'/nologo','/platform:x64','/target:'+target,'/out:'+str(dist/name),'/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Web.Extensions.dll']+[str(p) for p in sorted((ROOT/'src').glob('*.cs'))],check=True)
    files={str(p.relative_to(dist)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest() for p in [runtime/'UE4SS.dll',runtime/'UE4SS-settings.ini',mods/'mods.txt',script/'main.lua',script.parent/'enabled.txt',dist/'plugin-release.json',dist/'loader-version.json'] if p.exists()}
    (dist/'package-files.json').write_text(json.dumps(files,indent=2),encoding='utf-8')
    print('Built',dist)
if __name__=='__main__':main()
