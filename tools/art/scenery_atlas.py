"""Cuts the hunt's scenery sheets (docs/concept/scenery/<set>.png: six painted props on transparency, GPT Image 2.5)
into 2048x512 atlases of 256x512 cells and writes Rects.json (each prop's UV rect, height over width, height on the
lane in metres, and whether it may stand before the road). Writes docs/concept/scenery/atlas and copies the result
into client/Assets/Orsuun/Content/Scenery, which LaneScenery reads. Run with /usr/bin/python3 (PIL)."""
import json
import os
import shutil
from collections import deque
from PIL import Image

ROOT = os.path.join(os.path.dirname(__file__), '..', '..')
SRC = os.path.join(ROOT, 'docs', 'concept', 'scenery')
OUT = os.path.join(SRC, 'atlas')
CONTENT = os.path.join(ROOT, 'client', 'Assets', 'Orsuun', 'Content', 'Scenery')
SETS = ['steppe', 'mountain', 'desert', 'forest', 'ruins']
# Each prop's height on the lane (metres, left to right on the sheet) and which ones are low enough for the near row.
HEIGHT = {'steppe': [1.0, 1.4, 3.2, 0.9, 2.2, 1.2], 'mountain': [3.0, 1.6, 1.0, 3.2, 1.1, 1.4],
          'desert': [1.2, 1.0, 0.9, 2.6, 2.4, 1.1], 'forest': [3.6, 3.4, 0.8, 1.1, 2.4, 1.4],
          'ruins': [3.0, 2.6, 2.0, 0.6, 3.0, 1.5]}
LOW = {'steppe': [0, 1, 3, 5], 'mountain': [1, 2, 4], 'desert': [0, 1, 2, 5], 'forest': [2, 3, 5], 'ruins': [3]}
S = 4  # the pieces are found on a quarter-size alpha


def cut(name):
    sheet = Image.open(os.path.join(SRC, name + '.png')).convert('RGBA')
    w, h = sheet.width // S, sheet.height // S
    alpha = sheet.getchannel('A').resize((w, h), Image.BOX).load()
    label = [[-1] * w for _ in range(h)]
    comps = []
    for y in range(h):
        for x in range(w):
            if label[y][x] != -1 or alpha[x, y] <= 90:
                continue
            idx = len(comps)
            q = [(x, y)]
            label[y][x] = idx
            cells = []
            while q:
                cx, cy = q.pop()
                cells.append((cx, cy))
                for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
                    if 0 <= nx < w and 0 <= ny < h and label[ny][nx] == -1 and alpha[nx, ny] > 90:
                        label[ny][nx] = idx
                        q.append((nx, ny))
            comps.append(cells)
    # Six props: the six biggest pieces, left to right; faint pixels go to the nearest of them.
    big = sorted(range(len(comps)), key=lambda i: -len(comps[i]))[:6]
    big = sorted(big, key=lambda i: sum(c[0] for c in comps[i]) / len(comps[i]))
    own = [[-1] * w for _ in range(h)]
    dq = deque()
    for k, c in enumerate(big):
        for (x, y) in comps[c]:
            own[y][x] = k
            dq.append((x, y))
    while dq:
        x, y = dq.popleft()
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < w and 0 <= ny < h and own[ny][nx] == -1 and alpha[nx, ny] > 0:
                own[ny][nx] = own[y][x]
                dq.append((nx, ny))
    atlas = Image.new('RGBA', (2048, 512), (0, 0, 0, 0))
    rects = []
    for k in range(6):
        mask = Image.new('L', (w, h), 0)
        mp = mask.load()
        xs, ys = [], []
        for y in range(h):
            for x in range(w):
                if own[y][x] == k:
                    mp[x, y] = 255
                    if alpha[x, y] > 40:
                        xs.append(x)
                        ys.append(y)
        mask = mask.resize(sheet.size, Image.NEAREST)
        piece = Image.new('RGBA', sheet.size, (0, 0, 0, 0))
        piece.paste(sheet, (0, 0), mask)
        art = piece.crop((min(xs) * S, min(ys) * S, (max(xs) + 1) * S, (max(ys) + 1) * S))
        s = min(248 / art.width, 504 / art.height)
        art = art.resize((max(1, int(art.width * s)), max(1, int(art.height * s))), Image.LANCZOS)
        x0, y0 = k * 256 + (256 - art.width) // 2, 512 - art.height - 2
        atlas.alpha_composite(art, (x0, y0))
        rects.append({'u0': x0 / 2048, 'u1': (x0 + art.width) / 2048, 'v0': 2 / 512, 'v1': (2 + art.height) / 512,
                      'aspect': round(art.height / art.width, 3), 'height': HEIGHT[name][k], 'low': k in LOW[name]})
    atlas.save(os.path.join(OUT, name.capitalize() + '.png'))
    return rects


def main():
    os.makedirs(OUT, exist_ok=True)
    rects = {name: cut(name) for name in SETS}
    with open(os.path.join(OUT, 'rects.json'), 'w') as f:
        json.dump(rects, f, indent=1)
    for name in SETS:
        shutil.copy(os.path.join(OUT, name.capitalize() + '.png'), CONTENT)
    shutil.copy(os.path.join(OUT, 'rects.json'), os.path.join(CONTENT, 'Rects.json'))
    print('wrote', ', '.join(SETS))


if __name__ == '__main__':
    main()
