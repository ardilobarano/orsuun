"""Cuts a row of painted icons on a transparent background (Higgsfield GPT Image, background: transparent) into
square power-of-two icons for Resources/Icons: the solid pieces are found as blobs (glow haze left out), taken left
to right, cropped with a margin for their glow, padded to a square and scaled to the size given.

    python tools/ui/cut_icons.py docs/concept/icons/korshards-ranks.png 256 ShardTrooper ShardRider ShardCaptain ShardCommander ShardGuard
"""
import os
import sys

from PIL import Image

ICONS = os.path.join(os.path.dirname(__file__), "..", "..", "client", "Assets", "Orsuun", "Resources", "Icons")
STEP = 4  # blobs are found on a grid this coarse


def blobs(sheet):
    """Bounding boxes (full resolution) of the solid blobs, largest first."""
    w, h = sheet.width // STEP, sheet.height // STEP
    alpha = sheet.getchannel("A").resize((w, h), Image.BOX).load()
    seen = [[False] * w for _ in range(h)]
    found = []
    for sy in range(h):
        for sx in range(w):
            if seen[sy][sx] or alpha[sx, sy] <= 110:
                continue
            stack, count = [(sx, sy)], 0
            seen[sy][sx] = True
            x0, y0, x1, y1 = sx, sy, sx, sy
            while stack:
                x, y = stack.pop()
                count += 1
                x0, y0, x1, y1 = min(x0, x), min(y0, y), max(x1, x), max(y1, y)
                for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                    if 0 <= nx < w and 0 <= ny < h and not seen[ny][nx] and alpha[nx, ny] > 110:
                        seen[ny][nx] = True
                        stack.append((nx, ny))
            found.append((count, (x0 * STEP, y0 * STEP, (x1 + 1) * STEP, (y1 + 1) * STEP)))
    found.sort(reverse=True)
    return [box for _, box in found]


def main():
    sheet = Image.open(sys.argv[1]).convert("RGBA")
    size = int(sys.argv[2])
    names = sys.argv[3:]
    boxes = sorted(blobs(sheet)[:len(names)])
    for name, (x0, y0, x1, y1) in zip(names, boxes):
        m = int(max(x1 - x0, y1 - y0) * 0.06)
        art = sheet.crop((max(0, x0 - m), max(0, y0 - m), min(sheet.width, x1 + m), min(sheet.height, y1 + m)))
        side = int(max(art.width, art.height) * 1.04)
        square = Image.new("RGBA", (side, side), (0, 0, 0, 0))
        square.alpha_composite(art, ((side - art.width) // 2, (side - art.height) // 2))
        square.resize((size, size), Image.LANCZOS).save(os.path.join(ICONS, name + ".png"))
        print(name, (x0, y0, x1, y1))


if __name__ == "__main__":
    main()
