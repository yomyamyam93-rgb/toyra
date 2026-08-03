# -*- coding: utf-8 -*-
"""참고 도면(modeling/ref/base.jpg)을 픽셀로 잰다.

    blender -b --python modeling/measure_ref.py

앞모습에서 높이별 「좌우 폭」, 옆모습에서 「앞뒤 두께」를 뽑는다.
★ 단면 반지름을 손으로 타이핑하던 것을 없애려고 만든 것이다 (0번 규칙).

★ 밝기로 몸을 고르면 안 된다 — 몸 안쪽 회색이 배경 회색과 겹쳐서 흰 선만 잡힌다.
  그래서 **배경 쪽에서 물을 채워 넣고**(가장자리부터 번지게) 안 잠긴 데를 몸으로 친다.
  몸을 둘러싼 흰 실루엣 선이 둑 역할을 해서 물이 안 들어온다.
"""

import bpy
import os
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
IMG = os.path.join(HERE, "ref", "base.jpg")

img = bpy.data.images.load(IMG)
w, h = img.size
buf = np.empty(w * h * 4, dtype=np.float32)
img.pixels.foreach_get(buf)
px = buf.reshape(h, w, 4)[::-1]           # 블렌더는 아래부터 담는다 → 위부터로
rgb = px[:, :, :3]
lum = rgb.mean(axis=2)
sat = rgb.max(axis=2) - rgb.min(axis=2)

hist, edges = np.histogram(lum, bins=128, range=(0.0, 1.0))
bg = 0.5 * (edges[hist.argmax()] + edges[hist.argmax() + 1])

# ── 배경 후보: 배경 밝기에 가깝고 색기 없는 픽셀
bgish = (np.abs(lum - bg) < 0.045) & (sat < 0.10)

# ── 가장자리에서 물 채우기
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

mask = ~reached                            # 물이 안 닿은 곳 = 몸(과 워터마크·기즈모)

print("")
print("=== 그림 %d x %d · 배경 %.3f · 몸 픽셀 %d ===" % (w, h, bg, mask.sum()))

# ── 워터마크(오른쪽 위)·기즈모(가운데 색깔 화살표) 지우기
mask[:45, :] = False                       # 맨 위 띠 = 워터마크
mask[(sat > 0.10)] = False                 # 색 있는 것 = 기즈모

col = mask.sum(axis=0)
lo, hi = int(w * 0.55), int(w * 0.72)
split = lo + int(np.argmin(col[lo:hi]))
FRONT, SIDE = (0, split), (split, w)
print("=== 앞/옆 경계 x = %d ===" % split)


def runs(row, x0, x1, gap=3):
    xs = np.flatnonzero(mask[row, x0:x1])
    if len(xs) == 0:
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
    rows = np.flatnonzero(mask[:, x0:x1].any(axis=1))
    cols = np.flatnonzero(mask[:, x0:x1].any(axis=0)) + x0
    return rows[0], rows[-1], cols[0], cols[-1]


ft, fb, fl, fr = bbox(*FRONT)
st, sb, sl, sr = bbox(*SIDE)
H = fb - ft
cx = 0.5 * (fl + fr)
SH = sb - st
print("=== 앞 세로 %d~%d (키 %dpx) 중심 x=%.1f | 옆 세로 %d~%d (키 %dpx) ===" %
      (ft, fb, H, cx, st, sb, SH))
print("")
print("  높이  |  몸통/다리 (중심에서 px)        폭   | 팔      | 옆두께")
print(" (발=0) |                                     |         |")

N = 40
rows_out = []
for i in range(N + 1):
    t = 1.0 - i / N
    ry = int(round(fb - t * H))
    ry = min(max(ry, 0), h - 1)
    rs = runs(ry, *FRONT)

    # 중심선을 품은 덩어리 = 몸통·머리 / 중심을 사이에 둔 두 덩어리 = 다리
    core = [r for r in rs if r[0] - 3 <= cx <= r[1] + 3]
    if core:
        a, b = core[0]
        body = "[%+6.1f %+6.1f]" % (a - cx, b - cx)
        half = 0.5 * (b - a)
    else:
        inner = sorted(rs, key=lambda r: min(abs(r[0] - cx), abs(r[1] - cx)))[:2]
        inner.sort()
        if len(inner) == 2:
            body = "".join("[%+6.1f %+6.1f]" % (a - cx, b - cx) for (a, b) in inner)
            half = 0.5 * ((inner[0][1] - inner[0][0]) + (inner[1][1] - inner[1][0])) / 2.0
        else:
            body, half = "-", 0.0

    outer = [r for r in rs if r not in core and (r[1] < cx - 40 or r[0] > cx + 40)]
    arm = "%5.1f" % max([r[1] - r[0] for r in outer], default=0.0)

    sy = int(round(sb - t * SH))
    sy = min(max(sy, 0), h - 1)
    ss = runs(sy, *SIDE)
    dep = max([b - a for (a, b) in ss], default=0.0)

    print(" %5.3f  | %-34s %5.1f | %s   | %5.1f" % (t, body[:34], half * 2, arm, dep))
    rows_out.append((t, half, 0.5 * dep))

print("")
print("=== 키를 1.0 으로 봤을 때의 반지름 (hero.py 에 넣을 값) ===")
print("  t      좌우반지름   앞뒤반지름")
for (t, hw, hd) in rows_out:
    if hw > 0:
        print("  %5.3f   %7.4f    %7.4f" % (t, hw / H, hd / H))
