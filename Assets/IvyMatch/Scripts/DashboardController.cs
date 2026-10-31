using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace IvyMatch
{
    /// <summary>
    /// Attach next to the UIDocument (Source Asset = Dashboard.uxml). Loads the data, renders the card grid,
    /// wires filters / selection / detail sections. Element names must match CONTRACT section 6.
    /// </summary>
    public class DashboardController : MonoBehaviour
    {
        [SerializeField] UIDocument document;
        [SerializeField] VisualTreeAsset cardTemplate;
        [SerializeField] string remoteJsonUrl = "";
        [SerializeField] bool autoRefreshOnStart = true;

        const float NarrowWidth = 600f;

        SchoolRepository repo;
        SchoolDatabase db = new SchoolDatabase();
        readonly Dictionary<string, School> byId = new Dictionary<string, School>();
        readonly List<SchoolCardView> cards = new List<SchoolCardView>(); // visible cards, display order
        CardGridPresenter cardPresenter;  // builds cards once per database; filters only show/hide/reorder
        bool cardsDirty = true;            // set when the database object changes
        readonly Dictionary<RateFilter, Button> filterButtons = new Dictionary<RateFilter, Button>();

        VisualElement uiRoot;      // document.rootVisualElement
        VisualElement namedRoot;   // element named "root" (or uiRoot)
        VisualElement cardGrid;
        ScrollView cardScroll;
        Label headerTitle, headerSub, summaryBar, emptyState;
        VisualElement listControls;
        DetailPanelView detail;

        RateFilter filter = RateFilter.All;
        string selectedId;

        // Search + multi-filter (CONTRACT section 11). Applied after the acceptance-rate pills.
        readonly SchoolQuery query = new SchoolQuery();
        FilterPanelView filterPanel;
        int lastMissing; // schools hidden because an active filter's field is null

        // List / map switch (CONTRACT section 12). The map shows the same filtered list as the cards.
        const string MapResourcePath = "Map/us_states";
        UsMapView mapView;
        VisualElement mapContainer, detailPanelEl;
        Button viewListButton, viewMapButton, viewScoreButton;
        bool mapMode;
        bool scoreMode;
        ScoreView scoreView;
        VisualElement scoreContainer;

        // Target entry year. Published deadlines and costs are for the Fall 2027 cycle.
        const int DataEntryYear = 2027;
        const string YearPref = "ivymatch.entryYear";
        int entryYear = DataEntryYear;
        Button yearBtn;
        VisualElement yearRow;
        Label yearNote;
        readonly Dictionary<int, Button> yearButtons = new Dictionary<int, Button>();

        // NOTE: bound in Start (not OnEnable) because UIDocument builds its tree in its own OnEnable and the
        // component order is not guaranteed. If this GameObject is disabled and re-enabled the UIDocument
        // rebuilds its tree; re-enable the component through a scene reload in that case.
        async void Start()
        {
            try
            {
                if (!BindUi()) return;

                repo = new SchoolRepository();
                SchoolDatabase loaded = await repo.LoadAsync();
                if (this == null) return; // destroyed while loading
                ApplyDatabase(loaded);
                Render();

                if (!string.IsNullOrWhiteSpace(remoteJsonUrl) && autoRefreshOnStart)
                    await RefreshAsync();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>Downloads remoteJsonUrl; on success reloads and re-renders. Safe to call from a UI button.</summary>
        public async Task<bool> RefreshAsync()
        {
            try
            {
                if (repo == null || string.IsNullOrWhiteSpace(remoteJsonUrl)) return false;

                bool ok = await repo.TryRefreshAsync(remoteJsonUrl);
                if (this == null || !ok) return false;

                SchoolDatabase fresh = await repo.LoadAsync();
                if (this == null) return false;
                if (fresh == null || fresh.schools == null || fresh.schools.Count == 0) return false;

                ApplyDatabase(fresh);
                Render();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        // ------------------------------------------------------------------ binding

        /// <summary>Used by IvyMatchBootstrap when the dashboard is built in code.</summary>
        public void Configure(UIDocument doc, VisualTreeAsset card)
        {
            document = doc;
            cardTemplate = card;
        }

        bool BindUi()
        {
            // Every missing reference falls back to Resources/UI so a half-wired scene still renders.
            if (document == null) document = GetComponent<UIDocument>();
            if (document == null) document = gameObject.AddComponent<UIDocument>();
            if (document.panelSettings == null) document.panelSettings = IvyMatchBootstrap.GetPanelSettings();
            if (document.visualTreeAsset == null)
                document.visualTreeAsset = Resources.Load<VisualTreeAsset>(IvyMatchBootstrap.DashboardPath);
            if (cardTemplate == null) cardTemplate = Resources.Load<VisualTreeAsset>(IvyMatchBootstrap.CardPath);
            if (document.visualTreeAsset == null || cardTemplate == null)
            {
                Debug.LogError("[IvyMatch] DashboardController: Dashboard.uxml / SchoolCard.uxml not found in Resources/UI.");
                return false;
            }

            uiRoot = document.rootVisualElement;
            if (uiRoot == null)
            {
                Debug.LogError("[IvyMatch] DashboardController: UIDocument has no rootVisualElement (Source Asset / Panel Settings missing?).");
                return false;
            }

            namedRoot = uiRoot.Q<VisualElement>("root") ?? uiRoot;
            uiRoot.style.flexGrow = 1;

            Font font = IvyMatchBootstrap.LoadKoreanFont();
            if (font != null) uiRoot.style.unityFontDefinition = FontDefinition.FromFont(font);
            cardGrid = uiRoot.Q<VisualElement>("card-grid");
            cardScroll = uiRoot.Q<ScrollView>("card-scroll");
            headerSub = uiRoot.Q<Label>("header-sub");
            headerTitle = uiRoot.Q<Label>("header-title");
            listControls = uiRoot.Q<VisualElement>("list-controls");
            summaryBar = uiRoot.Q<Label>("summary-bar");
            emptyState = uiRoot.Q<Label>("empty-state");

            if (cardScroll != null)
            {
                cardScroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Elastic;
                cardScroll.mouseWheelScrollSize = 60f;
                cardScroll.AddManipulator(new DragScrollManipulator(cardScroll));

                // When the content shrinks (detail panel closed, fewer results, list -> map) the ScrollView can keep an
                // offset past the new end; with content shorter than the screen it then cannot be scrolled back and the
                // page looks empty. Pull the offset back into range whenever either side changes size.
                cardScroll.contentContainer.RegisterCallback<GeometryChangedEvent>(_ => ClampScrollOffset());
                cardScroll.contentViewport.RegisterCallback<GeometryChangedEvent>(_ => ClampScrollOffset());
            }

            if (cardGrid == null)
            {
                Debug.LogError("[IvyMatch] DashboardController: element 'card-grid' not found in Dashboard.uxml.");
                return false;
            }

            // Filters
            BindFilter("filter-all", RateFilter.All);
            BindFilter("filter-low", RateFilter.Low);
            BindFilter("filter-mid", RateFilter.Mid);
            BindFilter("filter-high", RateFilter.High);

            // Search + filter panel
            VisualElement searchBar = uiRoot.Q<VisualElement>("search-bar");
            if (searchBar != null)
            {
                filterPanel = new FilterPanelView(searchBar, uiRoot.Q<VisualElement>("filter-panel"),
                                                  uiRoot.Q<VisualElement>("active-filters"), query);
                filterPanel.Changed += Render;
            }
            else
            {
                Debug.LogWarning("[IvyMatch] 'search-bar' not found; search and filters are disabled.");
            }

            // List / map switch
            BindMap();
            BindYear();

            // Detail panel
            VisualElement panelEl = uiRoot.Q<VisualElement>("detail-panel");
            detailPanelEl = panelEl;
            if (panelEl != null)
            {
                detail = new DetailPanelView(panelEl, LookupSchool);

                Button close = uiRoot.Q<Button>("detail-close");
                if (close != null) close.clicked += ClearSelection;
                else Debug.LogWarning("[IvyMatch] 'detail-close' not found.");
            }
            else
            {
                Debug.LogWarning("[IvyMatch] 'detail-panel' not found; selection will only highlight cards.");
            }

            // Responsive: toggle "narrow" below 600 logical px.
            uiRoot.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            return true;
        }

        void BindFilter(string elementName, RateFilter f)
        {
            Button b = uiRoot.Q<Button>(elementName);
            if (b == null)
            {
                Debug.LogWarning("[IvyMatch] Filter button '" + elementName + "' not found.");
                return;
            }
            filterButtons[f] = b;
            b.clicked += () => SetFilter(f);
        }

        void BindSectionButton(string elementName, string sectionKey)
        {
            Button b = uiRoot.Q<Button>(elementName);
            if (b == null)
            {
                Debug.LogWarning("[IvyMatch] Section button '" + elementName + "' not found.");
                return;
            }
            b.clicked += () =>
            {
                if (detail != null && detail.Current != null) detail.ShowSection(sectionKey);
            };
        }

        /// <summary>
        /// The page scrolls under the top of the viewport now, so keep the viewport itself out of the status bar / notch
        /// and the navigation bar. Screen px -> panel units: ScaleWithScreenSize, reference width 390, match 0.
        /// </summary>
        void ApplySafeArea()
        {
            if (namedRoot == null || Screen.width <= 0) return;
            Rect sa = Screen.safeArea;
            float k = 390f / Screen.width;
            float top = Mathf.Max(0f, Screen.height - (sa.y + sa.height)) * k;
            float bottom = Mathf.Max(0f, sa.y) * k;
            namedRoot.style.paddingTop = top;
            namedRoot.style.paddingBottom = bottom;
            // The school page is absolutely positioned, so it ignores the root padding: inset it directly.
            if (detailPanelEl != null)
            {
                detailPanelEl.style.top = top;
                detailPanelEl.style.bottom = bottom;
            }
        }

        void OnRootGeometryChanged(GeometryChangedEvent e)
        {
            if (e.newRect.width <= 0f) return;
            ApplySafeArea();
            bool narrow = e.newRect.width < NarrowWidth;
            uiRoot.EnableInClassList("narrow", narrow);
            if (namedRoot != uiRoot) namedRoot.EnableInClassList("narrow", narrow);
            // Phones scroll by touch: no scrollbar there. Wider screens get the slim overlay bar (Dashboard.uss).
            if (cardScroll != null)
                cardScroll.verticalScrollerVisibility = narrow ? ScrollerVisibility.Hidden : ScrollerVisibility.Auto;
        }

        // ------------------------------------------------------------------ data

        void ApplyDatabase(SchoolDatabase newDb)
        {
            if (newDb == null || newDb.schools == null) newDb = new SchoolDatabase();
            db = newDb;
            cardsDirty = true;

            byId.Clear();
            foreach (School s in db.schools)
            {
                if (s != null && !string.IsNullOrEmpty(s.id)) byId[s.id] = s;
            }
        }

        School LookupSchool(string id)
        {
            School s;
            return (id != null && byId.TryGetValue(id, out s)) ? s : null;
        }

        static float? RateOf(School s)
        {
            return s != null && s.stats != null ? s.stats.rateOverall : null;
        }

        // ------------------------------------------------------------------ rendering

        void SetFilter(RateFilter f)
        {
            filter = f;
            Render();
        }

        /// <summary>Rebuilds the grid for the current filter. Never throws on empty data.</summary>
        void Render()
        {
            // Remember the open section so it survives a re-render.
            string section = detail != null ? detail.CurrentSection : null;

            // Cards are built once per database; afterwards a render only shows/hides/reorders them.
            if (cardPresenter == null && cardGrid != null) cardPresenter = new CardGridPresenter(cardGrid, MakeCard);
            if (cardsDirty && cardPresenter != null)
            {
                cardPresenter.Rebuild(db != null ? db.schools : null);
                cardsDirty = false;
            }

            List<School> byRate = RateFilters.Apply(db != null ? db.schools : null, filter);
            QueryResult found = query.Apply(byRate, DateTime.Today);
            List<School> list = found.Items;
            lastMissing = found.MissingData;

            cards.Clear();
            if (cardPresenter != null) cards.AddRange(cardPresenter.Show(list));

            UpdateHeader();
            UpdateFilterButtons();
            UpdateViewMode(list);

            if (list.Count == 0)
            {
                UiUtil.SetText(emptyState, UiText.NoMatch);
                UiUtil.SetVisible(emptyState, true);
                UiUtil.SetText(summaryBar, UiText.SummaryNoMatch + UiText.MissingNote(lastMissing));
                selectedId = null;
                if (detail != null) detail.Hide();
                SyncCardSelection();
                return;
            }

            UiUtil.SetVisible(emptyState, false);

            UpdateSummary(list);

            // Preserve selection if the selected school is still visible, else clear it.
            if (selectedId != null)
            {
                SchoolCardView selectedCard = cards.FirstOrDefault(c => c.School.id == selectedId);
                if (selectedCard == null)
                {
                    selectedId = null;
                    if (detail != null) detail.Hide();
                }
                else
                {
                    // Re-resolve by id: the database object may have been replaced by a refresh.
                    School current = LookupSchool(selectedId) ?? selectedCard.School;
                    if (detail != null)
                    {
                        detail.Show(current);
                        detail.ShowSection(section ?? DetailPanelView.SectionStrategy);
                    }
                }
            }
            else if (detail != null)
            {
                detail.Hide();
            }

            // Cards are reused, so sync the highlight on every card (hidden ones included).
            SyncCardSelection();
            if (mapView != null && mapMode) mapView.SetSelected(selectedId);
        }

        void SyncCardSelection()
        {
            if (cardPresenter == null) return;
            foreach (SchoolCardView c in cardPresenter.AllViews)
            {
                bool on = selectedId != null && c.School.id == selectedId;
                if (c.IsSelected != on) c.SetSelected(on);
            }
        }

        /// <summary>CardGridPresenter factory: instantiates one card template (not yet added to the grid).</summary>
        SchoolCardView MakeCard(School school, out VisualElement wrapper)
        {
            wrapper = null;
            try
            {
                // Instantiate() returns a TemplateContainer wrapping the UXML root.
                TemplateContainer container = cardTemplate.Instantiate();
                VisualElement cardRoot = container.Q<VisualElement>("card-root");
                if (cardRoot == null && container.childCount > 0) cardRoot = container[0];
                if (cardRoot == null)
                {
                    Debug.LogWarning("[IvyMatch] SchoolCard.uxml has no element named 'card-root'.");
                    return null;
                }

                // Dashboard.uss styles the TemplateContainer wrapper as the flex item
                // (".card-grid > TemplateContainer"), so the presenter adds the wrapper itself to the grid.
                wrapper = container;
                return new SchoolCardView(cardRoot, school, OnCardClicked);
            }
            catch (Exception e)
            {
                Debug.LogException(e); // one bad record must not kill the grid
                return null;
            }
        }

        void UpdateHeader()
        {
            int total = db != null && db.schools != null ? db.schools.Count : 0;
            int verified = total == 0 ? 0 : db.schools.Count(s => s != null && s.IsVerified);
            UiUtil.SetText(headerTitle, KoText.HeaderTitle(total));
            UiUtil.SetText(headerSub, KoText.HeaderSub(verified));
        }

        void UpdateSummary(List<School> list)
        {
            if (summaryBar == null) return;
            if (list == null || list.Count == 0)
            {
                summaryBar.text = UiText.SummaryNoMatch;
                return;
            }

            // Summary stats only over schools that have a rate; the rest are counted as "pending".
            List<School> rated = list.Where(s => s.HasRate).ToList();
            if (rated.Count == 0)
            {
                summaryBar.text = UiText.SummaryNoRates(list.Count) + UiText.MissingNote(lastMissing);
                return;
            }
            float avg = rated.Average(s => RateOf(s).Value);
            School best = rated[0];
            foreach (School s in rated)
                if (RateOf(s).Value < RateOf(best).Value) best = s;

            summaryBar.text = UiText.Summary(list.Count, list.Count - rated.Count, avg, best.name, RateOf(best).Value)
                              + UiText.MissingNote(lastMissing);
        }

        void UpdateFilterButtons()
        {
            foreach (KeyValuePair<RateFilter, Button> kv in filterButtons)
                kv.Value.EnableInClassList("active", kv.Key == filter);
        }

        // ------------------------------------------------------------------ selection

        void OnCardClicked(School school)
        {
            if (school == null) return;
            if (selectedId == school.id) ClearSelection();
            else SelectSchool(school);
        }

        void SelectSchool(School school)
        {
            selectedId = school.id;
            SyncCardSelection();
            if (mapView != null) mapView.SetSelected(selectedId);

            if (detail != null)
            {
                detail.Show(school);
                detail.ShowSection(DetailPanelView.SectionStrategy);
            }
        }

        void ClearSelection()
        {
            selectedId = null;
            SyncCardSelection();
            if (mapView != null) mapView.SetSelected(null);
            if (detail != null) detail.Hide();
        }

        // ------------------------------------------------------------------ list / map

        void BindMap()
        {
            mapContainer = uiRoot.Q<VisualElement>("map-view");
            viewListButton = uiRoot.Q<Button>("view-list");
            viewMapButton = uiRoot.Q<Button>("view-map");
            viewScoreButton = uiRoot.Q<Button>("view-score");
            scoreContainer = uiRoot.Q<VisualElement>("score-view");
            if (viewListButton != null) viewListButton.clicked += () => SetViewMode(false, false);
            if (viewMapButton != null) viewMapButton.clicked += () => SetViewMode(true, false);
            if (viewScoreButton != null) viewScoreButton.clicked += () => SetViewMode(false, true);
            if (scoreContainer != null)
            {
                try { scoreView = new ScoreView(scoreContainer); }
                catch (Exception e) { Debug.LogException(e); if (viewScoreButton != null) UiUtil.SetVisible(viewScoreButton, false); }
            }
            else if (viewScoreButton != null) UiUtil.SetVisible(viewScoreButton, false);

            if (mapContainer == null)
            {
                Debug.LogWarning("[IvyMatch] 'map-view' not found; map mode is disabled.");
                if (viewMapButton != null) UiUtil.SetVisible(viewMapButton, false);
                return;
            }

            UsMapData data = null;
            try
            {
                TextAsset asset = Resources.Load<TextAsset>(MapResourcePath);
                if (asset != null) data = UsMapData.Parse(asset.text);
                else Debug.LogWarning("[IvyMatch] Resources/" + MapResourcePath + ".json not found.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[IvyMatch] Map data unusable: " + e.Message);
            }

            mapView = new UsMapView(mapContainer, data);
            mapView.SchoolClicked += OnCardClicked;
        }

        void BindYear()
        {
            yearBtn = uiRoot.Q<Button>("year-btn");
            yearRow = uiRoot.Q<VisualElement>("year-row");
            yearNote = uiRoot.Q<Label>("year-note");
            if (yearBtn == null || yearRow == null) return;

            try { entryYear = PlayerPrefs.GetInt(YearPref, DataEntryYear); } catch (Exception) { entryYear = DataEntryYear; }
            if (entryYear < 2027 || entryYear > 2030) entryYear = DataEntryYear;

            yearBtn.clicked += () => UiUtil.SetVisible(yearRow, yearRow.ClassListContains("hidden"));
            for (int y = 2027; y <= 2030; y++)
            {
                Button b = uiRoot.Q<Button>("year-" + y);
                if (b == null) continue;
                int year = y;
                yearButtons[y] = b;
                b.clicked += () => SetEntryYear(year);
            }
            UpdateYear();
        }

        void SetEntryYear(int year)
        {
            entryYear = year;
            try { PlayerPrefs.SetInt(YearPref, year); PlayerPrefs.Save(); } catch (Exception) { }
            UiUtil.SetVisible(yearRow, false);
            UpdateYear();
        }

        void UpdateYear()
        {
            yearBtn.text = entryYear + " 입학";
            foreach (KeyValuePair<int, Button> kv in yearButtons)
                kv.Value.EnableInClassList("active", kv.Key == entryYear);
            bool differs = entryYear != DataEntryYear;
            if (yearNote != null)
            {
                yearNote.text = "마감일과 비용은 " + DataEntryYear + "년 가을 입학 기준이에요. " + entryYear + "년 입학 자료는 아직 공개 전이에요.";
                UiUtil.SetVisible(yearNote, differs);
            }
        }

        void SetViewMode(bool map, bool score)
        {
            if (mapMode == map && scoreMode == score) return;
            mapMode = map && mapContainer != null;
            scoreMode = score && scoreView != null;
            if (scoreMode)
            {
                selectedId = null;
                SyncCardSelection();
                if (detail != null) detail.Hide();
                if (mapView != null) mapView.SetSelected(null);
            }
            Render();
            ScrollToTop(); // each view starts at its top
        }

        void ScrollToTop()
        {
            if (cardScroll != null) cardScroll.scrollOffset = Vector2.zero;
        }

        /// <summary>Keeps the scroll offset inside [0, content height - viewport height].</summary>
        void ClampScrollOffset()
        {
            if (cardScroll == null) return;
            float contentH = cardScroll.contentContainer.layout.height;
            float viewH = cardScroll.contentViewport.layout.height;
            if (float.IsNaN(contentH) || float.IsNaN(viewH) || viewH <= 0f) return;

            float max = Mathf.Max(0f, contentH - viewH);
            Vector2 o = cardScroll.scrollOffset;
            if (o.y > max + 0.5f || o.y < 0f)
                cardScroll.scrollOffset = new Vector2(o.x, Mathf.Clamp(o.y, 0f, max));
        }

        /// <summary>Shows the map or the card grid and hands the current result list to the map.</summary>
        void UpdateViewMode(List<School> list)
        {
            if (viewMapButton != null) viewMapButton.EnableInClassList("active", mapMode);
            if (viewScoreButton != null) viewScoreButton.EnableInClassList("active", scoreMode);
            if (viewListButton != null) viewListButton.EnableInClassList("active", !mapMode && !scoreMode);
            UiUtil.SetVisible(mapContainer, mapMode);
            UiUtil.SetVisible(scoreContainer, scoreMode);
            UiUtil.SetVisible(listControls, !scoreMode);
            UiUtil.SetVisible(cardGrid, !mapMode && !scoreMode);
            if (scoreView != null && scoreMode) scoreView.SetSchools(list);
            if (mapView != null && mapMode)
            {
                mapView.SetSchools(list, selectedId);
                // If the map was hidden until now its size is not known yet; redraw once layout has run.
                UsMapView view = mapView;
                mapContainer.schedule.Execute(() => view.Refresh()).StartingIn(30);
            }
        }
    }
}
