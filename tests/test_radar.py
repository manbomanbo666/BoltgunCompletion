"""Replay captured dense geometry through the actual C# radar, without the game."""
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / 'work' / 'radar-tests'

def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    executable = OUTPUT / 'RadarReplay.exe'
    compiler = Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
    sources = sorted((ROOT / 'src').glob('*.cs')) + [ROOT / 'tests' / 'RadarReplay.cs']
    subprocess.run([str(compiler), '/nologo', '/platform:x64', '/target:exe',
                    '/main:RadarReplay', '/out:' + str(executable),
                    '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll',
                    '/r:System.Web.Extensions.dll'] + [str(p) for p in sources], check=True)
    subprocess.run([str(executable), str(ROOT / 'tests' / 'fixtures' / 'radar-dense-scenes.json'),
                    str(OUTPUT)], check=True)

if __name__ == '__main__':
    main()
