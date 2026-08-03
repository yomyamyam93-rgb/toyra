# -*- coding: utf-8 -*-
"""베이스 메시의 몸 비율을 참고 도면(base.jpg)에서 잰 값으로 옮기고, 머리를 잘라낸다.

    blender -b <번들.blend> --python modeling/retarget.py

★ 배율을 짐작하지 않는다. 참고 도면을 잰 표(REF_*)와 이 몸을 **같은 방식으로**
  잰 값의 비를 그대로 쓴다.

★★ 단면을 「x 좌표 간격」으로 가르면 안 된다. 두 번 터졌다:
    ① 정점으로 재니 간격(3~5cm)이 끊김 기준보다 넓어 머리조차 조각났다
    ② 모서리 교점을 x 로 투영해 재도 마찬가지였다 (허벅지 배율 3.13 이 나왔다)
   → **면의 연결**로 고리를 찾는다. 한 면 위의 교점 둘은 반드시 같은 고리다.
     이러면 몸통·팔·다리가 정확히 갈린다.

★ 겹마다 한 번만 계산해서 재활용한다. 정점마다 다시 자르면 수천만 번이 된다.

★ 머리는 애니풍으로 갈아 끼울 것이라 여기서 「잘라내기만」 한다.
"""

import bpy
import bmesh
import math
import os

HERE = r"C:\Users\ysim1\Documents\GitHub\toyra\modeling"
OUT = os.path.join(HERE, "preview")
os.makedirs(OUT, exist_ok=True)

KEY = 1.80
ITERS = 2
SLICES = 180

# ── 참고 도면 실측 (measure_ref.py) — 키를 1.0 으로 본 「반지름」. t=1 정수리
REF_BODY = [
    (0.800, 0.0425), (0.775, 0.0814), (0.750, 0.0970), (0.725, 0.1133),
    (0.700, 0.0538), (0.675, 0.0475), (0.650, 0.0517), (0.625, 0.0659),
    (0.600, 0.0779), (0.575, 0.0885), (0.550, 0.0942), (0.525, 0.0999),
]
REF_LEG = [
    (0.475, 0.0411), (0.450, 0.0407), (0.425, 0.0361), (0.400, 0.0351),
    (0.375, 0.0301), (0.350, 0.0280), (0.325, 0.0255), (0.300, 0.0273),
    (0.275, 0.0276), (0.250, 0.0297), (0.225, 0.0305), (0.200, 0.0276),
    (0.175, 0.0258), (0.150, 0.0248), (0.125, 0.0223), (0.100, 0.0188),
]

# ★ 참고 도면은 여자다. 남자에 그대로 씌우면 여자 몸이 된다 → 절반만 간다.
BLEND = {"female": 1.0, "male": 0.55}


def lerp_table(tab, t):
    if t >= tab[0][0]:
        return tab[0][1]
    if t <= tab[-1][0]:
        return tab[-1][1]
    for i in range(len(tab) - 1):
        t0, v0 = tab[i]
        t1, v1 = tab[i + 1]
        if t1 <= t <= t0:
            return v1 + (v0 - v1) * (t - t1) / (t0 - t1)
    return tab[-1][1]


def smooth(seq, k=3):
    out = []
    for i in range(len(seq)):
        vals = [v for v in seq[max(0, i - k):i + k + 1] if v is not None]
        out.append(sum(vals) / len(vals) if vals else None)
    return out


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def norm(o):
    """키 1.80 · 발바닥 z=0 · 좌우 가운데를 x=0 으로."""
    zs = [v.co.z for v in o.data.vertices]
    s = KEY / (max(zs) - min(zs))
    for v in o.data.vertices:
        v.co = v.co * s
    xs = [v.co.x for v in o.data.vertices]
    dx = 0.5 * (min(xs) + max(xs))
    dz = min(v.co.z for v in o.data.vertices)
    for v in o.data.vertices:
        v.co.x -= dx
        v.co.z -= dz
    o.data.update()


def prepare(gender):
    src = bpy.data.objects["GEO-body_%s_stylized" % gender]
    o = src.copy()
    o.data = src.data.copy()
    o.name = "hero_" + gender
    bpy.context.collection.objects.link(o)
    m = o.modifiers.new("unsub", 'DECIMATE')
    m.decimate_type = 'UNSUBDIV'
    m.iterations = ITERS
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.modifier_apply(modifier="unsub")
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    norm(o)
    return o


# ──────────────────────────────────────────────────────────────────────────
def sections(me):
    """겹마다 진짜 단면 고리들을 (xmin, xmax) 로 돌려준다. 면의 연결로 묶는다."""
    vs = me.vertices
    ekey = {}
    for e in me.edges:
        a, b = e.vertices
        ekey[(a, b) if a < b else (b, a)] = e.index

    # 겹마다 후보 모서리·면을 미리 담아 둔다 (매번 전부 훑지 않게)
    e_at = [[] for _ in range(SLICES)]
    f_at = [[] for _ in range(SLICES)]

    def span(zmin, zmax):
        a = max(0, int(zmin / KEY * SLICES) - 1)
        b = min(SLICES - 1, int(zmax / KEY * SLICES) + 1)
        return range(a, b + 1)

    for e in me.edges:
        z0 = vs[e.vertices[0]].co.z
        z1 = vs[e.vertices[1]].co.z
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
            a = vs[e.vertices[0]].co
            b = vs[e.vertices[1]].co
            if (a.z - z) * (b.z - z) <= 0.0 and abs(b.z - a.z) > 1e-9:
                f = (z - a.z) / (b.z - a.z)
                cut[ei] = a.x + (b.x - a.x) * f
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
            ids = []
            for (a, b) in me.polygons[fi].edge_keys:
                k = (a, b) if a < b else (b, a)
                ei = ekey.get(k)
                if ei in cut:
                    ids.append(ei)
            for j in ids[1:]:
                ra, rb = find(ids[0]), find(j)
                if ra != rb:
                    parent[rb] = ra

        groups = {}
        for i in cut:
            groups.setdefault(find(i), []).append(cut[i])
        out.append(sorted((min(g), max(g)) for g in groups.values()))
    return out


def classify(runs):
    """이 겹의 고리들을 몸통 / 다리 둘 / 나머지(팔) 로 가른다."""
    core = next((r for r in runs if r[0] <= 0.0 <= r[1]), None)
    if core:
        return core, []
    left = [r for r in runs if r[1] < 0.0]
    right = [r for r in runs if r[0] > 0.0]
    legs = []
    if left:
        legs.append(max(left, key=lambda r: r[1]))
    if right:
        legs.append(min(right, key=lambda r: r[0]))
    return None, legs


def retarget(o, gender):
    me = o.data
    sec = sections(me)
    blend = BLEND[gender]

    body_r, leg_r = [None] * SLICES, [None] * SLICES
    for s, runs in enumerate(sec):
        core, legs = classify(runs)
        if core:
            body_r[s] = 0.5 * (core[1] - core[0])
        elif legs:
            leg_r[s] = sum(0.5 * (b - a) for (a, b) in legs) / len(legs)
    body_r, leg_r = smooth(body_r), smooth(leg_r)

    neck_s, neck_v = None, 1e9
    for s in range(int(SLICES * 0.74), int(SLICES * 0.94)):
        if body_r[s] is not None and body_r[s] < neck_v:
            neck_s, neck_v = s, body_r[s]
    neck_z = (neck_s + 0.5) / SLICES * KEY
    crotch_s = next((s for s in range(int(SLICES * 0.62), int(SLICES * 0.30), -1)
                     if body_r[s] is None), int(SLICES * 0.45))
    crotch_z = (crotch_s + 0.5) / SLICES * KEY
    print("  %-6s 목 z=%.3f(반지름 %.4f) · 사타구니 z=%.3f" % (gender, neck_z, neck_v, crotch_z))
    print("         이 몸: 가슴 %.4f 골반 %.4f 허벅지 %.4f 종아리 %.4f" % (
        body_r[int(SLICES * 0.70)] or 0, body_r[int(SLICES * 0.55)] or 0,
        leg_r[int(SLICES * 0.45)] or 0, leg_r[int(SLICES * 0.25)] or 0))
    print("         참고  : 가슴 %.4f 골반 %.4f 허벅지 %.4f 종아리 %.4f" % (
        lerp_table(REF_BODY, 0.70) * KEY, lerp_table(REF_BODY, 0.55) * KEY,
        lerp_table(REF_LEG, 0.45) * KEY, lerp_table(REF_LEG, 0.25) * KEY))

    kb, kl = [1.0] * SLICES, [1.0] * SLICES
    for s in range(SLICES):
        t = (s + 0.5) / SLICES
        if body_r[s] and t < 0.82:
            kb[s] = 1.0 + (lerp_table(REF_BODY, t) * KEY / body_r[s] - 1.0) * blend
        if leg_r[s]:
            kl[s] = 1.0 + (lerp_table(REF_LEG, t) * KEY / leg_r[s] - 1.0) * blend
    kb, kl = smooth(kb, 4), smooth(kl, 4)
    print("         배율 : 가슴 %.3f 골반 %.3f 허벅지 %.3f 종아리 %.3f" % (
        kb[int(SLICES * 0.70)], kb[int(SLICES * 0.55)],
        kl[int(SLICES * 0.45)], kl[int(SLICES * 0.25)]))

    # ★ 경계를 딱 잘라 쓰면 안 된다 — 몸통은 줄이고 팔은 안 줄이면 어깨에서 면이 찢어진다.
    #   (한 번 그렇게 찢어졌다) → 가운데서 멀어질수록 배율을 1 로 풀어 준다.
    def ss(v, a, b):
        if abs(b - a) < 1e-9:
            return 0.0 if v < a else 1.0
        t = min(max((v - a) / (b - a), 0.0), 1.0)
        return t * t * (3.0 - 2.0 * t)

    for v in me.vertices:
        z = v.co.z
        if z >= neck_z:                       # 머리·목 — 어차피 잘라낸다
            continue
        s = min(max(int(z / KEY * SLICES), 0), SLICES - 1)
        core, legs = classify(sec[s])

        if z > crotch_z:
            if not core or not body_r[s]:
                continue
            cx = 0.5 * (core[0] + core[1])
            hw = body_r[s]
            k = kb[s]
            w = 1.0 - ss(abs(v.co.x - cx), hw * 0.95, hw * 1.75)   # 팔 쪽으로 풀림
        else:
            mine = min(legs, key=lambda r: abs(0.5 * (r[0] + r[1]) - v.co.x), default=None)
            if mine is None or not leg_r[s]:
                continue
            cx = 0.5 * (mine[0] + mine[1])
            lr = leg_r[s]
            k = kl[s]
            w = 1.0 - ss(abs(v.co.x - cx), lr * 0.95, lr * 1.9)    # 손 쪽으로 풀림
            w *= ss(z, 0.02, 0.11)                                  # 발가락에서 풀림

        w *= 1.0 - ss(z, neck_z - 0.10, neck_z)                     # 목에서 풀림
        ke = 1.0 + (k - 1.0) * w
        v.co.x = cx + (v.co.x - cx) * ke
        v.co.y = v.co.y * ke
    me.update()
    return neck_z


def behead(o, neck_z):
    """목에서 위를 잘라낸다. 잘린 자리는 열어 둔다 (새 머리를 붙일 자리)."""
    bm = bmesh.new()
    bm.from_mesh(o.data)
    kill = [f for f in bm.faces if sum(v.co.z for v in f.verts) / len(f.verts) > neck_z]
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bm.to_mesh(o.data)
    bm.free()
    o.data.update()


# ──────────────────────────────────────────────────────────────────────────
print("")
objs = []
for g in ("female", "male"):
    b = prepare(g)
    b.name = "before_" + g
    a = b.copy()
    a.data = b.data.copy()
    a.name = "after_" + g
    bpy.context.collection.objects.link(a)
    nz = retarget(a, g)
    behead(a, nz)
    print("         삼각형 전 %d → 후(머리 없음) %d" % (tris(b), tris(a)))
    objs += [b, a]

keep = set(objs)
for ob in list(bpy.data.objects):
    if ob.type == 'MESH' and ob not in keep:
        bpy.data.objects.remove(ob, do_unlink=True)
for (ob, x) in zip(objs, (-1.65, -0.55, 0.55, 1.65)):
    ob.location.x = x

# ★ 사람이 열어서 고칠 파일 — 와이어 사본을 만들기 **전에** 저장한다
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "hero_body.blend"))
print("=== hero_body.blend 저장 (몸 4개만) ===")

skin = bpy.data.materials.new("skin")
skin.use_nodes = False
skin.diffuse_color = (0.72, 0.72, 0.74, 1.0)
wire = bpy.data.materials.new("wire")
wire.use_nodes = False
wire.diffuse_color = (0.10, 0.10, 0.12, 1.0)
for ob in list(keep):
    ob.data.materials.clear()
    ob.data.materials.append(skin)
    for p in ob.data.polygons:
        p.use_smooth = True
    w = ob.copy()
    w.data = ob.data.copy()
    bpy.context.collection.objects.link(w)
    w.data.materials.clear()
    w.data.materials.append(wire)
    md = w.modifiers.new("Wireframe", 'WIREFRAME')
    md.thickness = 0.004
    md.use_replace = True

sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sc.render.resolution_x = 1300
sc.render.resolution_y = 800
sh = sc.display.shading
sh.light, sh.color_type = 'STUDIO', 'MATERIAL'
sh.show_shadows, sh.show_cavity = False, True
sh.background_type = 'VIEWPORT'
sh.background_color = (0.34, 0.34, 0.34)
sc.render.film_transparent = False
cd = bpy.data.cameras.new("cam")
cd.type = 'ORTHO'
cd.ortho_scale = 3.9
cam = bpy.data.objects.new("cam", cd)
bpy.context.collection.objects.link(cam)
sc.camera = cam
cam.location = (0.0, -6.0, KEY * 0.5)
cam.rotation_euler = (math.radians(90.0), 0.0, 0.0)
sc.render.filepath = os.path.join(OUT, "retarget_앞.png")
bpy.ops.render.render(write_still=True)
print("=== %s ===" % sc.render.filepath)
