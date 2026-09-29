"""Writes the big maps' layouts (client/Assets/Orsuun/Resources/FieldMaps/<Name>.json, read by FieldMap) for the maps
built after the Oathfields (whose layout was placed by hand). Each map is a clockwise trail loop; landmarks, camps and
groves are placed by how far along the trail they stand (a share of its length) and how far to its left (the loop's
outside, the side the lane camera looks across), so they stay 7-25 m off the trail wherever the loop runs.
Run with python3; then tools/art/field_map.py <Name> draws each full map's sketch."""
import json
import math
import os
import sys

ROOT = os.path.join(os.path.dirname(__file__), '..', '..')
OUT = os.path.join(ROOT, 'client', 'Assets', 'Orsuun', 'Resources', 'FieldMaps')


def catmull(a, b, c, d, t):
    t2, t3 = t * t, t * t * t
    return tuple(0.5 * (2 * b[i] + (-a[i] + c[i]) * t + (2 * a[i] - 5 * b[i] + 4 * c[i] - d[i]) * t2
                        + (-a[i] + 3 * b[i] - 3 * c[i] + d[i]) * t3) for i in range(2))


class Trail:
    def __init__(self, points):
        self.points = points
        n = len(points)
        self.curve = []
        for i in range(n):
            a, b, c, d = points[(i - 1) % n], points[i], points[(i + 1) % n], points[(i + 2) % n]
            steps = max(2, math.ceil(math.dist(b, c)))
            self.curve += [catmull(a, b, c, d, k / steps) for k in range(steps)]
        self.along = [0.0]
        for i in range(1, len(self.curve) + 1):
            self.along.append(self.along[-1] + math.dist(self.curve[i - 1], self.curve[i % len(self.curve)]))
        self.length = self.along[-1]

    def at(self, share, left=0.0):
        """The point `share` of the way round, `left` metres to the heading's left (negative: right)."""
        s = (share % 1.0) * self.length
        i = max(0, min(len(self.curve) - 1, next(k for k in range(len(self.along)) if self.along[k] >= s) - 1))
        p, q = self.curve[i], self.curve[(i + 1) % len(self.curve)]
        dx, dz = q[0] - p[0], q[1] - p[1]
        m = math.hypot(dx, dz) or 1.0
        return (round(p[0] - dz / m * left, 1), round(p[1] + dx / m * left, 1))

    def heading(self, share):
        a, b = self.at(share), self.at(share + 2.0 / self.length)
        return math.degrees(math.atan2(b[0] - a[0], b[1] - a[1]))

    def clearance(self, x, z):
        return min(math.dist((x, z), p) for p in self.curve)


def spot(name, xz, **kw):
    d = {'Name': name, 'X': xz[0], 'Z': xz[1]}
    d.update(kw)
    return d


def layout(name, map_id, trail, **kw):
    d = {'Name': name, 'Map': map_id, 'Size': 300, 'Trail': [{'x': x, 'y': z} for x, z in trail.points]}
    d.update(kw)
    return d


def row(trail, model, share, left, count, step, height, yaw=None, tint=None, jitter=0.0):
    """`count` models in a line along the trail's left from `share`, `step` metres apart."""
    out = []
    for k in range(count):
        at = trail.at(share + k * step / trail.length, left + (jitter * math.sin(k * 2.3)))
        y = trail.heading(share + k * step / trail.length) + 90 if yaw is None else yaw
        s = spot(model, at, Yaw=round(y % 360, 1), Height=height)
        if tint:
            s['Tint'] = tint
        out.append(s)
    return out


def check(d, trail):
    for group in ('Landmarks', 'Camps', 'Groves'):
        for s in d.get(group, []):
            c = trail.clearance(s['X'], s['Z'])
            if c < 5.5:
                print('  too near the trail:', d['Name'], group, s['Name'], round(c, 1))


def gorak():
    t = Trail([(-80, -20), (-62, 38), (-22, 70), (30, 66), (74, 34), (84, -18), (52, -60), (0, -72), (-52, -58)])
    marks = []
    # Tul-Gorak's war camp round the loop's north-east: black tents, the war drum, a palisade and banners.
    marks += [spot('GorakTent', t.at(0.30, 18), Yaw=200, Height=6.2), spot('GorakTent', t.at(0.34, 24), Yaw=230, Height=5.6),
              spot('GorakTent', t.at(0.38, 17), Yaw=250, Height=6.4), spot('GorakTent', t.at(0.26, 26), Yaw=180, Height=5.2)]
    marks += [spot('WarDrum', t.at(0.33, 12), Yaw=round(t.heading(0.33) + 180, 1), Height=6.5)]
    marks += row(t, 'Palisade', 0.22, 30, 7, 4.2, 4.2, jitter=0.6)
    marks += [spot('WarBanner', t.at(s, 8.5), Yaw=round(t.heading(s) + 90, 1), Height=7.5) for s in (0.25, 0.31, 0.37, 0.43)]
    # Skull Rocks and a burnt ford on the stream.
    marks += [spot('SteppeBoulder', t.at(0.62, 14), Yaw=20, Height=3.2, Tint='#8A8078'), spot('SteppeBoulder', t.at(0.64, 20), Yaw=140, Height=4.4, Tint='#8A8078'),
              spot('SteppeBoulder', t.at(0.66, 13), Yaw=260, Height=2.6, Tint='#8A8078')]
    marks += [spot('WarBanner', t.at(s, 9), Yaw=round(t.heading(s) + 90, 1), Height=6.5) for s in (0.70, 0.90)]
    d = layout('Gorak Pass', 2, t, ScenerySet='Steppe', KorstoneRing='SteppeStone', GrassCards=700, Stones=110, Weather='embers', Birds=True,
               River=[{'x': x, 'y': z} for x, z in [(-44, 160), (-48, 90), (-30, 30), (-44, -30), (-34, -90), (-40, -160)]],
               RiverWidth=5, WaterColor='#3E6B78', BankColor='#4A3B2C',
               Landmarks=marks,
               Camps=[spot('Hound Pens', t.at(0.06, 8)), spot('Raider Camp', t.at(0.15, 8)), spot('The Drum Ground', t.at(0.35, 7.5)),
                      spot('Marauder Ridge', t.at(0.52, 8)), spot('Skull Rocks', t.at(0.68, 7.5)), spot('Burnt Ford', t.at(0.83, 8))],
               Places=[spot("Tul-Gorak's War Camp", t.at(0.32, 30)), spot('The Palisade', t.at(0.24, 38))],
               Groves=[spot('SteppeBoulder', t.at(s, 20), Radius=8, Count=5, Height=2.6, Tint='#8A8078') for s in (0.02, 0.12, 0.48, 0.58, 0.78, 0.95)],
               Patches=[spot('mud', t.at(s, l), Radius=r, Height=r * 0.6, Yaw=a) for s, l, r, a in
                        ((0.08, 14, 9, 20), (0.30, 6, 12, 70), (0.55, 16, 8, 120), (0.80, 12, 10, 40))]
               + [spot('ash', t.at(s, l), Radius=r, Height=r * 0.7, Yaw=a) for s, l, r, a in ((0.36, 20, 10, 10), (0.88, 18, 9, 80), (0.15, -20, 12, 30))])
    check(d, t)
    return 'GorakPass', d


def saltsea():
    t = Trail([(-78, -8), (-56, 44), (-10, 72), (40, 70), (80, 36), (82, -22), (46, -62), (-8, -74), (-58, -52)])
    marks = [spot('CaravanWreck', t.at(0.12, 12), Yaw=40, Height=3.0), spot('CaravanWreck', t.at(0.60, 14), Yaw=200, Height=2.8)]
    marks += [spot('SaltPillar', t.at(s, l), Yaw=y, Height=h) for s, l, y, h in
              ((0.22, 16, 0, 8.5), (0.24, 22, 60, 6.2), (0.26, 15, 140, 7.4), (0.70, 18, 20, 9), (0.73, 13, 100, 6))]
    # Red rock spires (the salt pillar in the mesas' colour) far out.
    marks += [spot('SaltPillar', t.at(s, l), Yaw=y, Height=h, Tint='#C07A58') for s, l, y, h in
              ((0.40, 24, 30, 11), (0.44, 28, 150, 9), (0.90, 24, 80, 12), (0.03, 26, 10, 10))]
    oasis = t.at(0.50, 22)
    marks += [spot('DesertShrine', t.at(0.50, 12), Yaw=round(t.heading(0.5) + 180, 1), Height=5)]
    d = layout('The Salt Sea', 3, t, ScenerySet='Desert', KorstoneRing='SaltPillar', GrassCards=520, Stones=0, Weather='dust', Birds=True,
               Landmarks=marks,
               Camps=[spot('Bone Ridge', t.at(0.05, 8)), spot('Wreck of the Last Caravan', t.at(0.15, 7.5)),
                      spot('Pillar Field', t.at(0.30, 8)), spot('Oasis Shrine', t.at(0.47, 7.5)), spot('Glass Dunes', t.at(0.65, 8)),
                      spot('Scorpion Flats', t.at(0.82, 8))],
               Places=[spot('The Oasis', oasis), spot('The Last Caravan', t.at(0.12, 20)), spot('Pillars of Salt', t.at(0.24, 30))],
               Groves=[spot('DatePalm', oasis, Radius=11, Count=6, Height=7)],
               Patches=[spot('water', oasis, Radius=7, Height=5, Yaw=30)]
               + [spot('salt', t.at(s, l), Radius=r, Height=r * 0.7, Yaw=a) for s, l, r, a in
                  ((0.02, 18, 16, 10), (0.18, -24, 20, 40), (0.34, 20, 14, 70), (0.58, -22, 18, 20), (0.78, 20, 16, 110), (0.94, -20, 14, 60),
                   (0.66, 30, 12, 0), (0.86, 34, 12, 90))])
    check(d, t)
    return 'SaltSea', d


def whitefang():
    t = Trail([(-74, -24), (-66, 34), (-30, 70), (20, 74), (66, 46), (84, -4), (60, -54), (8, -70), (-44, -60)])
    marks = [spot('MountainHut', t.at(s, l), Yaw=round(t.heading(s) + 160, 1), Height=5) for s, l in ((0.08, 14), (0.10, 21), (0.13, 15))]
    marks += [spot('IceShrine', t.at(0.40, 10), Yaw=round(t.heading(0.40) + 180, 1), Height=3.8)]
    marks += [spot('IceCrag', t.at(s, l), Yaw=y, Height=h) for s, l, y, h in
              ((0.22, 18, 0, 7), (0.25, 24, 80, 9), (0.55, 16, 200, 6), (0.58, 22, 40, 8.5), (0.84, 20, 120, 7.5), (0.97, 24, 300, 6.5))]
    lake = t.at(0.70, 24)
    d = layout('Whitefang Range', 4, t, ScenerySet='Mountain', KorstoneRing='IceCrag', GrassCards=620, Stones=0, Weather='snow',
               River=[{'x': x, 'y': z} for x, z in [(40, 160), (46, 90), (36, 20), (48, -40), (40, -160)]],
               RiverWidth=6, WaterColor='#A8CCE0', BankColor='#E4ECF4',
               Landmarks=marks,
               Camps=[spot('Pine Hollow', t.at(0.03, 8)), spot('Whitefang Hamlet', t.at(0.16, 7.5)), spot('Bear Caves', t.at(0.29, 8)),
                      spot('Shrine Pass', t.at(0.44, 7.5)), spot('Frozen Lake', t.at(0.66, 8)), spot("Hag's Hollow", t.at(0.88, 8))],
               Places=[spot('Whitefang Hamlet', t.at(0.10, 26)), spot('The Wolf Shrine', t.at(0.40, 18)), spot('Frozen Lake', lake)],
               Groves=[spot('FrostPine', t.at(s, l), Radius=10, Count=7, Height=8) for s, l in
                       ((0.00, 22), (0.18, 20), (0.34, 22), (0.50, 24), (0.62, 16), (0.78, 22), (0.92, 18))]
               + [spot('FrostPine', t.at(s, -18), Radius=8, Count=4, Height=7) for s in (0.1, 0.45, 0.8)],
               Patches=[spot('ice', lake, Radius=13, Height=8, Yaw=20)]
               + [spot('snow', t.at(s, l), Radius=r, Height=r * 0.6, Yaw=a) for s, l, r, a in
                  ((0.05, 16, 12, 10), (0.30, -18, 14, 50), (0.48, 14, 10, 80), (0.75, -16, 12, 30), (0.93, 12, 10, 60))])
    check(d, t)
    return 'Whitefang', d


def shape(turn, stretch=1.0, wobble=0):
    """A loop for a budget map: the base loop turned by `turn` degrees, stretched east-west, with a wobble."""
    base = [(-76, -16), (-68, 28), (-40, 62), (0, 74), (40, 68), (70, 40), (82, -4), (64, -48), (20, -70), (-24, -70), (-60, -48)]
    out = []
    for k, (x, z) in enumerate(base):
        r = 1 + 0.08 * math.sin(k * 1.7 + wobble)
        x, z = x * stretch * r, z / stretch * r
        a = math.radians(turn)
        # Turning keeps the loop clockwise.
        out.append((round(x * math.cos(a) + z * math.sin(a), 1), round(-x * math.sin(a) + z * math.cos(a), 1)))
    return out


def budget(title, map_id, points, scenery, ring, marks, camps, places, groves=(), patches=(), river=None, river_width=5,
           water='#3D667A', bank='#5C4D33', grass=700, stones=0, weather='', birds=False, zones=None, dungeon=0, backdrop=None,
           extra=(), ring_tint=None):
    """A budget map: landmarks (model, share, left, height, tint[, yaw]) reuse the models in the map's colours."""
    t = Trail(points)
    landmarks = []
    for m in marks:
        model, share, left, height, tint = m[:5]
        yaw = m[5] if len(m) > 5 else round(t.heading(share) + 180, 1)
        d = spot(model, t.at(share, left), Yaw=yaw, Height=height)
        if tint:
            d['Tint'] = tint
        landmarks.append(d)
    landmarks += list(extra)
    d = layout(title, map_id, t, ScenerySet=scenery, KorstoneRing=ring, GrassCards=grass, Stones=stones, Landmarks=landmarks,
               Weather=weather, Birds=birds,
               Camps=[spot(n, t.at(sh, 7.5 + (k % 2))) for k, (n, sh) in enumerate(camps)],
               Places=[spot(n, t.at(sh, l)) for n, sh, l in places],
               Groves=[dict(spot(m, t.at(sh, l), Radius=r, Count=c, Height=h), **({'Tint': tint} if tint else {})) for m, sh, l, r, c, h, tint in groves],
               Patches=[spot(k, t.at(sh, l), Radius=r, Height=r * 0.65, Yaw=a) for k, sh, l, r, a in patches])
    if river:
        d.update(River=[{'x': x, 'y': z} for x, z in river], RiverWidth=river_width, WaterColor=water, BankColor=bank)
    if ring_tint:
        d['KorstoneRingTint'] = ring_tint
    if zones:
        d['Zones'] = list(zones)
    if dungeon:
        d['Dungeon'] = dungeon
    if backdrop:
        d['Backdrop'] = backdrop  # the lane backdrop it hunts under: tools/art/field_map.py takes the sketch's ground from it
    check(d, t)
    return d


def cinder():
    obsidian, burnt, dark = '#5C3A34', '#5A4A40', '#6A5A52'
    return 'CinderMarches', budget('The Cinder Marches', 5, shape(20, 1.05, 1), 'Desert', 'SaltPillar',
        [('SaltPillar', 0.12, 16, 9, obsidian), ('SaltPillar', 0.14, 22, 7, obsidian), ('SaltPillar', 0.62, 18, 10, obsidian),
         ('SaltPillar', 0.65, 24, 7.5, obsidian), ('IceCrag', 0.25, 20, 8, dark), ('LavaForge', 0.84, 18, 5, None),
         ('AshShrine', 0.46, 16, 6, None), ('Palisade', 0.48, 11, 3.8, burnt), ('Palisade', 0.495, 12, 3.6, burnt),
         ('WarBanner', 0.22, 9, 6.5, '#8A5A48'), ('WarBanner', 0.90, 9, 6.5, '#8A5A48')],
        [('Ash Fields', 0.04), ('Hound Craters', 0.19), ('Obsidian Spires', 0.36), ('The Burnt Watch', 0.50), ('Cultist Pyres', 0.66), ('Lava Ford', 0.76)],
        [('The Burnt Watchtower', 0.46, 26), ('Obsidian Spires', 0.13, 30)],
        groves=[('SteppeBoulder', s, 20, 8, 5, 2.6, dark) for s in (0.06, 0.40, 0.74, 0.95)],
        patches=[('ash', s, l, r, a) for s, l, r, a in ((0.08, 16, 12, 20), (0.34, -18, 14, 60), (0.58, 14, 10, 100), (0.84, -16, 12, 30))]
                + [('lava', 0.27, 22, 6, 40), ('lava', 0.76, 26, 7, 80)],
        river=[(120, 160), (60, 80), (20, 0), (-40, -80), (-100, -160)], river_width=5, water='#FF6A1A', bank='#2E2826', ring_tint=obsidian, weather='embers+ash', birds=False)


def whisper():
    pine, birch, stone = '#6E7E70', '#A8B0A0', '#7C8878'
    return 'Whisperwood', budget('Whisperwood', 6, shape(-15, 0.95, 2), 'Forest', 'SteppeStone',
        [('IceShrine', 0.18, 11, 3.6, stone), ('IceShrine', 0.20, 14, 3.2, stone), ('IceShrine', 0.22, 11, 3.4, stone),
         ('LanternShrine', 0.55, 13, 4.5, None), ('HangingTree', 0.33, 12, 7, None), ('WarBanner', 0.40, 8.5, 5.5, '#707870'),
         ('WarBanner', 0.78, 8.5, 5.5, '#707870')],
        [('Crooked Path', 0.04), ('Old Graves', 0.19), ('Hanging Tree', 0.33), ('The Forgotten Shrine', 0.52), ("Widow's Pond", 0.68), ('Mist Hollow', 0.84)],
        [('Old Graves', 0.20, 22), ("Widow's Pond", 0.70, 22)],
        groves=[('FrostPine', s, l, 10, 8, 8.5, pine) for s, l in ((0.02, 20), (0.10, 22), (0.28, 20), (0.36, 24), (0.46, 20), (0.60, 22), (0.80, 20), (0.92, 22))]
               + [('FrostPine', s, -16, 8, 5, 8, pine) for s in (0.15, 0.5, 0.85)]
               + [('SteppeBirch', s, 14, 6, 4, 6, birch) for s in (0.25, 0.64)],
        patches=[('water', 0.70, 20, 8, 20)] + [('moss', s, l, r, a) for s, l, r, a in ((0.12, 14, 10, 30), (0.42, -16, 12, 70), (0.88, 14, 9, 10))],
        river=[(120, 160), (60, 80), (20, 0), (-40, -80), (-100, -160)], water='#2E4442', bank='#3A3A2C', weather='fireflies', birds=False)


def bloodbirch():
    red, rock = '#D05A48', '#7A5048'
    return 'Bloodbirch', budget('The Bloodbirch', 7, shape(45, 1.1, 3), 'Forest', 'SteppeStone',
        [('RootThrone', 0.40, 14, 5.5, None), ('IceCrag', 0.15, 20, 7, rock), ('SapWell', 0.70, 18, 3.6, None),
         ('SteppeStone', 0.56, 10, 3.2, '#B07060'), ('SteppeStone', 0.58, 13, 3.4, '#B07060'), ('SteppeStone', 0.60, 10, 3, '#B07060')],
        [('Red Grove', 0.05), ('Stalker Trail', 0.20), ('Root Hollow', 0.37), ('Birch Circle', 0.55), ('Sap Pools', 0.72), ('Bleeding Ford', 0.88)],
        [("The Rootfather's Grove", 0.40, 26), ('Sap Pools', 0.74, 22)],
        groves=[('SteppeBirch', s, l, 11, 9, 6.5, red) for s, l in ((0.0, 18), (0.08, 22), (0.24, 20), (0.30, 24), (0.46, 20), (0.64, 20), (0.82, 22), (0.94, 20))]
               + [('SteppeBirch', s, -16, 9, 6, 6, red) for s in (0.12, 0.42, 0.66, 0.9)],
        patches=[('water', 0.74, 18, 6, 30), ('water', 0.76, 26, 5, 70)] + [('mud', s, l, r, a) for s, l, r, a in ((0.1, 14, 10, 20), (0.5, -16, 12, 60))],
        river=[(-160, 20), (-80, 30), (0, 10), (80, 30), (160, 20)], water='#6A2420', bank='#3E2A22', weather='leaves', birds=False)


def drowned():
    reed, sunk = '#8A9A7A', '#7C8C80'
    return 'DrownedSteppe', budget('The Drowned Steppe', 8, shape(-40, 1.0, 4), 'Forest', 'SteppeStone',
        [('Yurt', 0.30, 14, 3.8, sunk), ('SunkenBellTower', 0.32, 21, 8, None), ('Yurt', 0.34, 13, 3.6, sunk), ('Tent', 0.62, 12, 3.4, '#6A7A6A'),
         ('Tent', 0.64, 16, 3.2, '#6A7A6A'), ('WarBanner', 0.61, 8.5, 6, '#6A7060'), ('BogShrine', 0.12, 9, 5.5, None)],
        [('Reed Beds', 0.03), ('Serpent Pools', 0.17), ('The Sunken Yurts', 0.33), ('Leech Marsh', 0.48), ('Bog Rider Camp', 0.63), ('Drowned Ford', 0.84)],
        [('The Sunken Yurts', 0.32, 28), ("Coil Mother's Pool", 0.20, 26)],
        groves=[('SteppeBirch', s, l, 9, 5, 5.5, reed) for s, l in ((0.06, 20), (0.40, 22), (0.72, 20), (0.92, 22))],
        patches=[('water', s, l, r, a) for s, l, r, a in ((0.20, 24, 10, 20), (0.08, 18, 6, 60), (0.46, 20, 8, 110), (0.52, -18, 9, 30),
                                                           (0.78, 18, 7, 70), (0.9, -20, 10, 10), (0.26, -16, 6, 40))]
                + [('moss', s, l, r, a) for s, l, r, a in ((0.14, 14, 10, 20), (0.58, 14, 9, 80))],
        river=[(-160, -40), (-80, -30), (0, -46), (80, -28), (160, -40)], river_width=7, water='#3A5048', bank='#4A4A36', weather='midges', birds=True)


def graves():
    bone, stone = '#E0D6C4', '#9A9080'
    return 'ColossusGraves', budget('Colossus Graves', 9, shape(70, 1.05, 5), 'Ruins', 'SaltPillar',
        [('SaltPillar', s, l, h, bone, y) for s, l, h, y in ((0.20, 14, 9, 30), (0.22, 18, 11, 60), (0.24, 14, 10, 90), (0.26, 18, 12, 120),
                                                            (0.28, 14, 9, 150))]
        + [('GiantSkull', 0.50, 20, 6, None), ('ColossusHand', 0.535, 27, 9, None), ('Watchtower', 0.70, 16, 9, '#A09888'),
           ('SteppeStone', 0.86, 10, 3, '#B0A898'), ('SteppeStone', 0.875, 12, 3.2, '#B0A898'), ('SteppeStone', 0.89, 10, 2.8, '#B0A898')],
        [('Grave Steps', 0.05), ('Field of Ribs', 0.22), ('Bone Pickers\' Nest', 0.38), ('The Broken Colossus', 0.52), ('Siege Yard', 0.70), ('Giant\'s Rest', 0.87)],
        [('Field of Ribs', 0.24, 30), ('The Broken Colossus', 0.51, 32)],
        groves=[('SteppeBoulder', s, 20, 9, 5, 3, stone) for s in (0.10, 0.36, 0.62, 0.95)],
        patches=[('ash', s, l, r, a) for s, l, r, a in ((0.14, 16, 12, 20), (0.44, -16, 12, 70), (0.78, 14, 10, 30))]
                + [('mud', 0.6, -18, 11, 50)], ring_tint=bone, weather='dust', birds=True)


def bazaar():
    gold = '#D8B060'
    return 'SunkenBazaar', budget('The Sunken Bazaar', 10, shape(-70, 1.0, 6), 'Desert', 'SaltPillar',
        [('DesertShrine', 0.10, 14, 5.5, None), ('SunkenArch', 0.60, 14, 6.5, None), ('MerchantStall', 0.30, 12, 3.8, None),
         ('MerchantStall', 0.32, 16, 3.6, None), ('MerchantStall', 0.34, 12, 3.8, None), ('CaravanWreck', 0.46, 13, 3, None), ('CaravanWreck', 0.80, 14, 2.8, None),
         ('SaltPillar', 0.70, 20, 7, '#D8B070'), ('SaltPillar', 0.72, 25, 5.5, '#D8B070')],
        [('Palm Court', 0.04), ('Drowned Market', 0.18), ('Gilded Stalls', 0.33), ("Debtors' Row", 0.48), ('Cultist Steps', 0.63), ('Flooded Plaza', 0.84)],
        [('The Old Bazaar', 0.32, 28), ('Palm Court', 0.02, 24)],
        groves=[('DatePalm', s, l, 9, 5, 7, None) for s, l in ((0.02, 20), (0.22, 22), (0.56, 22), (0.92, 20))],
        patches=[('water', s, l, r, a) for s, l, r, a in ((0.16, 20, 9, 20), (0.84, 22, 11, 60), (0.40, -18, 8, 30))]
                + [('salt', s, l, r, a) for s, l, r, a in ((0.26, -18, 12, 10), (0.66, -20, 12, 80))], ring_tint='#D8B070', weather='dust+gold', birds=False)


def markers():
    violet = '#8A80A0'
    rows = []
    for r in range(3):
        for k in range(6):
            rows.append(('SteppeStone', 0.18 + k * 0.012, 12 + r * 5, 2.6 + (k % 3) * 0.4, violet, 0))
    return 'ThousandMarkers', budget('The Thousand Markers', 11, shape(110, 1.05, 7), 'Ruins', 'SteppeStone',
        rows + [('MarkerObelisk', s, 9, 6.5, None) for s in (0.36, 0.40, 0.44)]
        + [('Palisade', 0.60, 12, 3.6, '#6A6070'), ('Palisade', 0.615, 12.5, 3.4, '#6A6070'), ('BurialMound', 0.78, 16, 5, None)]
        + [('SteppeStone', 0.9 + k * 0.012, 12, 2.8, violet, 0) for k in range(5)],
        [('Trooper Lines', 0.05), ('Marker Rows', 0.21), ('Broken Standard', 0.40), ('Rider Barrows', 0.58), ("Captain's Mound", 0.74), ('Violet Field', 0.88)],
        [('The Thousand Markers', 0.22, 32), ("Varkesh's Mound", 0.76, 30)],
        groves=[('SteppeBoulder', s, 20, 8, 5, 2.4, '#7A7488') for s in (0.10, 0.50, 0.68)],
        patches=[('ash', s, l, r, a) for s, l, r, a in ((0.12, 14, 12, 20), (0.46, -16, 12, 60), (0.84, 16, 10, 10))], weather='wisps', birds=False)


def throne():
    gold, dark = '#C8A040', '#6A5030'
    return 'HollowThrone', budget('The Hollow Throne', 12, shape(-110, 0.95, 8), 'Ruins', 'IceShrine',
        [('KhanThrone', 0.50, 14, 6.5, None), ('GoldenStatue', 0.28, 16, 2.6, None), ('GoldenStatue', 0.31, 22, 2.4, None)]
        + [('WarBanner', s, 9, 7.5, gold) for s in (0.44, 0.47, 0.53, 0.56)]
        + [('IceShrine', s, 11, 3.6, '#8A8070') for s in (0.70, 0.72, 0.74)]
        + [('Palisade', 0.12, 12, 3.8, '#5A4A30'), ('Palisade', 0.135, 12.5, 3.6, '#5A4A30')],
        [('Golden Road', 0.04), ('Guard Barracks', 0.18), ('Hound Kennels', 0.33), ('Throne Steps', 0.50), ('Oath Stones', 0.72), ("Chanters' Ring", 0.88)],
        [('The Hollow Throne', 0.50, 28), ("The Khan's Standards", 0.46, 20)],
        groves=[('SteppeBoulder', s, 20, 8, 5, 2.6, '#8A7A60') for s in (0.08, 0.40, 0.62, 0.95)],
        patches=[('ash', s, l, r, a) for s, l, r, a in ((0.14, 14, 12, 20), (0.60, -16, 12, 60), (0.86, 14, 10, 10))], weather='gold+ash', birds=False)


def ember_steppe():
    return 'EmberSteppe', budget('Ember Steppe', 0, shape(160, 1.0, 9), 'Steppe', 'SteppeStone',
        [('Yurt', s, l, h, None, y) for s, l, h, y in ((0.10, 14, 4, 20), (0.12, 20, 3.6, 70), (0.14, 14, 4.2, 120), (0.60, 15, 3.8, 200), (0.62, 21, 3.4, 250))]
        + [('Tent', 0.61, 12, 3.2, None), ('WolfDen', 0.38, 18, 4.4, None), ('Watchtower', 0.84, 16, 10, None)]
        + [('SteppeStone', 0.25 + k * 0.01, 12 + (k % 2) * 3, 3.2, '#9A8878', 0) for k in range(6)],
        [('Ember Hollow', 0.05), ('Yurt Ring', 0.18), ('Standing Stones', 0.28), ('Wolf Hill', 0.40), ('Herders\' Camp', 0.66), ('Old Watch', 0.86)],
        [('Ember Steppe', 0.50, 30)],
        groves=[('SteppeBirch', s, l, 9, 6, 5, None) for s, l in ((0.02, 20), (0.46, 22), (0.74, 20), (0.94, 22))],
        patches=[('ash', s, l, r, a) for s, l, r, a in ((0.30, -16, 10, 20), (0.70, -18, 12, 60))],
        stones=90, weather='motes', birds=True, zones=[201], backdrop='HuntingGround')


def salt_flats():
    return 'SaltFlats', budget('Salt Flats', 0, shape(-150, 1.1, 10), 'Desert', 'SaltPillar',
        [('SaltPillar', s, l, h, None, y) for s, l, h, y in ((0.10, 16, 9, 10), (0.12, 22, 7, 80), (0.40, 15, 10, 30), (0.43, 20, 6.5, 140), (0.75, 17, 8.5, 60))]
        + [('CaravanWreck', 0.28, 13, 3, None), ('CaravanWreck', 0.88, 14, 2.8, None), ('DesertShrine', 0.58, 13, 4.6, None)],
        [('Mirage Flats', 0.04), ('White Pillars', 0.14), ('Wagon Graves', 0.29), ('Glass Pans', 0.46), ('Shrine Well', 0.58), ('Bone Drift', 0.80)],
        [('Salt Flats', 0.50, 30)],
        groves=[('DatePalm', 0.56, 20, 7, 4, 6.5, None)],
        patches=[('water', 0.56, 18, 5, 20)] + [('salt', s, l, r, a) for s, l, r, a in
                                                ((0.02, 18, 16, 10), (0.20, -22, 18, 40), (0.34, 20, 14, 70), (0.66, -22, 16, 20), (0.82, 20, 14, 110))],
        grass=480, weather='dust', birds=True, zones=[202], backdrop='SaltFlats')


def frost_pasture():
    frost = '#E6ECF4'
    lake = None
    return 'FrostPasture', budget('Frost Pasture', 0, shape(100, 0.95, 11), 'Mountain', 'IceCrag',
        [('Yurt', s, l, h, frost, y) for s, l, h, y in ((0.20, 14, 4, 30), (0.22, 20, 3.6, 90), (0.24, 14, 4.2, 150))]
        + [('MountainHut', 0.52, 14, 5, None), ('IceShrine', 0.70, 10, 3.6, None), ('IceCrag', 0.36, 20, 8, None), ('IceCrag', 0.88, 22, 7, None)],
        [('Frost Gate', 0.05), ('Herders\' Yurts', 0.19), ('Bear Rocks', 0.36), ('Lone Hut', 0.52), ('Wolf Stone', 0.70), ('White Fold', 0.86)],
        [('Frost Pasture', 0.50, 32)],
        groves=[('FrostPine', s, l, 10, 6, 8, None) for s, l in ((0.00, 22), (0.12, 22), (0.30, 24), (0.44, 22), (0.62, 22), (0.78, 24), (0.94, 22))],
        patches=[('ice', 0.60, 24, 12, 30)] + [('snow', s, l, r, a) for s, l, r, a in ((0.08, 16, 12, 10), (0.40, -18, 14, 60), (0.80, 14, 10, 30))],
        grass=600, weather='snow', zones=[203], backdrop='FrostPasture')


def stone_ring(trail, share, left, radius, count, height, tint):
    """A ring of standing stones round a point beside the trail."""
    cx, cz = trail.at(share, left)
    return [dict(spot('SteppeStone', (round(cx + math.cos(k * 2 * math.pi / count) * radius, 1), round(cz + math.sin(k * 2 * math.pi / count) * radius, 1)),
                      Yaw=k * 360 // count, Height=height + (k % 3) * 0.3), Tint=tint) for k in range(count)]


def korstone_fields():
    dark, obsidian = '#6A6068', '#3E3438'
    points = shape(-60, 1.0, 12)
    t = Trail(points)
    rings = []
    for share in (0.12, 0.45, 0.78):
        rings += stone_ring(t, share, 17, 5.5, 7, 3.2, dark)
    return 'KorstoneFields', budget('Korstone Fields', 0, points, 'Steppe', 'SteppeStone', [('SaltPillar', s, l, h, obsidian) for s, l, h in ((0.28, 18, 8), (0.30, 24, 6), (0.62, 20, 9), (0.95, 18, 7))]
        + [('IceCrag', 0.36, 22, 7, '#5A5058'), ('IceCrag', 0.70, 22, 8, '#5A5058')],
        [('First Circle', 0.08), ('Black Spires', 0.28), ('Second Circle', 0.44), ('Cracked Ground', 0.60), ('Third Circle', 0.76), ('Shard Scatter', 0.92)],
        [('Korstone Fields', 0.50, 30)],
        groves=[('SteppeBoulder', s, 20, 8, 5, 2.6, '#6A6068') for s in (0.02, 0.20, 0.54, 0.86)],
        patches=[('ash', s, l, r, a) for s, l, r, a in ((0.10, -16, 12, 20), (0.34, 14, 10, 60), (0.58, -18, 12, 10), (0.82, 14, 10, 80))],
        stones=60, weather='embers', zones=[211, 212, 213, 214, 215], backdrop='KorstoneField', extra=rings, ring_tint=dark)


def war_camp():
    return 'GorakWarCamp', budget('Gorak War Camp', 0, shape(40, 1.0, 13), 'Steppe', 'SteppeStone',
        [('GorakTent', s, l, h, None, y) for s, l, h, y in ((0.08, 16, 6, 30), (0.11, 22, 5.4, 70), (0.30, 16, 6.2, 110), (0.33, 23, 5.6, 150),
                                                             (0.56, 16, 6, 190), (0.59, 22, 5.2, 230), (0.80, 17, 6.4, 270))]
        + [('WarDrum', 0.20, 13, 6.5, None), ('WarDrum', 0.68, 13, 6, None)]
        + [('Palisade', 0.40 + k * 0.009, 26, 4.2, None) for k in range(8)]
        + [('WarBanner', s, 8.5, 7, None) for s in (0.05, 0.18, 0.26, 0.44, 0.52, 0.66, 0.74, 0.92)],
        [('Camp Gate', 0.03), ('Drum Circle', 0.20), ('Tent Rows', 0.32), ('Stockade', 0.46), ('War Drums', 0.68), ('Warlord\'s Tents', 0.84)],
        [("Tul-Gorak's War Camp", 0.50, 32)],
        groves=[('SteppeBoulder', s, 22, 8, 4, 2.6, '#8A8078') for s in (0.14, 0.50, 0.88)],
        patches=[('mud', s, l, r, a) for s, l, r, a in ((0.10, 8, 10, 20), (0.36, -14, 12, 70), (0.62, 10, 11, 40), (0.90, -14, 10, 10))]
                + [('ash', s, l, r, a) for s, l, r, a in ((0.24, 18, 9, 30), (0.72, 20, 10, 60))],
        grass=520, stones=70, weather='embers', birds=True, zones=[221], backdrop='CommanderGround')


def hall(title, key, dungeon, backdrop, scenery, pillar, statue, weather, camps, place, crag=None):
    """A dungeon's floors: a pillared hall round the trail (a colonnade on the camera's far side, a second row behind)."""
    marks = [('SaltPillar', 0.02 + k * 0.03, 8 if k % 2 == 0 else 8.8, 8, pillar, k * 37) for k in range(33)]
    marks += [('SaltPillar', 0.035 + k * 0.06, 17, 9, pillar, k * 53) for k in range(16)]
    marks += [('IceShrine', s, 11.5, 3.8, statue) for s in (0.10, 0.34, 0.58, 0.82)]
    if crag:
        marks += [('IceCrag', s, 24, 9, crag) for s in (0.2, 0.45, 0.7, 0.95)]
    return key, budget(title, 0, shape(dungeon * 70, 0.9, 20 + dungeon), scenery, 'SaltPillar', marks, camps, [(place, 0.5, 28)],
                       patches=[('ash', s, l, r, a) for s, l, r, a in ((0.2, -14, 12, 30), (0.5, -16, 12, 70), (0.8, -14, 10, 10))],
                       grass=260, weather=weather, dungeon=dungeon, backdrop=backdrop, ring_tint=pillar)


def spire():
    return hall('The Hollow Spire', 'HollowSpire', 1, 'HollowSpire', 'Ruins', '#5E586E', '#7A7488', 'wisps',
                [('Lower Hall', 0.05), ('Bone Stair', 0.21), ('Hollow Gallery', 0.38), ('Warden\'s Walk', 0.55), ('Violet Crypt', 0.72), ('Spire Heart', 0.88)],
                'The Hollow Spire')


def warren():
    return hall("Silkmother's Warren", 'SilkWarren', 2, 'SilkWarren', 'Desert', '#E6E0D4', '#B8AC98', 'dust',
                [('Silk Tunnels', 0.05), ('Egg Nests', 0.21), ('Cocoon Hall', 0.38), ('Brood Deep', 0.55), ('Webbed Pit', 0.72), ('Mother\'s Lair', 0.88)],
                "Silkmother's Warren", crag='#9A8A78')


def archive():
    return hall("The Carvers' Archive", 'CarversArchive', 3, 'CarversArchive', 'Mountain', '#9AA4B4', None, 'motes',
                [('Entry Vault', 0.05), ('Rune Hall', 0.21), ('Carved Gallery', 0.38), ('Sealed Stacks', 0.55), ('Cold Chamber', 0.72), ('Last Carver\'s Rest', 0.88)],
                "The Carvers' Archive")


MAPS = {'EmberSteppe': ember_steppe, 'SaltFlats': salt_flats, 'FrostPasture': frost_pasture, 'KorstoneFields': korstone_fields,
        'GorakWarCamp': war_camp, 'HollowSpire': spire, 'SilkWarren': warren, 'CarversArchive': archive,
        'GorakPass': gorak, 'SaltSea': saltsea, 'Whitefang': whitefang, 'CinderMarches': cinder, 'Whisperwood': whisper,
        'Bloodbirch': bloodbirch, 'DrownedSteppe': drowned, 'ColossusGraves': graves, 'SunkenBazaar': bazaar,
        'ThousandMarkers': markers, 'HollowThrone': throne}


def main():
    names = sys.argv[1:] or list(MAPS)
    for name in names:
        key, d = MAPS[name]()
        with open(os.path.join(OUT, key + '.json'), 'w') as f:
            json.dump(d, f, indent=1)
        print('wrote', key, 'trail', round(Trail([(p['x'], p['y']) for p in d['Trail']]).length), 'm')


if __name__ == '__main__':
    main()
