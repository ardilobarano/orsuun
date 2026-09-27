"""Item-look pipeline for the Vanguard (run inside Blender, e.g. through the Blender MCP).

Weapon and body armour are the only visible items (owner, 23 Sep 2026). Each level band has an armour look and a
weapon look, see src/Orsuun.Rules/ItemLooks.cs. This module turns Rodin models into Unity-ready looks:

  armor_look(glb, "Armor_T0")   full character holding a glaive -> the glaive is cut out; the rest becomes
                                Vanguard_Armor (glows with the armour) plus Vanguard_Body (face, banner; never glows);
                                empties WeaponBase and WeaponTip mark where the hand holds the pole.
  weapon_look(glb, "Weapon_T0") a standing glaive -> base at the origin, pole along +Z, blade on top.
  mob_model(glb, "Wolf", 1.1)   an enemy -> feet on the ground, centred, facing -Y like the Rodin output, scaled to
                                the given height; exports to Content/Models/Mobs/.

Both export FBX + base-colour PNG into client/Assets/Orsuun/Content/Models/Looks/. In Unity the weapon is scaled
from WeaponBase to WeaponTip of whichever armour is worn, so any glaive fits any armour.
"""
import bmesh
import bpy
import os
from mathutils import Matrix, Vector

HOME = os.path.expanduser("~/orsuun")
OUT = HOME + "/client/Assets/Orsuun/Content/Models/Looks/"
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


def _bake(mesh, tris=TRIS, weld=False):
    """Decimate to the budget and bake the object transform into the vertices."""
    t0 = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
    if weld:
        # glTF splits vertices along every UV seam; decimated split, each side of a seam collapses on its own and the
        # seams open into hairline cracks the background shows through (27 Sep 2026: white flecks on the camel).
        # Welded first they stay shut; UVs live on the face corners, so the texture mapping is unchanged. The heroes
        # keep the split mesh: their weapon cut (_held_islands) is tuned to it, and welded a sabre joins a boot it
        # touches.
        bm = bmesh.new()
        bm.from_mesh(mesh.data)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        bm.to_mesh(mesh.data)
        bm.free()
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


def armor_look(glb, look_id, pole=None, rig=True, yaw_degrees=0.0, plain_pole=False):
    """yaw_degrees turns the mesh about Z first so it faces -Y like Rodin's output (Tripo faces +X: -90). Also used for
    the Vanguard's wardrobe skins (look ids Skin_<Look>, 25 Sep 2026).
    plain_pole: the redesigned Vanguard (26 Sep 2026) holds a bare pole and carries no banner: only the pole is cut
    (his great pauldrons stand close to it), everything else is armour. Every rigged look also gets a WeaponGrip empty
    on the fist, where a sword's grip goes."""
    import math
    _clear()
    mesh = _import(glb)
    t0 = _bake(mesh)
    if yaw_degrees:
        mesh.data.transform(Matrix.Rotation(math.radians(yaw_degrees), 4, 'Z'))
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

    fist_z = None
    if plain_pole:
        import importlib
        import rig as rigging_probe
        importlib.reload(rigging_probe)
        fist_z = rigging_probe.measure_hands(vs, a + (b - a) * ((0.0 - a.z) / (b.z - a.z)),
                                             a + (b - a) * ((ARMOR_HEIGHT - a.z) / (b.z - a.z)))[0].z

    def classify(p):
        d = _distance_to_axis(p, a, b)
        if plain_pole:
            # The pole runs about 0.06 thick: cut to 0.075, but keep the boot beside its foot and the fingers around it.
            reach = 0.05 if p.z < 0.4 else 0.045 if abs(p.z - fist_z) < 0.14 else 0.075
            return "Weapon" if d < reach else "Vanguard_Armor"
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
        fist = bpy.data.objects.new("WeaponGrip", None)
        bpy.context.scene.collection.objects.link(fist)
        fist.location = rigging._pole_at(base + shift, tip + shift, hand_r.z)
        fist.parent = root
        bpy.context.view_layer.update()          # attach_to_bone keeps matrix_world, stale until the scene updates
        rigging.attach_to_bone(fist, _arm, "hand.R")
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


MOBS = HOME + "/client/Assets/Orsuun/Content/Models/Mobs/"
MOB_TRIS = 6000       # up to 16 on screen at once
KORSTONES = HOME + "/client/Assets/Orsuun/Content/Models/Korstones/"   # korstone shapes: mob_model(..., out_dir=KORSTONES)


def mob_model(glb, name, height, tris=MOB_TRIS, yaw_degrees=0.0, out_dir=None, rig=None, attack=None):
    """Enemy model: decimated, rotated by yaw_degrees about Z so it faces -Y, centred on X/Y with its lowest point on
    the ground, scaled so it stands `height` metres tall. Exports <name>.fbx and <name>BaseColor.png to MOBS.
    rig: a body plan from mobrig (biped, quadruped, serpent, scorpion) to rig it with the five clips; attack: the
    biped attack style (sword or staff)."""
    import math
    global OUT
    _clear()
    mesh = _import(glb)
    t0 = _bake(mesh, tris=tris, weld=True)
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


CLASSES = HOME + "/client/Assets/Orsuun/Content/Models/Classes/"


def _uv_texels(me, w, h):
    """Every texel the mesh's UV faces cover: rows, columns, the 3D point there and its face's normal."""
    import numpy as np
    uv = me.uv_layers.active.data
    rows, cols, pos, nrm = [], [], [], []
    for poly in me.polygons:
        pts = [(uv[i].uv.x * w, uv[i].uv.y * h) for i in poly.loop_indices]
        co = [me.vertices[me.loops[i].vertex_index].co for i in poly.loop_indices]
        n = np.array(poly.normal)
        for k in range(1, len(pts) - 1):
            tri = np.array([pts[0], pts[k], pts[k + 1]])
            x0, y0 = np.floor(tri.min(axis=0)).astype(int)
            x1, y1 = np.ceil(tri.max(axis=0)).astype(int)
            x0, y0 = max(0, x0), max(0, y0)
            x1, y1 = min(w - 1, x1), min(h - 1, y1)
            if x1 < x0 or y1 < y0:
                continue
            gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
            (ax, ay), (bx, by), (cx, cy) = tri
            d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
            if abs(d) < 1e-9:
                continue
            l1 = ((by - cy) * (gx - cx) + (cx - bx) * (gy - cy)) / d
            l2 = ((cy - ay) * (gx - cx) + (ax - cx) * (gy - cy)) / d
            inside = (l1 >= -0.05) & (l2 >= -0.05) & (1 - l1 - l2 >= -0.05)
            if not inside.any():
                continue
            a, b, c = (np.array(co[0]), np.array(co[k]), np.array(co[k + 1]))
            l1, l2 = l1[inside], l2[inside]
            pos.append(l1[:, None] * a + l2[:, None] * b + (1 - l1 - l2)[:, None] * c)
            nrm.append(np.broadcast_to(n, (len(l1), 3)))
            rows.append((gy[inside] - 0.5).astype(int))
            cols.append((gx[inside] - 0.5).astype(int))
    return np.concatenate(rows), np.concatenate(cols), np.concatenate(pos), np.concatenate(nrm)


def face_forward(mesh, height, keep_head=False):
    """Mirrors a model front to back whose shape and side paint face +Y while its front and back paint face -Y
    (FACE_FORWARD). Afterwards what is seen from the front and from behind takes again the paint that was seen there
    from that side, as the concept sheet's front and back views show it; the sides keep their mirrored paint, so the profile now looks forward too. keep_head: the head keeps its mirrored
    paint (its paint was turned round with it). Returns the texels repainted."""
    import numpy as np
    me = mesh.data
    # About the torso's own axis: hair, a flame or a sabre stretch the bounding box to one side.
    torso = [v.co.y for v in me.vertices if 0.45 * height < v.co.z < 0.8 * height and abs(v.co.x) < 0.12 * height]
    c = sum(torso) / len(torso)
    me.transform(Matrix.Translation(Vector((0.0, c, 0.0))) @ Matrix.Scale(-1.0, 4, Vector((0.0, 1.0, 0.0)))
                 @ Matrix.Translation(Vector((0.0, -c, 0.0))))
    me.flip_normals()
    me.update()
    bsdf = next(n for n in me.materials[0].node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    img = bsdf.inputs['Base Color'].links[0].from_node.image
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    rows, cols, pos, nrm = _uv_texels(me, w, h)
    own = px[rows, cols, :3].copy()
    # Where each texel was before the mirror, and which way its face looked.
    was_y, was_ny = 2.0 * c - pos[:, 1], -nrm[:, 1]
    changed = np.zeros(len(rows), dtype=bool)
    new = own.copy()
    for cell in (height / 500.0, height / 125.0):
        ix = np.floor(pos[:, 0] / cell).astype(np.int64) + 100000
        iz = np.floor(pos[:, 2] / cell).astype(np.int64) + 100000
        key = ix * 1000000 + iz
        for side in (-1.0, 1.0):
            # The paint seen from this side before: per cell, the face nearest the viewer.
            src = was_ny * side > 0.2
            order = np.lexsort((was_y[src] * side * -1.0, key[src]))
            k_sorted = key[src][order]
            keys, first = np.unique(k_sorted, return_index=True)
            colour = own[src][order][first]
            # The faces seen from this side now: per cell, those within a little of the nearest one.
            dst = nrm[:, 1] * side > 0.2
            order = np.lexsort((pos[dst, 1] * side * -1.0, key[dst]))
            t_keys, t_first = np.unique(key[dst][order], return_index=True)
            t_near = pos[dst, 1][order][t_first]
            at_t = np.minimum(np.searchsorted(t_keys, key[dst]), len(t_keys) - 1)
            seen = np.nonzero(dst)[0][np.abs(pos[dst, 1] - t_near[at_t]) < 0.015 * height]
            seen = seen[~changed[seen]]
            if keep_head:
                seen = seen[pos[seen, 2] < 0.84 * height]
            at = np.minimum(np.searchsorted(keys, key[seen]), len(keys) - 1)
            hit = keys[at] == key[seen]
            if keep_head:
                # The turned-round head's hair hung over the chest before: its dark paint stays off the body.
                hit &= colour[at].max(axis=1) > 0.15
            idx = seen[hit]
            weight = np.clip((nrm[idx, 1] * side - 0.2) / 0.3, 0.0, 1.0)[:, None]
            new[idx] = weight * colour[at[hit]] + (1.0 - weight) * own[idx]
            changed[idx] = True
    px[rows, cols, :3] = new
    # The gaps between the atlas's islands beside repainted texels follow them, as mipmaps blend them in.
    covered = np.zeros((h, w), dtype=bool)
    covered[rows, cols] = True
    reached = covered.copy()
    rgb = px[..., :3]
    for _ in range(8):
        grew = False
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            src = np.roll(np.roll(reached, dy, axis=0), dx, axis=1)
            add = src & ~reached
            if add.any():
                rgb[add] = np.roll(np.roll(rgb, dy, axis=0), dx, axis=1)[add]
                reached |= add
                grew = True
        if not grew:
            break
    img.pixels[:] = px.reshape(-1).tolist()
    img.update()
    return int(changed.sum())


def turn_reversed_feet(mesh, height):
    """Tripo now and then builds a boot backwards on a body that faces the right way (the owner, 27 Sep 2026: "their
    feet are reversed"; most Wraithsworn bands). A boot reaching clearly further behind its ankle than in front of it
    (the model faces -Y) is turned 180 degrees about the ankle's vertical axis, fully up to 0.05 of the height and
    easing back to no turn by 0.11, so the shin twists instead of tearing. Its partner goes with it when it too reaches
    further back than forward: Tripo misreads both boots of a pair, and the Wraithsworn's platform boots are nearly
    even at both ends. Pass the body without its weapon (a blade's tip or a staff's foot by a boot would count).
    Returns the sides turned (-1 right, 1 left)."""
    import math
    vs = mesh.data.vertices
    z0 = min(v.co.z for v in vs)
    boots = {}
    for s in (-1, 1):
        ankle = [v.co for v in vs if 0.07 * height < v.co.z - z0 < 0.1 * height and v.co.x * s > 0.01 * height
                 and abs(v.co.x) < 0.2 * height]
        if not ankle:
            continue
        ax = sum(p.x for p in ankle) / len(ankle)
        ay = sum(p.y for p in ankle) / len(ankle)
        sole = [v.co for v in vs if v.co.z - z0 < 0.035 * height and abs(v.co.x - ax) < 0.06 * height]
        if sole:
            boots[s] = (ax, ay, ay - min(p.y for p in sole), max(p.y for p in sole) - ay)
    clear = {s for s, (_x, _y, front, back) in boots.items() if back > front * 1.3 and back - front > 0.02 * height}
    turned = sorted(s for s, (_x, _y, front, back) in boots.items() if s in clear or (clear and back > front))
    low, high = z0 + 0.05 * height, z0 + 0.11 * height
    for s in turned:
        ax, ay = boots[s][0], boots[s][1]
        for v in vs:
            p = v.co
            if p.z >= high or p.x * s <= 0 or abs(p.x - ax) > 0.09 * height:
                continue
            t = 1.0 if p.z <= low else (high - p.z) / (high - low)
            a = math.pi * t
            dx, dy = p.x - ax, p.y - ay
            v.co.x = ax + dx * math.cos(a) - dy * math.sin(a)
            v.co.y = ay + dx * math.sin(a) + dy * math.cos(a)
    return turned


# Models whose Tripo build painted a face on the back of the head too (a view of each head from behind, 27 Sep 2026):
# paint_back_of_head runs on these only, since red ribbons and collars read like skin to any colour rule. Check a new
# model from behind and add it here if a face shows.
BACK_FACES = frozenset()

# Models Tripo built facing +Y as a whole (face, bust and boots behind, the back of the head in front; same audit):
# class_look mirrors them front to back, which keeps the sabre in the right hand.
BACKWARDS = frozenset()
# Models whose shape and side views face +Y while their front and back paint face the right way (the concept sheet's
# side view looked the other way; same audit): seen from the side, as the lane shows them, they walked backwards, feet
# and face and bust turned round. face_forward mirrors the shape and keeps the front and back paint where they were.
# True: the head's paint was turned round too, so it keeps the mirrored paint.
# All three lists are empty since 27 Sep 2026: the thirty models on them were made again with the views in Tripo's
# order (front, left, back, right). The repairs stay for a model that comes out wrong again.
FACE_FORWARD = dict()


def _uv_coverage(me, w, h):
    """Which texels of a w x h texture the mesh's UV faces cover (numpy bool array, rows from v = 0)."""
    import numpy as np
    uv = me.uv_layers.active.data
    covered = np.zeros((h, w), dtype=bool)
    for poly in me.polygons:
        pts = [(uv[i].uv.x * w, uv[i].uv.y * h) for i in poly.loop_indices]
        for k in range(1, len(pts) - 1):
            tri = np.array([pts[0], pts[k], pts[k + 1]])
            x0, y0 = np.floor(tri.min(axis=0)).astype(int)
            x1, y1 = np.ceil(tri.max(axis=0)).astype(int)
            x0, y0 = max(0, x0), max(0, y0)
            x1, y1 = min(w - 1, x1), min(h - 1, y1)
            if x1 < x0 or y1 < y0:
                continue
            gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
            (ax, ay), (bx, by), (cx, cy) = tri
            d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
            if abs(d) < 1e-9:
                continue
            l1 = ((by - cy) * (gx - cx) + (cx - bx) * (gy - cy)) / d
            l2 = ((cy - ay) * (gx - cx) + (ax - cx) * (gy - cy)) / d
            covered[y0:y1 + 1, x0:x1 + 1] |= (l1 >= -0.05) & (l2 >= -0.05) & (1 - l1 - l2 >= -0.05)
    return covered


def paint_back_of_head(mesh, height):
    """A face Tripo painted on the back of a head too (the owner, 27 Sep 2026: "they have face behind them"; most
    Wraithsworn bands, some Kestrel and costume ones, often half under strands of hair). On a head whose crown is not
    skin (a bald head is left alone), the skin on the back of the head and the eyes and lips it encloses take the
    head's hair colour (the darkest common colour at its crown) in fine falling streaks; the skin of the nape below
    (the false face's chin and throat) takes the colours around it, the collar's or the hair's, so no face shows
    through. Returns the faces painted."""
    import colorsys
    import numpy as np
    mat = mesh.data.materials[0]
    bsdf = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    if not bsdf.inputs['Base Color'].is_linked:
        return 0
    img = bsdf.inputs['Base Color'].links[0].from_node.image
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    uv = mesh.data.uv_layers.active.data
    me = mesh.data
    # The head: above 0.87 of the height, near the middle (a staff or a raised blade stands off to the side).
    head = [p for p in me.polygons if p.center.z > 0.87 * height and abs(p.center.x) < 0.12 * height]
    if not head:
        return 0
    cy = sum(p.center.y for p in head) / len(head)

    def colour(poly):
        u = sum(uv[i].uv.x for i in poly.loop_indices) / poly.loop_total
        v = sum(uv[i].uv.y for i in poly.loop_indices) / poly.loop_total
        x = min(w - 1, max(0, int(u * w))); y = min(h - 1, max(0, int(v * h)))
        return px[y, x, :3].copy()

    def skin(c, lit=0.22):
        hh, ss, vv = colorsys.rgb_to_hsv(*[float(k) for k in c])
        return (hh < 0.12 or hh > 0.95) and 0.12 < ss < 0.8 and vv > lit

    top = [p for p in head if p.center.z > 0.95 * height and abs(p.center.x) < 0.08 * height]
    if not top or sum(skin(colour(p), 0.35) for p in top) >= 0.5 * len(top):
        return 0
    in_head = {p.index for p in head}
    # Behind the middle of the head, sides included (the false face wraps round to the ears).
    area = [p for p in me.polygons if p.center.y > cy + 0.01 * height and p.normal.y > -0.1 and (
        p.index in in_head or 0.78 * height < p.center.z <= 0.87 * height and abs(p.center.x) < 0.09 * height)]
    def stroke(c):
        # The false face's own outlines and shadows (jaw, nose, lids) are deep orange, however dark or strong; red
        # ribbons are redder.
        hh, ss, vv = colorsys.rgb_to_hsv(*[float(k) for k in c])
        return 0.03 <= hh < 0.12 and ss > 0.12 and vv > 0.15

    painted = {p.index for p in area if skin(colour(p)) or p.center.z >= 0.86 * height and stroke(colour(p))}
    if not painted:
        return 0
    edge_polys = {}
    for p in me.polygons:
        for k in p.edge_keys:
            edge_polys.setdefault(k, []).append(p.index)

    def neighbours(p):
        return {q for k in p.edge_keys for q in edge_polys[k] if q != p.index}

    # Eyes, lips and brows inside the painted skin go too; hair (dark) and bands (strong colours) stay.
    for _ in range(3):
        grown = set()
        for p in area:
            if p.index in painted or p.center.z < 0.86 * height:
                continue
            hh, ss, vv = colorsys.rgb_to_hsv(*[float(k) for k in colour(p)])
            if vv < 0.2 or (ss > 0.25 and 0.12 <= hh <= 0.95):
                continue
            near = neighbours(p)
            if near and len(near & painted) >= 0.5 * len(near):
                grown.add(p.index)
        if not grown:
            break
        painted |= grown
    crown = [colour(p) for p in head if p.center.z > 0.95 * height]
    dark = [c for c in crown if max(c) < 0.35 and not skin(c, 0.35)]
    hair = np.median(np.array(dark), axis=0) if dark else np.array([0.05, 0.04, 0.04])
    hairs = {i for i in painted if me.polygons[i].center.z >= 0.86 * height}
    # The nape takes its surroundings, ring by ring from the unpainted faces (and the hair above) inward.
    known = {}
    flat = {}
    todo = painted - hairs
    for _ in range(60):
        step = {}
        for i in todo:
            cols = []
            for q in neighbours(me.polygons[i]):
                if q in hairs:
                    cols.append(hair * 0.9)
                elif q in flat:
                    cols.append(flat[q])
                elif q not in painted:
                    cols.append(known.setdefault(q, colour(me.polygons[q])))
            if cols:
                step[i] = np.mean(cols, axis=0)
        if not step:
            break
        flat.update(step)
        todo -= set(step)
    for i in todo:
        flat[i] = hair * 0.9
    # The hair is cloned from the crown's own hair (dark and dull, not its bands), a triangle of it for each painted one
    # (falling streaks when there is none).
    sources = []
    for p in head:
        c = colour(p)
        hh, ss, vv = colorsys.rgb_to_hsv(*[float(k) for k in c])
        if p.index not in painted and p.center.z > 0.9 * height and vv < 0.35 and ss < 0.5 and not skin(c, 0.35):
            t = np.array([(uv[k].uv.x * w, uv[k].uv.y * h) for k in list(p.loop_indices)[:3]])
            if abs(np.cross(t[1] - t[0], t[2] - t[0])) > 40.0:          # big enough to hold some hair
                sources.append(t)
    original = px[..., :3].copy()
    strand = 0.012 * height
    done = np.zeros((h, w), dtype=bool)
    for i in sorted(painted):
        poly = me.polygons[i]
        pts = [(uv[k].uv.x * w, uv[k].uv.y * h) for k in poly.loop_indices]
        pos = [me.vertices[me.loops[k].vertex_index].co for k in poly.loop_indices]
        for k in range(1, len(pts) - 1):
            tri = np.array([pts[0], pts[k], pts[k + 1]])
            xs = np.array([pos[0].x, pos[k].x, pos[k + 1].x])
            zs = np.array([pos[0].z, pos[k].z, pos[k + 1].z])
            x0, y0 = np.floor(tri.min(axis=0)).astype(int) - 1
            x1, y1 = np.ceil(tri.max(axis=0)).astype(int) + 1
            x0, y0 = max(0, x0), max(0, y0)
            x1, y1 = min(w - 1, x1), min(h - 1, y1)
            if x1 < x0 or y1 < y0:
                continue
            gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
            (ax_, ay_), (bx, by), (cx_, cy_) = tri
            d = (by - cy_) * (ax_ - cx_) + (cx_ - bx) * (ay_ - cy_)
            if abs(d) < 1e-9:
                continue
            l1 = ((by - cy_) * (gx - cx_) + (cx_ - bx) * (gy - cy_)) / d
            l2 = ((cy_ - ay_) * (gx - cx_) + (ax_ - cx_) * (gy - cy_)) / d
            inside = (l1 >= -0.02) & (l2 >= -0.02) & (1 - l1 - l2 >= -0.02)
            texels = px[y0:y1 + 1, x0:x1 + 1]
            if i in hairs and sources:
                src = sources[(i * 7919 + k) % len(sources)]
                # Only the inner part of the source: its edges carry the colours beside it.
                m1 = 0.1 + 0.7 * np.clip(l1, 0.0, 1.0)
                m2 = 0.1 + 0.7 * np.clip(l2, 0.0, 1.0)
                m3 = 1.0 - m1 - m2
                su = np.clip((m1 * src[0][0] + m2 * src[1][0] + m3 * src[2][0]).astype(int), 0, w - 1)
                sv = np.clip((m1 * src[0][1] + m2 * src[1][1] + m3 * src[2][1]).astype(int), 0, h - 1)
                fill = original[sv, su]
            elif i in hairs:
                # The texel's place on the head (from its corners), for strands that fall down it.
                l3 = 1 - l1 - l2
                x3 = l1 * xs[0] + l2 * xs[1] + l3 * xs[2]
                z3 = l1 * zs[0] + l2 * zs[1] + l3 * zs[2]
                wave = x3 / strand + 0.35 * np.sin(z3 / (2.5 * strand))
                shade = (0.8 + 0.2 * (0.5 + 0.5 * np.sin(2 * np.pi * wave))
                         + 0.12 * (0.5 + 0.5 * np.sin(2 * np.pi * 2.7 * wave + 1.3)))
                fill = np.clip(hair[None, None, :] * shade[..., None], 0.0, 1.0)
            else:
                fill = np.broadcast_to(np.clip(flat[i], 0.0, 1.0), texels[..., :3].shape)
            texels[..., :3] = np.where(inside[..., None], fill, texels[..., :3])
            done[y0:y1 + 1, x0:x1 + 1] |= inside
    # The atlas fills the gaps between its islands with their edge colours, which mipmaps blend in: the gaps beside the
    # painted faces (texels no face covers) take the new colours too, eight texels out.
    covered = _uv_coverage(me, w, h)
    reached = done.copy()
    rgb = px[..., :3]
    for _ in range(8):
        grew = False
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            src = np.roll(np.roll(reached, dy, axis=0), dx, axis=1)
            new = src & ~reached & ~covered
            if new.any():
                rgb[new] = np.roll(np.roll(rgb, dy, axis=0), dx, axis=1)[new]
                reached |= new
                grew = True
        if not grew:
            break
    img.pixels[:] = px.reshape(-1).tolist()
    img.update()
    return len(painted)


def class_look(glb, name, height, tris=TRIS, weapon="knives", attack=None, yaw_degrees=0.0):
    """Another playable class (A-pose sheet, weapon in hand): decimated, standing `height` tall, facing -Y, split
    into <name>_Armor and <name>_Weapon so each glows with its own item's level, rigged by rig.rig_humanoid with the
    shared actions and the class's own attack, exported to Content/Models/Classes with its texture.
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
    if name in BACKWARDS:
        mesh.data.transform(Matrix.Scale(-1.0, 4, Vector((0.0, 1.0, 0.0))))
        mesh.data.flip_normals()
    if name in FACE_FORWARD:
        face_forward(mesh, height, keep_head=FACE_FORWARD[name])
    painted = paint_back_of_head(mesh, height) if name in BACK_FACES else 0
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
    # Boots only: a sabre's tip or a staff's foot beside a boot would be measured and turned with it.
    turned = turn_reversed_feet(objs[name + "_Armor"], height)
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
                                             attack=attack, layout=layout,
                                             loose_hand="hand.L" if weapon == "sword" else None,
                                             drum=weapon == "staff", staff_axis=(a, b) if weapon == "staff" else None)
        _export_rigged(root, name)
    finally:
        OUT = looks_out
    faces = {n: len(o.data.polygons) for n, o in objs.items()}
    return dict(cls=name, tris_in=t0, faces=faces, texture=size, feet_turned=turned, head_painted=painted,
                hands=(tuple(round(c, 2) for c in hand_r), tuple(round(c, 2) for c in hand_l)))
