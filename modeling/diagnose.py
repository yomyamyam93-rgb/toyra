# -*- coding: utf-8 -*-
"""구멍·비다양체가 「어디」 있는지 찾는다. QuadriFlow 가 왜 취소됐는지 알아내려는 것.

    blender -b --python modeling/diagnose.py
"""

import bpy
import bmesh
import os

HERE = r"C:\Users\ysim1\Documents\GitHub\toyra\modeling"
KEY = 1.80


def load(gender):
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    with bpy.data.libraries.load(os.path.join(HERE, "%s.blend" % gender)) as (s, d):
        d.objects = [n for n in s.objects]
    obj = None
    for ob in d.objects:
        if ob and ob.type == 'MESH':
            bpy.context.collection.objects.link(ob)
            obj = ob
    return obj


def groups(edges):
    """이어진 모서리들을 덩어리로 묶어 위치·크기를 돌려준다."""
    seen, out = set(), []
    eset = set(e.index for e in edges)
    emap = {e.index: e for e in edges}
    for ei in eset:
        if ei in seen:
            continue
        stack, comp = [ei], []
        seen.add(ei)
        while stack:
            cur = emap[stack.pop()]
            comp.append(cur)
            for v in cur.verts:
                for e2 in v.link_edges:
                    if e2.index in eset and e2.index not in seen:
                        seen.add(e2.index)
                        stack.append(e2.index)
        pts = [v.co for e in comp for v in e.verts]
        out.append((len(comp),
                    sum(p.x for p in pts) / len(pts),
                    sum(p.y for p in pts) / len(pts),
                    sum(p.z for p in pts) / len(pts)))
    return out


for gender in ("woman", "man"):
    obj = load(gender)
    zs = [v.co.z for v in obj.data.vertices]
    H = max(zs) - min(zs)
    z0 = min(zs)

    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.edges.ensure_lookup_table()

    bound = [e for e in bm.edges if len(e.link_faces) == 1]
    nonman = [e for e in bm.edges if len(e.link_faces) > 2]

    # 같은 정점 집합을 가진 면(중복면) 찾기
    seenf, dupes = {}, 0
    for f in bm.faces:
        k = tuple(sorted(v.index for v in f.verts))
        if k in seenf:
            dupes += 1
        seenf[k] = 1

    print("")
    print("=== %s : 구멍모서리 %d · 비다양체 %d · 중복면 %d ==="
          % (gender, len(bound), len(nonman), dupes))
    for tag, es in (("구멍", bound), ("비다양체", nonman)):
        for (n, x, y, z) in groups(es):
            print("   %-6s 모서리 %2d개  위치 (%.3f, %.3f, %.3f)  키의 %.0f%% 높이"
                  % (tag, n, x, y, z, (z - z0) / H * 100))
    bm.free()
