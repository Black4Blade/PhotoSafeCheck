"""Run the actual published single-file EXE without opening the GUI.
Requires only the target OS runtime available in each matching GitHub runner.
"""
import argparse
from pathlib import Path
import subprocess

from pe_subsystem import windows_pe_subsystem
parser = argparse.ArgumentParser()
parser.add_argument('--publish', type=Path, required=True)
parser.add_argument('--rid', choices=['win-x64','linux-x64','osx-x64','osx-arm64'], required=True)
args = parser.parse_args()
exe = args.publish / ('PhotoSafeCheck.exe' if args.rid.startswith('win-') else 'PhotoSafeCheck')
if not exe.is_file():
    raise SystemExit(f'Expected single-file publish executable: {exe}')
if args.rid.startswith('win-'):
    subsystem = windows_pe_subsystem(exe)
    if subsystem != 2:
        raise SystemExit(
            f'Windows executable uses PE subsystem {subsystem}, expected 2 (GUI). '
            'The Windows release must start without a console window.'
        )
    print('Validated Windows GUI subsystem (no console):', exe)
run = subprocess.run([str(exe.resolve()), '--verify-bundle'], check=False)
if run.returncode:
    raise SystemExit(f'Standalone executable failed self-check (code {run.returncode})')
print('Validated executable without launching UI:', exe)
