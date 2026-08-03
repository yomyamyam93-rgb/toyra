# -*- coding: utf-8 -*-
"""아무 .blend 나 열어 속을 잰다 — 오브젝트·폴리·크기·모디파이어·이상한 곳.

    blender -b <파일.blend> --python modeling/peek.py
"""

import bpy
import bmesh

print("")
print("=== %s ===" % bpy.data.filepath)
for o in bpy.data.objects:
    mods = ",".join(m.type for m in o.modifiers) or "-"
    d = o.dimensions
    if o.type != 'MESH':
        print("[%s] %-22s  모디파이어 %s" % (o.type, o.name, mods))
        continue
    me = o.data
    tri = sum(len(p.vertices) - 2 for p in me.polygons)
    ngon = sum(1 for p in me.polygons if len(p.vertices) > 4)
    quad = sum(1 for p in me.polygons if len(p.vertices) == 4)

    bm = bmesh.new()
    bm.from_mesh(me)
    bound = sum(1 for e in bm.edges if len(e.link_faces) == 1)
    nonman = sum(1 for e in bm.edges if len(e.link_faces) > 2)
    loose = sum(1 for v in bm.verts if not v.link_faces)
    bm.free()

    zs = [v.co.z for v in me.vertices] or [0]
    xs = [v.co.x for v in me.vertices] or [0]
    print("[MESH] %-20s 정점 %5d  면 %5d (사각 %d, 5각이상 %d)  삼각형 %5d"
          % (o.name, len(me.vertices), len(me.polygons), quad, ngon, tri))
    print("       크기 %.3f x %.3f x %.3f m   z %.3f~%.3f   x %.3f~%.3f"
          % (d.x, d.y, d.z, min(zs), max(zs), min(xs), max(xs)))
    print("       스케일 (%.3f, %.3f, %.3f)   모디파이어 %s"
          % (o.scale.x, o.scale.y, o.scale.z, mods))
    print("       열린모서리 %d · 비다양체 %d · 뜬정점 %d · 셰이프키 %s"
          % (bound, nonman, loose, "있음" if me.shape_keys else "없음"))
