"""The town square's art (TownScene) from docs/concept/town (GPT Image 2.5 on Higgsfield): npcs.png (Forgemaster Dorun,
Ilke of the Scales, Elder Tamir, Pitmaster Bora, left to right, on transparency), props.png (the forge and anvil, the
merchant's stall, the lectern, the weapon rack, the banner pole, the lantern post) and backdrop.png (the square's far
side). Writes client/Assets/Orsuun/Content/Town: Folk.png (four 512x1024 cells), Props.png (eight 256x512 cells: the six
props, then the banner in the Sky and Gold Banners' colours), Rugs.png (four woven rugs, 256x512 cells: Ember, Sky, Gold,
plain), Shade.png (a soft shade under figures), Square.jpg (the backdrop), Paving.jpg (the Sunken Bazaar's floor, greyed to the painted square's stone) and
Rects.json (each card's UV rect, height over width and height in metres). Run with /usr/bin/python3 (PIL)."""
import colorsys
import json
import os
import random
from PIL import Image, ImageEnhance

ROOT = os.path.join(os.path.dirname(__file__), '..', '..')
SRC = os.path.join(ROOT, 'docs', 'concept', 'town')
OUT = os.path.join(ROOT, 'client', 'Assets', 'Orsuun', 'Content', 'Town')
FOLK_HEIGHT = [1.95, 1.8, 1.9, 2.05]
PROP_HEIGHT = [1.45, 2.4, 1.75, 2.1, 4.2, 2.8]


def groups(sheet, count):
    """Splits a row of figures by the empty columns between them: the `count` pieces, left to right (x0, x1)."""
    a = sheet.getchannel('A')
    w, h = sheet.size
    col = [max(a.crop((x, 0, x + 1, h)).getdata()) > 40 for x in range(w)]
    runs, start = [], None
    for x, on in enumerate(col + [False]):
        if on and start is None:
            start = x
        elif not on and start is not None:
            runs.append([start, x])
            start = None
    # Close small gaps, then merge the smallest pieces into their nearest neighbour until `count` are left.
    merged = [runs[0]]
    for r in runs[1:]:
        if r[0] - merged[-1][1] < 12:
            merged[-1][1] = r[1]
        else:
            merged.append(r)
    while len(merged) > count:
        i = min(range(len(merged)), key=lambda k: merged[k][1] - merged[k][0])
        left = merged[i][0] - merged[i - 1][1] if i > 0 else 10 ** 9
        right = merged[i + 1][0] - merged[i][1] if i + 1 < len(merged) else 10 ** 9
        j = i - 1 if left <= right else i + 1
        lo, hi = min(i, j), max(i, j)
        merged[lo] = [merged[lo][0], merged[hi][1]]
        del merged[hi]
    return merged


def cut(sheet, span):
    piece = sheet.crop((span[0], 0, span[1], sheet.height))
    box = piece.getchannel('A').point(lambda v: 255 if v > 40 else 0).getbbox()
    return piece.crop(box)


def pack(pieces, cell, cells, heights):
    cw, ch = cell
    atlas = Image.new('RGBA', (cw * cells, ch), (0, 0, 0, 0))
    rects = []
    for k, art in enumerate(pieces):
        s = min((cw - 8) / art.width, (ch - 8) / art.height)
        art = art.resize((max(1, int(art.width * s)), max(1, int(art.height * s))), Image.LANCZOS)
        x0, y0 = k * cw + (cw - art.width) // 2, ch - art.height - 2
        atlas.alpha_composite(art, (x0, y0))
        W, H = atlas.size
        rects.append({'u0': x0 / W, 'u1': (x0 + art.width) / W, 'v0': 2 / H, 'v1': (2 + art.height) / H,
                      'aspect': round(art.height / art.width, 3), 'height': heights[k]})
    return atlas, rects


def recolour(banner, cloth_hue, emblem=None):
    """The banner's red cloth turned to another hue (its gold emblem to `emblem` (hue, value) when given)."""
    px = banner.load()
    out = banner.copy()
    po = out.load()
    for y in range(banner.height):
        for x in range(banner.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            hh, ss, vv = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            deg = hh * 360
            if ss > 0.45 and (deg < 22 or deg > 335):
                nh, nv = cloth_hue, vv * (0.9 if cloth_hue > 180 else 1.12)
            elif emblem and ss > 0.35 and 30 <= deg <= 60:
                nh, nv = emblem[0], vv * emblem[1]
            else:
                continue
            nr, ng, nb = colorsys.hsv_to_rgb(nh / 360, ss, min(1, nv))
            po[x, y] = (int(nr * 255), int(ng * 255), int(nb * 255), a)
    return out


def rug(main, accent, light, seed):
    """A woven rug, 256x512 (its length up the cell): a field with stepped diamonds, a border of teeth, tassels."""
    rnd = random.Random(seed)
    w, h = 256, 512
    im = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    p = im.load()
    fringe = 22
    for y in range(fringe, h - fringe):
        for x in range(w):
            c = main
            bx, by = min(x, w - 1 - x), min(y - fringe, h - 1 - fringe - y)
            edge = min(bx, by)
            if edge < 10:
                c = accent
            elif edge < 34:
                # A band of teeth along the border.
                t = (x + y) % 24 if bx < by else (y + x) % 24
                c = light if (edge - 10) < abs(12 - t) else accent
            elif edge < 40:
                c = light
            else:
                cx, cy = w / 2, h / 2
                for my in (cy - 140, cy, cy + 140):
                    d = abs(x - cx) + abs(y - my)
                    step = int(d) // 10
                    if d < 66:
                        c = (accent, light, main, accent, light, accent, main)[min(step, 6)]
                        break
            # A little weave in each pixel.
            n = rnd.randint(-10, 10) + (6 if (x // 2 + y // 3) % 2 else -6)
            p[x, y] = tuple(max(0, min(255, v + n)) for v in c) + (255,)
    for y in list(range(fringe)) + list(range(h - fringe, h)):
        for x in range(8, w - 8):
            if x % 7 < 3:
                v = 225 + rnd.randint(-18, 12)
                p[x, y] = (v, v - 12, v - 34, 255)
    return im


def main():
    os.makedirs(OUT, exist_ok=True)
    npcs = Image.open(os.path.join(SRC, 'npcs.png')).convert('RGBA')
    folk = [cut(npcs, g) for g in groups(npcs, 4)]
    folk_atlas, folk_rects = pack(folk, (512, 1024), 4, FOLK_HEIGHT)
    folk_atlas.save(os.path.join(OUT, 'Folk.png'))

    sheet = Image.open(os.path.join(SRC, 'props.png')).convert('RGBA')
    props = [cut(sheet, g) for g in groups(sheet, 6)]
    banner = props[4]
    props += [recolour(banner, 218), recolour(banner, 44, emblem=(12, 0.55))]
    prop_atlas, prop_rects = pack(props, (256, 512), 8, PROP_HEIGHT + [PROP_HEIGHT[4]] * 2)
    prop_atlas.save(os.path.join(OUT, 'Props.png'))

    rugs = Image.new('RGBA', (1024, 512), (0, 0, 0, 0))
    colours = [((150, 34, 28), (58, 22, 18), (226, 178, 96)), ((36, 70, 140), (20, 28, 60), (214, 196, 150)),
               ((196, 140, 36), (96, 42, 22), (240, 222, 170)), ((120, 88, 60), (60, 40, 28), (210, 190, 150))]
    for k, (m, a, l) in enumerate(colours):
        rugs.alpha_composite(rug(m, a, l, k), (k * 256, 0))
    rugs.save(os.path.join(OUT, 'Rugs.png'))

    # A soft shade for the stones under a figure: black, its alpha falling off from the middle.
    shade = Image.new('RGBA', (128, 128), (0, 0, 0, 0))
    sp = shade.load()
    for y in range(128):
        for x in range(128):
            d = ((x - 63.5) ** 2 + (y - 63.5) ** 2) ** 0.5 / 63.5
            sp[x, y] = (0, 0, 0, int(max(0.0, 1 - d) ** 1.6 * 150))
    shade.save(os.path.join(OUT, 'Shade.png'))

    Image.open(os.path.join(SRC, 'backdrop.png')).convert('RGB').save(os.path.join(OUT, 'Square.jpg'), quality=88)
    floor = Image.open(os.path.join(ROOT, 'client', 'Assets', 'Orsuun', 'Content', 'Floors', 'SunkenBazaar.jpg')).convert('RGB')
    floor = ImageEnhance.Color(floor).enhance(0.45)
    floor = ImageEnhance.Brightness(floor).enhance(0.78)
    floor.save(os.path.join(OUT, 'Paving.jpg'), quality=88)

    with open(os.path.join(OUT, 'Rects.json'), 'w') as f:
        json.dump({'folk': folk_rects, 'props': prop_rects}, f, indent=1)
    print('folk', [r['aspect'] for r in folk_rects], 'props', [r['aspect'] for r in prop_rects])


if __name__ == '__main__':
    main()
