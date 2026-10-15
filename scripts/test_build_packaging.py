"""Test that platform app releases contain precisely ONE runnable file."""
import importlib.util
import os
from pathlib import Path
import tempfile
import tarfile
import zipfile

scripts = Path(__file__).parent
spec = importlib.util.spec_from_file_location('archive_release', scripts / 'archive_release.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

with tempfile.TemporaryDirectory() as td:
    root = Path(td)
    targets = ['win-x64'] if os.name == 'nt' else ['win-x64', 'linux-x64', 'osx-x64', 'osx-arm64']
    for rid in targets:
        bundle = root / rid
        exe_name = 'PhotoSafeCheck.exe' if rid.startswith('win-') else 'PhotoSafeCheck'
        exe = bundle / 'PhotoSafeCheck' / exe_name
        exe.parent.mkdir(parents=True)
        exe.write_bytes(b'test-single-executable')
        if not rid.startswith('win-'): exe.chmod(0o755)
        output = root / f'out-{rid}'
        archive = module.create_archive(bundle, output, rid)
        if rid.startswith('win-'):
            with zipfile.ZipFile(archive) as z:
                assert z.namelist() == ['PhotoSafeCheck/PhotoSafeCheck.exe']
        else:
            with tarfile.open(archive, 'r:gz') as t:
                members = t.getmembers()
                assert len(members) == 1
                assert members[0].name == 'PhotoSafeCheck/PhotoSafeCheck'
                assert members[0].mode & 0o111
        assert (output / (archive.name + '.sha256')).is_file()
        (bundle / 'PhotoSafeCheck' / 'state.json').write_text('private', encoding='utf-8')
        try:
            module.create_archive(bundle, root / f'unsafe-{rid}', rid)
        except ValueError as exc:
            assert 'ONLY' in str(exc)
        else:
            raise AssertionError('Must reject any unexpected file')
print('Passed clean single-file archive tests:', ', '.join(targets))
