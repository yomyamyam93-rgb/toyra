# -*- coding: utf-8 -*-
"""번들의 스타일라이즈드 머리(GEO-head_stylized)만 꺼내 세 각도로 본다.

    blender -b <번들.blend> --python modeling/peek_head.py
"""

import bpy
import math
import os

OUT = r"C:\Users\ysim1\Documents\GitHub\toyra\modeling\preview"
os.makedirs(OUT, exist_ok=True)

keep = []
for name in ("GEO-head_stylized", "GEO-head_stylized.eye.L", "GEO-head_stylized.eye.R"):
    o = bpy.data.objects.get(name)
    if o:
        keep.append(o)
for ob in list(bpy.data.objects):
    if ob not in keep:
        bpy.data.objects.remove(ob, do_unlink=True)

head = keep[0]
d = head.dimensions
print("=== 머리 크기 %.3f x %.3f x %.3f · 면 %d ===" %
      (d.x, d.y, d.z, len(head.data.polygons)))

# 눈에 보이게 재질
skin = bpy.data.materials.new("skin")
skin.use_nodes = False
skin.diffuse_color = (0.80, 0.72, 0.66, 1.0)
eye = bpy.data.materials.new("eye")
eye.use_nodes = False
eye.diffuse_color = (0.15, 0.13, 0.12, 1.0)
for o in keep:
    o.data.materials.clear()
    o.data.materials.append(eye if ".eye" in o.name else skin)
    for p in o.data.polygons:
        p.use_smooth = True

import mathutils
bb = [head.matrix_world @ mathutils.Vector(c) for c in head.bound_box]
cx_ = sum(v.x for v in bb) / 8
cy = sum(v.y for v in bb) / 8
cz = sum(v.z for v in bb) / 8
print("=== 머리 중심 (%.2f, %.2f, %.2f) ===" % (cx_, cy, cz))

sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sh = sc.display.shading
sh.light, sh.color_type = 'STUDIO', 'MATERIAL'
sh.show_shadows, sh.show_cavity = False, True
sh.background_type = 'VIEWPORT'
sh.background_color = (0.34, 0.34, 0.34)
sc.render.film_transparent = False
sc.render.resolution_x = sc.render.resolution_y = 760
cd = bpy.data.cameras.new("cam")
cd.type = 'ORTHO'
cd.ortho_scale = max(d.x, d.z) * 1.35
cam = bpy.data.objects.new("cam", cd)
bpy.context.collection.objects.link(cam)
sc.camera = cam

for (name, yaw) in (("앞", 0.0), ("비스듬", 32.0), ("옆", 90.0)):
    yr = math.radians(yaw)
    cam.location = (cx_ + 6 * math.sin(yr), cy - 6 * math.cos(yr), cz)
    cam.rotation_euler = (math.radians(90), 0.0, yr)
    sc.render.filepath = os.path.join(OUT, "head_%s.png" % name)
    bpy.ops.render.render(write_still=True)
    print("=== %s ===" % sc.render.filepath)
