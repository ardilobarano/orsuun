"""Cuts the painted UI kit (docs/concept/ui-kit/*.png, drawn by Higgsfield from the screen mockups on plain black) into
the sprites the client nine-slices, over the procedural kit from make_ui_kit.py:

  Ribbon, Section, Back           whole plates (title banner, section header, back button), untinted
  ButtonFill + ButtonRim          the button split into its grey lacquer (tinted per button) and its gold frame
  CardRim, SlotRim                panel and slot frames with the centre cut out (a tinted fill shows through)
  BarFrame                        the bar trough with its gold end caps
  RoundRim, Medallion             the skill ring (centre cut out) and the level medallion

Sizes are in the kit's 2x density (the client uses pixelsPerUnitMultiplier 2). Nine-slice borders go to
Resources/UI/Borders.json, which Ui.Kit reads. Run make_ui_kit.py first, then this.

    python tools/ui/cut_ai_kit.py            (needs Pillow)
"""
import json
import os

from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
SRC = os.path.join(ROOT, "docs", "concept", "ui-kit")
OUT = os.path.join(ROOT, "client", "Assets", "Orsuun", "Resources", "UI")
BORDERS = os.path.join(OUT, "Borders.json")


def outer_mask(img, threshold=26):
    """Everything that is not the plain black background reached from the image border."""
    rgb = img.convert("RGB")
    w, h = rgb.size
    bright = rgb.convert("L").point(lambda v: 255 if v > threshold else 0)
    # max channel, so a dark red or navy edge counts as object
    r, g, b = rgb.split()
    maxc = ImageChops.lighter(ImageChops.lighter(r, g), b).point(lambda v: 255 if v > threshold else 0)
    solid = ImageChops.lighter(bright, maxc)
    flood = solid.copy()
    for x in range(0, w, 16):
        for y in (0, h - 1):
            if flood.getpixel((x, y)) == 0:
                ImageDraw.floodfill(flood, (x, y), 128)
    for y in range(0, h, 16):
        for x in (0, w - 1):
            if flood.getpixel((x, y)) == 0:
                ImageDraw.floodfill(flood, (x, y), 128)
    mask = flood.point(lambda v: 0 if v == 128 else 255)
    return mask.filter(ImageFilter.GaussianBlur(1.0))


def interior_mask(img, tolerance=34, seed=None):
    """The flat centre (a panel's fill, a button's lacquer, a ring's hole): a flood from the middle over similar colours."""
    rgb = img.convert("RGB")
    w, h = rgb.size
    sx, sy = seed or (w // 2, h // 2)
    c = rgb.getpixel((sx, sy))
    diff = ImageChops.difference(rgb, Image.new("RGB", (w, h), c)).convert("L")
    near = diff.point(lambda v: 0 if v < tolerance else 255)
    flood = near.copy()
    ImageDraw.floodfill(flood, (sx, sy), 128)
    return flood.point(lambda v: 255 if v == 128 else 0)


def crop_box(mask):
    return mask.point(lambda v: 255 if v > 40 else 0).getbbox()


def fit(img, height=None, size=None):
    if size:
        return img.resize(size, Image.LANCZOS)
    return img.resize((max(1, round(img.width * height / img.height)), height), Image.LANCZOS)


def flatten_middle(sprite, source=0.3, start=0.34, end=0.66):
    """Nine-slicing stretches the middle column: repaint it from a plain column so centre ornaments (the banner's
    crest, small diamonds) do not smear across wide titles."""
    w, h = sprite.size
    column = sprite.crop((int(w * source), 0, int(w * source) + 1, h))
    out = sprite.copy()
    for x in range(int(w * start), int(w * end)):
        out.paste(column, (x, 0))
    return out


def with_alpha(img, alpha):
    out = img.convert("RGBA")
    out.putalpha(alpha)
    return out


def save(img, name):
    img.save(os.path.join(OUT, name + ".png"))
    print(name, img.size)


def main():
    borders = {}
    load = lambda n: Image.open(os.path.join(SRC, n + ".png")).convert("RGB")

    # Whole plates: cut out, crop, scale. Borders keep the ornamented ends and corners fixed.
    for name, src, height, lr, tb in (("Ribbon", "ribbon", 150, 0.17, 0.40), ("Section", "section", 72, 0.27, 0.45),
                                       ("Back", "back", 130, 0.13, 0.42), ("BarFrame", "bar", 56, 0.09, 0.40)):
        img = load(src)
        mask = outer_mask(img)
        box = crop_box(mask)
        sprite = flatten_middle(fit(with_alpha(img, mask).crop(box), height=height))
        save(sprite, name)
        borders[name] = [round(sprite.width * lr), round(sprite.height * tb), round(sprite.width * lr), round(sprite.height * tb)]

    # The button: the grey lacquer becomes a near-white plate (tinted per button); the gold frame stays as it is.
    img = load("button")
    outer = outer_mask(img)
    inner = interior_mask(img, tolerance=40)
    box = crop_box(outer)
    rim_alpha = ImageChops.subtract(outer, inner.filter(ImageFilter.GaussianBlur(1.2)))
    rim = fit(with_alpha(img, rim_alpha).crop(box), height=150)
    fill_alpha = inner.filter(ImageFilter.MaxFilter(9)).filter(ImageFilter.GaussianBlur(1.0))
    fill_alpha = ImageChops.multiply(fill_alpha, outer)
    grey = img.convert("L")
    stat = [grey.getpixel((x, y)) for x in range(0, img.width, 8) for y in range(0, img.height, 8) if inner.getpixel((x, y)) > 0]
    mean = sum(stat) / max(1, len(stat))
    plate = grey.point(lambda v: max(0, min(255, int(v * 238 / mean)))).convert("RGB")
    fill = fit(with_alpha(plate, fill_alpha).crop(box), height=150)
    save(rim, "ButtonRim")
    save(fill, "ButtonFill")
    lr, tb = round(rim.width * 0.17), round(rim.height * 0.42)
    borders["ButtonRim"] = borders["ButtonFill"] = [lr, tb, lr, tb]

    # Frames with the centre cut out, so the tinted fill underneath shows.
    for name, src, size, frac in (("CardRim", "panel", (256, 256), 0.18), ("SlotRim", "slot", (200, 200), 0.2)):
        img = load(src)
        outer = outer_mask(img)
        inner = interior_mask(img, tolerance=30)
        alpha = ImageChops.subtract(outer, inner.filter(ImageFilter.GaussianBlur(1.2)))
        box = crop_box(outer)
        sprite = fit(with_alpha(img, alpha).crop(box), size=size)
        save(sprite, name)
        b = round(size[0] * frac)
        borders[name] = [b, b, b, b]

    # The skill ring (centre cut out) and the level medallion (whole).
    img = load("ring")
    outer = outer_mask(img)
    inner = interior_mask(img, tolerance=30)
    box = crop_box(outer)
    save(fit(with_alpha(img, ImageChops.subtract(outer, inner.filter(ImageFilter.GaussianBlur(1.2)))).crop(box), size=(256, 256)), "RoundRim")
    if os.path.exists(os.path.join(SRC, "medallion.png")):
        img = load("medallion")
        mask = outer_mask(img)
        save(fit(with_alpha(img, mask).crop(crop_box(mask)), size=(256, 256)), "Medallion")

    with open(BORDERS, "w") as f:
        json.dump(borders, f, indent=1, sort_keys=True)
    print("borders", borders)


if __name__ == "__main__":
    main()
