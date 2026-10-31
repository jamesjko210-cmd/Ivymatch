using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace IvyMatch
{
    /// <summary>
    /// US map view (CONTRACT section 12), built in code inside the "map-view" container:
    ///   [ region buttons ]
    ///   [ map surface: state outlines drawn with Painter2D + school / cluster markers ]
    ///   [ hint + source caption ]
    /// Shows the schools passed to <see cref="SetSchools"/> (the current search/filter result).
    /// Nearby markers merge into a count badge; tapping it zooms in, and at max zoom it fans the schools out.
    /// </summary>
    public class UsMapView
    {
        public event Action<School> SchoolClicked;

        // Marker overlap ellipse (badges are about 40 x 20 px).
        const float ClusterRadiusX = 44f, ClusterRadiusY = 30f;
        const float SpreadBadgePx = 52f;
        const float MarkerBoxW = 56f, MarkerBoxH = 44f; // finger-sized hit box; the visible badge is smaller

        static readonly Color StateFill = new Color(0.894f, 0.910f, 0.929f, 1f);      // #E4E8ED
        static readonly Color StateFillActive = new Color(0.796f, 0.827f, 0.863f, 1f); // #CBD3DC, states with a shown school
        static readonly Color StateStroke = new Color(1f, 1f, 1f, 1f);
        static readonly Color SpokeColor = new Color(0.09f, 0.125f, 0.2f, 0.5f);

        readonly List<Vector4> spokes = new List<Vector4>(); // x1,y1 -> x2,y2 (pixels)

        readonly UsMapData map;
        readonly VisualElement root;
        readonly VisualElement surface;
        readonly VisualElement markerLayer;
        readonly Dictionary<string, Button> regionButtons = new Dictionary<string, Button>();
        readonly List<float[]> ringBounds = new List<float[]>(); // per state ring: minX,minY,maxX,maxY (same order as drawing)

        List<School> schools = new List<School>();
        readonly HashSet<string> activeStates = new HashSet<string>();
        string selectedId;
        string expandedKey;
        MapViewport viewport = MapViewport.Whole;
        string regionKey = "all";

        public UsMapView(VisualElement container, UsMapData map)
        {
            if (container == null) throw new ArgumentNullException("container");
            root = container;
            this.map = map;
            root.Clear();

            if (map == null)
            {
                var msg = new Label(UiText.MapUnavailable);
                msg.AddToClassList("map-caption");
                root.Add(msg);
                return;
            }

            // ---- region buttons ----
            var regions = new VisualElement();
            regions.AddToClassList("chip-row");
            regions.AddToClassList("map-regions");
            AddRegionButton(regions, "all", UiText.MapAll, null);
            foreach (Region r in (Region[])Enum.GetValues(typeof(Region)))
                AddRegionButton(regions, r.ToString(), UiText.Label(r), r);
            root.Add(regions);

            // ---- surface ----
            surface = new VisualElement();
            surface.name = "map-surface";
            surface.AddToClassList("map-surface");
            surface.generateVisualContent += DrawStates;
            surface.RegisterCallback<GeometryChangedEvent>(OnSurfaceGeometry);

            markerLayer = new VisualElement();
            markerLayer.AddToClassList("map-markers");
            markerLayer.pickingMode = PickingMode.Ignore;
            markerLayer.generateVisualContent += DrawSpokes;
            surface.Add(markerLayer);
            root.Add(surface);

            // ---- caption ----
            var hint = new Label(UiText.MapHint);
            hint.AddToClassList("map-caption");
            root.Add(hint);
            var cap = new Label(UiText.MapSource);
            cap.AddToClassList("map-caption");
            root.Add(cap);

            foreach (MapState s in map.states)
                foreach (float[] ring in s.rings)
                    ringBounds.Add(Bounds(ring));

            UpdateRegionButtons();
        }

        // ------------------------------------------------------------------ public API

        /// <summary>Schools to show (already filtered/sorted by the controller) and the selected id.</summary>
        public void SetSchools(List<School> list, string selected)
        {
            schools = list != null ? list.Where(s => s != null && s.lat.HasValue && s.lng.HasValue).ToList() : new List<School>();
            selectedId = selected;
            activeStates.Clear();
            foreach (School s in schools)
            {
                string st = StateOf(s);
                if (st != null) activeStates.Add(st);
            }
            if (surface != null) surface.MarkDirtyRepaint();
            Relayout();
        }

        public void SetSelected(string id)
        {
            selectedId = id;
            Relayout();
        }

        /// <summary>Redraws outlines and markers with the current layout (call after the map becomes visible).</summary>
        public void Refresh()
        {
            Redraw();
        }

        // ------------------------------------------------------------------ viewport

        void AddRegionButton(VisualElement row, string key, string label, Region? region)
        {
            var b = new Button(() =>
            {
                regionKey = key;
                expandedKey = null;
                viewport = region.HasValue ? MapViewport.Fit(map.RegionBounds(region), 0.08f) : MapViewport.Whole;
                UpdateRegionButtons();
                Redraw();
            });
            b.text = label;
            b.AddToClassList("chip");
            row.Add(b);
            regionButtons[key] = b;
        }

        void UpdateRegionButtons()
        {
            foreach (KeyValuePair<string, Button> kv in regionButtons)
                kv.Value.EnableInClassList("active", kv.Key == regionKey);
        }

        void Redraw()
        {
            if (surface == null) return;
            surface.MarkDirtyRepaint();
            Relayout();
        }

        void OnSurfaceGeometry(GeometryChangedEvent e)
        {
            float w = e.newRect.width;
            if (w <= 0f) return;
            // Keep the map's aspect ratio: the height follows the width.
            float wanted = Mathf.Round(w / Math.Max(0.1f, map.aspect));
            if (Mathf.Abs(e.newRect.height - wanted) > 1f)
            {
                surface.style.height = wanted;
                return; // a second GeometryChangedEvent follows with the new height
            }
            Redraw();
        }

        // ------------------------------------------------------------------ drawing

        void DrawStates(MeshGenerationContext mgc)
        {
            if (map == null) return;
            Rect r = surface.contentRect;
            float w = r.width, h = r.height;
            if (w <= 0f || h <= 0f) return;

            Painter2D p = mgc.painter2D;
            p.lineWidth = 1f;
            p.lineJoin = LineJoin.Round;
            p.strokeColor = StateStroke;

            float vx = viewport.x, vy = viewport.y, vs = viewport.size;
            int ringIndex = 0;
            foreach (MapState s in map.states)
            {
                p.fillColor = activeStates.Contains(s.id) ? StateFillActive : StateFill;
                foreach (float[] ring in s.rings)
                {
                    float[] b = ringBounds[ringIndex++];
                    // cull rings outside the window
                    if (b[2] < vx || b[0] > vx + vs || b[3] < vy || b[1] > vy + vs) continue;

                    p.BeginPath();
                    for (int i = 0; i + 1 < ring.Length; i += 2)
                    {
                        var pt = new Vector2((ring[i] - vx) / vs * w, (ring[i + 1] - vy) / vs * h);
                        if (i == 0) p.MoveTo(pt); else p.LineTo(pt);
                    }
                    p.ClosePath();
                    p.Fill();
                    p.Stroke();
                }
            }
        }

        // ------------------------------------------------------------------ markers

        void Relayout()
        {
            if (markerLayer == null) return;
            markerLayer.Clear();
            Rect r = surface.contentRect;
            float w = r.width, h = r.height;
            if (w <= 0f || h <= 0f || float.IsNaN(w) || float.IsNaN(h)) return;

            var items = schools.Select(s => new KeyValuePair<School, MapPoint>(s, map.Project(s.lat.Value, s.lng.Value))).ToList();
            List<MarkerGroup> groups = MapLayout.Cluster(items, viewport, w, h, ClusterRadiusX, ClusterRadiusY);

            bool expandedFound = false;
            spokes.Clear();
            foreach (MarkerGroup g in groups)
            {
                if (!g.IsCluster)
                {
                    AddSchoolMarker(g.Members[0], g.X, g.Y);
                    continue;
                }
                if (g.Key == expandedKey)
                {
                    expandedFound = true;
                    var spots = MapLayout.Spread(g.X, g.Y, g.Members.Count, SpreadBadgePx);
                    AddDot(g.X, g.Y);
                    for (int i = 0; i < g.Members.Count; i++)
                    {
                        spokes.Add(new Vector4(g.X, g.Y, spots[i].Key, spots[i].Value));
                        AddSchoolMarker(g.Members[i], spots[i].Key, spots[i].Value);
                    }
                    continue;
                }
                AddClusterMarker(g);
            }
            if (!expandedFound) expandedKey = null;
            // Spokes are drawn by the marker layer under the badges.
            markerLayer.MarkDirtyRepaint();
        }

        /// <summary>Thin lines from a fanned-out cluster's centre to each of its schools.</summary>
        void DrawSpokes(MeshGenerationContext mgc)
        {
            if (spokes.Count == 0) return;
            Painter2D p = mgc.painter2D;
            p.lineWidth = 1.5f;
            p.strokeColor = SpokeColor;
            foreach (Vector4 s in spokes)
            {
                p.BeginPath();
                p.MoveTo(new Vector2(s.x, s.y));
                p.LineTo(new Vector2(s.z, s.w));
                p.Stroke();
            }
        }

        /// <summary>Fixed-size, click-through box centred on the point; the badge inside is centred by flexbox.</summary>
        VisualElement AddAnchor(float x, float y, Action onTap = null)
        {
            var anchor = new VisualElement();
            anchor.AddToClassList("map-anchor");
            anchor.pickingMode = onTap != null ? PickingMode.Position : PickingMode.Ignore;
            if (onTap != null)
            {
                // Defer so the marker layer is not rebuilt while this tap is still being dispatched.
                anchor.AddManipulator(new Clickable(() => markerLayer.schedule.Execute(onTap)));
            }
            anchor.style.left = x - MarkerBoxW / 2f;
            anchor.style.top = y - MarkerBoxH / 2f;
            anchor.style.width = MarkerBoxW;
            anchor.style.height = MarkerBoxH;
            markerLayer.Add(anchor);
            return anchor;
        }

        void AddSchoolMarker(School s, float x, float y)
        {
            School school = s;
            VisualElement anchor = AddAnchor(x, y, () =>
            {
                Debug.Log("[IvyMatch] map tap school " + school.id);
                if (SchoolClicked != null) SchoolClicked(school);
            });
            var b = new Button();
            b.pickingMode = PickingMode.Ignore;
            b.text = s.LogoText;
            b.AddToClassList("map-marker");
            Color bg = UiUtil.ParseColor(s.colorHex, UiUtil.FallbackAccent);
            b.style.backgroundColor = bg;
            b.style.color = SchoolCardView.ContrastRatio(bg, Color.white) >= SchoolCardView.ContrastRatio(bg, Color.black)
                ? Color.white : new Color(0.047f, 0.055f, 0.071f, 1f);
            b.EnableInClassList("selected", s.id == selectedId);
            anchor.Add(b);
        }

        void AddClusterMarker(MarkerGroup g)
        {
            MarkerGroup group = g;
            VisualElement anchor = AddAnchor(g.X, g.Y, () =>
            {
                Debug.Log("[IvyMatch] map tap cluster " + group.Members.Count);
                OnClusterClicked(group);
            });
            var b = new Button();
            b.pickingMode = PickingMode.Ignore;
            b.text = g.Members.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            b.AddToClassList("map-cluster");
            b.EnableInClassList("selected", g.Members.Any(m => m.id == selectedId));
            anchor.Add(b);
        }

        void AddDot(float x, float y)
        {
            VisualElement anchor = AddAnchor(x, y);
            var dot = new VisualElement();
            dot.AddToClassList("map-dot");
            dot.pickingMode = PickingMode.Ignore;
            anchor.Add(dot);
        }

        void OnClusterClicked(MarkerGroup g)
        {
            MapViewport next = MapViewport.Fit(g.Bounds, 0.8f);
            // Zoom in while it still helps; once the window cannot shrink any more, fan the cluster out instead.
            if (viewport.AtMaxZoom || next.size >= viewport.size * 0.95f)
            {
                expandedKey = g.Key;
                Relayout();
                return;
            }
            viewport = next;
            expandedKey = null;
            regionKey = null;
            UpdateRegionButtons();
            Redraw();
        }

        // ------------------------------------------------------------------ helpers

        static float[] Bounds(float[] ring)
        {
            float minX = 1e9f, minY = 1e9f, maxX = -1e9f, maxY = -1e9f;
            for (int i = 0; i + 1 < ring.Length; i += 2)
            {
                minX = Math.Min(minX, ring[i]); maxX = Math.Max(maxX, ring[i]);
                minY = Math.Min(minY, ring[i + 1]); maxY = Math.Max(maxY, ring[i + 1]);
            }
            return new[] { minX, minY, maxX, maxY };
        }

        static string StateOf(School s)
        {
            if (string.IsNullOrEmpty(s.location)) return null;
            int comma = s.location.LastIndexOf(',');
            return (comma >= 0 ? s.location.Substring(comma + 1) : s.location).Trim().ToUpperInvariant();
        }
    }
}
