"""Create a downloadable archive of a vetted, portable PhotoSafe Check bundle.

Windows: ZIP. Linux/macOS: tar.gz to retain executable permissions and symlinks.
Never archive user settings or scan history.
"""
import argparse
import hashlib
from pathlib import Path
import tarfile
import zipfile

RIDS = {'win-x64', 'linux-x64', 'osx-x64', 'osx-arm64'}
PRIVATE_NAMES = {'state.json', 'pairing-token', 'pairing-token.txt', 'startup.log'}


def create_archive(bundle: Path, output: Path, rid: str) -> Path:
    if rid not in RIDS:
        raise ValueError(f'Unsupported RID: {rid}')
    if not bundle.is_dir():
        raise FileNotFoundError(f'Bundle directory missing: {bundle}')
    executable = bundle / 'App' / ('PhotoSafeCheck.exe' if rid.startswith('win-') else 'PhotoSafeCheck')
    if not executable.is_file():
        raise ValueError(f'Missing compiled executable: {executable}')
    if not (bundle / 'Extension' / 'manifest.json').is_file():
        raise ValueError('Browser extension missing from bundle')
    for file in bundle.rglob('*'):
        rel = file.relative_to(bundle)
        if 'Data' in rel.parts or rel.name in PRIVATE_NAMES:
            raise ValueError(f'Refusing to package private application data: {rel}')
    if not rid.startswith('win-') and not (executable.stat().st_mode & 0o111):
        raise ValueError('Unix executable lacks execute permission')
    output.mkdir(parents=True, exist_ok=True)
    suffix = '.zip' if rid.startswith('win-') else '.tar.gz'
    archive = output / f'PhotoSafeCheck-{rid}-portable{suffix}'
    if archive.exists():
        raise FileExistsError(f'Refusing to overwrite: {archive}')
    directory_name = f'PhotoSafeCheck-{rid}-portable'
    if suffix == '.zip':
        with zipfile.ZipFile(archive, 'x', compression=zipfile.ZIP_DEFLATED) as z:
            for item in sorted(bundle.rglob('*')):
                if item.is_file():
                    z.write(item, f'{directory_name}/{item.relative_to(bundle).as_posix()}')
    else:
        with tarfile.open(archive, 'w:gz') as t:
            t.add(bundle, arcname=directory_name, recursive=True)
    hasher = hashlib.sha256()
    with archive.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            hasher.update(chunk)
    digest = hasher.hexdigest()
    checksum = output / (archive.name + '.sha256')
    checksum.write_text(f'{digest}  {archive.name}\n', encoding='ascii')
    print(f'Created {archive} and SHA-256 checksum')
    return archive


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--bundle', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--rid', choices=sorted(RIDS), required=True)
    args = parser.parse_args()
    create_archive(args.bundle, args.output, args.rid)
