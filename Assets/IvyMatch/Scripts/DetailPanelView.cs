using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace IvyMatch
{
    /// <summary>
    /// School page (Dashboard.uxml "detail-panel"): a full-screen layer over the list with its own scroll.
    /// Content is built in code into "detail-body": 합격률, 지원 일정, 인기 전공, 비용과 국제학생 지원.
    /// Every section ends with its official source link.
    /// </summary>
    public class DetailPanelView
    {
        // Kept for the controller's API; the redesign has no strategy/compare sections.
        public const string SectionStrategy = "strategy";
        public const string SectionLookFor = "lookfor";
        public const string SectionCompare = "compare";

        public School Current { get; private set; }
        public string CurrentSection { get { return null; } }

        readonly VisualElement panel;
        readonly VisualElement body;
        readonly ScrollView scroll;
        readonly Func<string, School> lookupById;

        public DetailPanelView(VisualElement panel, Func<string, School> lookupById)
        {
            if (panel == null) throw new ArgumentNullException("panel");
            this.panel = panel;
            this.lookupById = lookupById;
            body = panel.Q<VisualElement>("detail-body") ?? panel;
            scroll = panel.Q<ScrollView>("detail-scroll");
            if (scroll != null)
            {
                scroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Elastic;
                scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
                scroll.mouseWheelScrollSize = 60f;
                scroll.AddManipulator(new DragScrollManipulator(scroll));
            }
            Hide();
        }

        public void Show(School school)
        {
            if (school == null) { Hide(); return; }
            bool same = Current != null && Current.id == school.id;
            Current = school;
            Build(school);
            UiUtil.SetVisible(panel, true);
            if (!same && scroll != null) scroll.scrollOffset = Vector2.zero;
        }

        public void Hide()
        {
            Current = null;
            UiUtil.SetVisible(panel, false);
        }

        public void ShowSection(string key) { }

        // ------------------------------------------------------------------ build

        void Build(School s)
        {
            body.Clear();
            DateTime today = DateTime.Today;
            Stats st = s.stats ?? new Stats();
            Admissions ad = s.admissions ?? new Admissions();
            Cost cost = s.cost ?? new Cost();
            Intl intl = s.intl ?? new Intl();

            // ---- title ----
            var head = Row("page-head");
            var logo = new Label(s.LogoText);
            logo.AddToClassList("page-logo");
            Color bg = UiUtil.ParseColor(s.colorHex, UiUtil.FallbackAccent);
            logo.style.backgroundColor = bg;
            logo.style.color = SchoolCardView.ContrastRatio(bg, Color.white) >= SchoolCardView.ContrastRatio(bg, KoText.Ink) ? Color.white : KoText.Ink;
            logo.style.fontSize = SchoolCardView.LogoFontSize(s.LogoText.Length) + 1f;
            head.Add(logo);
            var titles = new VisualElement();
            titles.AddToClassList("page-titles");
            titles.Add(Text(string.IsNullOrEmpty(s.nameKo) ? s.name : s.nameKo, "page-title"));
            titles.Add(Text(s.name, "page-sub"));
            head.Add(titles);
            body.Add(head);

            var chips = Row("page-chips");
            AddChip(chips, s.location);
            if (s.founded > 0) AddChip(chips, s.founded.ToString(CultureInfo.InvariantCulture) + "년 설립");
            if (s.undergradCount.HasValue) AddChip(chips, "학부생 " + s.undergradCount.Value.ToString("N0", CultureInfo.InvariantCulture) + "명");
            AddChip(chips, KoText.TypeName(s.type));
            body.Add(chips);

            // ---- 합격률 ----
            VisualElement rate = Box("합격률", "box-rate");
            var rateRow = Row("page-rate-row");
            rateRow.Add(Text(st.rateOverall.HasValue ? UiText.Rate(st.rateOverall.Value) : UiText.Dash, "page-rate"));
            if (st.rateOverall.HasValue) rateRow.Add(Text("%", "page-rate-unit"));
            rateRow.Add(Text(KoText.People(st.rateOverall), "page-rate-people"));
            rate.Add(rateRow);
            if (st.applied.HasValue && st.applied.Value > 0)
            {
                float applied = st.applied.Value;
                Funnel(rate, "지원", st.applied, 100f, "fill-light");
                Funnel(rate, "합격", st.admitted, st.admitted.HasValue ? st.admitted.Value / applied * 100f : 0f, "fill-ink");
                VisualElement f = Funnel(rate, "등록", st.enrolled, st.enrolled.HasValue ? st.enrolled.Value / applied * 100f : 0f, null);
                if (f != null) f.style.backgroundColor = bg;
            }
            if (!st.rateEarly.HasValue && !st.rateRegular.HasValue)
                rate.Add(Text("얼리와 정시 합격률은 학교가 따로 발표하지 않아요.", "box-note"));
            AddSource(rate, KoText.FindSource(s, "CDS"), "C1");
            body.Add(rate);

            // ---- 지원 일정 ----
            VisualElement dates = Box("지원 일정", "box-dates");
            int shownRounds = 0;
            if (ad.rounds != null)
            {
                foreach (Round r in ad.rounds)
                {
                    if (r == null) continue;
                    var row = Row("date-row");
                    DateTime d;
                    if (KoText.TryDate(r.deadline, out d))
                    {
                        int days = KoText.DaysUntil(d, today);
                        Label dd = Text(KoText.DDay(days), "date-dday");
                        if (days >= 0 && days <= KoText.SoonDays) { dd.AddToClassList("soon"); row.AddToClassList("soon"); }
                        row.Add(dd);
                        row.Add(Text(KoText.Date(d, today), "date-day"));
                    }
                    else
                    {
                        row.Add(Text("", "date-dday"));
                        row.Add(Text("날짜 확인 중", "date-day"));
                    }
                    row.Add(Text(KoText.RoundName(r.type), "date-round"));
                    dates.Add(row);
                    shownRounds++;
                }
            }
            if (shownRounds == 0) dates.Add(Text("공식 일정을 아직 확인하지 못했어요.", "box-note"));
            var extra = new List<string>();
            string test = KoText.TestPolicy(ad.testPolicy);
            if (test != null) extra.Add(test);
            if (ad.supplementEssays.HasValue)
                extra.Add(ad.supplementEssays.Value == 0 ? "보충 에세이 없음" : "보충 에세이 " + ad.supplementEssays.Value.ToString(CultureInfo.InvariantCulture) + "편");
            if (extra.Count > 0) dates.Add(Text(string.Join(". ", extra) + ".", "box-note"));
            AddSource(dates, KoText.FindSource(s, "Deadline", "Timeline", "Dates", "Admission"), null);
            body.Add(dates);

            // ---- 인기 전공 ----
            VisualElement majors = Box("인기 전공", "box-majors");
            majors.Add(Text("졸업생이 많은 순서예요. 지난 학년도 학사 졸업생의 분야별 비율이고, 전공의 수준을 뜻하지는 않아요.", "box-desc"));
            List<KeyValuePair<MajorGroup, float>> top = SchoolQuery.HasMajors(s) ? SchoolQuery.TopGroups(s, 5) : new List<KeyValuePair<MajorGroup, float>>();
            if (top.Count == 0) majors.Add(Text("공식 전공 자료가 없어요.", "box-note"));
            foreach (KeyValuePair<MajorGroup, float> kv in top)
            {
                var line = Row("bar-line");
                line.Add(Text(kv.Key.LabelKo, "bar-name"));
                line.Add(Text(UiText.Rate(kv.Value).Replace(".0", "") + "%", "bar-num"));
                majors.Add(line);
                majors.Add(Bar(kv.Value, "fill-ink", "bar-thin"));
            }
            if (top.Count > 0)
                AddSource(majors, KoText.FindSource(s, "J1", "CDS"), "J1");
            body.Add(majors);

            // ---- 비용과 국제학생 지원 ----
            VisualElement money = Box("비용과 국제학생 지원", "box-money");
            var big = Row("money-row");
            var c1 = new VisualElement(); c1.AddToClassList("money-col");
            c1.Add(Text("연간 총비용", "money-label")); c1.Add(Text(KoText.Usd(cost.costOfAttendance), "money-value"));
            var c2 = new VisualElement(); c2.AddToClassList("money-col");
            c2.Add(Text("등록금", "money-label")); c2.Add(Text(KoText.Usd(cost.tuition), "money-value"));
            big.Add(c1); big.Add(c2);
            money.Add(big);
            money.Add(Text("등록금, 필수 수수료, 기숙사·식비를 더한 금액이에요. 공립대는 주 밖 학생 기준이에요.", "box-note"));
            var rows = new VisualElement();
            rows.AddToClassList("info-list");
            InfoRow(rows, "집안 형편과 무관하게 심사", KoText.YesNo(cost.needBlindIntl), cost.needBlindIntl == true);
            InfoRow(rows, "필요한 금액 100% 지원", KoText.YesNo(cost.meetsFullNeedIntl), cost.meetsFullNeedIntl == true);
            InfoRow(rows, "CSS Profile 제출", cost.cssProfile.HasValue ? (cost.cssProfile.Value ? "필요" : "필요 없음") : null, false);
            InfoRow(rows, "재정지원 받는 국제학생", cost.intlAidShare.HasValue ? KoText.Pct(cost.intlAidShare) : null, false);
            InfoRow(rows, "학부생 중 국제학생", intl.intlShare.HasValue ? KoText.Pct(intl.intlShare) : null, false);
            InfoRow(rows, "TOEFL", KoText.Toefl(intl.toeflMin), false);
            if (rows.childCount > 0)
            {
                rows[rows.childCount - 1].AddToClassList("last");
                money.Add(rows);
            }
            AddSource(money, KoText.FindSource(s, "Financial Aid", "Aid", "Cost", "CDS"), null);
            body.Add(money);

            // ---- action + footer ----
            if (!string.IsNullOrEmpty(ad.applicationUrl))
            {
                string url = ad.applicationUrl;
                var cta = new Button(() => KoText.OpenUrl(url));
                cta.text = (string.IsNullOrEmpty(s.nameKo) ? s.name : s.nameKo) + " 입학처 열기";
                cta.AddToClassList("cta");
                body.Add(cta);
            }
            string status = KoText.StatusText(s.status);
            DateTime checkedOn;
            string foot = KoText.TryDate(s.verifiedAt, out checkedOn)
                ? checkedOn.Year.ToString(CultureInfo.InvariantCulture) + "년 " + checkedOn.Month.ToString(CultureInfo.InvariantCulture) + "월 " + checkedOn.Day.ToString(CultureInfo.InvariantCulture) + "일 공식 자료와 대조했어요"
                : "아직 공식 자료와 대조하지 않았어요";
            if (status != null) foot += " (" + status + ")";
            body.Add(Text(foot, "page-foot"));
        }

        // ------------------------------------------------------------------ helpers

        static Label Text(string text, string cls)
        {
            var l = new Label(text ?? string.Empty);
            if (!string.IsNullOrEmpty(cls)) l.AddToClassList(cls);
            return l;
        }

        static VisualElement Row(string cls)
        {
            var v = new VisualElement();
            v.AddToClassList(cls);
            return v;
        }

        static VisualElement Box(string title, string cls)
        {
            var box = new VisualElement();
            box.AddToClassList("box");
            if (!string.IsNullOrEmpty(cls)) box.AddToClassList(cls);
            box.Add(Text(title, "box-title"));
            return box;
        }

        static void AddChip(VisualElement row, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            row.Add(Text(text, "info-chip"));
        }

        static VisualElement Bar(float pct, string fillClass, string trackClass)
        {
            var track = new VisualElement();
            track.AddToClassList("bar-track");
            if (!string.IsNullOrEmpty(trackClass)) track.AddToClassList(trackClass);
            var fill = new VisualElement();
            fill.AddToClassList("bar-fill");
            if (!string.IsNullOrEmpty(fillClass)) fill.AddToClassList(fillClass);
            fill.style.width = Length.Percent(Mathf.Clamp(pct, pct > 0f ? 1f : 0f, 100f));
            track.Add(fill);
            return track;
        }

        /// <summary>Label + count line and a bar; returns the bar's fill element (null when the count is unknown).</summary>
        static VisualElement Funnel(VisualElement box, string label, int? count, float pct, string fillClass)
        {
            var line = Row("bar-line");
            line.Add(Text(label, "bar-name"));
            line.Add(Text(count.HasValue ? count.Value.ToString("N0", CultureInfo.InvariantCulture) + "명" : UiText.Dash, "bar-num"));
            box.Add(line);
            if (!count.HasValue) return null;
            VisualElement track = Bar(pct, fillClass, "bar-funnel");
            box.Add(track);
            return track[0];
        }

        static void InfoRow(VisualElement list, string key, string value, bool positive)
        {
            if (value == null) return;
            var row = Row("info-row");
            row.Add(Text(key, "info-key"));
            Label v = Text(value, "info-val");
            if (positive) v.AddToClassList("ok");
            row.Add(v);
            list.Add(row);
        }

        static void AddSource(VisualElement box, SourceRef src, string part)
        {
            if (src == null) return;
            string label = src.label ?? string.Empty;
            int paren = label.IndexOf(" (", StringComparison.Ordinal);
            if (paren > 0) label = label.Substring(0, paren);
            if (!string.IsNullOrEmpty(part) && label.IndexOf(part, StringComparison.Ordinal) < 0) label += ", " + part;
            var row = Row("src-row");
            VisualElement mark = Row("check-mark");
            mark.AddToClassList("small");
            row.Add(mark);
            row.Add(Text("출처 " + label, "src-link"));
            string url = src.url;
            row.RegisterCallback<ClickEvent>(e => KoText.OpenUrl(url));
            box.Add(row);
        }
    }
}
