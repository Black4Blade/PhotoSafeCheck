"""Run the actual published single-file EXE without opening the GUI.
Requires only the target OS runtime available in each matching GitHub runner.
"""
import argparse
from pathlib import Path
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument('--publish', type=Path, required=True)
parser.add_argument('--rid', choices=['win-x64','linux-x64','osx-x64','osx-arm64'], required=True)
args = parser.parse_args()
exe = args.publish / ('PhotoSafeCheck.exe' if args.rid.startswith('win-') else 'PhotoSafeCheck')
if not exe.is_file():
    raise SystemExit(f'Expected single-file publish executable: {exe}')
run = subprocess.run([str(exe.resolve()), '--verify-bundle'], check=False)
if run.returncode:
    raise SystemExit(f'Standalone executable failed self-check (code {run.returncode})')
print('Validated executable without launching UI:', exe)
