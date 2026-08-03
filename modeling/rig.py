# -*- coding: utf-8 -*-
"""hero_clean_<성별>.blend 에 유니티 휴머노이드 뼈대 19개를 앉히고 가중치를 물린다.

    blender -b --python modeling/rig.py

★ 뼈 좌표를 타이핑하지 않는다. 전부 메시 단면에서 실측한다 (0번 규칙).
★ A자세라 팔이 비스듬히 내려와 있다 → 팔 축을 **연속성으로** 따라간다.
  (겹마다 「제일 바깥 고리」를 고르면 아래에서 허벅지를 팔로 착각한다 — 한 번 그랬다)
★ 가중치는 블렌더 자동(본 히트)을 쓴다. 메시가 이제 완전히 닫혀 있어서 쓸 수 있다.
  실패하면 엔벨로프로 물러선다.
★ 뼈 이름은 유니티가 자동 매핑하는 표준 이름 — 임포트에서 Humanoid 만 고르면 된다.
"""

import bpy
import bmesh
import math
import os
from mathutils import Vector

HERE = r"C:\Users\ysim1\Documents\GitHub\toyra\modeling"
OUT = os.path.join(HERE, "preview")
os.makedirs(OUT, exist_ok=True)
KEY = 1.80
SLICES = 240


# ──────────────────────────────────────────────────────────────────────────
def sections(me):
    vs = me.vertices
    ekey = {}
    for e in me.edges:
        a, b = e.vertices
        ekey[(a, b) if a < b else (b, a)] = e.index
    e_at = [[] for _ in range(SLICES)]
    f_at = [[] for _ in range(SLICES)]

    def span(lo, hi):
        return range(max(0, int(lo / KEY * SLICES) - 1),
                     min(SLICES - 1, int(hi / KEY * SLICES) + 1) + 1)

    for e in me.edges:
        z0, z1 = vs[e.vertices[0]].co.z, vs[e.vertices[1]].co.z
        for s in span(min(z0, z1), max(z0, z1)):
            e_at[s].append(e.index)
    for p in me.polygons:
        zz = [vs[i].co.z for i in p.vertices]
        for s in span(min(zz), max(zz)):
            f_at[s].append(p.index)

    out = []
    for s in range(SLICES):
        z = (s + 0.5) / SLICES * KEY
        cut = {}
        for ei in e_at[s]:
            e = me.edges[ei]
            a, b = vs[e.vertices[0]].co, vs[e.vertices[1]].co
            if (a.z - z) * (b.z - z) <= 0.0 and abs(b.z - a.z) > 1e-9:
                f = (z - a.z) / (b.z - a.z)
                cut[ei] = (a.x + (b.x - a.x) * f, a.y + (b.y - a.y) * f)
        if not cut:
            out.append([])
            continue
        parent = {i: i for i in cut}

        def find(i):
            while parent[i] != i:
                parent[i] = parent[parent[i]]
                i = parent[i]
            return i

        for fi in f_at[s]:
            ids = [ekey.get((a, b) if a < b else (b, a))
                   for (a, b) in me.polygons[fi].edge_keys]
            ids = [i for i in ids if i in cut]
            for j in ids[1:]:
                ra, rb = find(ids[0]), find(j)
                if ra != rb:
                    parent[rb] = ra
        g = {}
        for i in cut:
            g.setdefault(find(i), []).append(cut[i])
        rings = []
        for pts in g.values():
            xs = [p[0] for p in pts]
            ys = [p[1] for p in pts]
            rings.append(dict(x0=min(xs), x1=max(xs), y0=min(ys), y1=max(ys),
                              cx=0.5 * (min(xs) + max(xs)),
                              cy=0.5 * (min(ys) + max(ys)),
                              r=0.5 * (max(xs) - min(xs))))
        out.append(rings)
    return out


def zof(s):
    return (s + 0.5) / SLICES * KEY


def core(rings):
    return next((r for r in rings if r["x0"] <= 0.0 <= r["x1"]), None)


def lefts(rings):
    return [r for r in rings if r["x1"] < 0.0]


def landmarks(me):
    sec = sections(me)

    neck_s, w = None, 9e9
    for s in range(int(SLICES * 0.72), int(SLICES * 0.95)):
        c = core(sec[s])
        if c and (c["x1"] - c["x0"]) < w:
            neck_s, w = s, c["x1"] - c["x0"]
    crotch_s = next((s for s in range(int(SLICES * 0.62), int(SLICES * 0.30), -1)
                     if not core(sec[s])), int(SLICES * 0.45))
    sh_s = next((s for s in range(int(SLICES * 0.90), int(SLICES * 0.55), -1)
                 if lefts(sec[s]) and core(sec[s])), int(SLICES * 0.72))

    # ── 팔: 어깨에서 아래로, 「직전 중심에 제일 가까운 고리」를 따라간다
    arm = []
    prev = None
    for s in range(sh_s, 0, -1):
        L = lefts(sec[s])
        if not L:
            break
        if prev is None:
            r = min(L, key=lambda r: r["x0"])          # 어깨에선 제일 바깥이 팔
        else:
            r = min(L, key=lambda r: abs(r["cx"] - prev))
            if abs(r["cx"] - prev) > 0.055:            # 훌쩍 뛰면 팔이 끝난 것
                break
        prev = r["cx"]
        arm.append((zof(s), r["cx"], r["cy"], r["r"]))

    # ── 다리: 사타구니 아래, 안쪽 고리
    leg = []
    for s in range(crotch_s, 0, -1):
        L = lefts(sec[s])
        if not L:
            continue
        r = max(L, key=lambda r: r["x1"])
        leg.append((zof(s), r["cx"], r["cy"], r["r"]))

    # ── 무릎: 고관절→발목의 38~52% 구간에서 제일 가는 곳
    #   ★창을 안 좁히면 정강이/발목을 무릎으로 잡는다 (woman 이 0.296m 로 나왔었다).
    #     사람 무릎은 고관절에서 발목까지의 약 44% 지점이다.
    lo, hi = int(len(leg) * 0.38), max(int(len(leg) * 0.52), int(len(leg) * 0.38) + 1)
    knee = min(leg[lo:hi], key=lambda p: p[3]) if len(leg) > hi > lo else leg[len(leg) // 2]

    # ── 팔꿈치·손목: 팔 경로를 어깨→손끝으로 보고 비율로 자른다
    #   ★팔 추적은 손끝까지 간다 (반지름이 0.002 까지 줄어든 게 증거). 손목이 아니다.
    #     사람은 어깨→손목이 어깨→손끝의 약 80%, 팔꿈치는 약 45%.
    elbow = arm[int(len(arm) * 0.45)] if arm else None
    wrist_i = int(len(arm) * 0.80)
    arm = arm[:wrist_i + 1] if arm else arm

    zs = [v.co.z for v in me.vertices]
    foot = [v.co for v in me.vertices if v.co.z < leg[-1][0] + 0.03]
    toe_y = min(p.y for p in foot)                     # 앞 = -y (실측)

    return dict(sec=sec, neck_z=zof(neck_s), crotch_z=zof(crotch_s),
                sh_z=zof(sh_s), arm=arm, leg=leg, knee=knee, elbow=elbow,
                top=max(zs), toe_y=toe_y)


# ──────────────────────────────────────────────────────────────────────────
def build(gender):
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    bpy.ops.wm.open_mainfile(filepath=os.path.join(HERE, "hero_clean_%s.blend" % gender))
    obj = next(o for o in bpy.data.objects if o.type == 'MESH')

    L = landmarks(obj.data)
    arm, leg = L["arm"], L["leg"]
    sh = arm[0]
    wrist = arm[-1]
    elbow = L["elbow"]
    hip = leg[0]
    knee = L["knee"]
    ankle = leg[-1]

    print("")
    print("=== %s 실측 ===" % gender)
    print("  목 %.3f · 어깨 %.3f · 사타구니 %.3f" % (L["neck_z"], L["sh_z"], L["crotch_z"]))
    print("  팔  어깨(%.3f, %.3f, %.3f) → 팔꿈치 z%.3f → 손목(%.3f, %.3f, %.3f)  반지름 %.3f→%.3f"
          % (sh[1], sh[2], sh[0], elbow[0], wrist[1], wrist[2], wrist[0], sh[3], wrist[3]))
    print("  다리 고관절 z%.3f → 무릎 z%.3f(r %.3f) → 발목 z%.3f  발끝 y%.3f"
          % (hip[0], knee[0], knee[3], ankle[0], L["toe_y"]))

    hips_z = L["crotch_z"] + 0.035
    chest_z = L["sh_z"] - 0.045
    spine_z = 0.5 * (hips_z + chest_z)
    neck_z = L["neck_z"]
    head_z = neck_z + 0.055

    def P(x, y, z):
        return (x, y, z)

    BONES = [
        ("Hips",  P(0, 0, hips_z),  P(0, 0, spine_z), None),
        ("Spine", P(0, 0, spine_z), P(0, 0, chest_z), "Hips"),
        ("Chest", P(0, 0, chest_z), P(0, 0, neck_z),  "Spine"),
        ("Neck",  P(0, 0, neck_z),  P(0, 0, head_z),  "Chest"),
        ("Head",  P(0, 0, head_z),  P(0, 0, L["top"] - 0.03), "Neck"),
    ]
    for sgn, sfx in ((1.0, ".L"), (-1.0, ".R")):
        # 블렌더에서 -x 쪽이 캐릭터의 왼쪽 (캐릭터가 -y 를 본다)
        s_ = sgn
        BONES += [
            ("Shoulder" + sfx, P(s_ * 0.022, 0.0, chest_z + 0.045),
             P(s_ * abs(sh[1]), sh[2], sh[0]), "Chest"),
            ("UpperArm" + sfx, P(s_ * abs(sh[1]), sh[2], sh[0]),
             P(s_ * abs(elbow[1]), elbow[2], elbow[0]), "Shoulder" + sfx),
            ("LowerArm" + sfx, P(s_ * abs(elbow[1]), elbow[2], elbow[0]),
             P(s_ * abs(wrist[1]), wrist[2], wrist[0]), "UpperArm" + sfx),
            ("Hand" + sfx, P(s_ * abs(wrist[1]), wrist[2], wrist[0]),
             P(s_ * abs(wrist[1]) * 1.06, wrist[2], wrist[0] - 0.085), "LowerArm" + sfx),
            ("UpperLeg" + sfx, P(s_ * abs(hip[1]), hip[2], hip[0]),
             P(s_ * abs(knee[1]), knee[2], knee[0]), "Hips"),
            ("LowerLeg" + sfx, P(s_ * abs(knee[1]), knee[2], knee[0]),
             P(s_ * abs(ankle[1]), ankle[2], ankle[0]), "UpperLeg" + sfx),
            ("Foot" + sfx, P(s_ * abs(ankle[1]), ankle[2], ankle[0]),
             P(s_ * abs(ankle[1]), L["toe_y"] + 0.02, 0.02), "LowerLeg" + sfx),
        ]

    ad = bpy.data.armatures.new("Armature")
    arо = bpy.data.objects.new("Armature", ad)
    bpy.context.collection.objects.link(arо)
    bpy.context.view_layer.objects.active = arо
    bpy.ops.object.mode_set(mode='EDIT')
    made = {}
    for (name, head, tail, par) in BONES:
        b = ad.edit_bones.new(name)
        b.head, b.tail, b.roll = head, tail, 0.0
        if par:
            b.parent = made[par]
            b.use_connect = (tuple(round(c, 6) for c in made[par].tail) ==
                             tuple(round(c, 6) for c in head))
        made[name] = b
    bpy.ops.object.mode_set(mode='OBJECT')
    print("  뼈 %d개" % len(BONES))

    # ── 자동 가중치
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    arо.select_set(True)
    bpy.context.view_layer.objects.active = arо
    mode = "자동(본 히트)"
    try:
        bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    except Exception as e:
        print("  자동 가중치 실패 → 엔벨로프로: %s" % e)
        bpy.ops.object.parent_set(type='ARMATURE_ENVELOPE')
        mode = "엔벨로프"
    ngroups = len(obj.vertex_groups)
    unweighted = 0
    for v in obj.data.vertices:
        if sum(g.weight for g in v.groups) < 1e-4:
            unweighted += 1
    print("  가중치 %s · 정점그룹 %d개 · 물리지 않은 정점 %d개"
          % (mode, ngroups, unweighted))

    return obj, arо, L


# ──────────────────────────────────────────────────────────────────────────
def render(obj, arо, gender, tag, poses):
    """포즈를 주고 그림을 뽑는다 — 스키닝이 터지는지 눈으로 본다."""
    bpy.context.view_layer.objects.active = arо
    bpy.ops.object.mode_set(mode='POSE')
    for (bone, axis, deg) in poses:
        pb = arо.pose.bones.get(bone)
        if pb:
            pb.rotation_mode = 'XYZ'
            v = [0.0, 0.0, 0.0]
            v["XYZ".index(axis)] = math.radians(deg)
            pb.rotation_euler = v
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.context.view_layer.update()

    skin = bpy.data.materials.get("skin") or bpy.data.materials.new("skin")
    skin.use_nodes = False
    skin.diffuse_color = (0.74, 0.71, 0.70, 1.0)
    obj.data.materials.clear()
    obj.data.materials.append(skin)
    for p in obj.data.polygons:
        p.use_smooth = True

    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading
    sh.light, sh.color_type = 'STUDIO', 'MATERIAL'
    sh.show_shadows, sh.show_cavity = False, True
    sh.background_type = 'VIEWPORT'
    sh.background_color = (0.34, 0.34, 0.34)
    sc.render.film_transparent = False
    sc.render.resolution_x, sc.render.resolution_y = 700, 950
    cd = bpy.data.cameras.new("cam")
    cd.type = 'ORTHO'
    cd.ortho_scale = 2.05
    cam = bpy.data.objects.new("cam", cd)
    bpy.context.collection.objects.link(cam)
    sc.camera = cam
    yr = math.radians(26)
    cam.location = (6 * math.sin(yr), -6 * math.cos(yr), 0.92)
    cam.rotation_euler = (math.radians(90), 0.0, yr)
    sc.render.filepath = os.path.join(OUT, "리그_%s_%s.png" % (gender, tag))
    bpy.ops.render.render(write_still=True)
    print("=== %s ===" % sc.render.filepath)


for gender in ("woman", "man"):
    obj, arо, L = build(gender)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "hero_rig_%s.blend" % gender))
    render(obj, arо, gender, "굽힘",
           [("LowerArm.L", "X", -75), ("LowerArm.R", "X", -75),
            ("LowerLeg.L", "X", 70), ("UpperLeg.L", "X", -45),
            ("Spine", "X", -12), ("Head", "X", 14)])
    print("=== hero_rig_%s.blend 저장 ===" % gender)
