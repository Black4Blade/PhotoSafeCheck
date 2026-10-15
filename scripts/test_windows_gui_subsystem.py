"""Regression check for reading the Windows executable subsystem field."""
from pathlib import Path
import struct
import tempfile

from pe_subsystem import windows_pe_subsystem as get_subsystem

with tempfile.TemporaryDirectory() as d:
    exe = Path(d) / 'example.exe'
    for subsystem in (2, 3):
        content = bytearray(512)
        content[:2] = b'MZ'
        struct.pack_into('<I', content, 0x3C, 0x80)
        content[0x80:0x84] = b'PE\0\0'
        struct.pack_into('<H', content, 0x80 + 4 + 20, 0x20B)
        struct.pack_into('<H', content, 0x80 + 4 + 20 + 68, subsystem)
        exe.write_bytes(content)
        assert get_subsystem(exe) == subsystem
    exe.write_bytes(b'not a PE')
    try:
        get_subsystem(exe)
    except ValueError:
        pass
    else:
        raise AssertionError('Must reject invalid PE')
print('Windows GUI-subsystem PE tests passed')
