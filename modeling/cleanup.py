# -*- coding: utf-8 -*-
"""man/woman 정리 + 크기 맞추기 + 자동 리토폴로지 비교.

    blender -b --python modeling/cleanup.py

★ 문제는 전부 미러 이음새(x=0)에 있었다 (diagnose.py 실측):
  가운데 정점이 두 겹으로 남았고, 이음새에 얇은 면이 끼어 비다양체가 됐다.
  → 이음새 정점을 x=0 으로 스냅 → 겹친 것 병합 → 이음새에 갇힌 면 제거 → 구멍 메움

★ QuadriFlow 는 메시가 닫혀 있지 않으면 **조용히 취소된다** (예외도 안 던진다).
  그래서 반환값을 찍고, 면 수가 실제로 변했는지 확인한다. (한 번 속았다)

★ 「훼손 안 했나」는 숫자로 잰다 — 결과 정점이 원래 표면에서 몇 mm 떨어졌나.
"""

import bpy
import bmesh
import math
import os
from mathutils.bvhtree import BVHTree

HERE = r"C:\Users\ysim1\Documents\GitHub\toyra\modeling"
OUT = os.path.join(HERE, "preview")
os.makedirs(OUT, exist_ok=True)
KEY = 1.80
QUAD_TARGETS = [1600, 3200]
SEAM = 0.0015          # 이 안쪽이면 이음새로 본다 (모델 키 1m 기준 1.5mm)


def stats(me):
    bm = bmesh.new()
    bm.from_mesh(me)
    nonman = sum(1 for e in bm.edges if len(e.link_faces) > 2)
    bound = sum(1 for e in bm.edges if len(e.link_faces) == 1)
    loose = sum(1 for v in bm.verts if not v.link_faces)
    bm.free()
    return dict(v=len(me.vertices), f=len(me.polygons),
                quad=sum(1 for p in me.polygons if len(p.vertices) == 4),
                tri=sum(len(p.vertices) - 2 for p in me.polygons),
                nonman=nonman, bound=bound, loose=loose)


def line(tag, s):
    print("    %-12s 정점 %4d  면 %4d (사각 %4d)  삼각형 %4d  "
          "비다양체 %d · 구멍 %d · 뜬점 %d"
          % (tag, s["v"], s["f"], s["quad"], s["tri"],
             s["nonman"], s["bound"], s["loose"]))


def repair(o):
    """미러 이음새 수리."""
    bm = bmesh.new()
    bm.from_mesh(o.data)

    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces],
                     context='VERTS')
    for v in bm.verts:                       # 이음새 정점을 정확히 x=0 으로
        if abs(v.co.x) < SEAM:
            v.co.x = 0.0
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=SEAM * 0.9)
    bmesh.ops.dissolve_degenerate(bm, dist=1e-6, edges=bm.edges[:])

    # 이음새 평면에 통째로 누워 있는 면 = 안쪽 칸막이 → 버린다
    wall = [f for f in bm.faces if all(abs(v.co.x) < 1e-6 for v in f.verts)]
    if wall:
        bmesh.ops.delete(bm, geom=wall, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces],
                     context='VERTS')

    bmesh.ops.holes_fill(bm, edges=bm.edges[:], sides=8)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(o.data)
    bm.free()
    o.data.update()
    return len(wall)


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
    return s


def deviation(src_me, dst_me):
    tris = []
    for p in src_me.polygons:
        ids = list(p.vertices)
        for k in range(1, len(ids) - 1):
            tris.append((ids[0], ids[k], ids[k + 1]))
    bvh = BVHTree.FromPolygons([v.co.copy() for v in src_me.vertices], tris)
    ds = []
    for v in dst_me.vertices:
        hit = bvh.find_nearest(v.co)
        if hit and hit[3] is not None:
            ds.append(hit[3])
    return (sum(ds) / len(ds) * 1000.0, max(ds) * 1000.0) if ds else (0.0, 0.0)


print("")
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
    obj.name = "hero_" + gender
    obj.data.name = "hero_" + gender

    print("  [%s]" % gender)
    line("받은 그대로", stats(obj.data))
    nwall = repair(obj)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    sc_ = fit(obj)
    line("수리+크기", stats(obj.data))
    print("                 이음새 칸막이 면 %d개 제거 · %.3f배 → 키 %.3fm"
          % (nwall, sc_, KEY))

    variants = [("원본", obj)]
    for tgt in QUAD_TARGETS:
        q = obj.copy()
        q.data = obj.data.copy()
        q.name = "%s_quad%d" % (gender, tgt)
        bpy.context.collection.objects.link(q)
        bpy.ops.object.select_all(action='DESELECT')
        q.select_set(True)
        bpy.context.view_layer.objects.active = q
        before = len(q.data.polygons)
        try:
            r = bpy.ops.object.quadriflow_remesh(
                mode='FACES', target_faces=tgt, use_mesh_symmetry=True,
                use_preserve_sharp=False, use_preserve_boundary=False,
                smooth_normals=True, seed=0)
        except Exception as e:
            r = "예외: %s" % e
        after = len(q.data.polygons)
        if after == before:
            print("    리토 %-5d 실패 — 반환 %s (면 %d 그대로)" % (tgt, r, after))
            bpy.data.objects.remove(q, do_unlink=True)
            continue
        line("리토 %d" % tgt, stats(q.data))
        avg, mx = deviation(obj.data, q.data)
        print("                 원래 표면에서 평균 %.2fmm · 최대 %.2fmm 벗어남" % (avg, mx))
        variants.append(("리토%d" % tgt, q))

    for i, (tag, ob) in enumerate(variants):
        ob.location.x = (i - (len(variants) - 1) / 2.0) * 0.95
    for ob in list(bpy.data.objects):
        if ob.type == 'MESH' and ob not in [v[1] for v in variants]:
            bpy.data.objects.remove(ob, do_unlink=True)

    skin = bpy.data.materials.new("skin")
    skin.use_nodes = False
    skin.diffuse_color = (0.74, 0.71, 0.70, 1.0)
    wirem = bpy.data.materials.new("wire")
    wirem.use_nodes = False
    wirem.diffuse_color = (0.10, 0.10, 0.12, 1.0)
    for (tag, ob) in variants:
        ob.data.materials.clear()
        ob.data.materials.append(skin)
        for p in ob.data.polygons:
            p.use_smooth = True
        w = ob.copy()
        w.data = ob.data.copy()
        bpy.context.collection.objects.link(w)
        w.data.materials.clear()
        w.data.materials.append(wirem)
        md = w.modifiers.new("wf", 'WIREFRAME')
        md.thickness = 0.0035
        md.use_replace = True

    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading
    sh.light, sh.color_type = 'STUDIO', 'MATERIAL'
    sh.show_shadows, sh.show_cavity = False, True
    sh.background_type = 'VIEWPORT'
    sh.background_color = (0.34, 0.34, 0.34)
    sc.render.film_transparent = False
    cd = bpy.data.cameras.new("cam")
    cd.type = 'ORTHO'
    cam = bpy.data.objects.new("cam", cd)
    bpy.context.collection.objects.link(cam)
    sc.camera = cam

    def shot(tag, yaw, pitch, oscale, cz, res):
        sc.render.resolution_x, sc.render.resolution_y = res
        cd.ortho_scale = oscale
        yr, pr = math.radians(yaw), math.radians(pitch)
        d = 8.0
        cam.location = (d * math.cos(pr) * math.sin(yr),
                        -d * math.cos(pr) * math.cos(yr), cz + d * math.sin(pr))
        cam.rotation_euler = (math.radians(90) - pr, 0.0, yr)
        sc.render.filepath = os.path.join(OUT, "리토_%s_%s.png" % (gender, tag))
        bpy.ops.render.render(write_still=True)
        print("=== %s ===" % sc.render.filepath)

    shot("나란히", 24, 8, 3.0, 0.92, (1400, 880))
    shot("상반신", 24, 6, 1.15, 1.42, (1400, 620))

    bpy.ops.wm.save_as_mainfile(
        filepath=os.path.join(HERE, "hero_clean_%s.blend" % gender))
    print("=== hero_clean_%s.blend 저장 ===" % gender)
