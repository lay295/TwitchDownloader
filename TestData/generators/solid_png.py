"""A dependency-free encoder for solid-colour RGB PNGs, shared by the generators that don't need Pillow."""
from __future__ import annotations

import base64
import struct
import zlib


def solid_png(width: int, height: int, rgb: tuple[int, int, int]) -> bytes:
    raw = b"".join(b"\x00" + bytes(rgb) * width for _ in range(height))  # filter byte 0 + pixels, per row

    def chunk(kind: bytes, data: bytes) -> bytes:
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)

    header = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)  # 8-bit RGB
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header) + chunk(b"IDAT", zlib.compress(raw)) + chunk(b"IEND", b"")


def solid_png_base64(width: int, height: int, rgb: tuple[int, int, int]) -> str:
    return base64.b64encode(solid_png(width, height, rgb)).decode()
