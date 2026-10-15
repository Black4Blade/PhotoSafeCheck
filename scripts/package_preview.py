"""Stage ONLY the self-contained executable for a clean user download.

The separate browser extension is a different GitHub Release asset.
The desktop app offers a first-run choice of Portable (app folder/Data) or Local.
"""
import argparse
import shutil
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--publish', type=Path, required=True)
parser.add_argument('--dest', type=Path, required=True)
parser.add_argument('--rid', choices=('win-x64', 'linux-x64', 'osx-x64', 'osx-arm64'), required=True)
args = parser.parse_args()
exe_name = 'PhotoSafeCheck.exe' if args.rid.startswith('win-') else 'PhotoSafeCheck'
source = args.publish / exe_name
if not source.is_file():
    raise SystemExit(f'Single-file executable missing: {source}')
if (args.publish / 'Data').exists() or (args.publish / 'state.json').exists():
    raise SystemExit('Publish directory contains private scan data; refusing to package.')
if args.dest.exists():
    raise SystemExit(f'Refusing to overwrite existing destination: {args.dest}')
# The archive's ONE top-level folder holds only the executable. Do not add
# runtime files, documentation, extension or private state here.
folder = args.dest / 'PhotoSafeCheck'
folder.mkdir(parents=True)
target = folder / exe_name
shutil.copy2(source, target)
if args.rid.startswith(('linux-', 'osx-')):
    target.chmod(target.stat().st_mode | 0o755)
print(f'Staged standalone executable: {target}')
