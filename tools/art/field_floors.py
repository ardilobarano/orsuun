"""The hunt's field ground (28 Sep 2026: the Metin2-style field): each lane floor (Content/Floors/<key>.jpg, a tileable
tile with the road across its middle) gives a road-free field texture, <key>Field.jpg: the band above the road, and the
same band mirrored under it, so it tiles both ways; LaneView lays it all around the one road strip. Run with
/usr/bin/python3 (PIL) after adding or changing a floor."""
import glob
import os
from PIL import Image, ImageOps

ROOT = os.path.join(os.path.dirname(__file__), '..', '..')
FLOORS = os.path.join(ROOT, 'client', 'Assets', 'Orsuun', 'Content', 'Floors')
BAND = 0.28   # the top share of a tile that holds no road


def main():
    for path in sorted(glob.glob(os.path.join(FLOORS, '*.jpg'))):
        name = os.path.splitext(os.path.basename(path))[0]
        if name.endswith('Field'):
            continue
        tile = Image.open(path).convert('RGB')
        w, h = tile.size
        band = tile.crop((0, 0, w, int(h * BAND)))
        field = Image.new('RGB', (w, band.height * 2))
        field.paste(band, (0, 0))
        field.paste(ImageOps.flip(band), (0, band.height))
        field = field.resize((1024, 1024), Image.LANCZOS)
        field.save(os.path.join(FLOORS, name + 'Field.jpg'), quality=88)
        print(name)


if __name__ == '__main__':
    main()
