# -*- coding: utf-8 -*-
"""참고 도면 실측(ref/profile.json)으로 캐릭터를 「상자 모델링 + 세분」으로 짓는다.

    blender -b --python modeling/build_hero2.py

1차(hero.py, 관 꽂기)가 마네킹이 된 원인 둘을 고친다:
  ① 모든 단면의 폭·두께·앞뒤 위치가 실측에서 온다 — 타이핑한 반지름이 없다
  ② 팔·다리가 몸통 「면」에서 이어져 나온다 (상자 모델링) + 캣멀클락 세분
     → 어깨·사타구니가 참고처럼 흐른다. 참고 모델이 실제로 만들어진 방식이다.
그리고 세분 결과를 실측과 비교해 케이지를 3회 자동 보정한다 — 그림이 곧 목표.

★토폴로지 핵심 (다시 만질 때 잊지 말 것):
  · 왼쪽 절반만 짓고 미러 모디파이어(용접 0.0012)로 오른쪽을 만든다
  · 다리 고리는 10점 — 바깥 5점이 몸통 반호(7점)의 6모서리와 1:1 로 맞는다
  · 다리 첫 고리의 앞·뒤 점을 x=0 에 박는다 → 미러가 좌우 다리를 사타구니에서
    맞용접한다 = 바지 토폴로지가 저절로 생긴다 (남는 구멍 하나는 마지막에 메움)
  · 팔은 몸통 옆면에 quad 4개짜리 구멍을 내고 8점 고리를 각도 정렬로 꿰맨다
결과물: 저폴(케이지, 삼각형 약 1,100) + 세분(약 4,500) 두 벌.
"""

import bpy
import bmesh
import json
import math
import os
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
OUTP = os.path.join(HERE, "preview")
os.makedirs(OUTP, exist_ok=True)
KEY = 1.8
DAMP = 0.85

# ★기본 씬의 큐브·라이트·카메라부터 지운다 — 한 번 큐브가 화면을 다 가렸다
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

PROF = json.load(open(os.path.join(HERE, "ref", "profile.json"), encoding="utf-8"))
rows = sorted(PROF["rows"], key=lambda r: r["t"])
CROTCH = max((r["t"] for r in rows if len(r["legs"]) == 2), default=0.45)


def series(fn):
    out = []
    for r in rows:
        v = fn(r)
        if v is not None:
            out.append((r["t"], v))
    return out


def interp(sr, t):
    if t <= sr[0][0]:
        return sr[0][1]
    if t >= sr[-1][0]:
        return sr[-1][1]
    for i in range(len(sr) - 1):
        (t0, v0), (t1, v1) = sr[i], sr[i + 1]
        if t0 <= t <= t1:
            f = (t - t0) / (t1 - t0)
            if isinstance(v0, (list, tuple)):
                return [a + (b - a) * f for a, b in zip(v0, v1)]
            return v0 + (v1 - v0) * f
    return sr[-1][1]


def sm(sr, k=1):
    out = []
    for i in range(len(sr)):
        seg = [sr[j][1] for j in range(max(0, i - k), min(len(sr), i + k + 1))]
        if isinstance(seg[0], (list, tuple)):
            out.append((sr[i][0], [sum(v[j] for v in seg) / len(seg)
                                   for j in range(len(seg[0]))]))
        else:
            out.append((sr[i][0], sum(seg) / len(seg)))
    return out


# 사타구니 언저리의 몸통 줄은 다리와 붙어 값이 지저분하다 → 버린다
CORE = sm(series(lambda r: 0.5 * (r["core"][1] - r["core"][0])
                 if r["core"] and r["t"] > CROTCH + 0.03 else None))
SIDE = sm(series(lambda r: r["side"]))
LEG_R = sm(series(lambda r: 0.25 * ((r["legs"][0][1] - r["legs"][0][0]) +
                                    (r["legs"][1][1] - r["legs"][1][0]))
                  if len(r["legs"]) == 2 else None))
LEG_CX = sm(series(lambda r: 0.5 * (abs(r["legs"][0][0] + r["legs"][0][1]) / 2 +
                                    abs(r["legs"][1][0] + r["legs"][1][1]) / 2)
                   if len(r["legs"]) == 2 else None))


def hw(t):
    return interp(CORE, t) * KEY


def cyhd(t):
    y0, y1 = interp(SIDE, t)
    return 0.5 * (y0 + y1) * KEY, 0.5 * (y1 - y0) * KEY


def leg_r(t):
    return interp(LEG_R, t) * KEY


def leg_cx(t):
    return interp(LEG_CX, t) * KEY


# ── 팔 (참고는 팔을 내리고 있다): 축을 맞추고 축 방향 두께를 뽑는다
T_SH = 0.755
X_SH = hw(0.775) * 0.80
arm_pts = []
for r in rows:
    if not (CROTCH + 0.04 < r["t"] < 0.75):
        continue
    cw = interp(CORE, r["t"]) if r["core"] else 0.10
    for (a, b) in r["arms"]:
        c, wd = abs(a + b) / 2, (b - a) / 2
        if 0.004 < wd < 0.05 and c > cw + 0.004:
            arm_pts.append((r["t"], c, wd))

if len(arm_pts) >= 6:
    T = np.array([p[0] for p in arm_pts])
    C = np.array([p[1] for p in arm_pts])
    W = np.array([p[2] for p in arm_pts])
    A = np.vstack([np.ones_like(T), (T_SH - T)]).T
    coef, *_ = np.linalg.lstsq(A, C, rcond=None)
    cosang = math.cos(math.atan2(coef[1], 1.0))
    prof = sorted((math.hypot(c - coef[0], T_SH - t) * KEY, wd * cosang * KEY)
                  for t, c, wd in arm_pts)
    ARM_LEN = prof[-1][0]
else:                                       # 실측이 모자라면 최소한의 기본값
    prof = [(0.05, 0.055), (0.30, 0.045), (0.57, 0.030)]
    ARM_LEN = 0.57
ARMP = sm(prof, 2)


def arm_r(d):
    return max(interp(ARMP, d), 0.018)


print("")
print("=== 실측: 사타구니 t=%.3f · 팔 길이 %.3fm · 팔뿌리 r=%.3f · 어깨 x=%.3f ===" %
      (CROTCH, ARM_LEN, arm_r(0.05), X_SH))
print("=== 가슴 hw %.3f · 허리 %.3f · 골반 %.3f · 허벅지 r %.3f · 머리 hw %.3f ===" %
      (hw(0.70), hw(0.675), hw(0.525), leg_r(0.42), hw(0.925)))

# ──────────────────────────────────────────────────────────────────────────
# 케이지 (왼쪽 절반, x ≤ 0)
# ──────────────────────────────────────────────────────────────────────────
bm = bmesh.new()
R = []                                       # 보정 루프가 만질 고리 기록


def rec(kind, st, verts, **kw):
    R.append(dict(kind=kind, st=st, verts=verts, **kw))


def arc7(t, p=2.25):
    """수평 반호 7점 — 앞(x=0) → 왼쪽 → 뒤(x=0). 초타원."""
    z = t * KEY
    hwv = hw(t)
    cy, hd = cyhd(t)
    out = []
    for k in range(7):
        th = math.radians(-90 - 30 * k)
        c, s = math.cos(th), math.sin(th)
        x = math.copysign(abs(c) ** (2 / p), c) * hwv if abs(c) > 1e-8 else 0.0
        y = cy + math.copysign(abs(s) ** (2 / p), s) * hd
        out.append(bm.verts.new((min(x, 0.0), y, z)))
    return out


RING10_TH = [-90, -120, -150, -180, -210, -240, -270, -315, -360, -405]


def ring10(cx, cy, z, rx, ry, p=2.2):
    """다리·발 고리 10점 — 바깥 5점이 몸통 반호의 모서리 수와 맞는 배치."""
    out = []
    for th in RING10_TH:
        thr = math.radians(th)
        c, s = math.cos(thr), math.sin(thr)
        x = cx + math.copysign(abs(c) ** (2 / p), c) * rx
        y = cy + math.copysign(abs(s) ** (2 / p), s) * ry
        out.append(bm.verts.new((x, y, z)))
    return out


def bridge7(a, b):
    for j in range(6):
        bm.faces.new((a[j], a[j + 1], b[j + 1], b[j]))


def bridge10(a, b):
    for j in range(10):
        bm.faces.new((a[j], a[(j + 1) % 10], b[(j + 1) % 10], b[j]))


# ── 몸통 (팔 구멍은 위 두 줄의 옆면 quad 4개를 비운다)
TOR_T = [CROTCH + 0.02, 0.505, 0.535, 0.565, 0.60, 0.6375,
         0.675, 0.705, 0.725, 0.7575, 0.79]
HOLE_ROWS = {8, 9}                            # 0.725-0.7575, 0.7575-0.79 사이
HOLE_COLS = {2, 3}
tor = [arc7(t) for t in TOR_T]
for t, a in zip(TOR_T, tor):
    rec("core", t, a)
for i in range(len(tor) - 1):
    for j in range(6):
        if i in HOLE_ROWS and j in HOLE_COLS:
            continue
        bm.faces.new((tor[i][j], tor[i][j + 1], tor[i + 1][j + 1], tor[i + 1][j]))

# ── 목 · 머리 · 정수리
NECK_T = [0.815, 0.845]
HEAD_T = [0.868, 0.90, 0.935, 0.965, 0.987]
neck = [arc7(t, p=2.05) for t in NECK_T]
head = [arc7(t, p=2.05) for t in HEAD_T]
for t, a in zip(NECK_T + HEAD_T, neck + head):
    rec("core", t, a)
chain = [tor[-1]] + neck + head
for i in range(len(chain) - 1):
    bridge7(chain[i], chain[i + 1])
cy_top = cyhd(0.99)[0]
pole = bm.verts.new((0.0, cy_top, KEY))
for j in range(6):
    bm.faces.new((head[-1][j], head[-1][j + 1], pole))

# ── 다리 (첫 고리의 앞·뒤 점을 x=0 에 박는다 → 미러가 사타구니를 용접)
T_LTOP = CROTCH - 0.012
zl = T_LTOP * KEY
cxL = -leg_cx(T_LTOP)
y0c, y1c = interp(SIDE, T_LTOP)
ltop = ring10(cxL, 0.5 * (y0c + y1c) * KEY, zl, leg_r(T_LTOP),
              0.5 * (y1c - y0c) * KEY)
ltop[0].co = Vector((0.0, y0c * KEY, zl))     # 앞 사타구니
ltop[6].co = Vector((0.0, y1c * KEY, zl))     # 뒤 사타구니
bridge7(tor[0], ltop[0:7])

LEG_T = [0.41, 0.37, 0.325, 0.295, 0.26, 0.21, 0.155, 0.10, 0.062]
CY_ANKLE = cyhd(0.12)[0]
prev = ltop
for t in LEG_T:
    if t < 0.10:                              # 발목 언저리는 옆모습에 발이 섞인다
        cy, ry = CY_ANKLE, leg_r(t) * 1.1
    else:
        cy, hd = cyhd(t)
        ry = hd
    ring = ring10(-leg_cx(t), cy, t * KEY, leg_r(t), ry)
    rec("leg", t, ring)
    bridge10(prev, ring)
    prev = ring

# ── 발 (쐐기: 발목 고리 → 납작한 발끝 고리 → 뾰족 마감)
y_toe = interp(SIDE, 0.02)[0] * KEY
toe = ring10(-leg_cx(0.05), y_toe * 0.72, 0.032, leg_r(0.03) * 0.95, 0.030, p=2.0)
bridge10(prev, toe)
toe_p = bm.verts.new((-leg_cx(0.05), y_toe * 0.97, 0.020))
for j in range(10):
    bm.faces.new((toe[j], toe[(j + 1) % 10], toe_p))

# ── 팔 (T자세, -x 로 뻗는다)
CY_ARM = cyhd(0.7575)[0]
CZ_ARM = 0.5 * (0.725 + 0.79) * KEY
hb = [tor[8][2], tor[8][3], tor[8][4], tor[9][4], tor[10][4],
      tor[10][3], tor[10][2], tor[9][2]]


def armring(d):
    x = -(X_SH + d)
    r = arm_r(d)
    ry, rz = (r * 1.30, r * 0.55) if d > ARM_LEN * 0.78 else (r, r)
    out = []
    for k in range(8):
        th = math.radians(90 - 45 * k)
        out.append(bm.verts.new((x, CY_ARM + ry * math.cos(th),
                                 CZ_ARM + rz * math.sin(th))))
    return out


ARM_D = [f * ARM_LEN for f in (0.06, 0.17, 0.30, 0.45, 0.56, 0.68, 0.78, 0.88, 0.96)]
arings = [armring(d) for d in ARM_D]
for d, a in zip(ARM_D[1:], arings[1:]):
    rec("arm", d, a, x=-(X_SH + d))
for i in range(len(arings) - 1):
    bridge10(arings[i], arings[i + 1]) if False else None
for i in range(len(arings) - 1):
    for j in range(8):
        bm.faces.new((arings[i][j], arings[i][(j + 1) % 8],
                      arings[i + 1][(j + 1) % 8], arings[i + 1][j]))
tip = bm.verts.new((-(X_SH + ARM_LEN * 1.02), CY_ARM, CZ_ARM))
for j in range(8):
    bm.faces.new((arings[-1][j], arings[-1][(j + 1) % 8], tip))


def stitch(la, lb):
    """구멍 테두리와 팔뿌리 고리를 (y,z) 각도 정렬로 꿰맨다."""
    def ctr(l):
        return (sum(v.co.y for v in l) / len(l), sum(v.co.z for v in l) / len(l))

    ca, cb = ctr(la), ctr(lb)

    def ang(v, c):
        return math.atan2(v.co.z - c[1], v.co.y - c[0])

    A = sorted(la, key=lambda v: ang(v, ca))
    B = sorted(lb, key=lambda v: ang(v, cb))
    a0 = ang(A[0], ca)
    k0 = min(range(8), key=lambda k: abs(math.atan2(
        math.sin(ang(B[k], cb) - a0), math.cos(ang(B[k], cb) - a0))))
    B = B[k0:] + B[:k0]
    for i in range(8):
        bm.faces.new((A[i], A[(i + 1) % 8], B[(i + 1) % 8], B[i]))


stitch(hb, arings[0])

# ──────────────────────────────────────────────────────────────────────────
bm.normal_update()
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bm.verts.index_update()
for r in R:
    r["ids"] = [v.index for v in r["verts"]]
    del r["verts"]

me = bpy.data.meshes.new("hero")
bm.to_mesh(me)
bm.free()
ob = bpy.data.objects.new("hero", me)
bpy.context.collection.objects.link(ob)
bpy.context.view_layer.objects.active = ob
ob.select_set(True)

mir = ob.modifiers.new("mir", 'MIRROR')
mir.use_axis[0] = True
mir.merge_threshold = 0.0012
mir.use_clip = True
sub = ob.modifiers.new("sub", 'SUBSURF')
sub.levels = 1
sub.render_levels = 1

ARM_GATE = X_SH + 0.02


def clampf(f, lo=0.75, hi=1.35):
    return min(max(f, lo), hi)


# ── 보정 루프: 세분 결과 실루엣을 실측에 맞춘다
for it in range(3):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = ob.evaluated_get(dg)
    m2 = ev.to_mesh()
    n = len(m2.vertices)
    co = np.empty(n * 3, np.float32)
    m2.vertices.foreach_get("co", co)
    P = co.reshape(-1, 3)
    ev.to_mesh_clear()

    worst = 0.0
    for r in R:
        ids = r["ids"]
        vs = [me.vertices[i] for i in ids]
        if r["kind"] == "core":
            t = r["st"]
            z = t * KEY
            band = np.abs(P[:, 2] - z) < 0.02
            if 0.70 < t < 0.82:
                band &= np.abs(P[:, 0]) < ARM_GATE
            if band.sum() < 4:
                continue
            q = P[band]
            cw = float(np.abs(q[:, 0]).max())
            cyc = 0.5 * float(q[:, 1].min() + q[:, 1].max())
            hdc = 0.5 * float(q[:, 1].max() - q[:, 1].min())
            tw = hw(t)
            if 0.70 < t < 0.82:
                tw = min(tw, ARM_GATE * 0.97)
            tcy, thd = cyhd(t)
            fx = clampf(1 + DAMP * (tw / max(cw, 1e-4) - 1))
            fy = clampf(1 + DAMP * (thd / max(hdc, 1e-4) - 1))
            dy = DAMP * (tcy - cyc)
            cyr = sum(v.co.y for v in vs) / len(vs)
            for v in vs:
                v.co.x *= fx
                v.co.y = cyr + (v.co.y - cyr) * fy + dy
            worst = max(worst, abs(tw - cw), abs(thd - hdc))
        elif r["kind"] == "leg":
            t = r["st"]
            z = t * KEY
            band = (np.abs(P[:, 2] - z) < 0.02) & (P[:, 0] < -0.004)
            if band.sum() < 4:
                continue
            q = P[band]
            rc = 0.5 * float(q[:, 0].max() - q[:, 0].min())
            cc = 0.5 * float(q[:, 0].max() + q[:, 0].min())
            hdc = 0.5 * float(q[:, 1].max() - q[:, 1].min())
            cyc = 0.5 * float(q[:, 1].max() + q[:, 1].min())
            tr = leg_r(t)
            tcx = -leg_cx(t)
            if t < 0.10:
                tcy, thd = CY_ANKLE, tr * 1.1
            else:
                tcy, thd = cyhd(t)
            fx = clampf(1 + DAMP * (tr / max(rc, 1e-4) - 1))
            fy = clampf(1 + DAMP * (thd / max(hdc, 1e-4) - 1))
            dx = DAMP * (tcx - cc)
            dy = DAMP * (tcy - cyc)
            cxr = sum(v.co.x for v in vs) / len(vs)
            cyr = sum(v.co.y for v in vs) / len(vs)
            for v in vs:
                v.co.x = cxr + (v.co.x - cxr) * fx + dx
                v.co.y = cyr + (v.co.y - cyr) * fy + dy
            worst = max(worst, abs(tr - rc))
        else:                                  # arm
            d = r["st"]
            band = (np.abs(P[:, 0] - r["x"]) < 0.022) & (P[:, 2] > 1.15) \
                & (P[:, 0] < -(X_SH + 0.03))
            if band.sum() < 4:
                continue
            q = P[band]
            ryc = 0.5 * float(q[:, 1].max() - q[:, 1].min())
            rzc = 0.5 * float(q[:, 2].max() - q[:, 2].min())
            tr = arm_r(d)
            try_, trz = (tr * 1.30, tr * 0.55) if d > ARM_LEN * 0.78 else (tr, tr)
            fy = clampf(1 + DAMP * (try_ / max(ryc, 1e-4) - 1))
            fz = clampf(1 + DAMP * (trz / max(rzc, 1e-4) - 1))
            cyr = sum(v.co.y for v in vs) / len(vs)
            czr = sum(v.co.z for v in vs) / len(vs)
            for v in vs:
                v.co.y = cyr + (v.co.y - cyr) * fy
                v.co.z = czr + (v.co.z - czr) * fz
    me.update()
    bpy.context.view_layer.update()
    print("   보정 %d회 — 최대 오차 %.1fmm" % (it + 1, worst * 1000))

# ── 마무리: 미러 적용 → 사타구니 구멍 메움 → 저폴 사본 → 세분 적용
bpy.ops.object.modifier_apply(modifier=mir.name)
bm2 = bmesh.new()
bm2.from_mesh(me)
nb = sum(1 for e in bm2.edges if len(e.link_faces) == 1)
bmesh.ops.holes_fill(bm2, edges=bm2.edges[:], sides=12)
nb2 = sum(1 for e in bm2.edges if len(e.link_faces) == 1)
bm2.to_mesh(me)
bm2.free()
print("=== 테두리 모서리 %d → %d (사타구니 메움) ===" % (nb, nb2))

low = ob.copy()                                # 저폴 버전 (세분 없이)
low.data = me.copy()
low.name = "hero_저폴"
low.modifiers.clear()
bpy.context.collection.objects.link(low)

bpy.ops.object.modifier_apply(modifier=sub.name)


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


for o in (ob, low):
    for v in o.data.vertices:                  # 발바닥 다림질 + 땅에 붙이기
        if v.co.z < 0.004:
            v.co.z = 0.004
    zmin = min(v.co.z for v in o.data.vertices)
    for v in o.data.vertices:
        v.co.z -= zmin
    for p in o.data.polygons:
        p.use_smooth = True

print("=== 세분: 삼각형 %d · 저폴: %d · 키 %.3f ===" %
      (tris(ob), tris(low),
       max(v.co.z for v in ob.data.vertices) - min(v.co.z for v in ob.data.vertices)))

# ──────────────────────────────────────────────────────────────────────────
# 렌더 4장: ①세분+저폴 나란히(앞) ②세분 비스듬 ③세분 옆 ④머리 확대
# ──────────────────────────────────────────────────────────────────────────
ob.location.x = -0.85
low.location.x = 0.85

skin = bpy.data.materials.new("skin")
skin.use_nodes = False
skin.diffuse_color = (0.72, 0.72, 0.74, 1.0)
wirem = bpy.data.materials.new("wire")
wirem.use_nodes = False
wirem.diffuse_color = (0.10, 0.10, 0.12, 1.0)
wires = []
for o in (ob, low):
    o.data.materials.clear()
    o.data.materials.append(skin)
    wv = o.copy()
    wv.data = o.data.copy()
    bpy.context.collection.objects.link(wv)
    wv.data.materials.clear()
    wv.data.materials.append(wirem)
    md = wv.modifiers.new("wf", 'WIREFRAME')
    md.thickness = 0.0032
    md.use_replace = True
    wires.append(wv)

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
    sc.render.filepath = os.path.join(OUTP, "v2_%s.png" % name)
    bpy.ops.render.render(write_still=True)
    print("=== %s ===" % sc.render.filepath)


shot("나란히", 0, 0, 3.4, 0.92, (1400, 860))

low.hide_render = True
wires[1].hide_render = True
ob.location.x = 0.0
wires[0].location.x = 0.0
shot("비스듬", 32, 10, 2.05, 0.90, (760, 1000))
shot("옆", 90, 0, 2.05, 0.90, (760, 1000))
shot("머리", 26, 8, 0.62, 1.60, (900, 900))

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "hero_v2.blend"))
print("=== hero_v2.blend 저장 ===")
