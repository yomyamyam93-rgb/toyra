# -*- coding: utf-8 -*-
"""번들의 스타일라이즈드 남/녀 전신을 「언서브디바이드」로 줄여 보고 그림을 뽑는다.

    blender -b <번들.blend> --python modeling/reduce_test.py

★ 데시메이트(면 붕괴)가 아니라 언서브디바이드다. 이 메시들은 사각형을 쪼개서
  만든 것이라, 되감으면 아티스트가 잡은 사각형 뼈대가 돌아온다.
  리깅에 필요한 엣지 루프(무릎·팔꿈치·어깨)가 살아남는 게 핵심이다.
"""

import bpy
import math
import os

HERE = r"C:\Users\ysim1\Documents\GitHub\toyra\modeling"
OUT = os.path.join(HERE, "preview")
os.makedirs(OUT, exist_ok=True)

KEY = 1.80          # 둘 다 이 키로 맞춘다 (Hero.height)
ITERS = 2           # 되감기 횟수


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


bodies = {}
for gender in ("female", "male"):
    src = bpy.data.objects.get("GEO-body_%s_stylized" % gender)
    print("원본 %s: 정점 %d  면 %d  삼각형 %d  키 %.3f" %
          (gender, len(src.data.vertices), len(src.data.polygons), tris(src), src.dimensions.z))

    # 되감기 횟수별로 얼마나 남는지 먼저 세어 본다
    for it in (1, 2, 3):
        t = src.copy()
        t.data = src.data.copy()
        bpy.context.collection.objects.link(t)
        m = t.modifiers.new("d", 'DECIMATE')
        m.decimate_type = 'UNSUBDIV'
        m.iterations = it
        dg = bpy.context.evaluated_depsgraph_get()
        ev = t.evaluated_get(dg)
        n_f = len(ev.data.polygons)
        n_t = sum(len(p.vertices) - 2 for p in ev.data.polygons)
        print("   되감기 %d회 → 면 %5d  삼각형 %5d" % (it, n_f, n_t))
        bpy.data.objects.remove(t, do_unlink=True)

    # 실제로 쓸 것
    o = src.copy()
    o.data = src.data.copy()
    o.name = "hero_" + gender
    bpy.context.collection.objects.link(o)
    m = o.modifiers.new("unsub", 'DECIMATE')
    m.decimate_type = 'UNSUBDIV'
    m.iterations = ITERS
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.modifier_apply(modifier="unsub")

    # 키 1.80 으로 맞추고 발바닥을 0 에 놓는다
    s = KEY / o.dimensions.z
    o.scale = (s, s, s)
    bpy.context.view_layer.update()
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    zmin = min((o.matrix_world @ v.co).z for v in o.data.vertices)
    o.location.z -= zmin
    bodies[gender] = o
    print("   → 쓸 것: 면 %d  삼각형 %d  키 %.3f" % (len(o.data.polygons), tris(o), o.dimensions.z))

# 원본·눈알·나머지 전부 숨기고 둘만 남긴다
keep = set(bodies.values())
for ob in list(bpy.data.objects):
    if ob.type == 'MESH' and ob not in keep:
        bpy.data.objects.remove(ob, do_unlink=True)

# 나란히 세운다
bodies["female"].location.x = -0.55
bodies["male"].location.x = 0.55

# ── 재질: 살 + 와이어
skin = bpy.data.materials.new("skin")
skin.use_nodes = False
skin.diffuse_color = (0.72, 0.72, 0.74, 1.0)
wire = bpy.data.materials.new("wire")
wire.use_nodes = False
wire.diffuse_color = (0.10, 0.10, 0.12, 1.0)

for o in list(bodies.values()):
    o.data.materials.clear()
    o.data.materials.append(skin)
    for p in o.data.polygons:
        p.use_smooth = True
    w = o.copy()
    w.data = o.data.copy()
    w.name = o.name + "_wire"
    bpy.context.collection.objects.link(w)
    w.data.materials.clear()
    w.data.materials.append(wire)
    md = w.modifiers.new("Wireframe", 'WIREFRAME')
    md.thickness = 0.004
    md.use_replace = True
    md.use_even_offset = False

# ── 렌더
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sc.render.resolution_x = 900
sc.render.resolution_y = 900
sh = sc.display.shading
sh.light = 'STUDIO'
sh.color_type = 'MATERIAL'
sh.show_shadows = False
sh.show_cavity = True
sh.background_type = 'VIEWPORT'
sh.background_color = (0.34, 0.34, 0.34)
sc.render.film_transparent = False

cam_data = bpy.data.cameras.new("cam")
cam_data.type = 'ORTHO'
cam_data.ortho_scale = 2.1
cam = bpy.data.objects.new("cam", cam_data)
bpy.context.collection.objects.link(cam)
sc.camera = cam

CENTER = (0.0, 0.0, KEY * 0.5)
DIST = 6.0
# 이 모델들은 -Y 를 보고 서 있다 → 앞모습은 카메라가 -Y 쪽
for (name, yaw_deg, pitch_deg) in [("앞", 0.0, 0.0), ("비스듬", 34.0, 12.0)]:
    yaw = math.radians(yaw_deg)
    pitch = math.radians(pitch_deg)
    cam.location = (CENTER[0] + DIST * math.cos(pitch) * math.sin(yaw),
                    CENTER[1] - DIST * math.cos(pitch) * math.cos(yaw),
                    CENTER[2] + DIST * math.sin(pitch))
    cam.rotation_euler = (math.radians(90.0) - pitch, 0.0, yaw)
    sc.render.filepath = os.path.join(OUT, "base_%s.png" % name)
    bpy.ops.render.render(write_still=True)
    print("=== %s ===" % sc.render.filepath)
