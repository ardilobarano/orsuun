"""Rigs and animations for enemies (run inside Blender through looks.mob_model(..., rig=...)).

Every enemy stands on z=0, faces -Y and is centred, as mob_model leaves it. Four body plans, each measured from the
mesh itself:

  biped      people and the undead: the heroes' humanoid rig (rig.rig_humanoid) with a sword or staff attack.
  quadruped  wolves, boars, bears: body and chest along the back, neck and head at the front, a tail, and four
             two-bone legs placed on the feet (the lowest vertices, one cluster per quarter).
  serpent    a coiled snake with its upper body raised: a chain up the raised body to the head.
  scorpion   a low body, a tail chain rising behind it, two claws and a leg group on each side.

All of them get the heroes' five clips by name (Idle, Run, Attack, Hit, Death), so LaneView plays them the same way.
Rolls: a bone that runs mostly up or down has its local Z forward (-Y), one that runs mostly level has it up. So a
positive rotation about local X swings a leg's tip forward and lifts a level bone's tip; local Z turns it sideways.
Clip locations are in the motion bone's own axes.
"""
import math

import bpy
from mathutils import Euler, Quaternion, Vector

import rig as rigging

FORWARD = Vector((0.0, -1.0, 0.0))
UP = Vector((0.0, 0.0, 1.0))


def _centre(vs, default):
    return sum(vs, Vector()) / len(vs) if vs else Vector(default)


def _build(root, layout, rig_name, level=()):
    """layout: bone -> [head, tail, parent, radius]. Rolls as in the module notes; bones named in `level` always count
    as level (a neck rising at 45 degrees still pitches like the back)."""
    arm_data = bpy.data.armatures.new(rig_name)
    arm = bpy.data.objects.new("Armature", arm_data)
    bpy.context.scene.collection.objects.link(arm)
    arm.parent = root
    bpy.context.view_layer.objects.active = arm
    with bpy.context.temp_override(active_object=arm, object=arm, selected_objects=[arm]):
        bpy.ops.object.mode_set(mode='EDIT')
        eb = arm_data.edit_bones
        for name, (head, tail, _parent, _r) in layout.items():
            b = eb.new(name)
            b.head = Vector(head)
            b.tail = Vector(tail)
            d = (b.tail - b.head).normalized()
            b.align_roll(UP if name in level or abs(d.z) <= 0.7 else FORWARD)
            b.use_deform = name != "root"
        for name, (_h, _t, parent, _r) in layout.items():
            if parent:
                eb[name].parent = eb[parent]
                eb[name].use_connect = False
        bpy.ops.object.mode_set(mode='OBJECT')
    return arm, {k: v[3] for k, v in layout.items()}


def _key(arm, clips, motion):
    """Keys every clip (fake users keep them): clips name -> (length, [(frame, {bone: degrees}, motion location)])."""
    arm.animation_data_create()
    for pb in arm.pose.bones:
        pb.rotation_mode = 'QUATERNION'
    for name, (length, keys) in clips.items():
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
                arm.pose.bones[bone].rotation_quaternion = Euler(tuple(math.radians(a) for a in deg), 'XYZ').to_quaternion()
            arm.pose.bones[motion].location = Vector(loc)
            for bone in touched:
                arm.pose.bones[bone].keyframe_insert("rotation_quaternion", frame=frame + 1)
            arm.pose.bones[motion].keyframe_insert("location", frame=frame + 1)
        action.use_frame_range = True
        action.frame_range = (1, length + 1)
    arm.animation_data.action = None
    for pb in arm.pose.bones:
        pb.rotation_quaternion = Quaternion()
        pb.location = Vector()


# ---- quadruped ----

def quadruped_layout(verts, H):
    ys = [v.y for v in verts]
    xs = [v.x for v in verts]
    ymin, ymax = min(ys), max(ys)
    L = ymax - ymin
    W = max(xs) - min(xs)
    cy = (ymin + ymax) / 2
    low = [v for v in verts if v.z < 0.12 * H]
    feet = {}
    for key, front, left in (("FL", True, True), ("FR", True, False), ("RL", False, True), ("RR", False, False)):
        vs = [v for v in low if (v.y < cy) == front and (v.x > 0) == left]
        feet[key] = _centre(vs, (0.2 * W * (1 if left else -1), cy + (-0.3 if front else 0.3) * L, 0.0))
    head = _centre([v for v in verts if v.y < ymin + 0.15 * L], (0, ymin + 0.07 * L, 0.75 * H))
    front_y = (feet["FL"].y + feet["FR"].y) / 2
    rear_y = (feet["RL"].y + feet["RR"].y) / 2
    back_z = 0.62 * H
    layout = {
        "root":  [(0, cy, 0), (0, cy, 0.1 * H), None, 0.0],
        "body":  [(0, rear_y, back_z), (0, cy, back_z), "root", 0.3 * H],
        "chest": [(0, cy, back_z), (0, front_y, back_z), "body", 0.3 * H],
        "neck":  [(0, front_y, back_z), (0, (front_y + head.y) / 2, (back_z + head.z) / 2 + 0.05 * H), "chest", 0.18 * H],
        "head":  [(0, (front_y + head.y) / 2, (back_z + head.z) / 2 + 0.05 * H), (0, ymin, head.z), "neck", 0.2 * H],
        "tail":  [(0, ymax - 0.1 * L, 0.6 * H), (0, ymax, 0.5 * H), "body", 0.1 * H],
    }
    hip = 0.55 * H
    for key, foot in feet.items():
        parent = "chest" if key[0] == "F" else "body"
        knee = (foot.x, foot.y, 0.3 * H)
        layout["upper." + key] = [(foot.x, foot.y, hip), knee, parent, 0.11 * H]
        layout["lower." + key] = [knee, (foot.x, foot.y, 0.02 * H), "upper." + key, 0.09 * H]
    return layout, L, W


def quadruped_clips(H, L, W):
    legs = ["upper.FL", "upper.FR", "upper.RL", "upper.RR"]
    rest = {b: (0, 0, 0) for b in legs + ["lower.FL", "lower.FR", "lower.RL", "lower.RR", "chest", "neck", "head", "tail", "body"]}
    drop = 0.62 * H - 0.3 * W
    def pose(**k):
        d = dict(rest)
        d.update({name.replace("_", "."): v for name, v in k.items()})
        return d
    return {
        "Idle": (48, [
            (0, pose(), (0, 0, 0)),
            (24, pose(chest=(1.5, 0, 0), neck=(4, 0, 0), head=(-3, 0, 0), tail=(0, 0, 12)), (0, 0, -0.008 * H)),
            (48, pose(), (0, 0, 0)),
        ]),
        # A trot: diagonal pairs swing together.
        "Run": (12, [
            (0, pose(upper_FL=(28, 0, 0), lower_FL=(-12, 0, 0), upper_RR=(22, 0, 0), upper_FR=(-24, 0, 0), upper_RL=(-24, 0, 0),
                     lower_FR=(-8, 0, 0), lower_RL=(-8, 0, 0), chest=(2, 0, 0), neck=(-4, 0, 0), tail=(6, 0, -10)), (0, 0, 0)),
            (3, pose(lower_FR=(-40, 0, 0), lower_RL=(-35, 0, 0), chest=(0, 0, 0), neck=(2, 0, 0), tail=(8, 0, 0)), (0, 0, 0.04 * H)),
            (6, pose(upper_FR=(28, 0, 0), lower_FR=(-12, 0, 0), upper_RL=(22, 0, 0), upper_FL=(-24, 0, 0), upper_RR=(-24, 0, 0),
                     lower_FL=(-8, 0, 0), lower_RR=(-8, 0, 0), chest=(2, 0, 0), neck=(-4, 0, 0), tail=(6, 0, 10)), (0, 0, 0)),
            (9, pose(lower_FL=(-40, 0, 0), lower_RR=(-35, 0, 0), neck=(2, 0, 0), tail=(8, 0, 0)), (0, 0, 0.04 * H)),
            (12, pose(upper_FL=(28, 0, 0), lower_FL=(-12, 0, 0), upper_RR=(22, 0, 0), upper_FR=(-24, 0, 0), upper_RL=(-24, 0, 0),
                      lower_FR=(-8, 0, 0), lower_RL=(-8, 0, 0), chest=(2, 0, 0), neck=(-4, 0, 0), tail=(6, 0, -10)), (0, 0, 0)),
        ]),
        # Crouch, then lunge and bite: the forelegs reach, the head snaps down.
        "Attack": (12, [
            (0, pose(), (0, 0, 0)),
            (4, pose(chest=(-6, 0, 0), neck=(10, 0, 0), head=(14, 0, 0), upper_RL=(-10, 0, 0), upper_RR=(-10, 0, 0)), (0, -0.06 * L, -0.04 * H)),
            (7, pose(chest=(-4, 0, 0), neck=(-22, 0, 0), head=(-18, 0, 0), upper_FL=(20, 0, 0), upper_FR=(16, 0, 0), lower_FL=(-22, 0, 0),
                     lower_FR=(-18, 0, 0), upper_RL=(-16, 0, 0), upper_RR=(-16, 0, 0)), (0, 0.14 * L, 0.0)),
            (12, pose(), (0, 0, 0)),
        ]),
        "Hit": (9, [
            (0, pose(), (0, 0, 0)),
            (3, pose(chest=(8, 0, 0), neck=(12, 0, 0), head=(6, 0, 0)), (0, -0.05 * L, 0)),
            (9, pose(), (0, 0, 0)),
        ]),
        # The legs give and it rolls onto its side. Holds the last frame.
        "Death": (24, [
            (0, pose(), (0, 0, 0)),
            (8, pose(body=(0, 25, 0), upper_FL=(20, 0, 0), upper_FR=(20, 0, 0), lower_FL=(-40, 0, 0), lower_FR=(-40, 0, 0),
                     upper_RL=(15, 0, 0), upper_RR=(15, 0, 0), neck=(-10, 0, 0)), (0, 0, -0.18 * H)),
            (18, pose(body=(0, 86, 0), upper_FL=(12, 0, 0), upper_FR=(18, 0, 0), lower_FL=(-12, 0, 0), lower_FR=(-18, 0, 0),
                      upper_RL=(10, 0, 0), upper_RR=(16, 0, 0), neck=(-14, 0, 0), head=(-8, 0, 0), tail=(-10, 0, 0)), (0, 0, -drop)),
            (24, pose(body=(0, 90, 0), upper_FL=(12, 0, 0), upper_FR=(18, 0, 0), lower_FL=(-12, 0, 0), lower_FR=(-18, 0, 0),
                      upper_RL=(10, 0, 0), upper_RR=(16, 0, 0), neck=(-16, 0, 0), head=(-10, 0, 0), tail=(-12, 0, 0)), (0, 0, -drop)),
        ]),
    }


# ---- serpent ----

def serpent_layout(verts, H):
    def at(z0, z1, default):
        return _centre([v for v in verts if z0 * H <= v.z < z1 * H], default)
    c0 = at(0.0, 0.2, (0, 0, 0.1 * H))
    points = [Vector((c0.x, c0.y, 0.0)), at(0.2, 0.3, (0, 0, 0.25 * H)), at(0.4, 0.5, (0, 0, 0.45 * H)),
              at(0.6, 0.7, (0, 0, 0.65 * H)), at(0.78, 0.86, (0, 0, 0.82 * H))]
    top = at(0.93, 1.01, (0, 0, H))
    names = ["coil", "s1", "s2", "s3"]
    layout = {"root": [(c0.x, c0.y, 0), (c0.x, c0.y, 0.05 * H), None, 0.0]}
    radii = [0.32 * H, 0.14 * H, 0.12 * H, 0.11 * H]
    parent = "root"
    for i, n in enumerate(names):
        a, b = points[i], points[i + 1]
        if i > 0:
            a = points[i]
        layout[n] = [tuple(a), tuple(b), parent, radii[i]]
        parent = n
    layout["head"] = [tuple(points[-1]), tuple(top), "s3", 0.13 * H]
    return layout


def serpent_clips(H):
    rest = {b: (0, 0, 0) for b in ("s1", "s2", "s3", "head", "coil")}
    def pose(**k):
        d = dict(rest)
        d.update(k)
        return d
    return {
        "Idle": (48, [
            (0, pose(s1=(0, 0, 3), s2=(0, 0, -4), s3=(0, 0, 5), head=(0, 0, -4)), (0, 0, 0)),
            (24, pose(s1=(0, 0, -3), s2=(0, 0, 4), s3=(0, 0, -5), head=(0, 0, 4)), (0, 0.01 * H, 0)),
            (48, pose(s1=(0, 0, 3), s2=(0, 0, -4), s3=(0, 0, 5), head=(0, 0, -4)), (0, 0, 0)),
        ]),
        "Run": (16, [
            (0, pose(s1=(4, 0, 9), s2=(0, 0, -12), s3=(0, 0, 12), head=(0, 0, -9)), (0, 0, 0)),
            (8, pose(s1=(4, 0, -9), s2=(0, 0, 12), s3=(0, 0, -12), head=(0, 0, 9)), (0, 0.02 * H, 0)),
            (16, pose(s1=(4, 0, 9), s2=(0, 0, -12), s3=(0, 0, 12), head=(0, 0, -9)), (0, 0, 0)),
        ]),
        # Rears back, then strikes forward and down.
        "Attack": (12, [
            (0, pose(), (0, 0, 0)),
            (4, pose(s1=(-8, 0, 0), s2=(-16, 0, 0), s3=(-20, 0, 0), head=(18, 0, 0)), (0, 0.03 * H, -0.04 * H)),
            (7, pose(s1=(14, 0, 0), s2=(28, 0, 0), s3=(32, 0, 0), head=(-18, 0, 0)), (0, 0, 0.12 * H)),
            (12, pose(), (0, 0, 0)),
        ]),
        "Hit": (9, [
            (0, pose(), (0, 0, 0)),
            (3, pose(s2=(-10, 0, 0), s3=(-12, 0, 0), head=(-8, 0, 0)), (0, 0, -0.03 * H)),
            (9, pose(), (0, 0, 0)),
        ]),
        "Death": (24, [
            (0, pose(), (0, 0, 0)),
            (10, pose(s1=(30, 0, 10), s2=(30, 0, -8), s3=(25, 0, 6), head=(15, 0, 0)), (0, -0.05 * H, 0)),
            (20, pose(s1=(55, 0, 14), s2=(45, 0, -10), s3=(35, 0, 8), head=(20, 0, 0)), (0, -0.12 * H, 0)),
            (24, pose(s1=(58, 0, 14), s2=(46, 0, -10), s3=(36, 0, 8), head=(20, 0, 0)), (0, -0.12 * H, 0)),
        ]),
    }


# ---- scorpion ----

def scorpion_layout(verts, H):
    ys = [v.y for v in verts]
    xs = [v.x for v in verts]
    ymin, ymax = min(ys), max(ys)
    L = ymax - ymin
    W = max(xs) - min(xs)
    cy = (ymin + ymax) / 2
    tail = [v for v in verts if v.z > 0.38 * H]
    def tail_at(z0, z1, default):
        return _centre([v for v in tail if z0 * H <= v.z < z1 * H], default)
    t0 = Vector((0, ymax - 0.18 * L, 0.3 * H))
    t1 = tail_at(0.45, 0.6, (0, ymax - 0.1 * L, 0.52 * H))
    t2 = tail_at(0.68, 0.82, (0, ymax - 0.15 * L, 0.75 * H))
    t3 = tail_at(0.88, 1.01, (0, cy, 0.95 * H))
    front = [v for v in verts if v.y < ymin + 0.3 * L and v.z < 0.42 * H]
    claw_l = _centre([v for v in front if v.x > 0.05 * W], (0.25 * W, ymin + 0.1 * L, 0.2 * H))
    claw_r = _centre([v for v in front if v.x < -0.05 * W], (-0.25 * W, ymin + 0.1 * L, 0.2 * H))
    low = [v for v in verts if v.z < 0.15 * H and ymin + 0.25 * L < v.y < ymax - 0.2 * L]
    leg_l = _centre([v for v in low if v.x > 0.1 * W], (0.35 * W, cy, 0.02 * H))
    leg_r = _centre([v for v in low if v.x < -0.1 * W], (-0.35 * W, cy, 0.02 * H))
    body_z = 0.28 * H
    front_y = ymin + 0.3 * L
    layout = {
        "root":   [(0, cy, 0), (0, cy, 0.1 * H), None, 0.0],
        "body":   [(0, ymax - 0.18 * L, body_z), (0, front_y, body_z), "root", 0.26 * H],
        "tail1":  [tuple(t0), tuple(t1), "body", 0.12 * H],
        "tail2":  [tuple(t1), tuple(t2), "tail1", 0.11 * H],
        "tail3":  [tuple(t2), tuple(t3), "tail2", 0.12 * H],
        "claw.L": [(0.12 * W, front_y, body_z), tuple(claw_l), "body", 0.14 * H],
        "claw.R": [(-0.12 * W, front_y, body_z), tuple(claw_r), "body", 0.14 * H],
        "legs.L": [(0.12 * W, cy, body_z), tuple(leg_l), "body", 0.14 * H],
        "legs.R": [(-0.12 * W, cy, body_z), tuple(leg_r), "body", 0.14 * H],
    }
    return layout, L


def scorpion_clips(H, L):
    rest = {b: (0, 0, 0) for b in ("body", "tail1", "tail2", "tail3", "claw.L", "claw.R", "legs.L", "legs.R")}
    def pose(**k):
        d = dict(rest)
        d.update({name.replace("_", "."): v for name, v in k.items()})
        return d
    return {
        "Idle": (48, [
            (0, pose(tail1=(0, 0, 4), tail2=(3, 0, 0), claw_L=(0, 0, 6), claw_R=(0, 0, -6)), (0, 0, 0)),
            (24, pose(tail1=(0, 0, -4), tail2=(-3, 0, 0), claw_L=(0, 0, -4), claw_R=(0, 0, 4)), (0, 0, -0.01 * H)),
            (48, pose(tail1=(0, 0, 4), tail2=(3, 0, 0), claw_L=(0, 0, 6), claw_R=(0, 0, -6)), (0, 0, 0)),
        ]),
        # A fast scuttle.
        "Run": (8, [
            (0, pose(legs_L=(0, 0, 16), legs_R=(0, 0, 16), tail1=(0, 0, 5), claw_L=(4, 0, 0), claw_R=(4, 0, 0)), (0, 0, 0)),
            (4, pose(legs_L=(0, 0, -16), legs_R=(0, 0, -16), tail1=(0, 0, -5), claw_L=(-4, 0, 0), claw_R=(-4, 0, 0)), (0, 0, 0.03 * H)),
            (8, pose(legs_L=(0, 0, 16), legs_R=(0, 0, 16), tail1=(0, 0, 5), claw_L=(4, 0, 0), claw_R=(4, 0, 0)), (0, 0, 0)),
        ]),
        # The stinger cocks back and strikes over the body; the claws snap.
        "Attack": (12, [
            (0, pose(), (0, 0, 0)),
            (4, pose(tail1=(-10, 0, 0), tail2=(-16, 0, 0), tail3=(-20, 0, 0), claw_L=(0, 0, 18), claw_R=(0, 0, -18)), (0, -0.04 * L, 0)),
            (7, pose(tail1=(22, 0, 0), tail2=(32, 0, 0), tail3=(38, 0, 0), claw_L=(0, 0, -14), claw_R=(0, 0, 14)), (0, 0.1 * L, 0)),
            (12, pose(), (0, 0, 0)),
        ]),
        "Hit": (9, [
            (0, pose(), (0, 0, 0)),
            (3, pose(tail1=(-8, 0, 0), body=(6, 0, 0)), (0, -0.05 * L, 0)),
            (9, pose(), (0, 0, 0)),
        ]),
        # Flips over, legs curling.
        "Death": (24, [
            (0, pose(), (0, 0, 0)),
            (10, pose(body=(0, 70, 0), legs_L=(30, 0, 0), legs_R=(30, 0, 0), tail1=(-20, 0, 0)), (0, 0, 0.08 * H)),
            (20, pose(body=(0, 160, 0), legs_L=(50, 0, 0), legs_R=(50, 0, 0), tail1=(-35, 0, 0), tail2=(-20, 0, 0)), (0, 0, 0.3 * H)),
            (24, pose(body=(0, 170, 0), legs_L=(55, 0, 0), legs_R=(55, 0, 0), tail1=(-35, 0, 0), tail2=(-20, 0, 0)), (0, 0, 0.3 * H)),
        ]),
    }


def rig_mob(mesh, root, height, rig_name, plan, attack=None):
    """Builds and skins the rig for one enemy and keys its clips. plan: biped, quadruped, serpent or scorpion."""
    verts = [v.co.copy() for v in mesh.data.vertices]
    if plan == "biped":
        arm, _layout = rigging.rig_humanoid([mesh], root, height, rig_name, attack=attack)
        return arm
    level = ()
    if plan == "quadruped":
        layout, L, W = quadruped_layout(verts, height)
        clips, motion = quadruped_clips(height, L, W), "body"
        level = ("body", "chest", "neck", "head", "tail")
    elif plan == "serpent":
        layout = serpent_layout(verts, height)
        clips, motion = serpent_clips(height), "coil"
    elif plan == "scorpion":
        layout, L = scorpion_layout(verts, height)
        clips, motion = scorpion_clips(height, L), "body"
        level = ("body", "claw.L", "claw.R", "legs.L", "legs.R")
    else:
        raise ValueError("Unknown body plan: " + plan)
    arm, radii = _build(root, layout, rig_name, level)
    rigging.skin(mesh, arm, radii)
    _key(arm, clips, motion)
    return arm
