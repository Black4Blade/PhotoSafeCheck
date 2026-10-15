"""Lightweight checks run locally and in GitHub Actions (no .NET required)."""
import importlib.util
import os
from pathlib import Path
import tempfile
import tarfile
import zipfile

spec = importlib.util.spec_from_file_location('archive_release', Path(__file__).with_name('archive_release.py'))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

with tempfile.TemporaryDirectory() as td:
    root = Path(td)
    # On Windows chmod cannot reliably set POSIX execute bits; Unix archive
    # permission tests therefore run on Linux and macOS runners instead.
    targets = ('win-x64',) if os.name == 'nt' else ('win-x64', 'linux-x64', 'osx-x64', 'osx-arm64')
    for rid in targets:
        bundle = root / rid
        exe = bundle / 'App' / ('PhotoSafeCheck.exe' if rid.startswith('win-') else 'PhotoSafeCheck')
        exe.parent.mkdir(parents=True)
        exe.write_bytes(b'portable-test-executable')
        if not rid.startswith('win-'):
            exe.chmod(0o755)
        ext = bundle / 'Extension' / 'manifest.json'
        ext.parent.mkdir()
        ext.write_text('{"manifest_version":3}', encoding='utf-8')
        archive = module.create_archive(bundle, root / f'output-{rid}', rid)
        assert archive.exists()
        assert Path(str(archive) + '.sha256').exists()
        if rid.startswith('win-'):
            with zipfile.ZipFile(archive) as z:
                assert any(n.endswith('/App/PhotoSafeCheck.exe') for n in z.namelist())
        else:
            with tarfile.open(archive, 'r:gz') as t:
                app = next(x for x in t.getmembers() if x.name.endswith('/App/PhotoSafeCheck'))
                assert app.mode & 0o111, 'Executable permission was lost'
        state = bundle / 'App' / 'Data' / 'state.json'
        state.parent.mkdir()
        state.write_text('private', encoding='utf-8')
        try:
            module.create_archive(bundle, root / f'unsafe-{rid}', rid)
        except ValueError as e:
            assert 'private' in str(e)
        else:
            raise AssertionError('Private data was not rejected')
print(f'Portable packaging and private-state refusal tests passed for: {", ".join(targets)}')
