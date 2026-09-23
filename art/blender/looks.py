"""Item-look pipeline for the Vanguard (run inside Blender, e.g. through the Blender MCP).

Weapon and body armour are the only visible items (owner, 23 Sep 2026). Each level band has an armour look and a
weapon look, see src/Orsuun.Rules/ItemLooks.cs. This module turns Rodin models into Unity-ready looks:

  armor_look(glb, "Armor_T0")   full character holding a glaive -> the glaive is cut out; the rest becomes
                                Vanguard_Armor (glows with the armour) plus Vanguard_Body (face, banner; never glows);
                                empties WeaponBase and WeaponTip mark where the hand holds the pole.
  weapon_look(glb, "Weapon_T0") a standing glaive -> base at the origin, pole along +Z, blade on top.

Both export FBX + base-colour PNG into client/Assets/Orsuun/Resources/Models/Looks/. In Unity the weapon is scaled
from WeaponBase to WeaponTip of whichever armour is worn, so any glaive fits any armour.
"""
import bmesh
import bpy
import os
from mathutils import Matrix, Vector

HOME = os.path.expanduser("~/orsuun")
OUT = HOME + "/client/Assets/Orsuun/Resources/Models/Looks/"
TRIS = 12000          # GDD hero budget 8-12k
ARMOR_HEIGHT = 2.5    # total height with the glaive raised, as the first Vanguard


def _import(glb):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=glb)
    new = [o for o in bpy.data.objects if o not in before]
    mesh = [o for o in new if o.type == 'MESH'][0]
    for o in new:
        if o is not mesh:
            bpy.data.objects.remove(o, do_unlink=True)
    return mesh


def _bake(mesh, tris=TRIS):
    """Decimate to the budget and bake the object transform into the vertices."""
    t0 = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
    if t0 > tris:
        dec = mesh.modifiers.new("Decimate", 'DECIMATE')
        dec.ratio = tris / t0
        dg = bpy.context.evaluated_depsgraph_get()
        new = bpy.data.meshes.new_from_object(mesh.evaluated_get(dg))
        mesh.modifiers.clear()
        old = mesh.data
        mesh.data = new
        bpy.data.meshes.remove(old)
    mw = mesh.matrix_world.copy()
    mesh.parent = None
    mesh.data.transform(mw)
    mesh.matrix_world = Matrix.Identity(4)
    return t0


def _texture(mesh, look_id):
    mat = mesh.data.materials[0]
    bsdf = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    if bsdf.inputs['Base Color'].is_linked:
        img = bsdf.inputs['Base Color'].links[0].from_node.image
        img.filepath_raw = OUT + look_id + "BaseColor.png"
        img.file_format = 'PNG'
        img.save()
        return img.size[:]
    return None


def _export(objs, root, look_id):
    os.makedirs(OUT, exist_ok=True)
    sel = [root] + objs
    with bpy.context.temp_override(selected_objects=sel, active_object=root, object=root):
        bpy.ops.export_scene.fbx(filepath=OUT + look_id + ".fbx", use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                                 bake_space_transform=True, mesh_smooth_type='FACE', path_mode='STRIP',
                                 embed_textures=False, object_types={'EMPTY', 'MESH'})


def _split(mesh, classify, names):
    """Rebuilds the mesh's faces into one new mesh per class, keeping UVs and the material."""
    out = {n: bmesh.new() for n in names}
    uv = {n: out[n].loops.layers.uv.new("UVMap") for n in names}
    bm = bmesh.new()
    bm.from_mesh(mesh.data)
    src_uv = bm.loops.layers.uv.active
    for f in bm.faces:
        n = classify(f.calc_center_median())
        vs = [out[n].verts.new(l.vert.co) for l in f.loops]
        nf = out[n].faces.new(vs)
        for ln, lo in zip(nf.loops, f.loops):
            ln[uv[n]].uv = lo[src_uv].uv
    bm.free()
    mat = mesh.data.materials[0]
    objs = {}
    for n in names:
        b = out[n]
        bmesh.ops.remove_doubles(b, verts=b.verts, dist=1e-5)
        me = bpy.data.meshes.new(n)
        b.to_mesh(me)
        b.free()
        me.materials.append(mat)
        ob = bpy.data.objects.new(n, me)
        bpy.context.scene.collection.objects.link(ob)
        objs[n] = ob
    return objs


def find_pole(verts, height):
    """The glaive pole: the leftmost thin vertical strip between knee and hand height. Returns (left, right)."""
    band = sorted(v.x for v in verts if 0.20 * height < v.z < 0.42 * height)
    left = band[0]
    near = [x for x in band if x < left + 0.25]
    right = left + 0.07
    for a, b in zip(near, near[1:]):
        if b - a > 0.02 and a - left > 0.025:
            right = a
            break
    return left, right


def _clear():
    for o in list(bpy.data.objects):
        if o.type in ('MESH', 'EMPTY'):
            bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes):
        if m.users == 0:
            bpy.data.meshes.remove(m)


def pole_axis(verts, height, left=None, right=None, iters=1500, radius=0.03):
    """The glaive pole: the only straight line with surface along the whole height. RANSAC over lines from a point near
    the feet to a point above the head, scored by how many height bins have surface within `radius` of the line;
    the winner is refined by least squares on its inliers. Returns the line's points at z=0 and z=height."""
    import random
    rng = random.Random(7)
    lows = [v for v in verts if v.z < 0.3 * height]
    highs = [v for v in verts if v.z > 0.8 * height]
    r2 = radius * radius
    bin_h = 0.05 * height
    best, best_score = None, -1
    for _ in range(iters):
        a = rng.choice(lows)
        b = rng.choice(highs)
        d = b - a
        if d.z < 0.5 * height or (d.x * d.x + d.y * d.y) > 0.0625:
            continue                                   # near vertical: the glaive stands upright, the banner pole leans
        bins = set()
        for v in verts:
            t = (v.z - a.z) / d.z
            dx = v.x - (a.x + d.x * t)
            dy = v.y - (a.y + d.y * t)
            if dx * dx + dy * dy < r2:
                bins.add(int(v.z / bin_h))
        if len(bins) > best_score:
            best, best_score = (a.copy(), b.copy()), len(bins)
    a, b = best
    d = b - a
    inl = []
    for v in verts:
        t = (v.z - a.z) / d.z
        if (v.x - (a.x + d.x * t)) ** 2 + (v.y - (a.y + d.y * t)) ** 2 < r2:
            inl.append(v)
    n = len(inl)
    mz = sum(c.z for c in inl) / n
    mx = sum(c.x for c in inl) / n
    my = sum(c.y for c in inl) / n
    szz = sum((c.z - mz) ** 2 for c in inl)
    kx = sum((c.z - mz) * (c.x - mx) for c in inl) / szz
    ky = sum((c.z - mz) * (c.y - my) for c in inl) / szz
    at = lambda z: Vector((mx + kx * (z - mz), my + ky * (z - mz), z))
    print("pole: %d/20 height bins covered, %d inliers" % (best_score, n))
    return at(0.0), at(height)


def _distance_to_axis(p, a, b):
    """Horizontal distance from p to the axis line at p's height."""
    t = (p.z - a.z) / (b.z - a.z)
    q = a + (b - a) * t
    return ((p.x - q.x) ** 2 + (p.y - q.y) ** 2) ** 0.5


def armor_look(glb, look_id, pole=None):
    _clear()
    mesh = _import(glb)
    t0 = _bake(mesh)
    vs = [v.co for v in mesh.data.vertices]
    zs = [v.z for v in vs]
    mesh.data.transform(Matrix.Scale(ARMOR_HEIGHT / (max(zs) - min(zs)), 4))
    minz = min(v.co.z for v in mesh.data.vertices)
    mesh.data.transform(Matrix.Translation(Vector((0, 0, -minz))))
    vs = [v.co.copy() for v in mesh.data.vertices]
    left, right = pole or find_pole(vs, ARMOR_HEIGHT)
    a, b = pole_axis(vs, ARMOR_HEIGHT, left, right)
    body_x = [v.x for v in vs if 0.45 * ARMOR_HEIGHT < v.z < 0.6 * ARMOR_HEIGHT and v.x > right + 0.05]
    cx = (min(body_x) + max(body_x)) / 2
    cy = sum(v.y for v in vs) / len(vs)

    def classify(p):
        d = _distance_to_axis(p, a, b)
        if d < 0.05 or (p.z > 0.74 * ARMOR_HEIGHT and d < 0.22 and p.x < cx - 0.17) or (p.z > 0.6 * ARMOR_HEIGHT and d < 0.16):
            return "Weapon"                              # pole along its line, the blade beside the head, tassels under it
        if p.z > 0.74 * ARMOR_HEIGHT and p.x - cx > 0.17:
            return "Vanguard_Body"                       # banner cloth and pole top
        return "Vanguard_Armor"

    objs = _split(mesh, classify, ["Vanguard_Armor", "Vanguard_Body", "Weapon"])
    size = _texture(mesh, look_id)
    bpy.data.objects.remove(mesh, do_unlink=True)
    base = a + (b - a) * ((0.0 - a.z) / (b.z - a.z))
    tip = a + (b - a) * ((ARMOR_HEIGHT - a.z) / (b.z - a.z))
    shift = Vector((-cx, -cy, 0.0))
    root = bpy.data.objects.new(look_id, None)
    bpy.context.scene.collection.objects.link(root)
    for n in ("Vanguard_Armor", "Vanguard_Body"):
        objs[n].data.transform(Matrix.Translation(shift))
        objs[n].parent = root
    for n, p in (("WeaponBase", base + shift), ("WeaponTip", tip + shift)):
        e = bpy.data.objects.new(n, None)
        bpy.context.scene.collection.objects.link(e)
        e.location = p
        e.parent = root
    objs["Weapon"].data.transform(Matrix.Translation(shift))
    _export([objs["Vanguard_Armor"], objs["Vanguard_Body"]] + [c for c in root.children if c.type == 'EMPTY'], root, look_id)
    faces = {n: len(o.data.polygons) for n, o in objs.items()}
    return dict(look=look_id, tris_in=t0, faces=faces, pole=(round(left, 3), round(right, 3)),
                base=tuple(round(c, 3) for c in base + shift), tip=tuple(round(c, 3) for c in tip + shift), texture=size,
                cut_weapon=objs["Weapon"])


def normalise_weapon(obj, look_id):
    """Base at the origin, pole along +Z, then export. Other objects in the scene are removed first."""
    for o in list(bpy.data.objects):
        if o is not obj and o.type in ('MESH', 'EMPTY'):
            bpy.data.objects.remove(o, do_unlink=True)
    vs = sorted((v.co.copy() for v in obj.data.vertices), key=lambda v: v.z)
    k = max(3, len(vs) // 25)
    base = sum(vs[:k], Vector()) / k
    tip = sum(vs[-k:], Vector()) / k
    obj.data.transform(Matrix.Translation(-base))
    rot = (tip - base).normalized().rotation_difference(Vector((0, 0, 1))).to_matrix().to_4x4()
    obj.data.transform(rot)
    obj.name = "Vanguard_Weapon"
    obj.data.name = obj.name
    root = bpy.data.objects.new(look_id, None)
    bpy.context.scene.collection.objects.link(root)
    obj.parent = root
    _export([obj], root, look_id)
    zs = [v.co.z for v in obj.data.vertices]
    return dict(look=look_id, length=round(max(zs) - min(zs), 3), faces=len(obj.data.polygons))


def weapon_look(glb, look_id):
    _clear()
    mesh = _import(glb)
    _bake(mesh, tris=3000)
    size = _texture(mesh, look_id)
    info = normalise_weapon(mesh, look_id)
    info["texture"] = size
    return info
