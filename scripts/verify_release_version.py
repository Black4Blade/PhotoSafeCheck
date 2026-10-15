"""Check UI/assembly version consistency and ensure release tag matches published app."""
from pathlib import Path
import os
import re
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
version = ET.parse(root / 'PhotoSafeCheck' / 'PhotoSafeCheck.csproj').findtext('.//Version')
assert version and re.fullmatch(r'[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?', version), 'Invalid .NET version'
program = (root / 'PhotoSafeCheck' / 'Program.cs').read_text(encoding='utf-8')
window = (root / 'PhotoSafeCheck' / 'MainWindow.cs').read_text(encoding='utf-8')
assert 'Starting PhotoSafe Check v' + version + '; OS=' in program, 'Startup log version differs'
assert ('v' + version) in window, 'Window version differs'
ref = os.environ.get('GITHUB_REF', '')
requested = ref[len('refs/tags/'):] if ref.startswith('refs/tags/v') else (
    os.environ.get('MANUAL_RELEASE_TAG', '') if os.environ.get('AFTER_BUILD') == 'draft_release' else ''
)
if requested:
    assert requested == 'v' + version, f'Tag {requested} differs from app v{version}; update .csproj before release'
print('PhotoSafe Check app version OK:', version, 'release tag:', requested or '(none)')
