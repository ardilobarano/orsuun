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
# The lane backdrop each campaign map hunts under (LaneView.BackdropKey): its field floor gives the sketch's ground colour.
BACKDROP = {1: 'HuntingGround', 2: 'CommanderGround', 3: 'SaltFlats', 4: 'FrostPasture', 5: 'CinderMarches', 6: 'Whisperwood',
            7: 'Bloodbirch', 8: 'DrownedSteppe', 9: 'ColossusGraves', 10: 'SunkenBazaar', 11: 'ThousandMarkers', 12: 'HollowThrone'}
# FieldMap.PatchLook's colours.
PATCH = {'salt': (237, 235, 224), 'snow': (235, 242, 255), 'ice': (168, 209, 237), 'water': (56, 97, 117), 'mud': (77, 61, 46),
         'ash': (51, 48, 48), 'lava': (255, 107, 20), 'moss': (87, 107, 51)}
# Pictograms of the landmark models: (shape, colour, size in pixels).
PICTO = {'Yurt': ('disc', (236, 226, 204), 11), 'Tent': ('tri', (150, 60, 40), 11), 'Watchtower': ('square', (110, 100, 90), 9),
         'WolfDen': ('oval', (96, 84, 70), 16), 'SteppeStone': ('square', (90, 86, 84), 4), 'SteppeBoulder': ('oval', (120, 116, 108), 6),
         'GorakTent': ('disc', (40, 36, 34), 12), 'WarDrum': ('square', (140, 100, 60), 10), 'Palisade': ('square', (100, 72, 44), 5),
         'WarBanner': ('tri', (170, 30, 30), 7), 'DatePalm': ('disc', (70, 120, 50), 8), 'CaravanWreck': ('square', (130, 95, 60), 9),
         'SaltPillar': ('tri', (245, 225, 225), 9), 'DesertShrine': ('disc', (80, 170, 160), 10), 'MountainHut': ('square', (90, 60, 40), 10),
         'IceShrine': ('square', (150, 160, 170), 7), 'FrostPine': ('tri', (40, 80, 55), 8), 'IceCrag': ('tri', (120, 135, 150), 12),
         'SteppeBirch': ('disc', (74, 110, 52), 7), 'SteppeCairn': ('disc', (128, 124, 116), 4)}


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


def hex_rgb(h, fallback):
    try:
        return tuple(int(h.lstrip('#')[i:i + 2], 16) for i in (0, 2, 4))
    except (ValueError, AttributeError):
        return fallback


def tinted(rgb, tint):
    if not tint:
        return rgb
    t = hex_rgb(tint, (255, 255, 255))
    return tuple(int(a * b / 255) for a, b in zip(rgb, t))


def ground(layout):
    key = layout.get('Backdrop') or BACKDROP.get(layout.get('Map', 1), 'HuntingGround')
    floor = os.path.join(ROOT, 'client', 'Assets', 'Orsuun', 'Content', 'Floors', key + 'Field.jpg')
    if not os.path.exists(floor):
        return (196, 160, 82)
    return Image.open(floor).convert('RGB').resize((1, 1), Image.BOX).getpixel((0, 0))


def sketch(layout):
    img = Image.new('RGB', (PX, PX), ground(layout))
    d = ImageDraw.Draw(img)
    s = PX / layout['Size']
    pts = lambda seq: [to_px(layout, p[0], p[1]) for p in seq]
    for p in layout.get('Patches') or []:
        kind = p['Name']
        rx, rz = p.get('Radius', 5) * s, (p.get('Height') or p.get('Radius', 5)) * s
        x, y = to_px(layout, p['X'], p['Z'])
        if kind in ('water', 'ice', 'lava'):
            rim = PATCH['snow' if kind == 'ice' else 'ash' if kind == 'lava' else 'mud']
            d.ellipse((x - rx * 1.15, y - rz * 1.15, x + rx * 1.15, y + rz * 1.15), fill=rim)
        d.ellipse((x - rx, y - rz, x + rx, y + rz), fill=PATCH.get(kind, (128, 115, 90)))
    if layout.get('River'):
        river = pts(curve([(p['x'], p['y']) for p in layout['River']], False))
        d.line(river, fill=hex_rgb(layout.get('BankColor'), (120, 96, 60)), width=int((layout['RiverWidth'] + 2.4) * s))
        d.line(river, fill=hex_rgb(layout.get('WaterColor'), (62, 104, 128)), width=int(layout['RiverWidth'] * s))
    trail = pts(curve([(p['x'], p['y']) for p in layout['Trail']], True))
    d.line(trail, fill=(222, 206, 170), width=int(3.4 * s))
    def picto(name, x, y, tint=None, scale=1.0):
        shape, colour, size = PICTO.get(name, ('disc', (110, 100, 90), 6))
        colour = tinted(colour, tint)
        r = size * scale
        dark = tuple(int(c * 0.5) for c in colour)
        if shape == 'tri':
            d.polygon([(x, y - r * 1.1), (x - r, y + r * 0.7), (x + r, y + r * 0.7)], fill=colour, outline=dark)
        elif shape == 'square':
            d.rectangle((x - r, y - r, x + r, y + r), fill=colour, outline=dark, width=2)
        elif shape == 'oval':
            d.ellipse((x - r * 1.3, y - r, x + r * 1.3, y + r), fill=colour, outline=dark, width=2)
        else:
            d.ellipse((x - r, y - r, x + r, y + r), fill=colour, outline=dark, width=2)

    for g in layout.get('Groves', []):
        x, y = to_px(layout, g['X'], g['Z'])
        r = g['Radius'] * s
        for k in range(g['Count']):
            a = k * 2.4
            picto(g['Name'], x + math.cos(a) * r * 0.7, y + math.sin(a) * r * 0.7, g.get('Tint'))
    for m in layout.get('Landmarks', []):
        x, y = to_px(layout, m['X'], m['Z'])
        picto(m['Name'], x, y, m.get('Tint'))
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
