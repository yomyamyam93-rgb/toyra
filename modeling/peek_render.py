# -*- coding: utf-8 -*-
"""아무 .blend 의 메시를 앞·비스듬·옆 세 각도로 뽑는다.

    blender -b <파일.blend> --python modeling/peek_render.py -- <이름>
"""

import bpy
import math
import os
import sys

name = sys.argv[-1] if "--" in sys.argv else "peek"
OUT = r"C:\Users\ysim1\Documents\GitHub\toyra\modeling\preview"
os.makedirs(OUT, exist_ok=True)

meshes = [o for o in bpy.data.objects if o.type == 'MESH']
for ob in list(bpy.data.objects):
    if ob.type != 'MESH':
        bpy.data.objects.remove(ob, do_unlink=True)

zs, xs, ys = [], [], []
for o in meshes:
    for v in o.data.vertices:
        w = o.matrix_world @ v.co
        xs.append(w.x)
        ys.append(w.y)
        zs.append(w.z)
cx, cy = 0.5 * (min(xs) + max(xs)), 0.5 * (min(ys) + max(ys))
cz = 0.5 * (min(zs) + max(zs))
H = max(zs) - min(zs)

skin = bpy.data.materials.new("skin")
skin.use_nodes = False
skin.diffuse_color = (0.74, 0.71, 0.70, 1.0)
wirem = bpy.data.materials.new("wire")
wirem.use_nodes = False
wirem.diffuse_color = (0.10, 0.10, 0.12, 1.0)
for o in meshes:
    o.data.materials.clear()
    o.data.materials.append(skin)
    for p in o.data.polygons:
        p.use_smooth = True
    w = o.copy()
    w.data = o.data.copy()
    bpy.context.collection.objects.link(w)
    w.data.materials.clear()
    w.data.materials.append(wirem)
    md = w.modifiers.new("wf", 'WIREFRAME')
    md.thickness = H * 0.0022
    md.use_replace = True

sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sh = sc.display.shading
sh.light, sh.color_type = 'STUDIO', 'MATERIAL'
sh.show_shadows, sh.show_cavity = False, True
sh.background_type = 'VIEWPORT'
sh.background_color = (0.34, 0.34, 0.34)
sc.render.film_transparent = False
sc.render.resolution_x, sc.render.resolution_y = 620, 900
cd = bpy.data.cameras.new("cam")
cd.type = 'ORTHO'
cd.ortho_scale = H * 1.12
cam = bpy.data.objects.new("cam", cd)
bpy.context.collection.objects.link(cam)
sc.camera = cam

for (tag, yaw, pitch) in (("앞", 0.0, 0.0), ("비스듬", 32.0, 8.0), ("옆", 90.0, 0.0)):
    yr, pr = math.radians(yaw), math.radians(pitch)
    d = H * 5.0
    cam.location = (cx + d * math.cos(pr) * math.sin(yr),
                    cy - d * math.cos(pr) * math.cos(yr),
                    cz + d * math.sin(pr))
    cam.rotation_euler = (math.radians(90) - pr, 0.0, yr)
    sc.render.filepath = os.path.join(OUT, "%s_%s.png" % (name, tag))
    bpy.ops.render.render(write_still=True)
    print("=== %s ===" % sc.render.filepath)
