"""Blender: the hunt's 3D scenery props (28 Sep 2026: "Scenery in 3D"), Tripo H3.1 models from the painted steppe
props (docs/concept/scenery/tripo), through looks.mob_model without a rig: decimated, facing -Y, a metre tall with the
lowest point on the ground (LaneScenery scales each to its prop's height), into Content/Scenery/Models.
  Blender -b -P art/blender/scenery_props.py -- [SteppeBoulder ...]"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import looks  # noqa: E402

OUT = looks.HOME + "/client/Assets/Orsuun/Content/Scenery/Models/"
# name: (source GLB in art/blender/scenery, triangles, yaw so the painted side faces -Y)
PROPS = {
    "SteppeBoulder": ("steppe-boulder-tripo.glb", 2500, -90),
    "SteppeStone": ("steppe-stone-tripo.glb", 2500, -90),
    "SteppeCairn": ("steppe-cairn-tripo.glb", 3000, -90),
    "SteppeBirch": ("steppe-birch-tripo.glb", 4000, -90),
    # The Oathfields' landmarks (FieldMap), from docs/concept/scenery/landmarks.
    "Yurt": ("yurt-tripo.glb", 4000, -90),
    "Watchtower": ("watchtower-tripo.glb", 5000, -90),
    "Tent": ("tent-tripo.glb", 3500, -90),
    "WolfDen": ("wolfden-tripo.glb", 5000, -90),
    # Gorak Pass, the Salt Sea and Whitefang Range (29 Sep 2026), from docs/concept/scenery/landmarks.
    "GorakTent": ("gorak-tent-tripo.glb", 5000, -90),
    "WarDrum": ("war-drum-tripo.glb", 5000, -90),
    "Palisade": ("palisade-tripo.glb", 3500, -90),
    "WarBanner": ("war-banner-tripo.glb", 3000, -90),
    "DatePalm": ("date-palm-tripo.glb", 5000, -90),
    "CaravanWreck": ("caravan-wreck-tripo.glb", 5000, -90),
    "SaltPillar": ("salt-pillar-tripo.glb", 3500, -90),
    "DesertShrine": ("desert-shrine-tripo.glb", 5000, -90),
    "MountainHut": ("mountain-hut-tripo.glb", 5000, -90),
    "IceShrine": ("ice-shrine-tripo.glb", 4000, -90),
    "FrostPine": ("frost-pine-tripo.glb", 5000, -90),
    "IceCrag": ("ice-crag-tripo.glb", 4000, -90),
    # Maps 5-12's own landmarks (29 Sep 2026).
    "AshShrine": ("ash-shrine-tripo.glb", 4500, -90),
    "LavaForge": ("lava-forge-tripo.glb", 4500, -90),
    "HangingTree": ("hanging-tree-tripo.glb", 5000, -90),
    "LanternShrine": ("lantern-shrine-tripo.glb", 4000, -90),
    "RootThrone": ("root-throne-tripo.glb", 5000, -90),
    "SapWell": ("sap-well-tripo.glb", 4500, -90),
    "SunkenBellTower": ("sunken-bell-tower-tripo.glb", 4500, -90),
    "BogShrine": ("bog-shrine-tripo.glb", 4000, -90),
    "GiantSkull": ("giant-skull-tripo.glb", 4500, -90),
    "ColossusHand": ("colossus-hand-tripo.glb", 4500, -90),
    "MerchantStall": ("merchant-stall-tripo.glb", 5000, -90),
    "SunkenArch": ("sunken-arch-tripo.glb", 4500, -90),
    "BurialMound": ("burial-mound-tripo.glb", 4500, -90),
    "MarkerObelisk": ("marker-obelisk-tripo.glb", 3000, -90),
    "KhanThrone": ("khan-throne-tripo.glb", 5000, -90),
    "GoldenStatue": ("golden-statue-tripo.glb", 5000, -90),
}
names = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv and sys.argv.index("--") + 1 < len(sys.argv) else list(PROPS)
for name in names:
    src, tris, yaw = PROPS[name]
    info = looks.mob_model(os.path.join(HERE, "scenery", src), name, 1.0, tris=tris, yaw_degrees=yaw, out_dir=OUT)
    info.pop("texture", None)
    print("BUILT", name, info)
