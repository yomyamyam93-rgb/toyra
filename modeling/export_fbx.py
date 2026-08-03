# -*- coding: utf-8 -*-
"""hero_rig_<성별>.blend → Assets/Game/Models/hero_<성별>.fbx

    blender -b modeling/hero_rig_<성별>.blend --python modeling/export_fbx.py -- <성별>

유니티 휴머노이드용 설정:
  · add_leaf_bones=False   — 끝뼈가 생기면 휴머노이드 매핑이 지저분해진다
  · bake_anim=False        — 모션은 유니티에서 붙인다 (믹사모 등)
  · use_mesh_modifiers=False — 아마추어 모디파이어를 구우면 안 된다 (스킨이 죽는다)
  · axis_forward='-Z', axis_up='Y' — 블렌더에서 -Y 를 보던 캐릭터가 유니티에서 +Z 를 본다
  · FBX_SCALE_ALL + global_scale 1.0 — 유니티에서 1/100 로 들어오는 사고 방지
★ 넣은 뒤 유니티에서 「실제 키」를 다시 잰다. 여기 값을 믿지 않는다.
"""

import bpy
import os
import sys

gender = sys.argv[-1] if "--" in sys.argv else "woman"
ROOT = r"C:\Users\ysim1\Documents\GitHub\toyra"
OUT = os.path.join(ROOT, "Assets", "Game", "Models")
os.makedirs(OUT, exist_ok=True)
path = os.path.join(OUT, "hero_%s.fbx" % gender)

arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
mesh = next(o for o in bpy.data.objects if o.type == 'MESH')
arm.name = "Armature"
mesh.name = "hero_%s" % gender
mesh.data.name = "hero_%s" % gender

zs = [(mesh.matrix_world @ v.co).z for v in mesh.data.vertices]
print("")
print("=== %s : 정점 %d · 면 %d · 삼각형 %d · 뼈 %d · 키 %.4fm ===" % (
    gender, len(mesh.data.vertices), len(mesh.data.polygons),
    sum(len(p.vertices) - 2 for p in mesh.data.polygons),
    len(arm.data.bones), max(zs) - min(zs)))

names = sorted(b.name for b in arm.data.bones)
print("=== 뼈 이름: %s ===" % ", ".join(names))

# ★ .blend 가 포즈/에디트 모드로 저장돼 있으면 select_all 이 막힌다 (한 번 터졌다)
#   → 오브젝트 모드로 되돌리고, 연산자 대신 속성으로 직접 선택한다
for o in bpy.data.objects:
    if o.mode != 'OBJECT':
        bpy.context.view_layer.objects.active = o
        try:
            bpy.ops.object.mode_set(mode='OBJECT')
        except Exception:
            pass
for o in bpy.data.objects:
    o.select_set(False)
arm.select_set(True)
mesh.select_set(True)
bpy.context.view_layer.objects.active = arm

bpy.ops.export_scene.fbx(
    filepath=path,
    use_selection=True,
    object_types={'ARMATURE', 'MESH'},
    use_mesh_modifiers=False,
    add_leaf_bones=False,
    bake_anim=False,
    apply_scale_options='FBX_SCALE_ALL',
    global_scale=1.0,
    axis_forward='-Z',
    axis_up='Y',
    mesh_smooth_type='FACE',
)
print("=== 내보냄: %s (%.2f MB) ===" % (path, os.path.getsize(path) / 1024 / 1024))
