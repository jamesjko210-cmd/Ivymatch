using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace IvyMatch
{
    /// <summary>
    /// Binds the detail panel (Dashboard.uxml "detail-panel") to the selected school and drives the
    /// three action sections (strategy / lookfor / compare). Null-tolerant: missing elements are skipped.
    /// </summary>
    public class DetailPanelView
    {
        public const string SectionStrategy = "strategy";
        public const string SectionLookFor = "lookfor";
        public const string SectionCompare = "compare";

        /// <summary>The school currently shown, or null when the panel is hidden.</summary>
        public School Current { get; private set; }

        /// <summary>The section key currently shown ("strategy"/"lookfor"/"compare"), or null for none.</summary>
        public string CurrentSection { get; private set; }

        readonly VisualElement panel;
        readonly Func<string, School> lookupById;

        readonly Label title, sub, statApplied, statAdmitted, statEnrolled, statSat, statAct, notable;
        readonly Label infoDeadlines, infoTest, infoCost, infoAid, infoIntl, infoEssays, infoMajors;
        readonly VisualElement tags, sources, secStrategy, secLookFor, secCompare;
        readonly Button btnStrategy, btnLookFor, btnCompare;

        public DetailPanelView(VisualElement panel, Func<string, School> lookupById)
        {
            if (panel == null) throw new ArgumentNullException("panel");
            this.panel = panel;
            this.lookupById = lookupById;

            title = UiUtil.Find<Label>(panel, "detail-title");
            sub = UiUtil.Find<Label>(panel, "detail-sub");
            statApplied = UiUtil.Find<Label>(panel, "stat-applied");
            statAdmitted = UiUtil.Find<Label>(panel, "stat-admitted");
            statEnrolled = UiUtil.Find<Label>(panel, "stat-enrolled");
            statSat = UiUtil.Find<Label>(panel, "stat-sat");
            statAct = UiUtil.Find<Label>(panel, "stat-act");
            notable = UiUtil.Find<Label>(panel, "detail-notable");
            infoDeadlines = UiUtil.Find<Label>(panel, "info-deadlines");
            infoTest = UiUtil.Find<Label>(panel, "info-test");
            infoCost = UiUtil.Find<Label>(panel, "info-cost");
            infoAid = UiUtil.Find<Label>(panel, "info-aid");
            infoIntl = UiUtil.Find<Label>(panel, "info-intl");
            infoEssays = UiUtil.Find<Label>(panel, "info-essays");
            infoMajors = UiUtil.Find<Label>(panel, "info-majors");
            tags = UiUtil.Find<VisualElement>(panel, "detail-tags");
            sources = UiUtil.Find<VisualElement>(panel, "detail-sources");
            secStrategy = UiUtil.Find<VisualElement>(panel, "section-strategy");
            secLookFor = UiUtil.Find<VisualElement>(panel, "section-lookfor");
            secCompare = UiUtil.Find<VisualElement>(panel, "section-compare");
            btnStrategy = UiUtil.Find<Button>(panel, "btn-strategy");
            btnLookFor = UiUtil.Find<Button>(panel, "btn-lookfor");
            btnCompare = UiUtil.Find<Button>(panel, "btn-compare");

            Hide();
        }

        // ------------------------------------------------------------------ show / hide

        /// <summary>Fills the panel for a school and makes it visible. All three sections start hidden;
        /// call <see cref="ShowSection"/> afterwards.</summary>
        public void Show(School school)
        {
            if (school == null) { Hide(); return; }

            Current = school;
            Stats st = school.stats ?? new Stats();

            UiUtil.SetText(title, UiText.DisplayName(school.name, school.nameKo));
            UiUtil.SetText(sub, UiText.DetailSub(school.location, school.founded, school.undergradSize));

            UiUtil.SetText(statApplied, UiText.Count(st.applied));
            UiUtil.SetText(statAdmitted, UiText.Count(st.admitted));
            UiUtil.SetText(statEnrolled, UiText.Count(st.enrolled));
            BuildInfo(school);
            UiUtil.SetText(statSat, string.IsNullOrEmpty(st.satMid50) ? UiText.Dash : st.satMid50);
            UiUtil.SetText(statAct, string.IsNullOrEmpty(st.actMid50) ? UiText.Dash : st.actMid50);

            BuildTags(school, st);
            UiUtil.SetText(notable, school.notable ?? string.Empty);
            BuildSources(school);

            ShowSection(null); // hide all three until the caller picks one
            UiUtil.SetVisible(panel, true);
        }

        public void Hide()
        {
            Current = null;
            CurrentSection = null;
            ShowSection(null);
            UiUtil.SetVisible(panel, false);
        }

        // ------------------------------------------------------------------ sections

        /// <summary>
        /// Shows only the requested section ("strategy" | "lookfor" | "compare"); any other key (or null)
        /// hides all three. Filling happens here so the content always matches <see cref="Current"/>.
        /// </summary>
        public void ShowSection(string key)
        {
            string k = string.IsNullOrEmpty(key) ? null : key.ToLowerInvariant();
            if (Current == null) k = null;
            if (k != SectionStrategy && k != SectionLookFor && k != SectionCompare) k = null;

            CurrentSection = k;

            UiUtil.SetVisible(secStrategy, k == SectionStrategy);
            UiUtil.SetVisible(secLookFor, k == SectionLookFor);
            UiUtil.SetVisible(secCompare, k == SectionCompare);

            if (btnStrategy != null) btnStrategy.EnableInClassList("active", k == SectionStrategy);
            if (btnLookFor != null) btnLookFor.EnableInClassList("active", k == SectionLookFor);
            if (btnCompare != null) btnCompare.EnableInClassList("active", k == SectionCompare);

            if (k == SectionStrategy) FillBullets(secStrategy, Current.strategy);
            else if (k == SectionLookFor) FillBullets(secLookFor, Current.lookFor);
            else if (k == SectionCompare) FillCompare(secCompare);
        }

        static void FillBullets(VisualElement container, List<string> items)
        {
            if (container == null) return;
            container.Clear();

            bool any = false;
            if (items != null)
            {
                foreach (string text in items)
                {
                    if (string.IsNullOrEmpty(text)) continue;
                    var l = new Label(UiText.BulletPrefix + text);
                    l.AddToClassList("bullet");
                    l.AddToClassList("section-item");
                    l.style.whiteSpace = WhiteSpace.Normal;
                    container.Add(l);
                    any = true;
                }
            }

            if (!any) AddNoData(container);
        }

        void FillCompare(VisualElement container)
        {
            if (container == null) return;
            container.Clear();

            var row = new VisualElement();
            row.AddToClassList("compare-row");
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;

            int added = 0;
            if (Current.compare != null && lookupById != null)
            {
                foreach (string id in Current.compare)
                {
                    School other = string.IsNullOrEmpty(id) ? null : lookupById(id);
                    if (other == null) continue;

                    var card = new VisualElement();
                    card.AddToClassList("compare-card");
                    card.AddToClassList("compare-chip"); // class name used by Dashboard.uss
                    if (string.Equals(id, Current.id, StringComparison.Ordinal))
                    {
                        card.AddToClassList("highlight");
                        card.AddToClassList("current"); // Dashboard.uss: .compare-chip.current
                    }

                    var name = new Label(other.name ?? string.Empty);
                    name.AddToClassList("compare-name");
                    var pct = new Label(other.HasRate ? UiText.Pct(other.stats.rateOverall.Value) : UiText.Dash);
                    pct.AddToClassList("compare-rate");

                    card.Add(name);
                    card.Add(pct);
                    row.Add(card);
                    added++;
                }
            }

            if (added > 0) container.Add(row);
            else AddNoData(container);
        }

        static void AddNoData(VisualElement container)
        {
            var l = new Label(UiText.NoData);
            l.AddToClassList("bullet");
            l.AddToClassList("section-item");
            l.style.whiteSpace = WhiteSpace.Normal;
            container.Add(l);
        }

        // ------------------------------------------------------------------ v2 info block (deadlines / test / cost / intl)

        void BuildInfo(School school)
        {
            Admissions ad = school.admissions ?? new Admissions();
            Cost cost = school.cost ?? new Cost();
            Intl intl = school.intl ?? new Intl();

            UiUtil.SetText(infoDeadlines, UiText.Deadlines(ad.rounds));
            UiUtil.SetText(infoTest, UiText.TestPolicy(ad.testPolicy, ad.testPolicyNote));
            UiUtil.SetText(infoCost, UiText.CostLine(cost.costOfAttendance, cost.tuition));
            UiUtil.SetText(infoAid, UiText.AidLine(cost.needBlindIntl, cost.meetsFullNeedIntl, cost.cssProfile, cost.intlAidShare));
            UiUtil.SetText(infoIntl, UiText.IntlLine(intl.intlShare, intl.koreanStudents, intl.toeflMin));
            UiUtil.SetText(infoEssays, UiText.EssaysLine(ad.supplementEssays));
            UiUtil.SetText(infoMajors, UiText.MajorsLine(school));
        }

        // ------------------------------------------------------------------ tags / sources

        void BuildTags(School school, Stats st)
        {
            if (tags == null) return;
            tags.Clear();

            if (school.tags != null)
            {
                foreach (string t in school.tags)
                {
                    if (string.IsNullOrEmpty(t)) continue;
                    var l = new Label(t);
                    l.AddToClassList("tag");
                    tags.Add(l);
                }
            }

            string early = null;
            if (st.earlyType == "ED") early = UiText.TagEdOffered;
            else if (st.earlyType == "REA") early = UiText.TagRea;
            else if (st.earlyType == "EA") early = UiText.TagEaOffered;
            if (early != null)
            {
                var l = new Label(early);
                l.AddToClassList("tag");
                l.AddToClassList("blue");
                tags.Add(l);
            }
        }

        void BuildSources(School school)
        {
            if (sources == null) return;
            sources.Clear();
            if (school.sources == null) return;

            foreach (SourceRef src in school.sources)
            {
                if (src == null) continue;
                bool hasUrl = !string.IsNullOrEmpty(src.url);
                var l = new Label((src.label ?? string.Empty) + (hasUrl ? UiText.SourceLinkSuffix : string.Empty));
                l.AddToClassList("source");
                l.AddToClassList("source-link");
                l.style.whiteSpace = WhiteSpace.Normal;
                if (hasUrl)
                {
                    l.AddToClassList("link");
                    string url = src.url;
                    l.RegisterCallback<ClickEvent>(e => OpenUrl(url));
                }
                sources.Add(l);
            }
        }

        /// <summary>Opens only http/https URLs.</summary>
        static void OpenUrl(string url)
        {
            Uri u;
            if (Uri.TryCreate(url, UriKind.Absolute, out u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps))
                Application.OpenURL(u.AbsoluteUri);
            else
                Debug.LogWarning("[IvyMatch] Refusing to open non-http(s) URL: " + url);
        }
    }
}
