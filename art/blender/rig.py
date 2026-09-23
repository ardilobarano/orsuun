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
    arm_data = bpy.data.armatures.new("VanguardRig")
    arm = bpy.data.objects.new("Armature", arm_data)
    bpy.context.scene.collection.objects.link(arm)
    arm.parent = root
    layout = {k: list(v) for k, v in LAYOUT.items()}
    layout["forearm.R"][1] = tuple(right_hand)
    layout["hand.R"][0] = tuple(right_hand)
    layout["hand.R"][1] = tuple(right_hand + pole_dir.normalized() * 0.16)   # along the pole, toward the blade
    layout["forearm.L"][1] = tuple(left_hand)
    layout["hand.L"][0] = tuple(left_hand)
    layout["hand.L"][1] = tuple(left_hand + Vector((0.0, -0.10, -0.02)))

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
    to one bone (the banner cloth rides the chest)."""
    bones = [(b.name, b.head_local.copy(), b.tail_local.copy(), radii[b.name]) for b in arm.data.bones if b.use_deform]
    me = mesh_obj.data
    n = len(me.vertices)
    weights = [dict() for _ in range(n)]
    for i, v in enumerate(me.vertices):
        if only:
            weights[i] = {only: 1.0}
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


def key_actions(arm):
    """Keys every action on the armature (fake users keep them); the FBX exporter writes one take per action."""
    arm.animation_data_create()
    for pb in arm.pose.bones:
        pb.rotation_mode = 'QUATERNION'
    made = []
    for name, (length, keys) in ACTIONS.items():
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
