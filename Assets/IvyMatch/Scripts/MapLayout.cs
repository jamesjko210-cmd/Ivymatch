using System;
using System.Collections.Generic;
using System.Linq;

namespace IvyMatch
{
    /// <summary>
    /// Visible part of the map: a square window (in normalized map units) at (x, y) with side <see cref="size"/>.
    /// Because the map element keeps the map's aspect ratio, a square window keeps the drawing undistorted.
    /// size 1 = whole country; size 1/MaxZoom = closest zoom.
    /// </summary>
    public struct MapViewport
    {
        public const float MaxZoom = 14f;

        public float x, y, size;

        public MapViewport(float x, float y, float size) { this.x = x; this.y = y; this.size = size; }

        public static MapViewport Whole { get { return new MapViewport(0f, 0f, 1f); } }

        public float Zoom { get { return 1f / size; } }

        public bool AtMaxZoom { get { return size <= 1f / MaxZoom + 1e-4f; } }

        /// <summary>Smallest window containing <paramref name="bounds"/> plus <paramref name="pad"/> (fraction), clamped to the map.</summary>
        public static MapViewport Fit(MapRect bounds, float pad)
        {
            float s = Math.Max(bounds.w, bounds.h) * (1f + pad);
            s = Math.Max(1f / MaxZoom, Math.Min(1f, s));
            float cx = bounds.x + bounds.w / 2f, cy = bounds.y + bounds.h / 2f;
            float x = Math.Max(0f, Math.Min(1f - s, cx - s / 2f));
            float y = Math.Max(0f, Math.Min(1f - s, cy - s / 2f));
            return new MapViewport(x, y, s);
        }

        /// <summary>Normalized map point -> pixel position inside a map element of size w x h.</summary>
        public void ToScreen(MapPoint p, float w, float h, out float sx, out float sy)
        {
            sx = (p.x - x) / size * w;
            sy = (p.y - y) / size * h;
        }
    }

    /// <summary>One marker on screen: a single school or a cluster of nearby schools.</summary>
    public sealed class MarkerGroup
    {
        public readonly List<School> Members = new List<School>();
        public readonly List<MapPoint> Points = new List<MapPoint>();
        public float X, Y; // pixel centre inside the map element

        public bool IsCluster { get { return Members.Count > 1; } }

        /// <summary>Stable identity of the cluster (sorted member ids).</summary>
        public string Key { get { return string.Join("|", Members.Select(m => m.id).OrderBy(i => i, StringComparer.Ordinal)); } }

        public MapRect Bounds
        {
            get
            {
                float minX = Points.Min(p => p.x), maxX = Points.Max(p => p.x);
                float minY = Points.Min(p => p.y), maxY = Points.Max(p => p.y);
                return new MapRect(minX, minY, maxX - minX, maxY - minY);
            }
        }
    }

    public static class MapLayout
    {
        /// <summary>
        /// Greedy screen-space clustering: a school joins the first group whose centre lies inside the ellipse with
        /// half-axes <paramref name="radiusX"/> x <paramref name="radiusY"/> px (badges are wider than tall);
        /// otherwise it starts a new group. Schools outside the view (plus a margin) are skipped.
        /// Input order is kept, so results are deterministic.
        /// </summary>
        public static List<MarkerGroup> Cluster(IList<KeyValuePair<School, MapPoint>> items, MapViewport vp,
                                                float w, float h, float radiusX, float radiusY)
        {
            var groups = new List<MarkerGroup>();
            if (items == null || w <= 0f || h <= 0f || radiusX <= 0f || radiusY <= 0f) return groups;
            const float margin = 8f;

            foreach (KeyValuePair<School, MapPoint> it in items)
            {
                float sx, sy;
                vp.ToScreen(it.Value, w, h, out sx, out sy);
                if (sx < -margin || sy < -margin || sx > w + margin || sy > h + margin) continue;

                MarkerGroup hit = null;
                foreach (MarkerGroup g in groups)
                {
                    float dx = (g.X - sx) / radiusX, dy = (g.Y - sy) / radiusY;
                    if (dx * dx + dy * dy < 1f) { hit = g; break; }
                }
                if (hit == null)
                {
                    hit = new MarkerGroup();
                    groups.Add(hit);
                }
                hit.Members.Add(it.Key);
                hit.Points.Add(it.Value);
                // centre = mean of the members' screen positions
                int n = hit.Members.Count;
                hit.X = hit.X + (sx - hit.X) / n;
                hit.Y = hit.Y + (sy - hit.Y) / n;
            }
            return groups;
        }

        /// <summary>
        /// Positions for an expanded ("spiderfied") cluster: members on a circle around the centre,
        /// spaced so badges about <paramref name="badgePx"/> wide do not overlap.
        /// </summary>
        public static List<KeyValuePair<float, float>> Spread(float cx, float cy, int count, float badgePx)
        {
            var list = new List<KeyValuePair<float, float>>();
            if (count <= 0) return list;
            float radius = Math.Max(badgePx, count * badgePx / (2f * (float)Math.PI));
            for (int i = 0; i < count; i++)
            {
                double a = -Math.PI / 2 + i * 2 * Math.PI / count;
                list.Add(new KeyValuePair<float, float>(cx + radius * (float)Math.Cos(a), cy + radius * (float)Math.Sin(a)));
            }
            return list;
        }
    }
}
