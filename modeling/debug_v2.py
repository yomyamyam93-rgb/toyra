# -*- coding: utf-8 -*-
"""hero_v2.blend 에서 제일 큰 면 5개를 찾는다 — 화면을 덮은 판의 정체 조사.

    blender -b modeling/hero_v2.blend --python modeling/debug_v2.py
"""

import bpy

for ob in bpy.data.objects:
    if ob.type != 'MESH':
        continue
    me = ob.data
    areas = sorted(((p.area, p.index, len(p.vertices)) for p in me.polygons),
                   reverse=True)[:5]
    d = ob.dimensions
    print("")
    print("%-12s  면 %5d  크기 %.2f x %.2f x %.2f" %
          (ob.name, len(me.polygons), d.x, d.y, d.z))
    for (a, idx, nv) in areas:
        p = me.polygons[idx]
        vs = [me.vertices[i].co for i in p.vertices]
        lo = min(v.z for v in vs)
        hi = max(v.z for v in vs)
        print("   면적 %.4f m2  꼭짓점 %d개  z %.3f~%.3f  첫점 (%.3f, %.3f, %.3f)" %
              (a, nv, lo, hi, vs[0].x, vs[0].y, vs[0].z))
