"""Cut Resources/Title/Outskirts.png into the title's parallax layers.

    python Tools/cut_title_layers.py <Outskirts.png> <output folder>

Writes OutskirtsSky (opaque), OutskirtsHills, OutskirtsSlope and OutskirtsRoof. Each layer keeps the
original pixels where it is visible and continues its own colour behind the layers in front, so the
four stacked at rest reproduce the plate exactly. Also writes shiftL/shiftR/dbg previews.
"""
import sys
from PIL import Image
import numpy as np
src, out = sys.argv[1], sys.argv[2]
a = np.asarray(Image.open(src).convert("RGB")).astype(np.int32)
H, W, _ = a.shape
R, G = a[..., 0], a[..., 1]

# --- roof: first bright rim row per column, wall columns own everything below
roofTop = np.full(W, H); rimEnd = np.full(W, H)
for x in range(980, W):
    ys = np.nonzero(R[700:, x] > 28)[0]
    if len(ys):
        t = 700 + ys[0]; roofTop[x] = t - 2
        e = t
        while e < H and (R[e, x] > 28 or e - t < 2): e += 1
        rimEnd[x] = e + 1
x0 = int(np.nonzero(roofTop < H)[0][0])
wall = np.array([np.median(G[885:905, x]) < 15.5 for x in range(W)])
xWall = int(np.nonzero(wall & (np.arange(W) > x0))[0][0])
roof = np.zeros((H, W), bool)
for x in range(x0, W):
    roof[roofTop[x]:(H if x >= xWall else 872), x] = True
if x0 > 0: roof[:, x0 - 1] = roof[:, x0]          # antialiased left edge

# --- near slope: dark from the bottom up
Gs = (G + np.roll(G, 1, 0) + np.roll(G, -1, 0)) / 3.0
nearTop = np.full(W, H)
for x in range(0, xWall):
    y = H - 2
    while y > 600 and Gs[y, x] < 22.5 and not roof[y, x]: y -= 1
    nearTop[x] = y            # y is the antialiased row, owned by the slope
# --- middle hills: first sharp drop under the far ridges
midTop = np.full(W, -1)
for x in range(W):
    lim = min(nearTop[x], roofTop[x] if x >= x0 else H)
    for y in range(705, lim):
        if G[y, x] <= 47 and G[y - 3, x] - G[y, x] >= 6 and Gs[min(y + 3, H - 1), x] <= 47:
            midTop[x] = y - 1; break
city = (np.arange(W) > 712) & (np.arange(W) < 992)
ok = (midTop > 0) & ~city
xs = np.arange(W)
mt = np.interp(xs, xs[ok], midTop[ok])
has = (midTop > 0) | city
mt = np.array([np.median(mt[max(0, x - 6):x + 7]) for x in range(W)])
midTop = np.where(has, np.round(mt), H).astype(int)
midTop = np.where((nearTop - midTop < 5) & (xs < x0), H, midTop)
np.save(out + "/curves.npy", np.stack([roofTop, nearTop, midTop]))
print("x0", x0, "xWall", xWall)
for x in [0, 300, 430, 470, 500, 600, 700, 712, 760, 850, 950, 992, 1020, 1031, 1040, 1100, 1300, 1500, 1671]:
    print(x, "roof", roofTop[x], "near", nearTop[x], "mid", midTop[x])

near = np.zeros((H, W), bool); mid = np.zeros((H, W), bool)
for x in range(W):
    near[nearTop[x]:, x] = True
    mid[midTop[x]:, x] = True
visRoof = roof
visNear = near & ~roof
visMid = mid & ~near & ~roof
visBack = ~mid & ~near & ~roof

def fill(vis, own):
    """Layer colour: original where visible, nearest visible pixel elsewhere."""
    c = a.copy(); done = vis.copy()
    for x in range(W):
        v = np.nonzero(vis[:, x])[0]
        if len(v) == 0: continue
        ys = np.arange(H)
        ip = np.searchsorted(v, ys, side="right") - 1
        inx = np.searchsorted(v, ys, side="left")
        prev = v[np.clip(ip, 0, len(v) - 1)]; nxt = v[np.clip(inx, 0, len(v) - 1)]
        pick = np.where(ip < 0, nxt, np.where(inx >= len(v), prev, np.where(ys - prev <= nxt - ys, prev, nxt)))
        # hidden rows take the row three above the hidden span so edge rows do not smear
        c[:, x] = a[pick, x]; done[:, x] = True
    cols = np.nonzero(done.any(0))[0]
    for x in range(W):
        if not done[:, x].any():
            c[:, x] = c[:, cols[np.argmin(np.abs(cols - x))]]
    return c

def save(name, vis, own, opaque=False):
    c = fill(vis, own)
    alpha = np.full((H, W), 255) if opaque else np.where(own, 255, 0)
    rgba = np.dstack([c, alpha]).astype(np.uint8)
    Image.fromarray(rgba, "RGBA").save(f"{out}/{name}.png")
    return rgba

ownNear = near.copy()
ownNear[:, xWall:] = ownNear[:, xWall - 1][:, None]      # continue behind the wall
ownMid = mid.copy()
L = [save("OutskirtsSky", visBack, None, True), save("OutskirtsHills", visMid, ownMid),
     save("OutskirtsSlope", visNear, ownNear), save("OutskirtsRoof", visRoof, roof)]

def comp(shifts):
    o = np.zeros((H, W, 3), float)
    for l, (dx, dy) in zip(L, shifts):
        s = np.roll(np.roll(l, dy, 0), dx, 1).astype(float)
        al = s[..., 3:] / 255; o = o * (1 - al) + s[..., :3] * al
    return o
rest = comp([(0, 0)] * 4)
print("rest max diff", np.abs(rest - a).max())
for tag, k in [("L", -1), ("R", 1)]:
    o = comp([(0, 0), (3 * k, 1 * k), (6 * k, 2 * k), (10 * k, 3 * k)])
    Image.fromarray(o.astype(np.uint8)).save(f"{out}/shift{tag}.png")
dbg = a.copy()
for x in range(W):
    for arr, col in [(midTop, (255, 0, 255)), (nearTop, (0, 255, 0)), (roofTop, (255, 255, 0))]:
        if arr[x] < H: dbg[arr[x], x] = col
Image.fromarray(dbg.astype(np.uint8)).crop((0, 640, W, H)).save(f"{out}/dbg.png")
