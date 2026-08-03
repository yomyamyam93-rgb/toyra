# -*- coding: utf-8 -*-
"""man/woman 수리·크기 맞추기 → 뼈를 앉힐 자리를 메시에서 실측한다.

    blender -b --python modeling/landmarks.py

리토폴로지는 안 한다 (사용자 확정 — 잔 디테일이 뭉개진다). 원본 삼각형 그대로 간다.

★ 단면은 「면의 연결」로 고리를 찾는다. x 좌표 간격으로 가르면 조각난다 (두 번 터졌다).
★ A자세라 팔이 비스듬히 내려와 있다 — 팔 축을 겹마다 추적해서 실측한다.
"""

import bpy
import bmesh
import os
import json

HERE = r"C:\Users\ysim1\Documents\GitHub\toyra\modeling"
KEY = 1.80
SLICES = 240
SEAM = 0.0015


def repair(o):
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    for v in bm.verts:
        if abs(v.co.x) < SEAM:
            v.co.x = 0.0
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=SEAM * 0.9)
    bmesh.ops.dissolve_degenerate(bm, dist=1e-6, edges=bm.edges[:])
    wall = [f for f in bm.faces if all(abs(v.co.x) < 1e-6 for v in f.verts)]
    if wall:
        bmesh.ops.delete(bm, geom=wall, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bmesh.ops.holes_fill(bm, edges=bm.edges[:], sides=8)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(o.data)
    bm.free()
    o.data.update()


def fit(o):
    vs = o.data.vertices
    zs = [v.co.z for v in vs]
    s = KEY / (max(zs) - min(zs))
    for v in vs:
        v.co = v.co * s
    xs = [v.co.x for v in vs]
    dx = 0.5 * (min(xs) + max(xs))
    dz = min(v.co.z for v in vs)
    for v in vs:
        v.co.x -= dx
        v.co.z -= dz
    o.data.update()


def sections(me):
    """겹마다 진짜 단면 고리들을 (xmin, xmax, ymin, ymax) 로."""
    vs = me.vertices
    ekey = {}
    for e in me.edges:
        a, b = e.vertices
        ekey[(a, b) if a < b else (b, a)] = e.index

    e_at = [[] for _ in range(SLICES)]
    f_at = [[] for _ in range(SLICES)]

    def span(lo, hi):
        a = max(0, int(lo / KEY * SLICES) - 1)
        b = min(SLICES - 1, int(hi / KEY * SLICES) + 1)
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
        groups = {}
        for i in cut:
            groups.setdefault(find(i), []).append(cut[i])
        rings = []
        for g in groups.values():
            xs = [p[0] for p in g]
            ys = [p[1] for p in g]
            rings.append((min(xs), max(xs), min(ys), max(ys)))
        out.append(sorted(rings))
    return out


def core_of(rings):
    return next((r for r in rings if r[0] <= 0.0 <= r[1]), None)


def sides_of(rings):
    """중심을 안 품은 고리들을 좌/우로."""
    left = [r for r in rings if r[1] < 0.0]
    right = [r for r in rings if r[0] > 0.0]
    return left, right


report = {}
for gender in ("woman", "man"):
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    with bpy.data.libraries.load(os.path.join(HERE, "%s.blend" % gender)) as (s_, d_):
        d_.objects = [n for n in s_.objects]
    obj = None
    for ob in d_.objects:
        if ob and ob.type == 'MESH':
            bpy.context.collection.objects.link(ob)
            obj = ob
    obj.name = obj.data.name = "hero_" + gender
    repair(obj)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    fit(obj)

    me = obj.data
    sec = sections(me)

    # ── 앞이 어느 쪽인가: 발(아래 8%)이 더 길게 뻗은 방향
    foot = [v.co for v in me.vertices if v.co.z < 0.14]
    fy = [p.y for p in foot]
    body_y = [v.co.y for v in me.vertices if 0.5 < v.co.z < 1.2]
    cy_body = 0.5 * (min(body_y) + max(body_y))
    front = -1.0 if abs(min(fy) - cy_body) > abs(max(fy) - cy_body) else 1.0

    # ── 목: 위쪽에서 제일 잘록한 몸통 고리
    neck_s, neck_w = None, 9e9
    for s in range(int(SLICES * 0.72), int(SLICES * 0.95)):
        c = core_of(sec[s]) if sec[s] else None
        if c and (c[1] - c[0]) < neck_w:
            neck_s, neck_w = s, c[1] - c[0]
    neck_z = (neck_s + 0.5) / SLICES * KEY

    # ── 사타구니: 몸통 고리가 끊기는 곳
    crotch_s = next((s for s in range(int(SLICES * 0.62), int(SLICES * 0.30), -1)
                     if not core_of(sec[s] or [])), int(SLICES * 0.45))
    crotch_z = (crotch_s + 0.5) / SLICES * KEY

    # ── 어깨: 팔 고리가 몸통에서 떨어져 나오기 시작하는 높이
    sh_s = None
    for s in range(int(SLICES * 0.90), int(SLICES * 0.55), -1):
        L, R = sides_of(sec[s] or [])
        if L and R and core_of(sec[s]):
            sh_s = s
            break
    sh_z = (sh_s + 0.5) / SLICES * KEY if sh_s else 0.80 * KEY

    # ── 팔 축: 어깨 아래로 내려가며 왼쪽 곁고리의 중심을 따라간다
    arm = []
    for s in range(sh_s or 0, int(SLICES * 0.30), -1):
        L, _ = sides_of(sec[s] or [])
        if not L:
            continue
        r = min(L, key=lambda r: r[0])          # 제일 바깥 = 팔
        arm.append(((s + 0.5) / SLICES * KEY,
                    0.5 * (r[0] + r[1]), 0.5 * (r[2] + r[3]),
                    0.5 * (r[1] - r[0])))
    wrist = arm[-1] if arm else None

    # ── 다리 축: 사타구니 아래 안쪽 고리
    leg = []
    for s in range(crotch_s, 0, -1):
        L, _ = sides_of(sec[s] or [])
        if not L:
            continue
        r = max(L, key=lambda r: r[1])          # 안쪽 = 다리
        leg.append(((s + 0.5) / SLICES * KEY,
                    0.5 * (r[0] + r[1]), 0.5 * (r[2] + r[3]),
                    0.5 * (r[1] - r[0])))

    print("")
    print("=== %s ===" % gender)
    print("  앞 방향  %+d y   (발이 뻗은 쪽)" % front)
    print("  머리끝 z=%.3f · 목 z=%.3f(폭 %.3f) · 어깨 z=%.3f · 사타구니 z=%.3f"
          % (KEY, neck_z, neck_w, sh_z, crotch_z))
    if arm:
        print("  팔  어깨 (x %.3f, y %.3f, z %.3f) → 손목 (x %.3f, y %.3f, z %.3f)  길이 %.3f"
              % (arm[0][1], arm[0][2], arm[0][0], wrist[1], wrist[2], wrist[0],
                 ((arm[0][1] - wrist[1]) ** 2 + (arm[0][0] - wrist[0]) ** 2) ** 0.5))
        print("  팔 반지름  뿌리 %.3f → 손목 %.3f" % (arm[0][3], wrist[3]))
    if leg:
        print("  다리 고관절 (x %.3f, z %.3f) → 발목 (x %.3f, z %.3f)"
              % (leg[0][1], leg[0][0], leg[-1][1], leg[-1][0]))
        print("  다리 반지름  허벅지 %.3f → 발목 %.3f" % (leg[0][3], leg[-1][3]))
    print("  정점 %d · 면 %d · 삼각형 %d"
          % (len(me.vertices), len(me.polygons),
             sum(len(p.vertices) - 2 for p in me.polygons)))

    report[gender] = dict(front=front, neck_z=neck_z, sh_z=sh_z, crotch_z=crotch_z,
                          arm=arm, leg=leg)

    for ob in list(bpy.data.objects):
        if ob is not obj:
            bpy.data.objects.remove(ob, do_unlink=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "hero_clean_%s.blend" % gender))
    print("=== hero_clean_%s.blend 저장 (수리+크기, 원본 토폴로지) ===" % gender)

with open(os.path.join(HERE, "landmarks.json"), "w", encoding="utf-8") as f:
    json.dump(report, f)
print("")
print("=== landmarks.json 저장 ===")
