"""Fetch exact development dependencies; verify digests before extraction."""
import hashlib
import json
from pathlib import Path
import urllib.request
import zipfile
ROOT = Path(__file__).resolve().parents[1]
WORK = ROOT / 'work'
RUNTIME = {
    'version': '3.0.1-1181-g83330635',
    'url': 'https://github.com/UE4SS-RE/RE-UE4SS/releases/download/experimental-latest/UE4SS_v3.0.1-1181-g83330635.zip',
    'sha256': 'b9a166a756605b21058a64cde93bcaab1a855dba3553e5c7e6ce0fa8fdfad7b9',
}
LUPA = {
    'version': '2.8',
    'url': 'https://files.pythonhosted.org/packages/0c/27/05f950d15b8ab120b39c43588b438ff3ace70c1b1b0225a960393a497483/lupa-2.8-cp312-cp312-win_amd64.whl',
    'sha256': '281bedc5deb92d31e649a3552edd662449365a635904fa4d5cb4509c7245e34e',
}
def fetch(url):
    request = urllib.request.Request(url, headers={'User-Agent': 'BoltgunCompletion-dev/0.1'})
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read()
def archive(dependency, target):
    path = WORK / dependency['url'].rsplit('/', 1)[1]
    content = path.read_bytes() if path.exists() else fetch(dependency['url'])
    if hashlib.sha256(content).hexdigest() != dependency['sha256']:
        raise ValueError('Dependency digest mismatch: ' + path.name)
    if not path.exists(): path.write_bytes(content)
    with zipfile.ZipFile(path) as z:
        if z.testzip(): raise ValueError('Corrupt runtime archive')
        for info in z.infolist():
            dest = (target / info.filename).resolve()
            if not dest.is_relative_to(target.resolve()): raise ValueError('Unsafe archive')
        z.extractall(target)

def main():
    WORK.mkdir(exist_ok=True)
    archive(RUNTIME, WORK / 'ue4ss-pinned')
    archive(LUPA, WORK / 'pythonlibs')
    (WORK / 'pinned-runtime.json').write_text(json.dumps(RUNTIME, indent=2), encoding='utf-8')
    (WORK / 'dependency-provenance.json').write_text(json.dumps({
        'ue4ss': RUNTIME,
        'lupa_test_only': LUPA,
    }, indent=2), encoding='utf-8')
    print('Verified pinned runtime and development-only Lua test wheel')
if __name__ == '__main__': main()
