"""Package only the extension, with manifest.json at ZIP root, for store upload / GitHub release.
Refuses to overwrite an existing artifact.
"""
import argparse
import json
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED

parser = argparse.ArgumentParser()
parser.add_argument('--dest', required=True, type=Path)
args = parser.parse_args()
root = Path(__file__).resolve().parents[1] / 'Extension'
manifest = json.loads((root / 'manifest.json').read_text(encoding='utf-8'))
assert manifest.get('manifest_version') == 3, 'Manifest V3 expected'
assert not args.dest.exists(), 'Refusing to overwrite existing extension ZIP'
args.dest.parent.mkdir(parents=True, exist_ok=True)
files = sorted(p for p in root.rglob('*') if p.is_file() and not p.name.startswith('.'))
with ZipFile(args.dest, 'x', ZIP_DEFLATED) as archive:
    for file in files:
        archive.write(file, file.relative_to(root).as_posix())
print(f'Extension v{manifest["version"]} ready: {args.dest}')
