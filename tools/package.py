"""Create allowlisted runtime/source archives and their SHA256 checksums."""
from pathlib import Path
import argparse
import hashlib
import json
import zipfile

ROOT = Path(__file__).resolve().parents[1]
VERSION = '0.1.0'


def write_archive(output, base, files):
    with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED) as archive:
        for name in sorted(files):
            path = (base / name).resolve()
            if not path.is_relative_to(base) or not path.is_file():
                raise ValueError('Unexpected package file: ' + name)
            archive.write(path, 'BoltgunCompletion/' + name)
    with zipfile.ZipFile(output) as archive:
        if archive.testzip() is not None:
            raise ValueError('Corrupt output archive')
        if any(name.startswith('BoltgunCompletion/data/') or name.endswith(('.log', '.dmp', '.sav', '.pyc'))
               for name in archive.namelist()):
            raise ValueError('Private or generated data in archive')
    print('Verified:', output.name, output.stat().st_size, 'bytes;', len(files), 'files')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--from-dir', type=Path, default=ROOT / 'dist/BoltgunCompletion')
    args = parser.parse_args()
    base = args.from_dir.resolve()
    if not base.is_relative_to((ROOT / 'dist').resolve()):
        raise ValueError('Unexpected package source')
    manifest = json.loads((base / 'package-files.json').read_text(encoding='utf-8'))
    for name, digest in manifest.items():
        path = (base / name).resolve()
        if not path.is_relative_to(base) or hashlib.sha256(path.read_bytes()).hexdigest() != digest:
            raise ValueError('Package integrity failed: ' + name)
    runtime_files = set(manifest) | {
        'package-files.json', 'BoltgunCompletion.exe', 'BoltgunCompletion-cli.exe',
        '使用说明.md', 'licenses/UE4SS-MIT.txt',
    }
    runtime_zip = ROOT / ('dist/BoltgunCompletion-' + VERSION + '-win-x64.zip')
    write_archive(runtime_zip, base, runtime_files)

    source_files = {'.gitignore', 'README.md', 'CHANGELOG.md', 'THIRD_PARTY_NOTICES.md',
                    'tools/build.py', 'tools/fetch_dependencies.py', 'tools/package.py'}
    for directory in ('src', 'tests', 'licenses'):
        for path in (ROOT / directory).rglob('*'):
            if path.is_file() and '__pycache__' not in path.parts and path.suffix != '.pyc':
                source_files.add(path.relative_to(ROOT).as_posix())
    source_zip = ROOT / ('dist/BoltgunCompletion-' + VERSION + '-source.zip')
    write_archive(source_zip, ROOT, source_files)
    checksums = ''.join(hashlib.sha256(path.read_bytes()).hexdigest() + '  ' + path.name + '\n'
                        for path in (runtime_zip, source_zip))
    (ROOT / 'dist/SHA256SUMS.txt').write_text(checksums, encoding='utf-8')


if __name__ == '__main__':
    main()
