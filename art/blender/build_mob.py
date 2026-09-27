"""Blender: rebuilds enemies from their settings in mobs/builds.json (source, height, body plan, attack, yaw, robe)
through looks.mob_model, into Content/Models/Mobs.
  Blender -b -P art/blender/build_mob.py -- Queen [Varkesh ...]
A new enemy gets its row in builds.json first; a source decimated before the weld goes through mob_repair.py."""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import looks  # noqa: E402

BUILDS = json.load(open(os.path.join(HERE, "mobs", "builds.json")))
for name in sys.argv[sys.argv.index("--") + 1:]:
    b = BUILDS[name]
    info = looks.mob_model(os.path.join(HERE, "..", "..", b["src"]), name, b["height"], tris=b.get("tris", looks.MOB_TRIS),
                           yaw_degrees=b["yaw"], rig=b["rig"], attack=b["attack"], robe=b.get("robe", False))
    info.pop("texture", None)
    print("BUILT", name, json.dumps(info))
