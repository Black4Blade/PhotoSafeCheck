"""Bundle a locally published PhotoSafeCheck app with its browser companion.

This script does not sign or distribute builds. It only creates a preview folder.
"""
import argparse
import shutil
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--publish', type=Path, required=True)
parser.add_argument('--dest', type=Path, required=True)
parser.add_argument('--rid', required=True)
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
assert args.publish.is_dir(), f'publish folder does not exist: {args.publish}'
assert args.rid in {'win-x64', 'linux-x64', 'osx-arm64', 'osx-x64'}
assert (args.publish / ('PhotoSafeCheck.exe' if args.rid == 'win-x64' else 'PhotoSafeCheck')).is_file(), 'App executable missing'
if (args.publish / 'Data').exists():
    raise SystemExit('Publish folder includes user Data; refusing to package private state.')
if args.dest.exists():
    raise SystemExit('Destination exists; refusing to overwrite. Choose a fresh destination.')
args.dest.mkdir(parents=True)
shutil.copytree(args.publish, args.dest / 'App')
# Marker only suggests Portable in first-run chooser. It does not force the mode.
# No Data directory or private state is shipped.
(args.dest / 'App' / 'portable.mode').write_text(
    'Suggested portable distribution; choose Portable or Local on first launch.\n',
    encoding='utf-8')
shutil.copytree(root / 'Extension', args.dest / 'Extension')
for name in ('README.md', 'PORTABLE_GUIDE.md', 'SETUP_GUIDE.md', 'RELEASE_CHECKLIST.md', 'CHANGELOG.md', 'EXTENSION_DISTRIBUTION.md', 'STORAGE_MODES.md'):
    shutil.copy2(root / name, args.dest / name)
print('First launch asks user to choose Portable or Local. Portable suggests App/Data.')
print(f'Preview created: {args.dest} ({args.rid}). Not signed or installer-packaged.')
