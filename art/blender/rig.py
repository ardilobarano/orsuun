"""Rig and animations for the Vanguard armour looks (run inside Blender through looks.armor_look).

Every armour look comes from the same sheet pose: standing, facing -Y, the glaive upright in the right hand (-X side),
a round shield on the left forearm (+X side), feet on z=0, the whole figure 2.5 m tall with the glaive. So one
skeleton layout fits all of them; only the two hands are measured per look (the right hand from the glaive pole, the
left from the shield).

  rig_vanguard(meshes, root, base, tip)  builds the armature under `root`, skins `meshes` by distance to the bones,
                                         parents WeaponBase/WeaponTip to hand.R (the glaive follows the hand) and
                                         keys the shared actions: Idle, Run, Attack, Hit, Death.

Bone rolls are set so each bone's local Z points forward (-Y): a positive rotation about local X always swings the
bone's tip forward. Unity plays the actions as legacy clips (LaneView).
"""
import math

import bmesh
import bpy
from mathutils import Euler, Quaternion, Vector

FPS = 24
FORWARD = Vector((0.0, -1.0, 0.0))

# name: (head, tail, parent, influence radius). Heads and tails are placeholders for the hands, measured per look.
LAYOUT = {
    "root":        ((0, 0, 0.0), (0, 0, 0.25), None, 0.0),
    "hips":        ((0, 0.02, 0.98), (0, 0.02, 1.22), "root", 0.26),
    "spine":       ((0, 0.02, 1.22), (0, 0.02, 1.50), "hips", 0.26),
    "chest":       ((0, 0.02, 1.50), (0, 0.02, 1.84), "spine", 0.28),
    "neck":        ((0, 0.00, 1.84), (0, 0.00, 1.98), "chest", 0.12),
    "head":        ((0, 0.00, 1.98), (0, 0.00, 2.38), "neck", 0.20),
    "shoulder.R":  ((-0.08, 0.02, 1.80), (-0.28, 0.02, 1.78), "chest", 0.12),
    "upper_arm.R": ((-0.28, 0.02, 1.78), (-0.48, -0.10, 1.55), "shoulder.R", 0.10),
    "forearm.R":   ((-0.48, -0.10, 1.55), None, "upper_arm.R", 0.09),
    "hand.R":      (None, None, "forearm.R", 0.08),
    "shoulder.L":  ((0.08, 0.02, 1.80), (0.28, 0.02, 1.78), "chest", 0.12),
    "upper_arm.L": ((0.28, 0.02, 1.78), (0.40, 0.02, 1.45), "shoulder.L", 0.10),
    "forearm.L":   ((0.40, 0.02, 1.45), None, "upper_arm.L", 0.12),
    "hand.L":      (None, None, "forearm.L", 0.10),
    "thigh.L":     ((0.12, 0.02, 1.00), (0.13, 0.00, 0.55), "hips", 0.13),
    "shin.L":      ((0.13, 0.00, 0.55), (0.14, 0.03, 0.10), "thigh.L", 0.11),
    "foot.L":      ((0.14, 0.03, 0.10), (0.14, -0.14, 0.02), "shin.L", 0.10),
    "thigh.R":     ((-0.12, 0.02, 1.00), (-0.13, 0.00, 0.55), "hips", 0.13),
    "shin.R":      ((-0.13, 0.00, 0.55), (-0.14, 0.03, 0.10), "thigh.R", 0.11),
    "foot.R":      ((-0.14, 0.03, 0.10), (-0.14, -0.14, 0.02), "shin.R", 0.10),
}


def _pole_at(base, tip, z):
    t = (z - base.z) / (tip.z - base.z)
    return base + (tip - base) * t


def measure_hands(verts, base, tip):
    """Right hand: where the armour hugs the glaive pole (densest band between 1.0 and 1.8 m). Left hand: behind the
    shield, the outermost surface on the +X side at forearm height."""
    bins = {}
    for v in verts:
        if not 1.0 < v.z < 1.8:
            continue
        p = _pole_at(base, tip, v.z)
        if ((v.x - p.x) ** 2 + (v.y - p.y) ** 2) ** 0.5 < 0.14:
            k = int(v.z / 0.05)
            bins[k] = bins.get(k, 0) + 1
    z = (max(bins, key=bins.get) + 0.5) * 0.05 if bins else 1.4
    right = _pole_at(base, tip, z)
    right = Vector((right.x + 0.03, right.y, z))          # the fist sits on the pole, a little toward the body

    shield = [v for v in verts if 0.8 < v.z < 1.45 and v.x > 0.22]
    if shield:
        c = sum(shield, Vector()) / len(shield)
        left = Vector((c.x - 0.07, c.y, c.z))
    else:
        left = Vector((0.33, -0.10, 1.15))
    return right, left


def build_armature(root, right_hand, left_hand, pole_dir):
    layout = {k: list(v) for k, v in LAYOUT.items()}
    layout["forearm.R"][1] = tuple(right_hand)
    layout["hand.R"][0] = tuple(right_hand)
    layout["hand.R"][1] = tuple(right_hand + pole_dir.normalized() * 0.16)   # along the pole, toward the blade
    layout["forearm.L"][1] = tuple(left_hand)
    layout["hand.L"][0] = tuple(left_hand)
    layout["hand.L"][1] = tuple(left_hand + Vector((0.0, -0.10, -0.02)))
    return build_from_layout(root, layout, "VanguardRig")


def build_from_layout(root, layout, rig_name):
    """layout: bone -> [head, tail, parent, radius]. Creates the armature under root with forward-facing rolls."""
    arm_data = bpy.data.armatures.new(rig_name)
    arm = bpy.data.objects.new("Armature", arm_data)
    bpy.context.scene.collection.objects.link(arm)
    arm.parent = root

    view = bpy.context.view_layer
    view.objects.active = arm
    with bpy.context.temp_override(active_object=arm, object=arm, selected_objects=[arm]):
        bpy.ops.object.mode_set(mode='EDIT')
        eb = arm_data.edit_bones
        for name, (head, tail, parent, _r) in layout.items():
            b = eb.new(name)
            b.head = Vector(head)
            b.tail = Vector(tail)
            b.align_roll(FORWARD)
            b.use_deform = name != "root"
        for name, (_h, _t, parent, _r) in layout.items():
            if parent:
                eb[name].parent = eb[parent]
                eb[name].use_connect = False
        bpy.ops.object.mode_set(mode='OBJECT')
    return arm, {k: v[3] for k, v in layout.items()}


def _segment_distance(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
    return (p - (a + ab * t)).length


def skin(mesh_obj, arm, radii, only=None):
    """Distance weights: each vertex takes its three nearest bones (distance over the bone's radius), sharpened and
    normalised, then smoothed twice over mesh edges so seams between bones do not tear. `only` pins every vertex
    to one bone (the banner cloth rides the chest), or is a function of the vertex position naming its bone (twin
    blades, each in its own hand)."""
    bones = [(b.name, b.head_local.copy(), b.tail_local.copy(), radii[b.name]) for b in arm.data.bones if b.use_deform]
    me = mesh_obj.data
    n = len(me.vertices)
    weights = [dict() for _ in range(n)]
    for i, v in enumerate(me.vertices):
        if only:
            weights[i] = {only(v.co) if callable(only) else only: 1.0}
            continue
        p = v.co
        scored = []
        for name, a, b, r in bones:
            d = _segment_distance(p, a, b) / r
            scored.append((d, name))
        scored.sort()
        top = scored[:3]
        raw = {name: 1.0 / max(d, 0.08) ** 6 for d, name in top}
        s = sum(raw.values())
        weights[i] = {k: w / s for k, w in raw.items()}

    if not only:
        bm = bmesh.new()
        bm.from_mesh(me)
        bm.verts.ensure_lookup_table()
        neighbours = [[e.other_vert(v).index for e in v.link_edges] for v in bm.verts]
        bm.free()
        for _ in range(2):
            smoothed = []
            for i in range(n):
                acc = dict(weights[i])
                for k in acc:
                    acc[k] *= 2.0
                for j in neighbours[i]:
                    for k, w in weights[j].items():
                        acc[k] = acc.get(k, 0.0) + w
                top = sorted(acc.items(), key=lambda kv: -kv[1])[:4]
                s = sum(w for _, w in top)
                smoothed.append({k: w / s for k, w in top})
            weights = smoothed

    mesh_obj.vertex_groups.clear()
    groups = {name: mesh_obj.vertex_groups.new(name=name) for name, _a, _b, _r in bones}
    for i, ws in enumerate(weights):
        for k, w in ws.items():
            if w > 0.01:
                groups[k].add([i], w, 'REPLACE')
    mesh_obj.parent = arm
    mod = mesh_obj.modifiers.new("Armature", 'ARMATURE')
    mod.object = arm


def free_cloth_from_arms(mesh_obj, arm, height, sides=("L", "R"), keep=0.035, fade=0.03):
    """Capes and skirts hang beside an A-pose forearm, so distance weights gave them to the arm and they stretched into
    sheets whenever it swung (the Drumcaller's drum beat, 27 Sep 2026). Below the elbow, whatever lies farther than
    `keep` (a share of the height) from the forearm and fingers hands its arm weights over to the body bones around it,
    all of them past keep + fade, so the cloth bends over that band instead of tearing. Returns the vertices changed."""
    me = mesh_obj.data
    bones = arm.data.bones
    groups = {g.index: g.name for g in mesh_obj.vertex_groups}
    changed = 0
    for side in sides:
        names = {"upper_arm." + side, "forearm." + side, "hand." + side}
        elbow = bones["forearm." + side].head_local
        hand = bones["hand." + side].head_local
        fingers = hand + Vector((0.0, 0.0, -0.045 * height))
        for v in me.vertices:
            if v.co.z > elbow.z:
                continue
            ws = {groups[g.group]: g.weight for g in v.groups}
            arm_w = sum(w for k, w in ws.items() if k in names)
            if arm_w <= 0.0:
                continue
            d = min(_segment_distance(v.co, elbow, hand), _segment_distance(v.co, hand, fingers)) / height
            share = max(0.0, min(1.0, (keep + fade - d) / fade))
            if share >= 1.0:
                continue
            rest = {k: w for k, w in ws.items() if k not in names and w > 0.0}
            if not rest:
                body = [(_segment_distance(v.co, b.head_local, b.tail_local), b.name) for b in bones
                        if b.use_deform and not b.name.startswith(("upper_arm", "forearm", "hand", "shoulder"))]
                rest = {min(body)[1]: 1.0}
            s = sum(rest.values())
            new = {k: w * share for k, w in ws.items() if k in names}
            for k, w in rest.items():
                new[k] = ws.get(k, 0.0) + arm_w * (1.0 - share) * w / s
            for k in names:
                if k in ws:
                    g = mesh_obj.vertex_groups[k]
                    if new.get(k, 0.0) > 0.001:
                        g.add([v.index], new[k], 'REPLACE')
                    else:
                        g.remove([v.index])
            for k in rest:
                mesh_obj.vertex_groups[k].add([v.index], new[k], 'REPLACE')
            changed += 1
    print("cloth: %d vertices eased off the arms" % changed)
    return changed


LEG_BONES = ("thigh.L", "shin.L", "foot.L", "thigh.R", "shin.R", "foot.R")


def free_robe_from_legs(mesh_obj, arm, height, keep=0.04, fade=0.06):
    """A robe hangs around the legs, so distance weights gave its hem to the thighs and shins and it tore into a flared
    sheet whenever a leg swung (the Mirage Queen, the Merchant-Prince and the Lantern Widow, 27 Sep 2026). Below the
    hips, whatever lies farther than `keep` (a share of the height) from every leg bone hands its leg weights to the
    hips, all of them past keep + fade, so the robe sways with the body and bends over that band. Returns the vertices
    changed."""
    me = mesh_obj.data
    bones = arm.data.bones
    groups = {g.index: g.name for g in mesh_obj.vertex_groups}
    hips_z = bones["thigh.L"].head_local.z
    segments = [(bones[n].head_local, bones[n].tail_local) for n in LEG_BONES]
    hips = mesh_obj.vertex_groups["hips"]
    changed = 0
    for v in me.vertices:
        if v.co.z > hips_z:
            continue
        ws = {groups[g.group]: g.weight for g in v.groups}
        leg_w = sum(w for k, w in ws.items() if k in LEG_BONES)
        if leg_w <= 0.0:
            continue
        d = min(_segment_distance(v.co, a, b) for a, b in segments) / height
        share = max(0.0, min(1.0, (keep + fade - d) / fade))
        if share >= 1.0:
            continue
        for k in LEG_BONES:
            if k in ws:
                g = mesh_obj.vertex_groups[k]
                if ws[k] * share > 0.001:
                    g.add([v.index], ws[k] * share, 'REPLACE')
                else:
                    g.remove([v.index])
        hips.add([v.index], ws.get("hips", 0.0) + leg_w * (1.0 - share), 'REPLACE')
        changed += 1
    print("robe: %d vertices eased off the legs" % changed)
    return changed


def hold_loose(mesh_obj, arm, height, hand):
    """A Wraithsworn's palm flame (26 Sep 2026): Tripo sometimes sets it floating off the hand (behind the body, or out
    past the fingers), where it was weighted to the chest and hung in the air when the arm swung. A sizeable piece
    floating free of the body at hand height is moved into the off hand (`hand`, e.g. "hand.L") and rides it whole."""
    from mathutils.kdtree import KDTree
    me = mesh_obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    island = [-1] * len(bm.verts)
    islands = []
    for start in bm.verts:
        if island[start.index] >= 0:
            continue
        members, stack = [], [start]
        island[start.index] = len(islands)
        while stack:
            v = stack.pop()
            members.append(v.index)
            for e in v.link_edges:
                o = e.other_vert(v)
                if island[o.index] < 0:
                    island[o.index] = len(islands)
                    stack.append(o)
        islands.append(members)
    bm.free()
    body = max(islands, key=len)
    tree = KDTree(len(body))
    for i in body:
        tree.insert(me.vertices[i].co, i)
    tree.balance()
    bone = arm.data.bones[hand]
    grip = (bone.head_local + bone.tail_local) / 2
    side = 1.0 if grip.x > 0 else -1.0
    group = mesh_obj.vertex_groups.get(hand)
    total = len(me.vertices)
    floating = []
    for members in islands:
        if members is body or len(members) > 0.08 * total:
            continue
        if min(tree.find(me.vertices[i].co)[2] for i in members) < 0.02 * height:
            continue
        centre = sum((me.vertices[i].co for i in members), Vector()) / len(members)
        if 0.35 * height < centre.z < 0.85 * height:
            floating.append((members, centre))
    # The flame itself, moved beside the palm, a little in front of the hand and out from the hip; its loose tips (and
    # any other shred at hand height out past the hands) go with it.
    flames = [(m, c, grip + Vector((0.05 * height * side, -0.04 * height, 0.0)) - c)
              for m, c in floating if len(m) >= 0.01 * total]
    reach = max(abs(grip.x), abs(arm.data.bones["hand.R" if hand == "hand.L" else "hand.L"].head_local.x))
    moved = 0
    for members, centre in floating:
        shift = next((f[2] for f in flames if f[0] is members), None)
        if shift is None:
            near = [f for f in flames if (f[1] - centre).length < 0.25 * height]
            if near:
                shift = min(near, key=lambda f: (f[1] - centre).length)[2]
            elif abs(centre.x) > reach:
                shift = grip + Vector((0.05 * height * side, -0.04 * height, 0.0)) - centre
            else:
                continue
        for i in members:
            me.vertices[i].co += shift
            for g in list(me.vertices[i].groups):
                mesh_obj.vertex_groups[g.group].remove([i])
            group.add([i], 1.0, 'REPLACE')
        moved += len(members)
    print("loose pieces: %d vertices moved into %s" % (moved, hand))


def pin_drum(mesh_obj, arm, height):
    """The Drumcaller's frame drum (the owner, 27 Sep 2026: "drum is fked"): Tripo fuses it into the body, and weighted by
    distance it stretched between the forearm, the hip and the cape whenever the arm swung. The disc beside the left
    forearm (facing forward, below the elbow, out past the hip) now rides forearm.L whole. Returns the vertices pinned."""
    me = mesh_obj.data
    fore = arm.data.bones["forearm.L"]
    hand = fore.tail_local
    near = [v for v in me.vertices if v.co.x > 0.14 * height and 0.28 * height < v.co.z < 0.62 * height
            and abs(v.co.y - hand.y) < 0.09 * height]
    if len(near) < 30:
        return 0
    cx = sum(v.co.x for v in near) / len(near)
    cy = sum(v.co.y for v in near) / len(near)
    cz = sum(v.co.z for v in near) / len(near)
    group = mesh_obj.vertex_groups.get("forearm.L")
    pinned = 0
    for v in me.vertices:
        p = v.co
        if p.x < 0.12 * height or abs(p.y - cy) > 0.07 * height:
            continue
        if (p.x - cx) ** 2 + (p.z - cz) ** 2 > (0.11 * height) ** 2:
            continue
        for g in list(v.groups):
            mesh_obj.vertex_groups[g.group].remove([v.index])
        group.add([v.index], 1.0, 'REPLACE')
        pinned += 1
    print("drum: %d vertices pinned to forearm.L" % pinned)
    return pinned


def pin_staff_charms(mesh_obj, arm, height, a, b):
    """Charms and crystals of a staff's head that Tripo left out of the staff line (loose pieces next to it) ride the
    staff hand, as the staff does, instead of the chest they were weighted to."""
    import looks
    me = mesh_obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    island = [-1] * len(bm.verts)
    islands = []
    for start in bm.verts:
        if island[start.index] >= 0:
            continue
        members, stack = [], [start]
        island[start.index] = len(islands)
        while stack:
            v = stack.pop()
            members.append(v.index)
            for e in v.link_edges:
                o = e.other_vert(v)
                if island[o.index] < 0:
                    island[o.index] = len(islands)
                    stack.append(o)
        islands.append(members)
    bm.free()
    body = max(islands, key=len)
    group = mesh_obj.vertex_groups.get("hand.R")
    pinned = 0
    for members in islands:
        if members is body or len(members) > 0.05 * len(me.vertices):
            continue
        centre = sum((me.vertices[i].co for i in members), Vector()) / len(members)
        if centre.z < 0.6 * height or looks._distance_to_axis(centre, a, b) > 0.07 * height:
            continue
        for i in members:
            for g in list(me.vertices[i].groups):
                mesh_obj.vertex_groups[g.group].remove([i])
            group.add([i], 1.0, 'REPLACE')
        pinned += len(members)
    print("staff charms: %d vertices pinned to hand.R" % pinned)
    return pinned


def attach_to_bone(obj, arm, bone):
    """Keeps obj where it is and makes it follow the bone."""
    world = obj.matrix_world.copy()
    obj.parent = arm
    obj.parent_type = 'BONE'
    obj.parent_bone = bone
    bpy.context.view_layer.update()
    obj.matrix_world = world


# ---- actions: (frame, {bone: (x, y, z) degrees about the bone's local axes}), hips location in local space ----

def _rot(deg):
    return Euler(tuple(math.radians(a) for a in deg), 'XYZ').to_quaternion()


ACTIONS = {
    # Breathing and a slow settle of the weight. Loops.
    "Idle": (48, [
        (0,  {"chest": (0, 0, 0), "neck": (0, 0, 0), "upper_arm.L": (0, 0, 0), "spine": (0, 0, 0)}, (0, 0, 0)),
        (24, {"chest": (-2.5, 0, 0), "neck": (2, 0, 0), "upper_arm.L": (2, 0, 0), "spine": (1, 0, 0)}, (0, -0.012, 0)),
        (48, {"chest": (0, 0, 0), "neck": (0, 0, 0), "upper_arm.L": (0, 0, 0), "spine": (0, 0, 0)}, (0, 0, 0)),
    ]),
    # Jog along the lane, glaive held steady. Loops.
    "Run": (16, [
        (0,  {"thigh.L": (28, 0, 0), "shin.L": (-10, 0, 0), "thigh.R": (-22, 0, 0), "shin.R": (-45, 0, 0),
              "upper_arm.L": (-14, 0, 0), "upper_arm.R": (6, 0, 0), "spine": (6, 0, 0), "chest": (0, -5, 0)}, (0, 0, 0)),
        (4,  {"thigh.L": (5, 0, 0), "shin.L": (-5, 0, 0), "thigh.R": (5, 0, 0), "shin.R": (-60, 0, 0),
              "upper_arm.L": (0, 0, 0), "upper_arm.R": (0, 0, 0), "spine": (7, 0, 0), "chest": (0, 0, 0)}, (0, -0.04, 0)),
        (8,  {"thigh.L": (-22, 0, 0), "shin.L": (-45, 0, 0), "thigh.R": (28, 0, 0), "shin.R": (-10, 0, 0),
              "upper_arm.L": (14, 0, 0), "upper_arm.R": (-6, 0, 0), "spine": (6, 0, 0), "chest": (0, 5, 0)}, (0, 0, 0)),
        (12, {"thigh.L": (5, 0, 0), "shin.L": (-60, 0, 0), "thigh.R": (5, 0, 0), "shin.R": (-5, 0, 0),
              "upper_arm.L": (0, 0, 0), "upper_arm.R": (0, 0, 0), "spine": (7, 0, 0), "chest": (0, 0, 0)}, (0, -0.04, 0)),
        (16, {"thigh.L": (28, 0, 0), "shin.L": (-10, 0, 0), "thigh.R": (-22, 0, 0), "shin.R": (-45, 0, 0),
              "upper_arm.L": (-14, 0, 0), "upper_arm.R": (6, 0, 0), "spine": (6, 0, 0), "chest": (0, -5, 0)}, (0, 0, 0)),
    ]),
    # Glaive chop: wind the blade back, bring it over and down in front, recover.
    "Attack": (14, [
        (0,  {"upper_arm.R": (0, 0, 0), "forearm.R": (0, 0, 0), "hand.R": (0, 0, 0), "chest": (0, 0, 0), "spine": (0, 0, 0)}, (0, 0, 0)),
        (4,  {"upper_arm.R": (-22, 0, 0), "forearm.R": (-12, 0, 0), "hand.R": (-40, 0, 0), "chest": (-7, 12, 0), "spine": (-3, 0, 0)}, (0, 0, 0)),
        (8,  {"upper_arm.R": (50, 0, 0), "forearm.R": (18, 0, 0), "hand.R": (88, 0, 0), "chest": (14, -14, 0), "spine": (7, 0, 0)}, (0, -0.06, 0)),
        (14, {"upper_arm.R": (0, 0, 0), "forearm.R": (0, 0, 0), "hand.R": (0, 0, 0), "chest": (0, 0, 0), "spine": (0, 0, 0)}, (0, 0, 0)),
    ]),
    # Rocked back by a blow.
    "Hit": (9, [
        (0, {"chest": (0, 0, 0), "neck": (0, 0, 0), "spine": (0, 0, 0)}, (0, 0, 0)),
        (3, {"chest": (-12, 0, 0), "neck": (-10, 0, 0), "spine": (-5, 0, 0)}, (0, 0, 0)),
        (9, {"chest": (0, 0, 0), "neck": (0, 0, 0), "spine": (0, 0, 0)}, (0, 0, 0)),
    ]),
    # Knees give, then he falls on his back. Holds the last frame.
    "Death": (24, [
        (0,  {"hips": (0, 0, 0), "thigh.L": (0, 0, 0), "thigh.R": (0, 0, 0), "shin.L": (0, 0, 0), "shin.R": (0, 0, 0),
              "chest": (0, 0, 0), "neck": (0, 0, 0)}, (0, 0, 0)),
        (8,  {"hips": (-10, 0, 0), "thigh.L": (40, 0, 0), "thigh.R": (35, 0, 0), "shin.L": (-70, 0, 0), "shin.R": (-65, 0, 0),
              "chest": (8, 0, 0), "neck": (10, 0, 0)}, (0, -0.35, 0)),
        (18, {"hips": (-84, 0, 0), "thigh.L": (18, 0, 0), "thigh.R": (8, 0, 0), "shin.L": (-28, 0, 0), "shin.R": (-12, 0, 0),
              "chest": (-4, 0, 0), "neck": (-10, 0, 0)}, (0, -0.72, 0.0)),
        (24, {"hips": (-88, 0, 0), "thigh.L": (14, 0, 0), "thigh.R": (4, 0, 0), "shin.L": (-24, 0, 0), "shin.R": (-8, 0, 0),
              "chest": (-6, 0, 0), "neck": (-12, 0, 0)}, (0, -0.74, 0.0)),
    ]),
}


# Class attack styles replace the glaive chop (bone names are shared, so any humanoid rig takes them).
ATTACKS = {
    # Kestrel: two quick slashes, right then left, low and fast.
    "knives": (12, [
        (0, {"upper_arm.R": (0, 0, 0), "forearm.R": (0, 0, 0), "upper_arm.L": (0, 0, 0), "forearm.L": (0, 0, 0), "chest": (0, 0, 0), "spine": (0, 0, 0)}, (0, 0, 0)),
        (3, {"upper_arm.R": (-25, 0, 0), "forearm.R": (-10, 0, 0), "upper_arm.L": (-20, 0, 0), "forearm.L": (-10, 0, 0), "chest": (-4, 10, 0), "spine": (-2, 0, 0)}, (0, 0, 0)),
        (6, {"upper_arm.R": (60, 0, 0), "forearm.R": (35, 0, 0), "upper_arm.L": (-10, 0, 0), "forearm.L": (0, 0, 0), "chest": (10, -14, 0), "spine": (5, 0, 0)}, (0, -0.04, 0)),
        (9, {"upper_arm.R": (20, 0, 0), "forearm.R": (10, 0, 0), "upper_arm.L": (62, 0, 0), "forearm.L": (35, 0, 0), "chest": (10, 12, 0), "spine": (5, 0, 0)}, (0, -0.04, 0)),
        (12, {"upper_arm.R": (0, 0, 0), "forearm.R": (0, 0, 0), "upper_arm.L": (0, 0, 0), "forearm.L": (0, 0, 0), "chest": (0, 0, 0), "spine": (0, 0, 0)}, (0, 0, 0)),
    ]),
    # Wraithsworn: the sabre goes up over the shoulder, then a full-body diagonal cut; the void hand thrusts.
    "sword": (16, [
        (0, {"upper_arm.R": (0, 0, 0), "forearm.R": (0, 0, 0), "hand.R": (0, 0, 0), "upper_arm.L": (0, 0, 0), "forearm.L": (0, 0, 0), "chest": (0, 0, 0), "spine": (0, 0, 0)}, (0, 0, 0)),
        (5, {"upper_arm.R": (150, 0, 0), "forearm.R": (40, 0, 0), "hand.R": (30, 0, 0), "upper_arm.L": (20, 0, 0), "forearm.L": (10, 0, 0), "chest": (-8, 18, 0), "spine": (-6, 0, 0)}, (0, 0.02, 0)),
        (9, {"upper_arm.R": (25, 0, 0), "forearm.R": (5, 0, 0), "hand.R": (-15, 0, 0), "upper_arm.L": (48, 0, 0), "forearm.L": (22, 0, 0), "chest": (14, -24, 0), "spine": (11, 0, 0)}, (0, -0.08, 0)),
        (11, {"upper_arm.R": (18, 0, 0), "forearm.R": (4, 0, 0), "hand.R": (-18, 0, 0), "upper_arm.L": (40, 0, 0), "forearm.L": (18, 0, 0), "chest": (12, -22, 0), "spine": (9, 0, 0)}, (0, -0.07, 0)),
        (16, {"upper_arm.R": (0, 0, 0), "forearm.R": (0, 0, 0), "hand.R": (0, 0, 0), "upper_arm.L": (0, 0, 0), "forearm.L": (0, 0, 0), "chest": (0, 0, 0), "spine": (0, 0, 0)}, (0, 0, 0)),
    ]),
    # Drumcaller: the staff rises and slams down while the drum arm lifts the drum to meet it (a bigger swing dragged
    # the cape along, 27 Sep 2026).
    "staff": (15, [
        (0, {"upper_arm.R": (0, 0, 0), "forearm.R": (0, 0, 0), "upper_arm.L": (0, 0, 0), "forearm.L": (0, 0, 0), "chest": (0, 0, 0), "spine": (0, 0, 0)}, (0, 0, 0)),
        (5, {"upper_arm.R": (110, 0, 0), "forearm.R": (20, 0, 0), "upper_arm.L": (10, 0, 0), "forearm.L": (6, 0, 0), "chest": (-10, 6, 0), "spine": (-4, 0, 0)}, (0, 0.03, 0)),
        (9, {"upper_arm.R": (35, 0, 0), "forearm.R": (0, 0, 0), "upper_arm.L": (18, 0, 0), "forearm.L": (28, 0, 0), "chest": (10, -6, 0), "spine": (6, 0, 0)}, (0, -0.05, 0)),
        (15, {"upper_arm.R": (0, 0, 0), "forearm.R": (0, 0, 0), "upper_arm.L": (0, 0, 0), "forearm.L": (0, 0, 0), "chest": (0, 0, 0), "spine": (0, 0, 0)}, (0, 0, 0)),
    ]),
    # The enemies' staff blow (FlameCultist, LanternWisp, LastCarver, SnowHag): "staff" as it was before the drum arm was
    # calmed for the Drumcaller's cape (27 Sep 2026), their free arm swinging wider.
    "staff_wide": (15, [
        (0, {"upper_arm.R": (0, 0, 0), "forearm.R": (0, 0, 0), "upper_arm.L": (0, 0, 0), "forearm.L": (0, 0, 0), "chest": (0, 0, 0), "spine": (0, 0, 0)}, (0, 0, 0)),
        (5, {"upper_arm.R": (110, 0, 0), "forearm.R": (20, 0, 0), "upper_arm.L": (25, 0, 0), "forearm.L": (10, 0, 0), "chest": (-10, 6, 0), "spine": (-4, 0, 0)}, (0, 0.03, 0)),
        (9, {"upper_arm.R": (35, 0, 0), "forearm.R": (0, 0, 0), "upper_arm.L": (50, 0, 0), "forearm.L": (40, 0, 0), "chest": (10, -6, 0), "spine": (6, 0, 0)}, (0, -0.05, 0)),
        (15, {"upper_arm.R": (0, 0, 0), "forearm.R": (0, 0, 0), "upper_arm.L": (0, 0, 0), "forearm.L": (0, 0, 0), "chest": (0, 0, 0), "spine": (0, 0, 0)}, (0, 0, 0)),
    ]),
    # Robed casters (the Mirage Queen, the Merchant-Prince, the Lantern Widow, 27 Sep 2026): the staff hand stays low, so
    # a staff standing on the ground does not bend, and the free hand draws back and thrusts the spell forward.
    "cast": (15, [
        (0, {"upper_arm.R": (0, 0, 0), "forearm.R": (0, 0, 0), "upper_arm.L": (0, 0, 0), "forearm.L": (0, 0, 0), "chest": (0, 0, 0), "spine": (0, 0, 0), "neck": (0, 0, 0)}, (0, 0, 0)),
        (5, {"upper_arm.R": (4, 0, 0), "forearm.R": (3, 0, 0), "upper_arm.L": (-12, 0, 0), "forearm.L": (30, 0, 0), "chest": (-6, 8, 0), "spine": (-3, 0, 0), "neck": (-4, 0, 0)}, (0, 0.02, 0)),
        (9, {"upper_arm.R": (6, 0, 0), "forearm.R": (4, 0, 0), "upper_arm.L": (58, 0, 0), "forearm.L": (12, 0, 0), "chest": (9, -10, 0), "spine": (5, 0, 0), "neck": (6, 0, 0)}, (0, -0.04, 0)),
        (15, {"upper_arm.R": (0, 0, 0), "forearm.R": (0, 0, 0), "upper_arm.L": (0, 0, 0), "forearm.L": (0, 0, 0), "chest": (0, 0, 0), "spine": (0, 0, 0), "neck": (0, 0, 0)}, (0, 0, 0)),
    ]),
}


# A robed figure glides: its run takes this share of the legs' swing, so the hem is not kicked out.
ROBE_STRIDE = 0.35


def key_actions(arm, attack=None, robe=False):
    """Keys every action on the armature (fake users keep them); the FBX exporter writes one take per action.
    attack: a style from ATTACKS that replaces the Vanguard's glaive chop; robe: the run's stride is shortened."""
    actions = dict(ACTIONS)
    if attack:
        actions["Attack"] = ATTACKS[attack]
    if robe:
        length, keys = actions["Run"]
        actions["Run"] = (length, [(f, {b: tuple(a * ROBE_STRIDE for a in deg) if b in LEG_BONES else deg for b, deg in rots.items()}, loc)
                                   for f, rots, loc in keys])
    arm.animation_data_create()
    for pb in arm.pose.bones:
        pb.rotation_mode = 'QUATERNION'
    made = []
    for name, (length, keys) in actions.items():
        action = bpy.data.actions.new(name)
        action.use_fake_user = True
        arm.animation_data.action = action
        touched = set()
        for _f, rots, _loc in keys:
            touched.update(rots)
        for frame, rots, loc in keys:
            for pb in arm.pose.bones:
                pb.rotation_quaternion = Quaternion()
                pb.location = Vector()
            for bone, deg in rots.items():
                arm.pose.bones[bone].rotation_quaternion = _rot(deg)
            arm.pose.bones["hips"].location = Vector(loc)
            for bone in touched:
                arm.pose.bones[bone].keyframe_insert("rotation_quaternion", frame=frame + 1)
            arm.pose.bones["hips"].keyframe_insert("location", frame=frame + 1)
        action.use_frame_range = True
        action.frame_range = (1, length + 1)
        made.append(name)
    arm.animation_data.action = None
    for pb in arm.pose.bones:
        pb.rotation_quaternion = Quaternion()
        pb.location = Vector()
    return made


def rig_vanguard(meshes, root, base, tip, body=None):
    """meshes: the armour mesh objects (already in their final place under root); body: the banner cloth (rides the
    chest); base, tip: the glaive pole's ends. Returns the armature."""
    verts = [v.co.copy() for m in meshes for v in m.data.vertices]
    right, left = measure_hands(verts, base, tip)
    arm, radii = build_armature(root, right, left, tip - base)
    for m in meshes:
        skin(m, arm, radii)
    if body is not None:
        skin(body, arm, radii, only="chest")
    for name in ("WeaponBase", "WeaponTip"):
        e = next(c for c in root.children if c.name.split(".")[0] == name)
        attach_to_bone(e, arm, "hand.R")
    key_actions(arm)
    return arm, right, left


# ---- other classes: a humanoid standing in an A-pose with a weapon in each hand (Kestrel's knives) ----

def humanoid_layout(verts, height):
    """Bone layout for an A-pose figure `height` tall, feet on z=0, facing -Y. Proportions are fractions of the
    height; the hands are measured (the outermost surface at hand height on each side)."""
    H = height
    def f(x, y, z):
        return (x * H, y * H, z * H)
    band = [v for v in verts if 0.42 * H < v.z < 0.53 * H]
    right = [v for v in band if v.x < -0.16 * H]
    left = [v for v in band if v.x > 0.16 * H]
    def centre(vs, default):
        return sum(vs, Vector()) / len(vs) if vs else Vector(default)
    hand_r = centre(right, f(-0.20, 0, 0.48))
    hand_l = centre(left, f(0.20, 0, 0.48))
    elbow_r = Vector(f(-0.18, 0.01, 0.605))
    elbow_l = Vector(f(0.18, 0.01, 0.605))
    down = Vector((0.0, 0.0, -0.11 * H))                     # hand bones run down the knife
    return {
        "root":        [f(0, 0, 0), f(0, 0, 0.1), None, 0.0],
        "hips":        [f(0, 0.01, 0.51), f(0, 0.01, 0.59), "root", 0.11 * H],
        "spine":       [f(0, 0.01, 0.59), f(0, 0.01, 0.67), "hips", 0.11 * H],
        "chest":       [f(0, 0.01, 0.67), f(0, 0.01, 0.78), "spine", 0.12 * H],
        "neck":        [f(0, 0, 0.78), f(0, 0, 0.845), "chest", 0.05 * H],
        "head":        [f(0, 0, 0.845), f(0, 0, 1.0), "neck", 0.09 * H],
        "shoulder.R":  [f(-0.03, 0.01, 0.755), f(-0.09, 0.01, 0.745), "chest", 0.05 * H],
        "upper_arm.R": [f(-0.09, 0.01, 0.745), tuple(elbow_r), "shoulder.R", 0.045 * H],
        "forearm.R":   [tuple(elbow_r), tuple(hand_r), "upper_arm.R", 0.04 * H],
        "hand.R":      [tuple(hand_r), tuple(hand_r + down), "forearm.R", 0.05 * H],
        "shoulder.L":  [f(0.03, 0.01, 0.755), f(0.09, 0.01, 0.745), "chest", 0.05 * H],
        "upper_arm.L": [f(0.09, 0.01, 0.745), tuple(elbow_l), "shoulder.L", 0.045 * H],
        "forearm.L":   [tuple(elbow_l), tuple(hand_l), "upper_arm.L", 0.04 * H],
        "hand.L":      [tuple(hand_l), tuple(hand_l + down), "forearm.L", 0.05 * H],
        "thigh.L":     [f(0.05, 0.01, 0.50), f(0.06, 0, 0.28), "hips", 0.06 * H],
        "shin.L":      [f(0.06, 0, 0.28), f(0.065, 0.01, 0.045), "thigh.L", 0.05 * H],
        "foot.L":      [f(0.065, 0.01, 0.045), f(0.065, -0.065, 0.01), "shin.L", 0.045 * H],
        "thigh.R":     [f(-0.05, 0.01, 0.50), f(-0.06, 0, 0.28), "hips", 0.06 * H],
        "shin.R":      [f(-0.06, 0, 0.28), f(-0.065, 0.01, 0.045), "thigh.R", 0.05 * H],
        "foot.R":      [f(-0.065, 0.01, 0.045), f(-0.065, -0.065, 0.01), "shin.R", 0.045 * H],
    }


def rig_humanoid(meshes, root, height, rig_name, staff=False, weapon=None, weapon_bone=None, attack=None, layout=None,
                 loose_hand=None, drum=False, staff_axis=None, robe=False):
    """Rigs an A-pose class model with the shared bone names, so the same five actions play on it. staff=True finds a
    long straight staff in the right hand (the straightest near-vertical line on that side) and pins it to hand.R,
    so it swings as one piece instead of bending with the head and shoulder. robe=True (a figure in a long robe) eases
    the robe off both arms and the legs and shortens the run's stride."""
    verts = [v.co.copy() for m in meshes for v in m.data.vertices]
    if weapon is not None:
        verts += [v.co.copy() for v in weapon.data.vertices]
    layout = layout or humanoid_layout(verts, height)
    if staff:
        import looks
        a, b = looks.pole_axis(verts, height, max_x=-0.12 * height, radius=0.035)
        hand = Vector(layout["hand.R"][0])
        axis = (b - a).normalized()
        # The hand bone runs up the staff from the grip, so the pinned staff turns about the fist.
        layout["hand.R"][1] = tuple(hand + axis * 0.12 * height)
    arm, radii = build_from_layout(root, layout, rig_name)
    for m in meshes:
        skin(m, arm, radii)
        if drum or loose_hand:
            # The drum arm and the Wraithsworn's thrusting void hand swing far from the hip cloth beside them.
            free_cloth_from_arms(m, arm, height, sides=("L",))
        if robe:
            free_cloth_from_arms(m, arm, height)
            free_robe_from_legs(m, arm, height)
        if loose_hand:
            hold_loose(m, arm, height, loose_hand)
        if drum:
            pin_drum(m, arm, height)
        if staff_axis is not None:
            pin_staff_charms(m, arm, height, staff_axis[0], staff_axis[1])
        if staff:
            group = m.vertex_groups.get("hand.R")
            pinned = 0
            for v in m.data.vertices:
                if looks._distance_to_axis(v.co, a, b) < 0.05 and (v.co.z > 0.62 * height or v.co.z < 0.40 * height):
                    for g in list(v.groups):
                        m.vertex_groups[g.group].remove([v.index])
                    group.add([v.index], 1.0, 'REPLACE')
                    pinned += 1
            print("staff: %d vertices pinned to hand.R" % pinned)
    if weapon is not None:
        # The weapon part rides its hand whole: hand.R for a staff or sword; paired blades pass a function of the
        # position, so each rides its own hand.
        skin(weapon, arm, radii, only=weapon_bone)
    key_actions(arm, attack, robe=robe)
    return arm, layout
