# -*- coding: utf-8 -*-
"""받은 Human Base Meshes 번들에서 「전신」만 골라 잰다 (폴리 수·키).

    blender -b <번들.blend> --python modeling/inspect_hbm.py
"""

import bpy

KEEP = ("body", "Body", "head", "Head", "hand", "Hand", "foot", "Foot")
SKIP = ("primitive_", "GEO-toe", "GEO-finger")

print("")
print("%-38s %7s %7s %7s   크기 x/y/z (m)" % ("이름", "정점", "면", "삼각형"))
print("-" * 92)
rows = []
for o in bpy.data.objects:
    if o.type != 'MESH':
        continue
    n = o.name
    if any(s in n for s in SKIP):
        continue
    if not any(k in n for k in KEEP):
        continue
    me = o.data
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    d = o.dimensions
    rows.append((n, len(me.vertices), len(me.polygons), tris, d))

for (n, v, f, t, d) in sorted(rows):
    print("%-38s %7d %7d %7d   %.2f / %.2f / %.2f" % (n, v, f, t, d.x, d.y, d.z))
print("")
print("추린 것 %d 개 (전체 메시 %d)" % (
    len(rows), len([o for o in bpy.data.objects if o.type == 'MESH'])))
