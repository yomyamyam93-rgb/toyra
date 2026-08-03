# -*- coding: utf-8 -*-
"""조립: 비율 고친 여자 몸(hero_body.blend) + 번들의 만화풍 머리(GEO-head_stylized).

    blender -b <번들.blend> --python modeling/assemble.py

- 머리의 옷깃(제일 잘록한 목 아래)을 잘라낸다
- 참고 도면 실측 머리 폭(0.126×2×1.8 ≈ 0.25m 급)에 맞춰 줄인다
- 몸의 목 절단면(z=1.485) 위에 얹는다 (2cm 겹침 — 붙이는 건 사람 몫)
결과: hero_assembled.blend + 미리보기 3장
"""

import bpy
import bmesh
import math
import os
from mathutils import Vector

HERE = r"C:\Users\ysim1\Documents\GitHub\toyra\modeling"
OUT = os.path.join(HERE, "preview")
os.makedirs(OUT, exist_ok=True)

HEAD_W = 0.245          # 목표 머리 폭 (참고 실측 0.126·2·1.8=0.254 에서 살짝 얌전하게)
NECK_Z = 1.485          # 몸의 목 절단 높이 (retarget.py 실측)
OVERLAP = 0.025         # 목을 이만큼 몸에 묻는다

# ── 1. 머리 셋만 남긴다
heads = []
for n in ("GEO-head_stylized", "GEO-head_stylized.eye.L", "GEO-head_stylized.eye.R"):
    o = bpy.data.objects.get(n)
    if o:
        heads.append(o)
for ob in list(bpy.data.objects):
    if ob not in heads:
        bpy.data.objects.remove(ob, do_unlink=True)

bpy.ops.object.select_all(action='SELECT')
bpy.context.view_layer.objects.active = heads[0]
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

head = heads[0]
eyes = heads[1:]

# ── 2. 몸을 가져온다 (after_female)
bp = os.path.join(HERE, "hero_body.blend")
bpy.ops.wm.append(filepath=os.path.join(bp, "Object", "after_female"),
                  directory=os.path.join(bp, "Object") + os.sep,
                  filename="after_female")
body = bpy.data.objects["after_female"]
body.location = (0.0, 0.0, 0.0)

# ── 3. 머리에서 옷깃 잘라내기 — 아래쪽에서 제일 잘록한 높이를 찾아 그 밑을 버린다
me = head.data
zs = [v.co.z for v in me.vertices]
z0, z1 = min(zs), max(zs)


def width_at(z, band=0.006):
    xs = [v.co.x for v in me.vertices if abs(v.co.z - z) < band]
    return (max(xs) - min(xs)) if len(xs) >= 2 else None


best_z, best_w = None, 1e9
n = 30
for i in range(n):
    z = z0 + (0.06 + 0.44 * i / n) * (z1 - z0)
    wv = width_at(z)
    if wv is not None and wv < best_w:
        best_z, best_w = z, wv
print("=== 목 잘록이 z=%.3f (폭 %.3f) — 그 밑 옷깃 제거 ===" % (best_z, best_w))

bm = bmesh.new()
bm.from_mesh(me)
kill = [f for f in bm.faces
        if sum(v.co.z for v in f.verts) / len(f.verts) < best_z]
bmesh.ops.delete(bm, geom=kill, context='FACES')
loose = [v for v in bm.verts if not v.link_faces]
bmesh.ops.delete(bm, geom=loose, context='VERTS')
bm.to_mesh(me)
bm.free()
me.update()

# ── 4. 크기·자리 맞추기 (머리+눈에 같은 변환)
zs = [v.co.z for v in me.vertices]
zb = min(zs)
sk = [v.co for v in me.vertices if v.co.z > zb + 0.6 * (max(zs) - zb)]
cur_w = max(v.x for v in sk) - min(v.x for v in sk)      # 두개골 폭
s = HEAD_W / cur_w

nk = [v.co for v in me.vertices if v.co.z < zb + 0.01]    # 절단면 고리
piv = Vector((sum(v.x for v in nk) / len(nk),
              sum(v.y for v in nk) / len(nk), zb))

bt = [v.co for v in body.data.vertices if v.co.z > NECK_Z - 0.015]
tgt = Vector((0.0, sum(v.y for v in bt) / len(bt), NECK_Z - OVERLAP))
print("=== 크기 %.3f배 · 목 붙는 자리 y=%.3f ===" % (s, tgt.y))

for o in [head] + eyes:
    for v in o.data.vertices:
        v.co = tgt + (v.co - piv) * s
    o.data.update()

# ── 5. 재질 + 렌더
skin = bpy.data.materials.new("skin")
skin.use_nodes = False
skin.diffuse_color = (0.76, 0.72, 0.70, 1.0)
eyem = bpy.data.materials.new("eye")
eyem.use_nodes = False
eyem.diffuse_color = (0.14, 0.12, 0.11, 1.0)
for o in [head, body] + eyes:
    o.data.materials.clear()
    o.data.materials.append(eyem if ".eye" in o.name else skin)
    for p in o.data.polygons:
        p.use_smooth = True

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


def shot(name, yaw, pitch, scale, cz, res):
    sc.render.resolution_x, sc.render.resolution_y = res
    cd.ortho_scale = scale
    yr, pr = math.radians(yaw), math.radians(pitch)
    d = 6.0
    cam.location = (d * math.cos(pr) * math.sin(yr),
                    -d * math.cos(pr) * math.cos(yr),
                    cz + d * math.sin(pr))
    cam.rotation_euler = (math.radians(90) - pr, 0.0, yr)
    sc.render.filepath = os.path.join(OUT, "조립_%s.png" % name)
    bpy.ops.render.render(write_still=True)
    print("=== %s ===" % sc.render.filepath)


shot("앞", 0, 0, 2.05, 0.90, (760, 1000))
shot("비스듬", 30, 8, 2.05, 0.90, (760, 1000))
shot("얼굴", 24, 6, 0.55, 1.60, (860, 860))

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "hero_assembled.blend"))
print("=== hero_assembled.blend 저장 ===")
