"""Minimal PE-header reader to check Windows GUI vs console subsystem."""
from pathlib import Path
import struct

def windows_pe_subsystem(exe: Path) -> int:
    """Return PE OptionalHeader.Subsystem; GUI=2, console=3.

    This checks the *published* apphost rather than merely trusting a csproj
    property, so a future CI change cannot silently restore console windows.
    """
    with exe.open('rb') as f:
        if f.read(2) != b'MZ':
            raise ValueError('Windows output is not a valid PE executable (missing MZ)')
        f.seek(0x3C)
        offset_bytes = f.read(4)
        if len(offset_bytes) != 4:
            raise ValueError('Windows PE header offset is truncated')
        pe_offset, = struct.unpack('<I', offset_bytes)
        if pe_offset < 0x40 or pe_offset > 16 * 1024 * 1024:
            raise ValueError('Windows PE header offset is invalid')
        f.seek(pe_offset)
        if f.read(4) != b'PE\x00\x00':
            raise ValueError('Windows output has no PE header signature')
        # PE header: 4-byte signature + 20-byte COFF header +
        # OptionalHeader.Subsystem at offset 68 (for PE32 and PE32+).
        f.seek(pe_offset + 4 + 20)
        magic = f.read(2)
        if magic not in (b'\x0b\x01', b'\x0b\x02'):
            raise ValueError('Windows output has unknown optional header format')
        f.seek(pe_offset + 4 + 20 + 68)
        subsystem_bytes = f.read(2)
        if len(subsystem_bytes) != 2:
            raise ValueError('Windows output has a truncated subsystem field')
        return struct.unpack('<H', subsystem_bytes)[0]

