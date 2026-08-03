# -*- coding: utf-8 -*-
"""뼈가 몸 안 어디에 박혀 있는지 그림으로 뽑는다 + 계층·좌표를 찍는다.

    blender -b modeling/hero_rig_<성별>.blend --python modeling/show_rig.py -- <성별>

헤드리스 렌더는 뼈(아마추어)를 안 그린다 → 뼈마다 팔면체 메시를 만들어 대신 그린다.
몸은 와이어로만 그려서 속이 비쳐 보이게 한다.
"""

import bpy
import bmesh
import math
import os
import sys
from mathutils import Vector

gender = sys.argv[-1] if "--" in sys.argv else "woman"
OUT = r"C:\Users\ysim1\Documents\GitHub\toyra\modeling\preview"
os.makedirs(OUT, exist_ok=True)

arm_obj = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
body = next(o for o in bpy.data.objects if o.type == 'MESH')

# ── 계층·좌표 찍기
print("")
print("=== %s 뼈대 %d개 ===" % (gender, len(arm_obj.data.bones)))


def dump(b, depth=0):
    h, t = b.head_local, b.tail_local
    print("  %s%-14s 머리(%+.3f, %+.3f, %.3f) → 꼬리(%+.3f, %+.3f, %.3f)  길이 %.3f"
          % ("  " * depth, b.name, h.x, h.y, h.z, t.x, t.y, t.z, b.length))
    for c in b.children:
        dump(c, depth + 1)


for b in arm_obj.data.bones:
    if b.parent is None:
        dump(b)

# ── 뼈를 팔면체 메시로
bm = bmesh.new()
for b in arm_obj.data.bones:
    h, t = b.head_local.copy(), b.tail_local.copy()
    axis = (t - h)
    L = axis.length
    if L < 1e-5:
        continue
    axis.normalize()
    up = Vector((0, 0, 1)) if abs(axis.z) < 0.9 else Vector((1, 0, 0))
    u = axis.cross(up).normalized()
    v = axis.cross(u).normalized()
    r = max(min(L * 0.14, 0.030), 0.008)
    mid = h + axis * (L * 0.22)
    ring = [bm.verts.new(mid + u * r), bm.verts.new(mid + v * r),
            bm.verts.new(mid - u * r), bm.verts.new(mid - v * r)]
    vh = bm.verts.new(h)
    vt = bm.verts.new(t)
    for k in range(4):
        a, c = ring[k], ring[(k + 1) % 4]
        bm.faces.new((vh, c, a))
        bm.faces.new((vt, a, c))
bm.normal_update()
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bmesh_me = bpy.data.meshes.new("bones")
bm.to_mesh(bmesh_me)
bm.free()
bones_obj = bpy.data.objects.new("bones", bmesh_me)
bpy.context.collection.objects.link(bones_obj)

# ── 재질: 뼈는 주황, 몸은 와이어만
bonem = bpy.data.materials.new("bone")
bonem.use_nodes = False
bonem.diffuse_color = (1.0, 0.52, 0.10, 1.0)
bones_obj.data.materials.append(bonem)

wirem = bpy.data.materials.new("wire")
wirem.use_nodes = False
wirem.diffuse_color = (0.16, 0.17, 0.20, 1.0)
body.data.materials.clear()
body.data.materials.append(wirem)
md = body.modifiers.new("wf", 'WIREFRAME')
md.thickness = 0.004
md.use_replace = True

sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sh = sc.display.shading
sh.light, sh.color_type = 'FLAT', 'MATERIAL'
sh.show_shadows, sh.show_cavity = False, False
sh.background_type = 'VIEWPORT'
sh.background_color = (0.88, 0.88, 0.89)
sc.render.film_transparent = False
sc.render.resolution_x, sc.render.resolution_y = 700, 980
cd = bpy.data.cameras.new("cam")
cd.type = 'ORTHO'
cd.ortho_scale = 2.0
cam = bpy.data.objects.new("cam", cd)
bpy.context.collection.objects.link(cam)
sc.camera = cam

for (tag, yaw) in (("앞", 0.0), ("옆", 90.0)):
    yr = math.radians(yaw)
    cam.location = (6 * math.sin(yr), -6 * math.cos(yr), 0.92)
    cam.rotation_euler = (math.radians(90), 0.0, yr)
    sc.render.filepath = os.path.join(OUT, "뼈대_%s_%s.png" % (gender, tag))
    bpy.ops.render.render(write_still=True)
    print("=== %s ===" % sc.render.filepath)
