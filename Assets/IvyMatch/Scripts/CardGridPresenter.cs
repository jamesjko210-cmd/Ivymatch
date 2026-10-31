using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace IvyMatch
{
    /// <summary>
    /// Owns the school cards inside "card-grid". Cards are built once per database (<see cref="Rebuild"/>);
    /// filtering and sorting only toggle visibility and, when the order really changed, move cards
    /// (<see cref="Show"/>). This avoids re-instantiating ~50 card templates on every search keystroke.
    /// </summary>
    public class CardGridPresenter
    {
        /// <summary>Creates the card wrapper (the grid's flex item) and its view; return null to skip a school.</summary>
        public delegate SchoolCardView CardFactory(School school, out VisualElement container);

        sealed class Entry
        {
            public VisualElement Container;
            public SchoolCardView View;
            public bool Visible = true;
        }

        readonly VisualElement grid;
        readonly CardFactory factory;
        readonly Dictionary<string, Entry> byId = new Dictionary<string, Entry>();
        readonly List<Entry> all = new List<Entry>();
        List<string> lastOrder;

        /// <summary>How many cards have been created in total (for diagnostics/tests).</summary>
        public int CreatedCount { get; private set; }

        public CardGridPresenter(VisualElement grid, CardFactory factory)
        {
            if (grid == null) throw new ArgumentNullException("grid");
            if (factory == null) throw new ArgumentNullException("factory");
            this.grid = grid;
            this.factory = factory;
        }

        /// <summary>Every card view, visible or not, in creation order.</summary>
        public IEnumerable<SchoolCardView> AllViews
        {
            get { return all.Select(e => e.View); }
        }

        /// <summary>Drops the old cards and builds one per school (call when the database object changes).</summary>
        public void Rebuild(IEnumerable<School> schools)
        {
            foreach (Entry e in all) e.Container.RemoveFromHierarchy();
            all.Clear();
            byId.Clear();
            lastOrder = null;
            if (schools == null) return;

            foreach (School s in schools)
            {
                if (s == null || string.IsNullOrEmpty(s.id) || byId.ContainsKey(s.id)) continue;
                VisualElement container;
                SchoolCardView view = factory(s, out container);
                if (view == null || container == null) continue;
                CreatedCount++;
                grid.Add(container);
                var entry = new Entry { Container = container, View = view };
                byId[s.id] = entry;
                all.Add(entry);
            }
        }

        /// <summary>
        /// Shows exactly <paramref name="ordered"/> in that order and hides the rest.
        /// Returns the visible card views in display order.
        /// </summary>
        public List<SchoolCardView> Show(IList<School> ordered)
        {
            var order = new List<string>();
            var wanted = new HashSet<string>();
            if (ordered != null)
            {
                foreach (School s in ordered)
                {
                    if (s == null || s.id == null || !byId.ContainsKey(s.id) || !wanted.Add(s.id)) continue;
                    order.Add(s.id);
                }
            }

            foreach (KeyValuePair<string, Entry> kv in byId)
            {
                bool v = wanted.Contains(kv.Key);
                if (kv.Value.Visible == v) continue;
                kv.Value.Visible = v;
                UiUtil.SetVisible(kv.Value.Container, v);
            }

            // Re-ordering touches the hierarchy, so only do it when the visible order changed.
            // BringToFront moves a card to the end, so walking the wanted order leaves them in that order
            // (hidden cards stay in front of them, where display:none makes them irrelevant).
            if (lastOrder == null || !order.SequenceEqual(lastOrder))
            {
                foreach (string id in order) byId[id].Container.BringToFront();
                lastOrder = order;
            }

            return order.Select(id => byId[id].View).ToList();
        }
    }
}
