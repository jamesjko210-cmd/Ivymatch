using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace IvyMatch
{
    /// <summary>
    /// Builds the search box, the collapsible filter panel and the active-filter chip row in code
    /// (Dashboard.uxml only provides the three empty containers, CONTRACT section 11) and edits a
    /// <see cref="SchoolQuery"/>. Raises <see cref="Changed"/> after every edit.
    /// </summary>
    public class FilterPanelView
    {
        public event Action Changed;

        readonly SchoolQuery query;
        readonly VisualElement panel;
        readonly VisualElement activeRow;
        readonly TextField field;
        readonly Label placeholder;
        readonly Button clearButton;
        readonly Button toggleButton;

        // Every chip and the predicate that says whether it is selected, for refreshing the "active" class.
        readonly List<KeyValuePair<Button, Func<bool>>> chips = new List<KeyValuePair<Button, Func<bool>>>();

        bool open;

        const long SearchDebounceMs = 150;
        IVisualElementScheduledItem pendingSearch;

        public FilterPanelView(VisualElement searchBar, VisualElement panel, VisualElement activeRow, SchoolQuery query)
        {
            if (searchBar == null) throw new ArgumentNullException("searchBar");
            if (query == null) throw new ArgumentNullException("query");
            this.query = query;
            this.panel = panel;
            this.activeRow = activeRow;

            // ---- search row: [ text field (+ placeholder) ][ × ][ Filters ] ----
            searchBar.Clear();
            var wrap = new VisualElement();
            wrap.AddToClassList("search-wrap");

            field = new TextField();
            field.name = "search-field";
            field.AddToClassList("search-field");
            field.isDelayed = false;
            field.RegisterValueChangedCallback(evt =>
            {
                query.Text = evt.newValue ?? string.Empty;
                RefreshSearchChrome();
                // Debounce: re-filter once typing pauses, not on every keystroke.
                if (pendingSearch != null) pendingSearch.Pause();
                pendingSearch = field.schedule.Execute(RaiseChanged);
                pendingSearch.StartingIn(SearchDebounceMs);
            });

            // UI Toolkit placeholder support differs between Unity versions, so draw our own.
            placeholder = new Label(UiText.SearchPlaceholder);
            placeholder.AddToClassList("search-placeholder");
            placeholder.pickingMode = PickingMode.Ignore;

            wrap.Add(field);
            wrap.Add(placeholder);
            searchBar.Add(wrap);

            clearButton = new Button(() => { field.value = string.Empty; });
            clearButton.text = UiText.SearchClear;
            clearButton.AddToClassList("search-clear");
            searchBar.Add(clearButton);

            toggleButton = new Button(() => SetOpen(!open));
            toggleButton.AddToClassList("filter-toggle");
            searchBar.Add(toggleButton);

            if (panel != null) BuildPanel();
            SetOpen(false);
            Refresh();
        }

        public void SetOpen(bool value)
        {
            open = value && panel != null;
            UiUtil.SetVisible(panel, open);
            toggleButton.EnableInClassList("active", open || query.ActiveCount > 0);
        }

        // ------------------------------------------------------------------ panel

        void BuildPanel()
        {
            panel.Clear();

            AddCategory(UiText.CatType, (SchoolType[])Enum.GetValues(typeof(SchoolType)), query.Types, UiText.Label);
            AddCategory(UiText.CatRegion, (Region[])Enum.GetValues(typeof(Region)), query.Regions, UiText.Label);
            AddCategory(UiText.CatSize, (SizeBucket[])Enum.GetValues(typeof(SizeBucket)), query.Sizes, UiText.Label);
            AddCategory(UiText.CatCost, (CostBucket[])Enum.GetValues(typeof(CostBucket)), query.Costs, UiText.Label);
            AddCategory(UiText.CatRounds, (RoundFilter[])Enum.GetValues(typeof(RoundFilter)), query.Rounds, UiText.Label);
            AddCategory(UiText.CatTests, (TestFilter[])Enum.GetValues(typeof(TestFilter)), query.Tests, UiText.Label);
            AddCategory(UiText.CatAid, (AidFilter[])Enum.GetValues(typeof(AidFilter)), query.Aid, UiText.Label);

            VisualElement majorRow = AddCategoryRow(UiText.CatMajors);
            foreach (MajorGroup g in SchoolQuery.MajorGroups)
            {
                string key = g.Key;
                AddChip(majorRow, UiText.Label(g), () => query.Majors.Contains(key), () => Toggle(query.Majors, key));
            }

            VisualElement sortRow = AddCategoryRow(UiText.CatSort);
            foreach (SortOrder o in (SortOrder[])Enum.GetValues(typeof(SortOrder)))
            {
                SortOrder order = o;
                AddChip(sortRow, UiText.Label(order), () => query.Sort == order, () => query.Sort = order);
            }

            var reset = new Button(ResetAll);
            reset.text = UiText.FilterReset;
            reset.AddToClassList("filter-reset");
            panel.Add(reset);
        }

        void AddCategory<T>(string title, T[] values, HashSet<T> set, Func<T, string> label)
        {
            VisualElement row = AddCategoryRow(title);
            foreach (T v in values)
            {
                T value = v;
                AddChip(row, label(value), () => set.Contains(value), () => Toggle(set, value));
            }
        }

        VisualElement AddCategoryRow(string title)
        {
            var t = new Label(title);
            t.AddToClassList("filter-cat-title");
            panel.Add(t);

            var row = new VisualElement();
            row.AddToClassList("chip-row");
            panel.Add(row);
            return row;
        }

        void AddChip(VisualElement row, string label, Func<bool> isActive, Action onClick)
        {
            var b = new Button(() => { onClick(); Refresh(); RaiseChanged(); });
            b.text = label;
            b.AddToClassList("chip");
            row.Add(b);
            chips.Add(new KeyValuePair<Button, Func<bool>>(b, isActive));
        }

        static void Toggle<T>(HashSet<T> set, T value)
        {
            if (!set.Remove(value)) set.Add(value);
        }

        void ResetAll()
        {
            if (pendingSearch != null) pendingSearch.Pause();
            query.Clear();
            field.SetValueWithoutNotify(string.Empty);
            Refresh();
            RaiseChanged();
        }

        // ------------------------------------------------------------------ refresh

        void Refresh()
        {
            foreach (KeyValuePair<Button, Func<bool>> kv in chips)
                kv.Key.EnableInClassList("active", kv.Value());

            toggleButton.text = UiText.FilterToggleLabel(query.ActiveCount);
            toggleButton.EnableInClassList("active", open || query.ActiveCount > 0);
            RefreshSearchChrome();
            BuildActiveRow();
        }

        void RefreshSearchChrome()
        {
            bool hasText = !string.IsNullOrEmpty(field.value);
            UiUtil.SetVisible(placeholder, !hasText);
            UiUtil.SetVisible(clearButton, hasText);
        }

        /// <summary>One removable chip per active option, so filters stay visible while the panel is closed.</summary>
        void BuildActiveRow()
        {
            if (activeRow == null) return;
            activeRow.Clear();

            foreach (SchoolType v in query.Types) AddActive(UiText.Label(v), () => query.Types.Remove(v));
            foreach (Region v in query.Regions) AddActive(UiText.Label(v), () => query.Regions.Remove(v));
            foreach (SizeBucket v in query.Sizes) AddActive(UiText.Label(v), () => query.Sizes.Remove(v));
            foreach (CostBucket v in query.Costs) AddActive(UiText.Label(v), () => query.Costs.Remove(v));
            foreach (RoundFilter v in query.Rounds) AddActive(UiText.Label(v), () => query.Rounds.Remove(v));
            foreach (TestFilter v in query.Tests) AddActive(UiText.Label(v), () => query.Tests.Remove(v));
            foreach (AidFilter v in query.Aid) AddActive(UiText.Label(v), () => query.Aid.Remove(v));
            foreach (string k in query.Majors) AddActive(UiText.Label(SchoolQuery.GroupByKey(k)), () => query.Majors.Remove(k));
            if (query.Sort != SortOrder.Default) AddActive(UiText.Label(query.Sort), () => query.Sort = SortOrder.Default);

            bool any = activeRow.childCount > 0;
            if (any)
            {
                var reset = new Button(ResetAll);
                reset.text = UiText.FilterReset;
                reset.AddToClassList("chip");
                reset.AddToClassList("chip-reset");
                activeRow.Add(reset);
            }
            UiUtil.SetVisible(activeRow, any);
        }

        void AddActive(string label, Action remove)
        {
            // Collected before the click, so removing from the set inside the callback is safe.
            var b = new Button(() => { remove(); Refresh(); RaiseChanged(); });
            b.text = label + "  ×";
            b.AddToClassList("chip");
            b.AddToClassList("active");
            activeRow.Add(b);
        }

        void RaiseChanged()
        {
            if (Changed != null) Changed();
        }
    }
}
