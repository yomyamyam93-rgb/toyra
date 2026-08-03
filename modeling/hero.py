# -*- coding: utf-8 -*-
"""토이라 주인공 — 로우폴리 인간형 (5.5등신 · 키 1.8m · 약 1,400 삼각형)

돌리는 법 (블렌더 5.1, 창 안 띄우고):
    blender -b --python modeling/hero.py

나오는 것:
    Assets/Game/Models/hero.fbx   뼈대 19개 + 슬롯 4조각(머리/상체/하체/발)

★ 숫자는 전부 이 파일 맨 위에 있다. 유니티에 세워 놓고 보면서 여기를 고친다.
  손으로 깎은 .blend 를 남기지 않는 이유가 이것이다.

★ 만드는 방식: 부위마다 「고리(ring)를 쌓아 잇는 관」이다. 온몸이 하나로 이어진
  토폴로지가 아니라 부위가 서로 겹쳐 있다 — 화면에서 캐릭터가 37~162픽셀이라
  이음새가 안 보인다. 대신 절차 생성이 안 망가진다.

★ 슬롯은 「통짜로 만들어 뼈에 물린 다음」 자른다. 그래야 자른 면의 정점이
  양쪽에서 똑같은 뼈 가중치를 갖고, 애니메이션이 돌아도 안 벌어진다.
  → 자른 뒤에 정점을 옮기지 말 것.
"""

import bpy
import bmesh
import math
import os
import sys

# ──────────────────────────────────────────────────────────────────────────
# 1. 숫자 — 여기만 고친다
# ──────────────────────────────────────────────────────────────────────────

KEY = 1.80          # 키 (m). Hero.height 와 같아야 한다

# 자르는 선 (슬롯 경계) — 여기에 반드시 고리가 하나 놓인다
CUT_NECK = 1.44     # 머리 ↔ 상체
CUT_WAIST = 1.06    # 상체 ↔ 하체
CUT_ANKLE = 0.10    # 하체 ↔ 발

# 둘레를 몇 칸으로 나누나 — 폴리 수를 정하는 손잡이
SEG_TORSO = 14
SEG_LEG = 10
SEG_ARM = 10
SEG_HEAD = 14
SEG_NECK = 10
SEG_FOOT = 8

# 몸통 — (높이, 좌우 반지름, 앞뒤 반지름)
TORSO = [
    (0.80, 0.150, 0.110),   # 사타구니
    (0.86, 0.168, 0.120),
    (0.92, 0.172, 0.122),   # 골반
    (0.98, 0.160, 0.115),
    (1.06, 0.138, 0.102),   # ← 허리 (CUT_WAIST)
    (1.13, 0.146, 0.106),
    (1.20, 0.162, 0.114),
    (1.27, 0.176, 0.120),   # 가슴
    (1.33, 0.190, 0.120),
    (1.38, 0.198, 0.114),   # 어깨
    (1.42, 0.150, 0.095),   # 목 밑
]

# 목 — (높이, 반지름)
NECK = [
    (1.36, 0.060),
    (1.40, 0.056),
    (1.44, 0.054),          # ← CUT_NECK
    (1.48, 0.056),
    (1.52, 0.060),
]

# 머리 — 눌린 공
HEAD_BOT, HEAD_TOP = 1.47, 1.80
HEAD_RX, HEAD_RY = 0.120, 0.135
HEAD_BANDS = 6

# 팔 (왼쪽 기준, T자세라 +X 로 뻗는다) — (x, 앞뒤 반지름, 위아래 반지름)
ARM = [
    (0.19, 0.062, 0.064),   # 어깨 (몸통 속에 묻힌다)
    (0.28, 0.054, 0.056),
    (0.38, 0.049, 0.050),
    (0.47, 0.045, 0.046),   # 팔꿈치
    (0.58, 0.041, 0.042),
    (0.68, 0.037, 0.037),
    (0.75, 0.035, 0.033),   # 손목
    (0.80, 0.048, 0.026),   # 손 — 넓어지고 납작해진다 (벙어리장갑)
    (0.86, 0.048, 0.026),
    (0.90, 0.028, 0.019),   # 손끝
]

# 다리 (왼쪽 기준) — (높이, 좌우 반지름, 앞뒤 반지름)
LEG_X = 0.085           # 다리 중심이 몸 가운데서 얼마나 벌어져 있나
LEG = [
    (0.92, 0.088, 0.092),   # 고관절 (골반 속에 묻힌다)
    (0.83, 0.084, 0.090),
    (0.74, 0.078, 0.085),
    (0.64, 0.070, 0.078),
    (0.54, 0.063, 0.070),
    (0.50, 0.061, 0.067),   # 무릎
    (0.42, 0.057, 0.063),
    (0.32, 0.051, 0.056),
    (0.22, 0.045, 0.049),
    (0.14, 0.041, 0.044),
    (0.10, 0.040, 0.043),   # ← 발목 (CUT_ANKLE)
]

# 발 (왼쪽 기준) — (앞뒤 y, 좌우 반지름, 윗면 z, 바닥 z)
#   -Y 가 앞쪽(캐릭터가 보는 방향)이다
FOOT = [
    (0.055, 0.042, 0.120, 0.010),   # 뒤꿈치
    (0.010, 0.050, 0.125, 0.000),
    (-0.060, 0.050, 0.075, 0.000),
    (-0.120, 0.045, 0.045, 0.000),
    (-0.170, 0.034, 0.028, 0.000),  # 발끝
]

# 뼈대 — 유니티 휴머노이드가 자동으로 알아보는 이름을 쓴다
#   (이름, 머리, 꼬리, 부모)
BONES = [
    ("Hips",        (0.0, 0.0, 0.92), (0.0, 0.0, 1.06), None),
    ("Spine",       (0.0, 0.0, 1.06), (0.0, 0.0, 1.20), "Hips"),
    ("Chest",       (0.0, 0.0, 1.20), (0.0, 0.0, 1.38), "Spine"),
    ("Neck",        (0.0, 0.0, 1.38), (0.0, 0.0, 1.47), "Chest"),
    ("Head",        (0.0, 0.0, 1.47), (0.0, 0.0, 1.80), "Neck"),

    ("Shoulder.L",  (0.045, 0.0, 1.34), (0.19, 0.0, 1.35), "Chest"),
    ("UpperArm.L",  (0.19, 0.0, 1.35), (0.47, 0.0, 1.35), "Shoulder.L"),
    ("LowerArm.L",  (0.47, 0.0, 1.35), (0.75, 0.0, 1.35), "UpperArm.L"),
    ("Hand.L",      (0.75, 0.0, 1.35), (0.90, 0.0, 1.35), "LowerArm.L"),

    ("Shoulder.R",  (-0.045, 0.0, 1.34), (-0.19, 0.0, 1.35), "Chest"),
    ("UpperArm.R",  (-0.19, 0.0, 1.35), (-0.47, 0.0, 1.35), "Shoulder.R"),
    ("LowerArm.R",  (-0.47, 0.0, 1.35), (-0.75, 0.0, 1.35), "UpperArm.R"),
    ("Hand.R",      (-0.75, 0.0, 1.35), (-0.90, 0.0, 1.35), "LowerArm.R"),

    ("UpperLeg.L",  (LEG_X, 0.0, 0.92), (LEG_X, 0.0, 0.50), "Hips"),
    ("LowerLeg.L",  (LEG_X, 0.0, 0.50), (LEG_X, 0.0, 0.10), "UpperLeg.L"),
    ("Foot.L",      (LEG_X, 0.0, 0.10), (LEG_X, -0.14, 0.02), "LowerLeg.L"),

    ("UpperLeg.R",  (-LEG_X, 0.0, 0.92), (-LEG_X, 0.0, 0.50), "Hips"),
    ("LowerLeg.R",  (-LEG_X, 0.0, 0.50), (-LEG_X, 0.0, 0.10), "UpperLeg.R"),
    ("Foot.R",      (-LEG_X, 0.0, 0.10), (-LEG_X, -0.14, 0.02), "LowerLeg.R"),
]

SLOTS = ["머리", "상체", "하체", "발"]


# ──────────────────────────────────────────────────────────────────────────
# 2. 뼈 가중치 — 정점을 내가 만드니까 가중치도 내가 정한다
#    (블렌더의 자동 가중치는 부위가 겹친 메시에서 자주 실패한다)
# ──────────────────────────────────────────────────────────────────────────

def ramp(v, a, b):
    """a 에서 0, b 에서 1. 부드럽게(smoothstep)."""
    if abs(b - a) < 1e-9:
        return 0.0 if v < a else 1.0
    t = (v - a) / (b - a)
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)


def side_name(base, x):
    return base + (".L" if x >= 0 else ".R")


def w_torso(p):
    """몸통 — 높이만 보고 Hips → Spine → Chest 로 섞는다."""
    z = p[2]
    b1 = ramp(z, 1.00, 1.13)    # Hips → Spine
    b2 = ramp(z, 1.15, 1.28)    # Spine → Chest
    return {
        "Hips": 1.0 - b1,
        "Spine": b1 * (1.0 - b2),
        "Chest": b1 * b2,
    }


def w_neck(p):
    z = p[2]
    b1 = ramp(z, 1.34, 1.42)    # Chest → Neck
    b2 = ramp(z, 1.44, 1.52)    # Neck → Head
    return {
        "Chest": 1.0 - b1,
        "Neck": b1 * (1.0 - b2),
        "Head": b1 * b2,
    }


def w_head(p):
    z = p[2]
    b = ramp(z, 1.46, 1.53)     # 목이랑 만나는 밑동만 조금 섞는다
    return {"Neck": 1.0 - b, "Head": b}


def w_arm(p):
    ax = abs(p[0])
    b1 = ramp(ax, 0.17, 0.27)   # Shoulder → UpperArm
    b2 = ramp(ax, 0.41, 0.53)   # UpperArm → LowerArm  (팔꿈치 0.47)
    b3 = ramp(ax, 0.70, 0.79)   # LowerArm → Hand      (손목 0.75)
    return {
        side_name("Shoulder", p[0]): 1.0 - b1,
        side_name("UpperArm", p[0]): b1 * (1.0 - b2),
        side_name("LowerArm", p[0]): b1 * b2 * (1.0 - b3),
        side_name("Hand", p[0]): b1 * b2 * b3,
    }


def w_leg(p):
    z = p[2]
    b1 = 1.0 - ramp(z, 0.84, 0.94)   # 위로 갈수록 Hips (골반 속에 묻힌 부분)
    b2 = 1.0 - ramp(z, 0.43, 0.57)   # 아래로 갈수록 LowerLeg (무릎 0.50)
    b3 = 1.0 - ramp(z, 0.10, 0.19)   # 아래로 갈수록 Foot   (발목 0.10)
    hips = 1.0 - b1
    rest = b1
    return {
        "Hips": hips,
        side_name("UpperLeg", p[0]): rest * (1.0 - b2),
        side_name("LowerLeg", p[0]): rest * b2 * (1.0 - b3),
        side_name("Foot", p[0]): rest * b2 * b3,
    }


def w_foot(p):
    z = p[2]
    b = 1.0 - ramp(z, 0.09, 0.14)    # 발목 위쪽만 종아리에 조금 물린다
    return {
        side_name("LowerLeg", p[0]): 1.0 - b,
        side_name("Foot", p[0]): b,
    }


# ──────────────────────────────────────────────────────────────────────────
# 3. 메시 짓기
# ──────────────────────────────────────────────────────────────────────────

V = []   # 정점 위치
W = []   # 정점마다 {뼈이름: 가중치}
F = []   # (정점 인덱스 튜플, 슬롯 이름)


def av(p, wfn):
    V.append(p)
    W.append(wfn(p))
    return len(V) - 1


def ring_z(z, rx, ry, n, wfn, cx=0.0, cy=0.0):
    """수평 고리 — 몸통·다리·목처럼 위아래로 뻗는 관에 쓴다."""
    out = []
    for k in range(n):
        a = 2.0 * math.pi * k / n
        out.append(av((cx + rx * math.cos(a), cy + ry * math.sin(a), z), wfn))
    return out


def ring_x(x, ry, rz, n, wfn, cz=0.0):
    """수직 고리 — 팔처럼 좌우로 뻗는 관에 쓴다."""
    out = []
    for k in range(n):
        a = 2.0 * math.pi * k / n
        out.append(av((x, ry * math.cos(a), cz + rz * math.sin(a)), wfn))
    return out


def bridge(a, b, slot):
    n = len(a)
    for k in range(n):
        k1 = (k + 1) % n
        F.append(((a[k], a[k1], b[k1], b[k]), slot))


def cap(idx, slot, flip=False):
    f = list(idx)
    if flip:
        f.reverse()
    F.append((tuple(f), slot))


def slot_by_z(z):
    if z >= CUT_NECK:
        return "머리"
    if z >= CUT_WAIST:
        return "상체"
    return "하체"


def build_torso():
    rings = [ring_z(z, rx, ry, SEG_TORSO, w_torso) for (z, rx, ry) in TORSO]
    for i in range(len(rings) - 1):
        zmid = 0.5 * (TORSO[i][0] + TORSO[i + 1][0])
        bridge(rings[i], rings[i + 1], slot_by_z(zmid))
    cap(rings[0], "하체", flip=True)
    cap(rings[-1], "상체")


def build_neck():
    rings = [ring_z(z, r, r, SEG_NECK, w_neck) for (z, r) in NECK]
    for i in range(len(rings) - 1):
        zmid = 0.5 * (NECK[i][0] + NECK[i + 1][0])
        bridge(rings[i], rings[i + 1], slot_by_z(zmid))
    cap(rings[0], "상체", flip=True)
    cap(rings[-1], "머리")


def build_head():
    cz = 0.5 * (HEAD_BOT + HEAD_TOP)
    hh = 0.5 * (HEAD_TOP - HEAD_BOT)
    rings = []
    for i in range(1, HEAD_BANDS):
        th = math.pi * i / HEAD_BANDS          # 0 = 정수리
        z = cz + hh * math.cos(th)
        s = math.sin(th)
        rings.append(ring_z(z, HEAD_RX * s, HEAD_RY * s, SEG_HEAD, w_head))
    top = av((0.0, 0.0, HEAD_TOP), w_head)
    bot = av((0.0, 0.0, HEAD_BOT), w_head)
    for i in range(len(rings) - 1):
        bridge(rings[i], rings[i + 1], "머리")
    n = SEG_HEAD
    for k in range(n):
        k1 = (k + 1) % n
        F.append(((top, rings[0][k], rings[0][k1]), "머리"))
        F.append(((bot, rings[-1][k1], rings[-1][k]), "머리"))


def build_arm(sign):
    rings = []
    for (x, ry, rz) in ARM:
        rings.append(ring_x(sign * x, ry, rz, SEG_ARM, w_arm, cz=1.35))
    if sign < 0:
        rings = [list(reversed(r)) for r in rings]   # 좌우 뒤집으면 감기 방향도 뒤집힌다
    for i in range(len(rings) - 1):
        bridge(rings[i], rings[i + 1], "상체")
    cap(rings[0], "상체", flip=True)
    cap(rings[-1], "상체")


def build_leg(sign):
    cx = sign * LEG_X
    rings = [ring_z(z, rx, ry, SEG_LEG, w_leg, cx=cx) for (z, rx, ry) in LEG]
    for i in range(len(rings) - 1):
        bridge(rings[i], rings[i + 1], "하체")
    cap(rings[0], "하체")
    cap(rings[-1], "하체", flip=True)


def build_foot(sign):
    cx = sign * LEG_X
    e = 0.6                                  # 0.5 에 가까울수록 네모지다 (발바닥이 평평해야 한다)
    rings = []
    for (y, rx, ztop, zbot) in FOOT:
        zc = 0.5 * (ztop + zbot)
        rz = 0.5 * (ztop - zbot)
        out = []
        for k in range(SEG_FOOT):
            a = 2.0 * math.pi * k / SEG_FOOT
            c, s = math.cos(a), math.sin(a)
            x = cx + rx * math.copysign(abs(c) ** e, c)
            z = zc + rz * math.copysign(abs(s) ** e, s)
            out.append(av((x, y, z), w_foot))
        rings.append(out)
    for i in range(len(rings) - 1):
        bridge(rings[i], rings[i + 1], "발")
    cap(rings[0], "발")
    cap(rings[-1], "발", flip=True)


# ──────────────────────────────────────────────────────────────────────────
# 4. 블렌더에 올리기
# ──────────────────────────────────────────────────────────────────────────

def wipe():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.armatures, bpy.data.objects):
        for b in list(block):
            if b.users == 0:
                block.remove(b)


def make_armature():
    arm = bpy.data.armatures.new("Armature")
    obj = bpy.data.objects.new("Armature", arm)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode='EDIT')
    made = {}
    for (name, head, tail, parent) in BONES:
        b = arm.edit_bones.new(name)
        b.head = head
        b.tail = tail
        b.roll = 0.0
        if parent:
            b.parent = made[parent]
            b.use_connect = (tuple(made[parent].tail) == tuple(head))
        made[name] = b
    bpy.ops.object.mode_set(mode='OBJECT')
    return obj


def make_slot(name, armature):
    """이 슬롯에 속한 면만 골라 메시 하나를 만든다."""
    faces = [f for (f, s) in F if s == name]
    used = sorted({i for f in faces for i in f})
    remap = {old: new for new, old in enumerate(used)}
    verts = [V[i] for i in used]
    polys = [tuple(remap[i] for i in f) for f in faces]

    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], polys)
    me.validate()

    obj = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(obj)

    # 바깥쪽을 보게 법선 정리 — 부위마다 따로 계산된다
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True

    # 가중치
    groups = {}
    for new, old in enumerate(used):
        for bone, w in W[old].items():
            if w <= 1e-4:
                continue
            if bone not in groups:
                groups[bone] = obj.vertex_groups.new(name=bone)
            groups[bone].add([new], w, 'REPLACE')

    obj.parent = armature
    mod = obj.modifiers.new("Armature", 'ARMATURE')
    mod.object = armature
    return obj


def main():
    wipe()

    build_torso()
    build_neck()
    build_head()
    for s in (1, -1):
        build_arm(s)
        build_leg(s)
        build_foot(s)

    armature = make_armature()
    objs = [make_slot(n, armature) for n in SLOTS]

    # 삼각형 수 — 짐작하지 말고 센다
    total = 0
    print("")
    print("=== 슬롯별 삼각형 ===")
    for o in objs:
        t = sum(len(p.vertices) - 2 for p in o.data.polygons)
        total += t
        print("  %-4s  정점 %4d  면 %4d  삼각형 %5d" %
              (o.name, len(o.data.vertices), len(o.data.polygons), t))
    print("  ----  합계 삼각형 %d" % total)

    # 키도 잰다
    zs = [v.co.z for o in objs for v in o.data.vertices]
    print("  키 실측 %.3f m  (목표 %.2f)" % (max(zs) - min(zs), KEY))
    print("  뼈 %d 개" % len(BONES))
    print("")

    # 내보내기
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    out_dir = os.path.join(root, "Assets", "Game", "Models")
    os.makedirs(out_dir, exist_ok=True)
    path = os.path.join(out_dir, "hero.fbx")

    bpy.ops.object.select_all(action='SELECT')
    bpy.context.view_layer.objects.active = armature
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={'ARMATURE', 'MESH'},
        use_mesh_modifiers=False,      # 아마추어 모디파이어를 굽지 않는다
        add_leaf_bones=False,          # 유니티에서 쓸데없는 끝뼈가 안 생기게
        bake_anim=False,
        apply_scale_options='FBX_SCALE_ALL',
        global_scale=1.0,
        axis_forward='-Z',
        axis_up='Y',
    )
    print("=== 내보냄: %s ===" % path)

    # 다시 뽑을 때 참고하도록 .blend 도 남긴다
    bpy.ops.wm.save_as_mainfile(
        filepath=os.path.join(os.path.dirname(os.path.abspath(__file__)), "hero.blend"))


main()
