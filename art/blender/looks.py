"""Item-look pipeline for the Vanguard (run inside Blender, e.g. through the Blender MCP).

Weapon and body armour are the only visible items (owner, 23 Sep 2026). Each level band has an armour look and a
weapon look, see src/Orsuun.Rules/ItemLooks.cs. This module turns Rodin models into Unity-ready looks:

  armor_look(glb, "Armor_T0")   full character holding a glaive -> the glaive is cut out; the rest becomes
                                Vanguard_Armor (glows with the armour) plus Vanguard_Body (face, banner; never glows);
                                empties WeaponBase and WeaponTip mark where the hand holds the pole.
  weapon_look(glb, "Weapon_T0") a standing glaive -> base at the origin, pole along +Z, blade on top.
  mob_model(glb, "Wolf", 1.1)   an enemy -> feet on the ground, centred, facing -Y like the Rodin output, scaled to
                                the given height; exports to Resources/Models/Mobs/.

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


def _held_islands(mesh, classify, keep, other, hands):
    """The region rules also catch boots and cloth shreds beside a blade. Faces classified `keep` are joined into
    islands through shared vertex positions (glTF splits vertices at UV seams); for each hand only the island nearest
    it (the one it grips) stays `keep`, the rest become `other`. Returns the refined classify."""
    bm = bmesh.new()
    bm.from_mesh(mesh.data)
    key = lambda c: (round(c.x, 5), round(c.y, 5), round(c.z, 5))
    cand = [(key(f.calc_center_median()), f) for f in bm.faces if classify(f.calc_center_median()) == keep]
    parent = list(range(len(cand)))

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i

    owner = {}
    for i, (_k, f) in enumerate(cand):
        for v in f.verts:
            j = owner.setdefault(key(v.co), i)
            if find(j) != find(i):
                parent[find(i)] = find(j)
    groups = {}
    for i, (k, f) in enumerate(cand):
        groups.setdefault(find(i), []).append((k, f.calc_center_median().copy()))
    kept = set()
    for hand in hands:
        held = min(groups.values(), key=lambda members: min((c - hand).length for _k, c in members))
        kept.update(k for k, _c in held)
    dropped = {k for k, _f in cand if k not in kept}
    bm.free()
    return lambda p: other if key(p) in dropped else classify(p)


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
        if o.type in ('MESH', 'EMPTY', 'ARMATURE'):
            bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes):
        if m.users == 0:
            bpy.data.meshes.remove(m)
    for a in list(bpy.data.armatures):
        if a.users == 0:
            bpy.data.armatures.remove(a)


def pole_axis(verts, height, left=None, right=None, iters=1500, radius=0.03, max_x=None, top=0.8, near=None):
    """The glaive pole: the only straight line with surface along the whole height. RANSAC over lines from a point near
    the feet to a point above the head, scored by how many height bins have surface within `radius` of the line;
    the winner is refined by least squares on its inliers. max_x keeps the search on the glaive hand's side (the
    Banner Lamellar carries a second, straight banner pole on its back). top: where the line's upper point is sampled
    from (a staff held at the side ends below the head). near: a point the line must pass within 0.1 of (the hand that
    holds it). Returns the line's points at z=0 and z=height."""
    import random
    rng = random.Random(7)
    lows = [v for v in verts if v.z < 0.3 * height]
    highs = [v for v in verts if v.z > top * height]
    r2 = radius * radius
    bin_h = 0.05 * height
    best, best_score = None, -1
    for _ in range(iters):
        a = rng.choice(lows)
        b = rng.choice(highs)
        d = b - a
        if d.z < 0.5 * height or (d.x * d.x + d.y * d.y) > 0.0625:
            continue                                   # near vertical: the glaive stands upright, the banner pole leans
        if max_x is not None and (a.x + b.x) / 2 > max_x:
            continue
        if near is not None:
            t = (near.z - a.z) / d.z
            if (near.x - (a.x + d.x * t)) ** 2 + (near.y - (a.y + d.y * t)) ** 2 > 0.01:
                continue
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


def _export_rigged(root, look_id):
    """Armour look with its armature and actions: one FBX take per action (Idle, Run, Attack, Hit, Death)."""
    os.makedirs(OUT, exist_ok=True)
    sel = [root] + list(root.children_recursive)
    with bpy.context.temp_override(selected_objects=sel, active_object=root, object=root):
        bpy.ops.export_scene.fbx(filepath=OUT + look_id + ".fbx", use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                                 bake_space_transform=True, mesh_smooth_type='FACE', path_mode='STRIP',
                                 embed_textures=False, object_types={'EMPTY', 'MESH', 'ARMATURE'},
                                 add_leaf_bones=False, use_armature_deform_only=False,
                                 bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
                                 bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0)


def armor_look(glb, look_id, pole=None, rig=True):
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
    mid_x = sorted(v.x for v in vs)[len(vs) // 2]
    a, b = pole_axis(vs, ARMOR_HEIGHT, left, right, max_x=mid_x - 0.2)
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
    hands = None
    if rig:
        import importlib
        import rig as rigging
        importlib.reload(rigging)
        for a in list(bpy.data.actions):
            bpy.data.actions.remove(a)
        _arm, hand_r, hand_l = rigging.rig_vanguard([objs["Vanguard_Armor"]], root, base + shift, tip + shift, body=objs["Vanguard_Body"])
        hands = (tuple(round(c, 3) for c in hand_r), tuple(round(c, 3) for c in hand_l))
        _export_rigged(root, look_id)
    else:
        _export([objs["Vanguard_Armor"], objs["Vanguard_Body"]] + [c for c in root.children if c.type == 'EMPTY'], root, look_id)
    faces = {n: len(o.data.polygons) for n, o in objs.items()}
    return dict(look=look_id, tris_in=t0, faces=faces, pole=(round(left, 3), round(right, 3)),
                base=tuple(round(c, 3) for c in base + shift), tip=tuple(round(c, 3) for c in tip + shift), texture=size, hands=hands,
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


MOBS = HOME + "/client/Assets/Orsuun/Resources/Models/Mobs/"
MOB_TRIS = 6000       # up to 16 on screen at once
KORSTONES = HOME + "/client/Assets/Orsuun/Resources/Models/Korstones/"   # korstone shapes: mob_model(..., out_dir=KORSTONES)


def mob_model(glb, name, height, tris=MOB_TRIS, yaw_degrees=0.0, out_dir=None, rig=None, attack=None):
    """Enemy model: decimated, rotated by yaw_degrees about Z so it faces -Y, centred on X/Y with its lowest point on
    the ground, scaled so it stands `height` metres tall. Exports <name>.fbx and <name>BaseColor.png to MOBS.
    rig: a body plan from mobrig (biped, quadruped, serpent, scorpion) to rig it with the five clips; attack: the
    biped attack style (sword or staff)."""
    import math
    global OUT
    _clear()
    mesh = _import(glb)
    t0 = _bake(mesh, tris=tris)
    if yaw_degrees:
        mesh.data.transform(Matrix.Rotation(math.radians(yaw_degrees), 4, 'Z'))
    vs = [v.co for v in mesh.data.vertices]
    zs = [v.z for v in vs]
    mesh.data.transform(Matrix.Scale(height / (max(zs) - min(zs)), 4))
    vs = [v.co for v in mesh.data.vertices]
    cx = (max(v.x for v in vs) + min(v.x for v in vs)) / 2
    cy = (max(v.y for v in vs) + min(v.y for v in vs)) / 2
    mesh.data.transform(Matrix.Translation(Vector((-cx, -cy, -min(v.z for v in vs)))))
    mesh.name = name + "_Body"
    mesh.data.name = mesh.name
    target = out_dir or MOBS
    looks_out, OUT = OUT, target
    try:
        os.makedirs(target, exist_ok=True)
        size = _texture(mesh, name)
        root = bpy.data.objects.new(name, None)
        bpy.context.scene.collection.objects.link(root)
        mesh.parent = root
        if rig:
            import importlib
            import mobrig
            importlib.reload(mobrig)
            for a in list(bpy.data.actions):
                bpy.data.actions.remove(a)
            mobrig.rig_mob(mesh, root, height, name + "Rig", rig, attack=attack)
            _export_rigged(root, name)
        else:
            _export([mesh], root, name)
    finally:
        OUT = looks_out
    return dict(mob=name, tris_in=t0, faces=len(mesh.data.polygons), size=[round(c, 2) for c in mesh.dimensions],
                texture=size)


CLASSES = HOME + "/client/Assets/Orsuun/Resources/Models/Classes/"


def class_look(glb, name, height, tris=TRIS, weapon="knives", attack=None, yaw_degrees=0.0):
    """Another playable class (A-pose sheet, weapon in hand): decimated, standing `height` tall, facing -Y, split
    into <name>_Armor and <name>_Weapon so each glows with its own item's level, rigged by rig.rig_humanoid with the
    shared actions and the class's own attack, exported to Resources/Models/Classes with its texture.
    weapon: "knives" (a blade below each hand, also twin swords), "sword" (a blade below the right hand) or "staff"
    (a straight staff in the right hand, found as a line and pinned to that hand). yaw_degrees turns the mesh about Z
    first so it faces -Y (Tripo exports face +X: -90)."""
    import importlib
    import math
    import rig as rigging
    importlib.reload(rigging)
    global OUT
    _clear()
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)
    mesh = _import(glb)
    t0 = _bake(mesh, tris=tris)
    if yaw_degrees:
        mesh.data.transform(Matrix.Rotation(math.radians(yaw_degrees), 4, 'Z'))
    zs = [v.co.z for v in mesh.data.vertices]
    mesh.data.transform(Matrix.Scale(height / (max(zs) - min(zs)), 4))
    vs = [v.co for v in mesh.data.vertices]
    cx = (max(v.x for v in vs) + min(v.x for v in vs)) / 2
    cy = (max(v.y for v in vs) + min(v.y for v in vs)) / 2
    mesh.data.transform(Matrix.Translation(Vector((-cx, -cy, -min(v.z for v in vs)))))
    verts = [v.co.copy() for v in mesh.data.vertices]
    layout = rigging.humanoid_layout(verts, height)
    H = height
    hand_r = Vector(layout["hand.R"][0])
    hand_l = Vector(layout["hand.L"][0])
    if weapon == "staff":
        # Through the right hand, clear of the body's own edge.
        a, b = pole_axis(verts, height, max_x=min(-0.12 * height, hand_r.x + 0.1), radius=0.035, top=0.7, near=hand_r)
        classify = lambda p: name + "_Weapon" if _distance_to_axis(p, a, b) < 0.05 else name + "_Armor"
    else:
        def blade(p, hand):
            # Below the hand on its side, out past the hips (past the shins under the knee, where a sabre's tip
            # curves in). Boots and kilt shreds caught here are separate islands and _held_islands drops them.
            side = 1 if hand.x > 0 else -1
            return p.x * side > (0.16 if p.z > 0.25 * H else 0.1) * H and p.z < hand.z + 0.03 * H
        if weapon == "sword":
            classify = lambda p: name + "_Weapon" if blade(p, hand_r) else name + "_Armor"
        else:
            classify = lambda p: name + "_Weapon" if blade(p, hand_r) or blade(p, hand_l) else name + "_Armor"
    if weapon != "staff":                       # a staff is already a clean line; the fist splits it into pieces
        classify = _held_islands(mesh, classify, name + "_Weapon", name + "_Armor",
                                 [hand_r, hand_l] if weapon == "knives" else [hand_r])
    objs = _split(mesh, classify, [name + "_Armor", name + "_Weapon"])
    # The weapon rides the hand that holds it whole; weighted by nearness, a blade by the thigh would follow the leg.
    bone = (lambda p: "hand.R" if p.x < 0 else "hand.L") if weapon == "knives" else "hand.R"
    looks_out, OUT = OUT, CLASSES
    try:
        os.makedirs(CLASSES, exist_ok=True)
        size = _texture(mesh, name)
        bpy.data.objects.remove(mesh, do_unlink=True)
        root = bpy.data.objects.new(name, None)
        bpy.context.scene.collection.objects.link(root)
        for o in objs.values():
            o.parent = root
        _arm, layout = rigging.rig_humanoid([objs[name + "_Armor"]], root, height, name + "Rig",
                                             weapon=objs[name + "_Weapon"], weapon_bone=bone,
                                             attack=attack, layout=layout)
        _export_rigged(root, name)
    finally:
        OUT = looks_out
    faces = {n: len(o.data.polygons) for n, o in objs.items()}
    return dict(cls=name, tris_in=t0, faces=faces, texture=size,
                hands=(tuple(round(c, 2) for c in hand_r), tuple(round(c, 2) for c in hand_l)))
