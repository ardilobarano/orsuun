"""Blender: a watertight copy of an enemy's source mesh that keeps the source's own UV layout and texture.

Voxel remesh (closes the hairline gaps of the pre-decimated split source), decimate to a working budget, then give
the new mesh the source's UV charts: each new face joins the chart (source UV island) of the nearest source point to
its centre (a majority pass removes single-face specks), and each face corner takes that chart's UV at the corner's
position (its nearest point on the chart, extended a little past the chart's rim by the nearest triangle's own
mapping, so a rim face reads the texture's padding instead of stretching the last texel). The texture is the
source's, untouched. Exports in the source's frame (same yaw and scale), smooth shaded.

Args: -- <name> <src_glb> <voxel_frac_of_height> <work_tris> <out_glb> <work_prefix>
Used on the 8 enemies still cracked after mob_model's UV weld (27 Sep 2026): voxel 0.002, 60000 work tris, then
looks.mob_model (build_mob.py) with each one's settings in mobs/builds.json. 0.0015 leaks (the body dissolves), 0.0025 blurs small openings.
  Blender -b -P art/blender/mob_repair.py -- Varkesh art/blender/mobs/Varkesh-tripo.glb 0.002 60000 art/blender/mobs/Varkesh-repaired.glb /tmp/varkesh
"""
import json
import math
import os
import sys
import time
from collections import Counter

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import looks  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:]
name, glb, frac, work_tris, out_glb, prefix = argv[0], argv[1], float(argv[2]), int(argv[3]), argv[4], argv[5]
EXTEND = 0.004     # how far past a chart's rim (fraction of height) a corner may extend the chart's mapping
T0 = time.time()
stats = dict(name=name, src=glb, voxel_frac=frac, work_tris=work_tris)


def log(*a):
    print("XFER %6.1fs" % (time.time() - T0), *a, flush=True)


def topo(me):
    bm = bmesh.new()
    bm.from_mesh(me)
    out = dict(open=sum(e.is_boundary for e in bm.edges), nonmanifold=sum(not e.is_manifold for e in bm.edges),
               tris=sum(len(f.verts) - 2 for f in bm.faces), verts=len(bm.verts))
    bm.free()
    return out


def apply_mod(ob, kind, **kw):
    m = ob.modifiers.new("M", kind)
    for k, v in kw.items():
        setattr(m, k, v)
    dg = bpy.context.evaluated_depsgraph_get()
    new = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    ob.modifiers.clear()
    old = ob.data
    ob.data = new
    bpy.data.meshes.remove(old)


# ---- source: triangles, UVs, charts ----
looks._clear()
src = looks._import(glb)
mw = src.matrix_world.copy()
src.parent = None
src.data.transform(mw)
src.matrix_world = Matrix.Identity(4)
sme = src.data
sco = np.empty(len(sme.vertices) * 3, np.float64)
sme.vertices.foreach_get("co", sco)
sco = sco.reshape(-1, 3)
H = float(sco[:, 2].max() - sco[:, 2].min())
sme.calc_loop_triangles()
m = len(sme.loop_triangles)
stv = np.empty(m * 3, np.int32)
sme.loop_triangles.foreach_get("vertices", stv)
stl = np.empty(m * 3, np.int32)
sme.loop_triangles.foreach_get("loops", stl)
suv = np.empty(len(sme.loops) * 2, np.float64)
sme.uv_layers.active.data.foreach_get("uv", suv)
SP = sco[stv].reshape(m, 3, 3)
SU = suv.reshape(-1, 2)[stl].reshape(m, 3, 2)
stv = stv.reshape(m, 3)
# Charts: the glTF import splits vertices wherever the UV jumps, so a connected piece is one UV chart.
parent = np.arange(len(sco))


def find(x):
    r = x
    while parent[r] != r:
        r = parent[r]
    while parent[x] != r:
        parent[x], x = r, parent[x]
    return r


for a, b, c in stv:
    ra, rb, rc = find(a), find(b), find(c)
    parent[rb] = ra
    parent[find(rc)] = ra
roots = np.array([find(i) for i in range(len(sco))])
_, chart_of_vert = np.unique(roots, return_inverse=True)
chart_of_tri = chart_of_vert[stv[:, 0]]
n_charts = int(chart_of_tri.max()) + 1
stats.update(src_dims=(sco.max(0) - sco.min(0)).round(4).tolist(), src_topo=topo(sme), charts=n_charts)
log("source", stats["src_dims"], stats["src_topo"], "charts", n_charts)
tree_all = BVHTree.FromPolygons([Vector(c) for c in sco], stv.tolist())
chart_tris = [np.nonzero(chart_of_tri == k)[0] for k in range(n_charts)]
chart_tree = []
for k in range(n_charts):
    ids = chart_tris[k]
    chart_tree.append(BVHTree.FromPolygons([Vector(c) for c in SP[ids].reshape(-1, 3)],
                                           np.arange(len(ids) * 3).reshape(-1, 3).tolist()))
log("chart trees built")

# ---- watertight copy ----
fix = bpy.data.objects.new(name + "_Repaired", sme.copy())
bpy.context.scene.collection.objects.link(fix)
apply_mod(fix, 'REMESH', mode='VOXEL', voxel_size=frac * H, adaptivity=0.0)
stats["remesh_topo"] = topo(fix.data)
log("remesh", stats["remesh_topo"])
t = stats["remesh_topo"]["tris"]
if t > work_tris:
    apply_mod(fix, 'DECIMATE', decimate_type='COLLAPSE', ratio=work_tris / t, use_collapse_triangulate=True)
bm = bmesh.new()
bm.from_mesh(fix.data)
bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 3])
bm.to_mesh(fix.data)
bm.free()
me = fix.data
me.materials.clear()
me.materials.append(sme.materials[0])
me.shade_smooth()
stats["fix_topo"] = topo(me)
fco = np.empty(len(me.vertices) * 3, np.float64)
me.vertices.foreach_get("co", fco)
fco = fco.reshape(-1, 3)
stats["fix_dims"] = (fco.max(0) - fco.min(0)).round(4).tolist()
log("decimated", stats["fix_topo"], stats["fix_dims"])
# Leak / loss checks: surface area against the source's (a leak through a gap leaves inner walls and raises it), and
# how far the source's vertices lie from the new surface (a lost thin part leaves source vertices far away).
stats["area_ratio"] = round(sum(p.area for p in me.polygons) / sum(p.area for p in sme.polygons), 3)
_bm = bmesh.new()
_bm.from_mesh(me)
_tree = BVHTree.FromBMesh(_bm)
_bm.free()
_d = np.array([_tree.find_nearest(Vector(p))[3] for p in sco.tolist()]) / H
stats["src_to_new_pctH"] = dict(p50=round(100 * float(np.median(_d)), 4), p99=round(100 * float(np.percentile(_d, 99)), 4),
                                max=round(100 * float(_d.max()), 4), over1=int((_d > 0.01).sum()), over2=int((_d > 0.02).sum()))
log("area ratio", stats["area_ratio"], "source->new", stats["src_to_new_pctH"])

# ---- chart of each new face: nearest source point of its centre, then a majority pass over neighbours ----
nf = len(me.polygons)
fv = np.empty(nf * 3, np.int32)
me.polygons.foreach_get("vertices", fv)
fv = fv.reshape(nf, 3)
cent = fco[fv].mean(1)
chart = np.empty(nf, np.int64)
cdist = np.empty(nf)
for i, c in enumerate(cent.tolist()):
    r = tree_all.find_nearest(c)
    chart[i] = chart_of_tri[r[2]]
    cdist[i] = r[3]
stats["centre_dist_pctH"] = [round(float(x), 4) for x in 100 * np.percentile(cdist, [50, 99, 100]) / H]
bm = bmesh.new()
bm.from_mesh(me)
bm.faces.ensure_lookup_table()
nbrs = [[g.index for e in f.edges for g in e.link_faces if g is not f] for f in bm.faces]
bm.free()
changed_total = 0
for _ in range(3):
    changed = 0
    new = chart.copy()
    for i in range(nf):
        cnt = Counter(chart[j] for j in nbrs[i])
        top, n = cnt.most_common(1)[0]
        if chart[i] not in cnt and n >= 2:
            new[i] = top
            changed += 1
    chart = new
    changed_total += changed
    if not changed:
        break
stats["speck_faces_reassigned"] = changed_total
stats["charts_used"] = int(len(np.unique(chart)))
log("face charts", stats["charts_used"], "reassigned", changed_total)

# ---- UV of each corner in its face's chart ----
cache = {}
far = 0


def chart_uv(k, vi):
    global far
    key = (k, vi)
    if key in cache:
        return cache[key]
    p = Vector(fco[vi])
    loc, _n, j, d = chart_tree[k].find_nearest(p)
    tri = chart_tris[k][j]
    A, B, C = (Vector(x) for x in SP[tri])
    # The corner projected into that triangle's plane, in barycentric terms (unclamped past the rim, bounded).
    q = p if d <= EXTEND * H else loc + (p - loc) * (EXTEND * H / d)
    v0, v1, v2 = B - A, C - A, q - A
    d00, d01, d11 = v0.dot(v0), v0.dot(v1), v1.dot(v1)
    d20, d21 = v2.dot(v0), v2.dot(v1)
    den = d00 * d11 - d01 * d01
    if abs(den) < 1e-20:
        bb = (1 / 3, 1 / 3, 1 / 3)
    else:
        bv = (d11 * d20 - d01 * d21) / den
        bw = (d00 * d21 - d01 * d20) / den
        bb = (1 - bv - bw, bv, bw)
    if d > EXTEND * H:
        far += 1
    uv = SU[tri, 0] * bb[0] + SU[tri, 1] * bb[1] + SU[tri, 2] * bb[2]
    cache[key] = uv
    return uv


luv = np.empty((len(me.loops), 2))
lv = np.empty(len(me.loops), np.int32)
me.loops.foreach_get("vertex_index", lv)
lstart = np.empty(nf, np.int32)
me.polygons.foreach_get("loop_start", lstart)
for i in range(nf):
    k = int(chart[i])
    for c in range(3):
        li = lstart[i] + c
        luv[li] = chart_uv(k, int(lv[li]))
uvl = me.uv_layers.get("UVMap") or me.uv_layers.new(name="UVMap")
me.uv_layers.active = uvl
uvl.data.foreach_set("uv", luv.ravel())
stats["corner_pairs"] = len(cache)
stats["corners_beyond_extend"] = far
log("uv corners", len(cache), "beyond extend", far)

for o in bpy.context.scene.objects:
    o.select_set(o is fix)
bpy.context.view_layer.objects.active = fix
bpy.ops.export_scene.gltf(filepath=out_glb, export_format='GLB', use_selection=True, export_image_format='AUTO',
                          export_materials='EXPORT', export_normals=True, export_texcoords=True, export_yup=True,
                          export_apply=False)
log("exported", out_glb)
with open(prefix + "_xfer.json", "w") as fh:
    json.dump(stats, fh, indent=1)
print("XFERRED", name, json.dumps(stats))
