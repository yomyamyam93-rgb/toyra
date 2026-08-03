# -*- coding: utf-8 -*-
"""hero.blend 를 열어 앞·옆·비스듬 세 장을 뽑는다 (와이어 얹은 클레이).

    blender -b modeling/hero.blend --python modeling/preview.py

★ 유니티에 넣기 전에 비율을 눈으로 보려는 용도다. 숫자를 고칠 때마다
  hero.py → preview.py 를 연달아 돌리면 몇 초 만에 다시 본다.
"""

import bpy
import math
import os

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "preview")
os.makedirs(OUT, exist_ok=True)

KEY = 1.80

# ── 재질 두 개: 살(밝은 회색) · 와이어(어두운 선)
skin = bpy.data.materials.new("skin")
skin.use_nodes = False
skin.diffuse_color = (0.72, 0.72, 0.74, 1.0)

wire = bpy.data.materials.new("wire")
wire.use_nodes = False
wire.diffuse_color = (0.10, 0.10, 0.12, 1.0)

meshes = [o for o in bpy.data.objects if o.type == 'MESH']
for o in meshes:
    o.data.materials.clear()
    o.data.materials.append(skin)

    # 와이어는 복제본에 Wireframe 모디파이어를 걸어서 낸다 (헤드리스 렌더는 오버레이가 안 나온다)
    w = o.copy()
    w.data = o.data.copy()
    w.name = o.name + "_wire"
    bpy.context.collection.objects.link(w)
    w.data.materials.clear()
    w.data.materials.append(wire)
    m = w.modifiers.new("Wireframe", 'WIREFRAME')
    m.thickness = 0.005
    m.use_replace = True
    m.use_even_offset = False

# ── 렌더 설정: 워크벤치 클레이
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sc.render.resolution_x = 620
sc.render.resolution_y = 1000
sc.render.film_transparent = False
sh = sc.display.shading
sh.light = 'STUDIO'
sh.color_type = 'MATERIAL'
sh.show_shadows = False
sh.show_cavity = True

world = bpy.data.worlds.new("w")
world.use_nodes = False
world.color = (0.34, 0.34, 0.34)
sc.world = world

# ── 카메라: 직교. 캐릭터가 세로를 가득 채우게
cam_data = bpy.data.cameras.new("cam")
cam_data.type = 'ORTHO'
cam_data.ortho_scale = KEY * 1.10
cam = bpy.data.objects.new("cam", cam_data)
bpy.context.collection.objects.link(cam)
sc.camera = cam

CENTER = (0.0, 0.0, KEY * 0.5)
DIST = 6.0

VIEWS = [
    ("앞", 180.0, 0.0),      # -Y 쪽에서 본다 (캐릭터가 보는 방향)
    ("옆", 90.0, 0.0),
    ("비스듬", 145.0, 14.0),
]

for (name, yaw_deg, pitch_deg) in VIEWS:
    yaw = math.radians(yaw_deg)
    pitch = math.radians(pitch_deg)
    cam.location = (
        CENTER[0] + DIST * math.cos(pitch) * math.sin(yaw),
        CENTER[1] - DIST * math.cos(pitch) * math.cos(yaw),
        CENTER[2] + DIST * math.sin(pitch),
    )
    cam.rotation_euler = (math.radians(90.0) - pitch, 0.0, yaw)
    sc.render.filepath = os.path.join(OUT, "hero_%s.png" % name)
    bpy.ops.render.render(write_still=True)
    print("=== %s ===" % sc.render.filepath)
