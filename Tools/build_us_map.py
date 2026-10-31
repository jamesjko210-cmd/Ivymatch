"""Build Assets/IvyMatch/Resources/Map/us_states.json from the Census cartographic boundary shapefile.
Lower 48 + DC, Albers equal-area conic (USGS parameters, spherical), Douglas-Peucker simplified, normalized to 0..1
(y down). Also writes a PNG preview with the schools plotted from schools.json lat/lng.
Usage: python -I build_map.py <cb_..._state_20m dir> <schools.json> <out json> <preview png>"""
import json, math, os, struct, sys

shp_dir, schools_path, out_path, png_path = sys.argv[1:5]
base = [f for f in os.listdir(shp_dir) if f.endswith(".shp")][0][:-4]

# ---------------------------------------------------------------- DBF (attribute table)
def read_dbf(path):
    data = open(path, "rb").read()
    nrec, hlen, rlen = struct.unpack("<IHH", data[4:12])
    fields, pos = [], 32
    while data[pos] != 0x0D:
        name = data[pos:pos + 11].split(b"\0")[0].decode("ascii")
        flen = data[pos + 16]
        fields.append((name, flen)); pos += 32
    recs = []
    for i in range(nrec):
        off = hlen + i * rlen + 1
        rec = {}
        for name, flen in fields:
            rec[name] = data[off:off + flen].decode("latin-1").strip(); off += flen
        recs.append(rec)
    return recs

# ---------------------------------------------------------------- SHP polygons (lon/lat rings)
def read_shp(path):
    data = open(path, "rb").read()
    pos, shapes = 100, []
    while pos < len(data):
        _, clen = struct.unpack(">II", data[pos:pos + 8]); pos += 8
        rec = data[pos:pos + clen * 2]; pos += clen * 2
        stype = struct.unpack("<i", rec[:4])[0]
        if stype != 5:
            shapes.append([]); continue
        nparts, npts = struct.unpack("<ii", rec[36:44])
        parts = list(struct.unpack("<%di" % nparts, rec[44:44 + 4 * nparts])) + [npts]
        pts_off = 44 + 4 * nparts
        pts = [struct.unpack("<dd", rec[pts_off + 16 * i: pts_off + 16 * i + 16]) for i in range(npts)]
        shapes.append([pts[parts[k]:parts[k + 1]] for k in range(nparts)])
    return shapes

# ---------------------------------------------------------------- Albers (must match UsMapData.cs)
P = {"lat0": 23.0, "lon0": -96.0, "lat1": 29.5, "lat2": 45.5}
r = math.radians
N_ = (math.sin(r(P["lat1"])) + math.sin(r(P["lat2"]))) / 2
C_ = math.cos(r(P["lat1"])) ** 2 + 2 * N_ * math.sin(r(P["lat1"]))
RHO0 = math.sqrt(C_ - 2 * N_ * math.sin(r(P["lat0"]))) / N_

def albers(lon, lat):
    rho = math.sqrt(C_ - 2 * N_ * math.sin(r(lat))) / N_
    th = N_ * r(lon - P["lon0"])
    return rho * math.sin(th), RHO0 - rho * math.cos(th)   # unit sphere; y grows north

def dp(pts, tol):
    """Douglas-Peucker on a closed ring: split at the vertex farthest from the start, simplify both halves."""
    if len(pts) < 5: return pts
    x0, y0 = pts[0]
    k = max(range(len(pts)), key=lambda i: (pts[i][0] - x0) ** 2 + (pts[i][1] - y0) ** 2)
    a, b = dp_open(pts[:k + 1], tol), dp_open(pts[k:], tol)
    return a[:-1] + b

def dp_open(pts, tol):
    """Douglas-Peucker on an open polyline (iterative)."""
    if len(pts) < 3: return pts
    keep = [False] * len(pts); keep[0] = keep[-1] = True
    stack = [(0, len(pts) - 1)]
    while stack:
        a, b = stack.pop()
        ax, ay = pts[a]; bx, by = pts[b]; dx, dy = bx - ax, by - ay
        L = math.hypot(dx, dy) or 1e-12
        best, idx = 0.0, -1
        for i in range(a + 1, b):
            px, py = pts[i]
            d = abs(dy * px - dx * py + bx * ay - by * ax) / L
            if d > best: best, idx = d, i
        if best > tol and idx > 0:
            keep[idx] = True; stack += [(a, idx), (idx, b)]
    return [p for p, k in zip(pts, keep) if k]

recs = read_dbf(os.path.join(shp_dir, base + ".dbf"))
shapes = read_shp(os.path.join(shp_dir, base + ".shp"))
EXCLUDE = {"AK", "HI", "PR", "VI", "GU", "AS", "MP"}
TOL = 0.0007       # ~4.5 km on the unit sphere scale
MIN_AREA = 2.5e-6  # drop tiny islands

def area(ring):
    return abs(sum(ring[i][0] * ring[i - 1][1] - ring[i - 1][0] * ring[i][1] for i in range(len(ring)))) / 2

states = []
for rec, rings in zip(recs, shapes):
    if rec["STUSPS"] in EXCLUDE or not rings: continue
    out = []
    for ring in rings:
        pr = [albers(lon, lat) for lon, lat in ring]
        if area(pr) < MIN_AREA: continue
        s = dp(pr, TOL)
        if len(s) >= 4: out.append(s)
    if not out:  # keep at least the biggest ring (DC is tiny)
        pr = max(([albers(lo, la) for lo, la in rg] for rg in rings), key=area)
        out = [dp(pr, TOL / 4)]
    states.append({"id": rec["STUSPS"], "name": rec["NAME"], "rings": out})

xs = [x for s in states for rg in s["rings"] for x, _ in rg]
ys = [y for s in states for rg in s["rings"] for _, y in rg]
minX, maxX, minY, maxY = min(xs), max(xs), min(ys), max(ys)
pad = 0.02 * (maxX - minX)
minX -= pad; maxX += pad; minY -= pad; maxY += pad
W, H = maxX - minX, maxY - minY

def norm(x, y):
    return round((x - minX) / W, 5), round((maxY - y) / H, 5)   # y down

for s in states:
    s["rings"] = [[c for x, y in rg for c in norm(x, y)] for rg in s["rings"]]
npts = sum(len(rg) // 2 for s in states for rg in s["rings"])

doc = {
    "source": "US Census Bureau cartographic boundary file " + base + " (public domain); lower 48 + DC",
    "projection": {"type": "albers", **P, "minX": minX, "maxX": maxX, "minY": minY, "maxY": maxY},
    "aspect": round(W / H, 5),
    "states": sorted(states, key=lambda s: s["id"]),
}
os.makedirs(os.path.dirname(out_path), exist_ok=True)
json.dump(doc, open(out_path, "w", encoding="utf-8"), separators=(",", ":"))
print("states", len(states), "points", npts, "aspect", doc["aspect"], "bytes", os.path.getsize(out_path))

# ---------------------------------------------------------------- preview PNG
from PIL import Image, ImageDraw, ImageFont
PW = 1600; PH = int(PW / doc["aspect"])
img = Image.new("RGB", (PW, PH), (12, 14, 18)); d = ImageDraw.Draw(img)
for s in states:
    for rg in s["rings"]:
        pts = [(rg[i] * PW, rg[i + 1] * PH) for i in range(0, len(rg), 2)]
        d.polygon(pts, fill=(28, 32, 48), outline=(70, 78, 102))
schools = json.load(open(schools_path, encoding="utf-8"))["schools"]
try:
    font = ImageFont.truetype("arialbd.ttf", 13)
except Exception:
    font = ImageFont.load_default()
for sc in schools:
    if sc.get("lat") is None: continue
    x, y = norm(*albers(sc["lng"], sc["lat"])); x *= PW; y *= PH
    col = sc["colorHex"]; rgb = tuple(int(col[i:i + 2], 16) for i in (1, 3, 5))
    d.rounded_rectangle([x - 17, y - 11, x + 17, y + 11], 5, fill=rgb, outline=(12, 14, 18))
    d.text((x, y), sc.get("abbr") or sc["name"][:2], fill=(255, 255, 255), font=font, anchor="mm")
img.save(png_path)
print("preview", png_path, PW, PH)
