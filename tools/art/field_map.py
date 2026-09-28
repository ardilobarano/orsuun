"""Draws the layout sketch of a big map's full map (FieldMap: Resources/FieldMaps/<Name>.json) at 1024 px, the map's
Size square centred on its origin, north up: steppe, the river, the trail, landmarks and groves as pictograms. The sketch
goes to docs/concept/fieldmaps/<Name>-sketch.png; it is painted over (GPT Image 2.5, keeping every feature in place) into
<Name>-painted.png, and `field_map.py <Name> --finish` then writes the game's copy to Content/FieldMaps/<Name>.jpg
(1024 px) with the trail and river checked against the layout (an overlay goes to <Name>-check.png).
Run with /usr/bin/python3 (PIL)."""
import json
import math
import os
import sys
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.join(os.path.dirname(__file__), '..', '..')
LAYOUTS = os.path.join(ROOT, 'client', 'Assets', 'Orsuun', 'Resources', 'FieldMaps')
OUT = os.path.join(ROOT, 'docs', 'concept', 'fieldmaps')
CONTENT = os.path.join(ROOT, 'client', 'Assets', 'Orsuun', 'Content', 'FieldMaps')
PX = 1024


def catmull(a, b, c, d, t):
    t2, t3 = t * t, t * t * t
    return tuple(0.5 * (2 * b[i] + (-a[i] + c[i]) * t + (2 * a[i] - 5 * b[i] + 4 * c[i] - d[i]) * t2
                        + (-a[i] + 3 * b[i] - 3 * c[i] + d[i]) * t3) for i in range(2))


def curve(points, closed):
    n = len(points)
    out = []
    last = n if closed else n - 1
    for i in range(last):
        a = points[(i - 1) % n] if closed else points[max(0, i - 1)]
        b, c = points[i], points[(i + 1) % n]
        d = points[(i + 2) % n] if closed else points[min(n - 1, i + 2)]
        steps = max(2, int(math.dist(b, c)))
        out += [catmull(a, b, c, d, k / steps) for k in range(steps)]
    if closed:
        out.append(out[0])
    else:
        out.append(points[-1])
    return out


def to_px(layout, x, z):
    size = layout['Size']
    return ((x / size + 0.5) * PX, (0.5 - z / size) * PX)


def sketch(layout):
    img = Image.new('RGB', (PX, PX), (196, 160, 82))
    d = ImageDraw.Draw(img)
    s = PX / layout['Size']
    pts = lambda seq: [to_px(layout, p[0], p[1]) for p in seq]
    if layout.get('River'):
        river = pts(curve([(p['x'], p['y']) for p in layout['River']], False))
        d.line(river, fill=(120, 96, 60), width=int((layout['RiverWidth'] + 2.4) * s))
        d.line(river, fill=(62, 104, 128), width=int(layout['RiverWidth'] * s))
    trail = pts(curve([(p['x'], p['y']) for p in layout['Trail']], True))
    d.line(trail, fill=(222, 206, 170), width=int(3.4 * s))
    for g in layout.get('Groves', []):
        x, y = to_px(layout, g['X'], g['Z'])
        r = g['Radius'] * s
        for k in range(g['Count']):
            a = k * 2.4
            px, py = x + math.cos(a) * r * 0.7, y + math.sin(a) * r * 0.7
            if g['Name'] == 'SteppeCairn':
                d.ellipse((px - 4, py - 4, px + 4, py + 4), fill=(128, 124, 116))
            else:
                d.ellipse((px - 7, py - 7, px + 7, py + 7), fill=(74, 110, 52))
    for m in layout.get('Landmarks', []):
        x, y = to_px(layout, m['X'], m['Z'])
        name = m['Name']
        if name == 'Yurt':
            d.ellipse((x - 11, y - 11, x + 11, y + 11), fill=(236, 226, 204), outline=(120, 70, 40), width=3)
        elif name == 'Tent':
            d.polygon([(x, y - 12), (x - 11, y + 8), (x + 11, y + 8)], fill=(150, 60, 40), outline=(70, 40, 20))
        elif name == 'Watchtower':
            d.rectangle((x - 9, y - 9, x + 9, y + 9), fill=(110, 100, 90), outline=(60, 50, 40), width=3)
        elif name == 'WolfDen':
            d.ellipse((x - 16, y - 12, x + 16, y + 12), fill=(96, 84, 70), outline=(50, 40, 30), width=3)
        elif name == 'SteppeStone':
            d.rectangle((x - 4, y - 4, x + 4, y + 4), fill=(90, 86, 84))
        else:
            d.ellipse((x - 6, y - 5, x + 6, y + 5), fill=(120, 116, 108))
    return img


def finish(name):
    layout = json.load(open(os.path.join(LAYOUTS, name + '.json')))
    painted = Image.open(os.path.join(OUT, name + '-painted.png')).convert('RGB')
    side = min(painted.size)
    painted = painted.crop(((painted.width - side) // 2, (painted.height - side) // 2,
                            (painted.width + side) // 2, (painted.height + side) // 2)).resize((PX, PX), Image.LANCZOS)
    os.makedirs(CONTENT, exist_ok=True)
    painted.save(os.path.join(CONTENT, name + '.jpg'), quality=90)
    check = painted.copy()
    d = ImageDraw.Draw(check)
    d.line([to_px(layout, p[0], p[1]) for p in curve([(p['x'], p['y']) for p in layout['Trail']], True)], fill=(255, 0, 0), width=2)
    if layout.get('River'):
        d.line([to_px(layout, p[0], p[1]) for p in curve([(p['x'], p['y']) for p in layout['River']], False)], fill=(0, 0, 255), width=2)
    for c in layout.get('Camps', []):
        x, y = to_px(layout, c['X'], c['Z'])
        d.ellipse((x - 6, y - 6, x + 6, y + 6), outline=(255, 0, 0), width=2)
    check.save(os.path.join(OUT, name + '-check.png'))
    print('wrote', os.path.join(CONTENT, name + '.jpg'))


def main():
    name = sys.argv[1] if len(sys.argv) > 1 else 'Oathfields'
    if '--finish' in sys.argv:
        finish(name)
        return
    os.makedirs(OUT, exist_ok=True)
    layout = json.load(open(os.path.join(LAYOUTS, name + '.json')))
    sketch(layout).save(os.path.join(OUT, name + '-sketch.png'))
    print('wrote', os.path.join(OUT, name + '-sketch.png'))


if __name__ == '__main__':
    main()
