# -*- coding: utf-8 -*-
"""참고 도면(ref/base.jpg)을 픽셀로 재서 ref/profile.json 으로 남긴다.

    blender -b --python modeling/measure2.py

measure_ref.py 와 다른 점: 옆모습에서 「두께」만이 아니라 **[앞끝, 뒤끝] 위치**까지
뽑는다 — 배·등·엉덩이·턱이 어디로 튀어나오는지가 이 값에서 나온다.
build_hero2.py 가 이 JSON 만 읽는다. 좌표는 전부 키=1 로 정규화, 앞 = -y.
"""

import bpy
import os
import json
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
IMG = os.path.join(HERE, "ref", "base.jpg")
OUT = os.path.join(HERE, "ref", "profile.json")

img = bpy.data.images.load(IMG)
w, h = img.size
buf = np.empty(w * h * 4, np.float32)
img.pixels.foreach_get(buf)
px = buf.reshape(h, w, 4)[::-1]
rgb = px[:, :, :3]
lum = rgb.mean(2)
sat = rgb.max(2) - rgb.min(2)

hist, edges = np.histogram(lum, bins=128, range=(0.0, 1.0))
bg = 0.5 * (edges[hist.argmax()] + edges[hist.argmax() + 1])

# 배경에서 물 채우기 — 몸 안 회색이 배경과 겹쳐서 밝기로는 못 가른다
bgish = (np.abs(lum - bg) < 0.045) & (sat < 0.10)
reached = np.zeros_like(bgish)
reached[0, :] = bgish[0, :]
reached[-1, :] = bgish[-1, :]
reached[:, 0] = bgish[:, 0]
reached[:, -1] = bgish[:, -1]
for _ in range(4000):
    nxt = reached.copy()
    nxt[1:, :] |= reached[:-1, :]
    nxt[:-1, :] |= reached[1:, :]
    nxt[:, 1:] |= reached[:, :-1]
    nxt[:, :-1] |= reached[:, 1:]
    nxt &= bgish
    if nxt.sum() == reached.sum():
        break
    reached = nxt
mask = ~reached
mask[:45, :] = False          # 워터마크
mask[sat > 0.10] = False      # 색 있는 기즈모

col = mask.sum(0)
lo, hi = int(w * 0.55), int(w * 0.72)
split = lo + int(np.argmin(col[lo:hi]))


def runs(row, x0, x1, gap=3):
    xs = np.flatnonzero(mask[row, x0:x1])
    if not len(xs):
        return []
    xs = xs + x0
    out, s, p = [], xs[0], xs[0]
    for x in xs[1:]:
        if x - p > gap:
            out.append((s, p))
            s = x
        p = x
    out.append((s, p))
    return [r for r in out if r[1] - r[0] >= 2]


def bbox(x0, x1):
    rr = np.flatnonzero(mask[:, x0:x1].any(1))
    cc = np.flatnonzero(mask[:, x0:x1].any(0)) + x0
    return rr[0], rr[-1], cc[0], cc[-1]


ft, fb, fl, fr = bbox(0, split)
st, sb, sl, sr = bbox(split, w)
H = float(fb - ft)
cx = 0.5 * (fl + fr)
SH = float(sb - st)

# 옆모습 기준축(수직선): 다리가 곧은 t=0.18~0.30 의 제일 넓은 런 중심
plumbs = []
for t in np.arange(0.18, 0.31, 0.02):
    ry = int(round(sb - t * SH))
    rs = runs(ry, split, w)
    if rs:
        widest = max(rs, key=lambda r: r[1] - r[0])
        plumbs.append(0.5 * (widest[0] + widest[1]))
plumb = float(np.mean(plumbs))

# 앞뒤 부호: 발끝(t=0.03)이 뻗은 쪽이 앞
ry = int(round(sb - 0.03 * SH))
rs = runs(ry, split, w)
widest = max(rs, key=lambda r: r[1] - r[0])
front_sign = 1.0 if abs(widest[1] - plumb) > abs(widest[0] - plumb) else -1.0


def ynorm(u):
    """옆모습 픽셀 → 정규화 y (앞이 음수)."""
    return -front_sign * (u - plumb) / H


N = 88
rows_out = []
for i in range(N + 1):
    t = 1.0 - i / N
    ry = min(max(int(round(fb - t * H)), 0), h - 1)
    rs = runs(ry, 0, split)
    core, legs, arms = None, [], []
    if rs:
        core_run = next((r for r in rs if r[0] - 3 <= cx <= r[1] + 3), None)
        if core_run:
            core = [(core_run[0] - cx) / H, (core_run[1] - cx) / H]
            rest = [r for r in rs if r is not core_run]
        else:
            left = [r for r in rs if r[1] < cx]
            right = [r for r in rs if r[0] > cx]
            pick = []
            if left:
                pick.append(max(left, key=lambda r: r[1]))
            if right:
                pick.append(min(right, key=lambda r: r[0]))
            legs = [[(a - cx) / H, (b - cx) / H] for (a, b) in sorted(pick)]
            rest = [r for r in rs if r not in pick]
        arms = [[(a - cx) / H, (b - cx) / H] for (a, b) in rest]

    sy = min(max(int(round(sb - t * SH)), 0), h - 1)
    ss = runs(sy, split, w)
    side = None
    if ss:
        widest = max(ss, key=lambda r: r[1] - r[0])
        y0, y1 = ynorm(widest[0]), ynorm(widest[1])
        side = [min(y0, y1), max(y0, y1)]

    rows_out.append({"t": round(t, 4), "core": core, "legs": legs,
                     "arms": arms, "side": side})

with open(OUT, "w", encoding="utf-8") as f:
    json.dump({"H": H, "rows": rows_out}, f)

n_core = sum(1 for r in rows_out if r["core"])
n_leg = sum(1 for r in rows_out if len(r["legs"]) == 2)
n_arm = sum(1 for r in rows_out if r["arms"])
n_side = sum(1 for r in rows_out if r["side"])
crotch = max((r["t"] for r in rows_out if len(r["legs"]) == 2), default=0)
print("")
print("=== profile.json: 줄 %d (몸통 %d · 다리 %d · 팔 %d · 옆 %d) ===" %
      (len(rows_out), n_core, n_leg, n_arm, n_side))
print("=== 사타구니 t=%.3f · 앞부호 %+d · 기준축 x=%.0fpx ===" % (crotch, front_sign, plumb))
