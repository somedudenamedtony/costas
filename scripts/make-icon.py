#!/usr/bin/env python3
# Costas - a station-centric FT8/FT4 client.
# Copyright (C) 2026 Costas contributors. GPLv3; see LICENSE.
#
# Draws the app icon (the "Sync grid" mark) and writes src/Ft8Client.App/Assets/costas.ico and costas-256.png.
# The mark is the FT8 Costas sync array, tones 3 1 4 0 6 5 2: one lit cell per symbol on a 7 x 7 time/tone grid,
# tone 0 at the bottom. Large sizes show the faint grid; small sizes drop it and enlarge the cells so they stay legible.
# Needs Pillow. Run from anywhere: python3 scripts/make-icon.py

from pathlib import Path

from PIL import Image, ImageDraw

COSTAS = [3, 1, 4, 0, 6, 5, 2]
NIGHT = (0x0E, 0x1A, 0x24, 255)
GRID = (0x2A, 0x42, 0x56, 255)
SIGNAL = (0xF5, 0xB2, 0x3D, 255)
SUPERSAMPLE = 8


def draw(size: int) -> Image.Image:
    big = size >= 48
    s = size * SUPERSAMPLE
    u = s / 64  # one unit of the 64 x 64 design grid
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    radius = 14 if big else (12 if size >= 24 else 10)
    d.rounded_rectangle((0, 0, s - 1, s - 1), radius=radius * u, fill=NIGHT)

    if big:
        origin, cell, lit, corner = 8.0, 48 / 7, 5.4, 1.2
    else:
        origin, cell, lit, corner = 4.0, 8.0, 6.4, 1.0

    if size >= 64:
        for col in range(7):
            for row in range(7):
                if row == 6 - COSTAS[col]:
                    continue
                cx, cy, r = (origin + cell * (col + 0.5)) * u, (origin + cell * (row + 0.5)) * u, 0.8 * u
                d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=GRID)

    pad = (cell - lit) / 2
    for col, tone in enumerate(COSTAS):
        x = (origin + cell * col + pad) * u
        y = (origin + cell * (6 - tone) + pad) * u
        d.rounded_rectangle((x, y, x + lit * u, y + lit * u), radius=corner * u, fill=SIGNAL)

    return img.resize((size, size), Image.LANCZOS)


def main() -> None:
    assets = Path(__file__).resolve().parent.parent / "src" / "Ft8Client.App" / "Assets"
    assets.mkdir(parents=True, exist_ok=True)
    sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
    images = [draw(n) for n in sizes]
    images[-1].save(assets / "costas-256.png")
    images[-1].save(assets / "costas.ico", sizes=[(n, n) for n in sizes], append_images=images[:-1])


if __name__ == "__main__":
    main()
