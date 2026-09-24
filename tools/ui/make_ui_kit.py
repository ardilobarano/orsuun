"""UI kit for Orsuun (direction B: dusk-indigo panels, bronze rims). Draws every frame, plate, ring and bar the client
nine-slices, into client/Assets/Orsuun/Resources/UI. Fills are near-white so the code tints them; rims, the ribbon,
the backdrop and the bars are drawn in their final colours. Drawn at 2x (the client uses pixelsPerUnitMultiplier 2)
and 4x supersampled for clean edges. Every size is a power of two (Unity's default import rescales other sizes).

    python tools/ui/make_ui_kit.py            (needs Pillow)
"""
import math
import os
import random

from PIL import Image, ImageChops, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "client", "Assets", "Orsuun", "Resources", "UI")
SS = 4  # supersampling

BRONZE_HI = (246, 216, 142)
BRONZE_MID = (190, 140, 66)
BRONZE_LO = (104, 70, 30)
BRONZE_DARK = (40, 26, 12)

random.seed(7)


def big(w, h, color=(0, 0, 0, 0)):
    return Image.new("RGBA", (w * SS, h * SS), color)


def down(img):
    return img.resize((img.width // SS, img.height // SS), Image.LANCZOS)


def rrect_mask(w, h, box, r):
    m = Image.new("L", (w, h), 0)
    ImageDraw.Draw(m).rounded_rectangle(box, radius=r, fill=255)
    return m


def diag_gradient(w, h, stops):
    """Top-left to bottom-right gradient through (t, rgb) stops."""
    g = Image.new("RGB", (w, h))
    px = g.load()
    for y in range(h):
        for x in range(w):
            t = (x / max(1, w - 1) * 0.35 + y / max(1, h - 1) * 0.65)
            px[x, y] = lerp_stops(stops, t)
    return g


def vert_gradient(w, h, stops):
    g = Image.new("RGB", (w, h))
    d = ImageDraw.Draw(g)
    for y in range(h):
        d.line([(0, y), (w, y)], fill=lerp_stops(stops, y / max(1, h - 1)))
    return g


def lerp_stops(stops, t):
    t = max(0.0, min(1.0, t))
    for i in range(len(stops) - 1):
        t0, c0 = stops[i]
        t1, c1 = stops[i + 1]
        if t <= t1:
            k = (t - t0) / max(1e-6, t1 - t0)
            return tuple(int(c0[j] + (c1[j] - c0[j]) * k) for j in range(3))
    return stops[-1][1]


def noise(img, amount):
    """Soft grain on the RGB channels, keeping alpha."""
    w, h = img.size
    n = Image.effect_noise((w, h), 64).filter(ImageFilter.GaussianBlur(0.6))
    n = n.point(lambda v: 128 + (v - 128) * amount / 64)
    r, g, b, a = img.split()
    out = []
    for ch in (r, g, b):
        out.append(ImageChops.add(ch, n, 1, -128))
    return Image.merge("RGBA", (*out, a))


BRONZE_STOPS = [(0.0, BRONZE_HI), (0.35, BRONZE_MID), (0.7, BRONZE_LO), (1.0, (150, 104, 48))]


def ring(w, h, outer, inner_inset, r, stops=BRONZE_STOPS):
    """A bronze rim: the band between a rounded rect and its inset, with a dark inner line and a lit top edge."""
    band = rrect_mask(w, h, outer, r)
    x0, y0, x1, y1 = outer
    inner = (x0 + inner_inset, y0 + inner_inset, x1 - inner_inset, y1 - inner_inset)
    hole = rrect_mask(w, h, inner, max(1, r - inner_inset))
    band = ImageChops.subtract(band, hole)
    metal = diag_gradient(w // 8, h // 8, stops).resize((w, h), Image.BILINEAR).convert("RGBA")
    layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    layer.paste(metal, (0, 0), band)
    d = ImageDraw.Draw(layer)
    # dark line just inside the band, and a thin highlight along the outer top
    d.rounded_rectangle(inner, radius=max(1, r - inner_inset), outline=BRONZE_DARK + (230,), width=SS * 2)
    d.rounded_rectangle((x0 + SS, y0 + SS, x1 - SS, y1 - SS), radius=r, outline=(255, 238, 190, 90), width=SS)
    return layer


def stud(layer, cx, cy, rad):
    d = ImageDraw.Draw(layer)
    for i in range(rad, 0, -1):
        t = 1 - i / rad
        c = lerp_stops([(0, BRONZE_LO), (0.6, BRONZE_MID), (1, (255, 240, 200))], t)
        off = int(rad * 0.25 * t)
        d.ellipse((cx - i - off, cy - i - off, cx + i - off, cy + i - off), fill=c + (255,))
    d.ellipse((cx - rad, cy - rad, cx + rad, cy + rad), outline=BRONZE_DARK + (255,), width=SS)


def diamond(layer, cx, cy, s):
    d = ImageDraw.Draw(layer)
    d.polygon([(cx, cy - s), (cx + s, cy), (cx, cy + s), (cx - s, cy)], fill=BRONZE_MID + (255,), outline=BRONZE_DARK + (255,))
    s2 = s // 2
    d.polygon([(cx, cy - s2), (cx + s2, cy), (cx, cy + s2), (cx - s2, cy)], fill=BRONZE_HI + (255,))


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    img.save(os.path.join(OUT, name + ".png"))
    print(name, img.size)


def fill_plate(w, h, r, top, bottom, lip=True, gloss=True):
    """Near-white plate for tinting: a vertical bevel, a gloss band on top, a darker lip at the bottom."""
    W, H = w * SS, h * SS
    m = rrect_mask(W, H, (0, 0, W - 1, H - 1), r * SS)
    grad = vert_gradient(W // 4, H // 4, [(0, top), (0.55, lerp_stops([(0, top), (1, bottom)], 0.5)), (1, bottom)]).resize((W, H))
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    img.paste(grad.convert("RGBA"), (0, 0), m)
    d = ImageDraw.Draw(img, "RGBA")
    if gloss:
        g = rrect_mask(W, H, (SS * 3, SS * 3, W - SS * 3, int(H * 0.45)), (r - 2) * SS)
        glossy = Image.new("RGBA", (W, H), (255, 255, 255, 46))
        img = Image.alpha_composite(img, Image.composite(glossy, Image.new("RGBA", (W, H)), g))
    if lip:
        l = rrect_mask(W, H, (0, int(H * 0.86), W - 1, H - 1), r * SS)
        l = ImageChops.multiply(l, m)
        shade = Image.new("RGBA", (W, H), (0, 0, 0, 70))
        img = Image.alpha_composite(img, Image.composite(shade, Image.new("RGBA", (W, H)), l))
    return noise(down(img), 5)


def main():
    # Buttons: a tintable plate plus an untinted bronze rim with corner studs. 256x128, border 44.
    save(fill_plate(256, 128, 22, (250, 250, 250), (170, 170, 170)), "ButtonFill")
    W, H = 256 * SS, 128 * SS
    rim = ring(W, H, (0, 0, W - 1, H - 1), 7 * SS, 22 * SS)
    for cx, cy in ((22, 22), (W // SS - 22, 22), (22, H // SS - 22), (W // SS - 22, H // SS - 22)):
        stud(rim, cx * SS, cy * SS, 6 * SS)
    save(down(rim), "ButtonRim")

    # Cards (rows, boxes): a flat tintable fill with a faint inner gradient, and a slimmer rim with corner diamonds.
    save(fill_plate(128, 128, 14, (236, 236, 240), (205, 205, 214), lip=False, gloss=False), "CardFill")
    W, H = 128 * SS, 128 * SS
    rim = ring(W, H, (0, 0, W - 1, H - 1), 4 * SS, 14 * SS)
    for cx, cy in ((13, 13), (W // SS - 13, 13), (13, H // SS - 13), (W // SS - 13, H // SS - 13)):
        diamond(rim, cx * SS, cy * SS, 7 * SS)
    save(down(rim), "CardRim")

    # Round buttons (skills, the level badge): plate, bronze ring with four studs, and a disc for the cooldown sweep.
    S = 256 * SS
    plate = Image.new("RGBA", (S, S))
    m = Image.new("L", (S, S), 0)
    ImageDraw.Draw(m).ellipse((0, 0, S - 1, S - 1), fill=255)
    grad = vert_gradient(64, 64, [(0, (250, 250, 250)), (1, (150, 150, 150))]).resize((S, S))
    plate.paste(grad.convert("RGBA"), (0, 0), m)
    save(noise(down(plate), 4), "RoundFill")
    ringimg = Image.new("RGBA", (S, S))
    outer = Image.new("L", (S, S), 0)
    ImageDraw.Draw(outer).ellipse((0, 0, S - 1, S - 1), fill=255)
    inner = Image.new("L", (S, S), 0)
    t = 18 * SS
    ImageDraw.Draw(inner).ellipse((t, t, S - 1 - t, S - 1 - t), fill=255)
    band = ImageChops.subtract(outer, inner)
    metal = diag_gradient(64, 64, BRONZE_STOPS).resize((S, S)).convert("RGBA")
    ringimg.paste(metal, (0, 0), band)
    d = ImageDraw.Draw(ringimg)
    d.ellipse((t, t, S - 1 - t, S - 1 - t), outline=BRONZE_DARK + (255,), width=3 * SS)
    d.ellipse((SS * 2, SS * 2, S - SS * 2, S - SS * 2), outline=(255, 236, 180, 120), width=SS * 2)
    c = S // 2
    for a in (45, 135, 225, 315):
        rr = c - t // 2
        stud(ringimg, int(c + rr * math.cos(math.radians(a))), int(c + rr * math.sin(math.radians(a))), 7 * SS)
    save(down(ringimg), "RoundRim")
    disc = Image.new("RGBA", (S, S))
    ImageDraw.Draw(disc).ellipse((0, 0, S - 1, S - 1), fill=(255, 255, 255, 255))
    save(down(disc), "Disc")

    # Bars (HP, XP, walls): a dark trough with a bronze rim, and a glossy tintable fill. 256x64, border 24 / 16.
    W, H = 256 * SS, 64 * SS
    trough = Image.new("RGBA", (W, H))
    m = rrect_mask(W, H, (0, 0, W - 1, H - 1), 14 * SS)
    trough.paste(vert_gradient(8, 8, [(0, (12, 8, 14)), (1, (30, 22, 30))]).resize((W, H)).convert("RGBA"), (0, 0), m)
    trough = Image.alpha_composite(trough, ring(W, H, (0, 0, W - 1, H - 1), 5 * SS, 14 * SS))
    save(down(trough), "BarFrame")
    save(fill_plate(128, 64, 10, (255, 255, 255), (150, 150, 150), lip=False), "BarFill")

    # Pills (currencies, chips): dark capsule, thin rim. 128x64, border 30.
    W, H = 128 * SS, 64 * SS
    pill = Image.new("RGBA", (W, H))
    m = rrect_mask(W, H, (0, 0, W - 1, H - 1), H // 2)
    pill.paste(vert_gradient(8, 8, [(0, (22, 20, 34)), (1, (10, 9, 16))]).resize((W, H)).convert("RGBA"), (0, 0), m)
    pill = Image.alpha_composite(pill, ring(W, H, (0, 0, W - 1, H - 1), 3 * SS, H // 2, [(0, BRONZE_MID), (1, BRONZE_LO)]))
    save(down(pill), "Pill")

    # Screen-title ribbon: crimson lacquer with bronze edges and swallowtail ends. 512x128, border 96 / 40.
    W, H = 512 * SS, 128 * SS
    rib = Image.new("RGBA", (W, H))
    notch = 44 * SS
    shape = [(0, H * 0.15), (notch * 2.2, H * 0.15), (notch * 2.2, 0), (W - notch * 2.2, 0), (W - notch * 2.2, H * 0.15),
             (W, H * 0.15), (W - notch, H * 0.5), (W, H * 0.85), (W - notch * 2.2, H * 0.85), (W - notch * 2.2, H),
             (notch * 2.2, H), (notch * 2.2, H * 0.85), (0, H * 0.85), (notch, H * 0.5)]
    m = Image.new("L", (W, H), 0)
    ImageDraw.Draw(m).polygon(shape, fill=255)
    body = vert_gradient(8, 32, [(0, (150, 34, 30)), (0.5, (112, 22, 22)), (1, (70, 12, 14))]).resize((W, H)).convert("RGBA")
    rib.paste(body, (0, 0), m)
    d = ImageDraw.Draw(rib)
    d.polygon(shape, outline=BRONZE_MID + (255,), width=5 * SS)
    d.line([(notch * 2.2, H * 0.12), (W - notch * 2.2, H * 0.12)], fill=BRONZE_HI + (200,), width=2 * SS)
    d.line([(notch * 2.2, H * 0.88), (W - notch * 2.2, H * 0.88)], fill=BRONZE_LO + (230,), width=2 * SS)
    for x in (notch * 2.2, W - notch * 2.2):
        d.line([(x, 0), (x, H)], fill=BRONZE_MID + (255,), width=4 * SS)
    save(noise(down(rib), 5), "Ribbon")

    # Screen backdrop: opaque dusk indigo with grain, a warm glow from the top and a vignette. 512x1024.
    W, H = 512, 1024
    bd = vert_gradient(W, H, [(0, (30, 26, 44)), (0.5, (16, 15, 27)), (1, (9, 9, 15))]).convert("RGBA")
    glow = Image.new("L", (W, H), 0)
    ImageDraw.Draw(glow).ellipse((-W * 0.4, -H * 0.25, W * 1.4, H * 0.3), fill=70)
    glow = glow.filter(ImageFilter.GaussianBlur(90))
    warm = Image.new("RGBA", (W, H), (120, 70, 30, 255))
    bd = Image.composite(warm, bd, glow).convert("RGBA")
    vig = Image.new("L", (W, H), 0)
    ImageDraw.Draw(vig).ellipse((-W * 0.3, -H * 0.15, W * 1.3, H * 1.15), fill=255)
    vig = ImageChops.invert(vig.filter(ImageFilter.GaussianBlur(120)))
    dark = Image.new("RGBA", (W, H), (0, 0, 0, 255))
    bd = Image.composite(dark, bd, vig.point(lambda v: int(v * 0.7))).convert("RGBA")
    # faint diamond lattice, like tooled leather
    lat = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ld = ImageDraw.Draw(lat)
    step = 64
    for k in range(-H, W + H, step):
        ld.line([(k, 0), (k + H, H)], fill=(255, 220, 160, 9), width=2)
        ld.line([(k, 0), (k - H, H)], fill=(255, 220, 160, 9), width=2)
    bd = Image.alpha_composite(bd, lat)
    save(noise(bd, 7), "Backdrop")

    # Bars across the top and the bottom of the HUD: dark lacquer with a bronze trim on the lane side. 256x128, border 40.
    W, H = 256 * SS, 128 * SS
    bar = vert_gradient(8, 32, [(0, (24, 22, 36)), (1, (12, 11, 19))]).resize((W, H)).convert("RGBA")
    d = ImageDraw.Draw(bar)
    d.rectangle((0, 0, W, 7 * SS), fill=BRONZE_LO + (255,))
    d.rectangle((0, 0, W, 3 * SS), fill=BRONZE_MID + (255,))
    d.line([(0, SS), (W, SS)], fill=BRONZE_HI + (255,), width=SS)
    d.line([(0, 9 * SS), (W, 9 * SS)], fill=(0, 0, 0, 160), width=2 * SS)
    save(noise(down(bar), 5), "NavBar")
    save(noise(down(bar), 5).transpose(Image.FLIP_TOP_BOTTOM), "TopBar")

    # Ornamental rule with a centre diamond. 512x32, border 200 / 0.
    W, H = 512 * SS, 32 * SS
    rule = Image.new("RGBA", (W, H))
    d = ImageDraw.Draw(rule)
    d.line([(0, H // 2), (W, H // 2)], fill=BRONZE_MID + (255,), width=3 * SS)
    d.line([(0, H // 2 - 3 * SS), (W, H // 2 - 3 * SS)], fill=BRONZE_LO + (160,), width=SS)
    diamond(rule, W // 2, H // 2, 12 * SS)
    diamond(rule, W // 2 - 26 * SS, H // 2, 6 * SS)
    diamond(rule, W // 2 + 26 * SS, H // 2, 6 * SS)
    save(down(rule), "Rule")

    # Notification badge and a soft glow.
    S = 64 * SS
    badge = Image.new("RGBA", (S, S))
    d = ImageDraw.Draw(badge)
    d.ellipse((0, 0, S - 1, S - 1), fill=(120, 14, 14, 255))
    d.ellipse((4 * SS, 4 * SS, S - 4 * SS, S - 4 * SS), fill=(214, 44, 36, 255))
    d.ellipse((12 * SS, 8 * SS, S - 22 * SS, S // 2), fill=(255, 150, 130, 110))
    save(down(badge), "Badge")
    S = 128
    g = Image.new("L", (S, S), 0)
    ImageDraw.Draw(g).ellipse((16, 16, S - 16, S - 16), fill=255)
    g = g.filter(ImageFilter.GaussianBlur(18))
    glow = Image.new("RGBA", (S, S), (255, 255, 255, 0))
    glow.putalpha(g)
    save(glow, "Glow")

    # The tutorial's pointer: a bronze arrow pointing down (the client turns it), with a dark edge and a lit ridge.
    S = 128 * SS
    head = [(S * 0.5, S * 0.96), (S * 0.08, S * 0.5), (S * 0.3, S * 0.5), (S * 0.3, S * 0.06), (S * 0.7, S * 0.06),
            (S * 0.7, S * 0.5), (S * 0.92, S * 0.5)]
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).polygon(head, fill=255)
    metal = diag_gradient(S // 8, S // 8, BRONZE_STOPS).resize((S, S), Image.BILINEAR).convert("RGBA")
    arrow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    arrow.paste(metal, (0, 0), mask)
    d = ImageDraw.Draw(arrow)
    d.polygon(head, outline=BRONZE_DARK + (255,), width=3 * SS)
    d.line([(S * 0.5, S * 0.12), (S * 0.5, S * 0.86)], fill=(255, 240, 200, 150), width=3 * SS)
    save(down(arrow), "Pointer")


if __name__ == "__main__":
    main()
