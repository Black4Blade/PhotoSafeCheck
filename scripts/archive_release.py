"""Archive a single-executable app directory, preserving Unix execute permissions.

Expected user-facing ZIP/TAR.GZ structure: PhotoSafeCheck/PhotoSafeCheck[.exe].
Reject all extra files, including private state, extension files, and documentation.
"""
import argparse
import hashlib
from pathlib import Path
import tarfile
import zipfile

RIDS = {'win-x64', 'linux-x64', 'osx-x64', 'osx-arm64'}


def create_archive(bundle: Path, output: Path, rid: str) -> Path:
    if rid not in RIDS:
        raise ValueError(f'Unsupported RID: {rid}')
    exe = bundle / 'PhotoSafeCheck' / ('PhotoSafeCheck.exe' if rid.startswith('win-') else 'PhotoSafeCheck')
    if not exe.is_file():
        raise ValueError(f'Missing standalone executable: {exe}')
    entries = [p.relative_to(bundle).as_posix() for p in bundle.rglob('*') if p.is_file() or p.is_symlink()]
    if entries != [exe.relative_to(bundle).as_posix()]:
        raise ValueError(f'App archive must contain ONLY the executable, found: {entries}')
    if not rid.startswith('win-') and not exe.stat().st_mode & 0o111:
        raise ValueError('Unix executable lacks execute permission')
    output.mkdir(parents=True, exist_ok=True)
    suffix = '.zip' if rid.startswith('win-') else '.tar.gz'
    archive = output / f'PhotoSafeCheck-{rid}-portable{suffix}'
    if archive.exists():
        raise FileExistsError(f'Refusing to overwrite: {archive}')
    if suffix == '.zip':
        with zipfile.ZipFile(archive, 'x', compression=zipfile.ZIP_DEFLATED) as z:
            z.write(exe, 'PhotoSafeCheck/PhotoSafeCheck.exe')
    else:
        with tarfile.open(archive, 'w:gz') as t:
            t.add(exe, arcname='PhotoSafeCheck/PhotoSafeCheck', recursive=False)
    digest = hashlib.sha256(archive.read_bytes()).hexdigest()
    (output / (archive.name + '.sha256')).write_text(f'{digest}  {archive.name}\n', encoding='ascii')
    print(f'Created {archive} (only app executable) and SHA-256 checksum')
    return archive


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--bundle', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--rid', choices=sorted(RIDS), required=True)
    args = parser.parse_args()
    create_archive(args.bundle, args.output, args.rid)
