#!/usr/bin/env python3
# Generate self-contained placeholder PNGs for MEE items (no vanilla path refs).
# Pure stdlib (zlib + struct). Solid fill with a darker border.
import zlib, struct, os

SIZE = 64
BORDER = 4

def write_png(path, width, height, rgba_rows):
    def chunk(typ, data):
        return (struct.pack(">I", len(data)) + typ + data
                + struct.pack(">I", zlib.crc32(typ + data) & 0xffffffff))
    sig = b"\x89PNG\r\n\x1a\n"
    ihdr = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)  # 8-bit RGBA
    raw = b"".join(b"\x00" + rgba_rows[y * width * 4:(y + 1) * width * 4]
                   for y in range(height))
    idat = zlib.compress(raw, 9)
    with open(path, "wb") as f:
        f.write(sig + chunk(b"IHDR", ihdr) + chunk(b"IDAT", idat)
                + chunk(b"IEND", b""))

def make(path, fill, border):
    w = h = SIZE
    buf = bytearray()
    fr, fg, fb, fa = fill
    br, bg, bb, ba = border
    for y in range(h):
        for x in range(w):
            edge = x < BORDER or y < BORDER or x >= w - BORDER or y >= h - BORDER
            if edge:
                buf += bytes((br, bg, bb, ba))
            else:
                buf += bytes((fr, fg, fb, fa))
    write_png(path, w, h, bytes(buf))
    print("wrote", path)

OUT = os.path.join(os.path.dirname(__file__), "..", "Textures", "Things", "Item", "MEE")
OUT = os.path.abspath(OUT)
os.makedirs(OUT, exist_ok=True)

make(os.path.join(OUT, "MEE_Salt.png"),          (245, 245, 250, 255), (200, 200, 210, 255))
make(os.path.join(OUT, "MEE_ProteinExtract.png"),(220, 140, 140, 255), (180, 100, 100, 255))
make(os.path.join(OUT, "MEE_WaterBottle.png"),   (120, 180, 230, 255), (80, 140, 200, 255))
make(os.path.join(OUT, "MEE_GlucoseMash.png"),   (230, 190, 90, 255),  (200, 150, 50, 255))
