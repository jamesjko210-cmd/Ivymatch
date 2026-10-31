using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace IvyMatch
{
    // US lower-48 state outlines (Resources/Map/us_states.json, built from the Census cartographic boundary file)
    // and the Albers projection that places schools on it (CONTRACT section 12). Pure C#: no UnityEngine.
    //
    // Coordinates are normalized: x 0..1 left->right, y 0..1 top->bottom, over a map whose width/height = Aspect.

    [Serializable]
    public class UsMapData
    {
        public string source;
        public MapProjection projection = new MapProjection();
        public float aspect = 1.55f;
        public List<MapState> states = new List<MapState>();

        /// <summary>Parses the map JSON. Throws on empty/invalid input.</summary>
        public static UsMapData Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("empty map json");
            UsMapData d = JsonConvert.DeserializeObject<UsMapData>(json);
            if (d == null || d.states == null || d.states.Count == 0) throw new ArgumentException("map json has no states");
            if (d.projection == null) throw new ArgumentException("map json has no projection");
            d.states.RemoveAll(s => s == null || s.rings == null || s.rings.Count == 0);
            return d;
        }

        /// <summary>Projects WGS84 lat/lng to normalized map coordinates (may fall outside 0..1 for points off the map).</summary>
        public MapPoint Project(double lat, double lng)
        {
            MapProjection p = projection;
            double r = Math.PI / 180.0;
            double n = (Math.Sin(p.lat1 * r) + Math.Sin(p.lat2 * r)) / 2.0;
            double c = Math.Cos(p.lat1 * r) * Math.Cos(p.lat1 * r) + 2.0 * n * Math.Sin(p.lat1 * r);
            double rho0 = Math.Sqrt(c - 2.0 * n * Math.Sin(p.lat0 * r)) / n;
            double rho = Math.Sqrt(c - 2.0 * n * Math.Sin(lat * r)) / n;
            double theta = n * (lng - p.lon0) * r;
            double x = rho * Math.Sin(theta);
            double y = rho0 - rho * Math.Cos(theta);
            return new MapPoint(
                (float)((x - p.minX) / (p.maxX - p.minX)),
                (float)((p.maxY - y) / (p.maxY - p.minY)));
        }

        /// <summary>Bounding box (normalized) of every state in the region, or the whole map for null.</summary>
        public MapRect RegionBounds(Region? region)
        {
            if (!region.HasValue) return new MapRect(0f, 0f, 1f, 1f);
            float minX = 1f, minY = 1f, maxX = 0f, maxY = 0f;
            bool any = false;
            foreach (MapState s in states)
            {
                if (SchoolQuery.RegionOfState(s.id) != region) continue;
                foreach (float[] ring in s.rings)
                    for (int i = 0; i + 1 < ring.Length; i += 2)
                    {
                        any = true;
                        minX = Math.Min(minX, ring[i]); maxX = Math.Max(maxX, ring[i]);
                        minY = Math.Min(minY, ring[i + 1]); maxY = Math.Max(maxY, ring[i + 1]);
                    }
            }
            return any ? new MapRect(minX, minY, maxX - minX, maxY - minY) : new MapRect(0f, 0f, 1f, 1f);
        }
    }

    [Serializable]
    public class MapProjection
    {
        public string type = "albers";
        public double lat0, lon0, lat1, lat2;
        public double minX, maxX, minY, maxY;
    }

    [Serializable]
    public class MapState
    {
        public string id;               // "CA"
        public string name;             // "California"
        public List<float[]> rings = new List<float[]>();   // each ring: x0,y0,x1,y1,... (normalized)
    }

    public struct MapPoint
    {
        public float x, y;
        public MapPoint(float x, float y) { this.x = x; this.y = y; }
    }

    /// <summary>Axis-aligned rectangle in normalized map units.</summary>
    public struct MapRect
    {
        public float x, y, w, h;
        public MapRect(float x, float y, float w, float h) { this.x = x; this.y = y; this.w = w; this.h = h; }
        public bool Contains(MapPoint p) { return p.x >= x && p.x <= x + w && p.y >= y && p.y <= y + h; }
    }
}
