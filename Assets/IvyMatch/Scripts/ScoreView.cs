using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace IvyMatch
{
    /// <summary>
    /// "내 점수로 보기" tab. The student types an SAT or ACT score. Each school's published middle 50%
    /// (CDS C9, enrolled first-years) puts the score in one of four groups: 높음 / 평균 / 낮음 / 크게 낮음.
    /// The four count tiles filter the school list; a histogram shows how many schools' ranges include each score.
    /// Position only: not eligibility, not a prediction.
    /// </summary>
    public class ScoreView
    {
        const float SatMin = 1300f, SatMax = 1600f, SatStep = 20f, SatGap = 100f;
        const float ActMin = 28f, ActMax = 36f, ActStep = 1f, ActGap = 3f;
        const float HistoHeight = 96f;
        const int RowLimit = 10;

        static readonly string[] KindLabels = { "높음", "평균", "낮음", "크게 낮음" };
        static readonly Color[] KindColors = { KoText.Ok, KoText.Ink, KoText.Warn, KoText.Danger };
        static readonly Color BinDim = new Color(0xCB / 255f, 0xD3 / 255f, 0xDC / 255f, 1f);

        class Entry
        {
            public School School;
            public float Lo, Hi;
            public int Kind = -1;
        }

        readonly VisualElement root;
        readonly Button satTab, actTab;
        readonly Label inputLabel;
        readonly TextField field;
        readonly Button[] tiles = new Button[4];
        readonly Label[] tileNums = new Label[4];
        readonly Label summary;
        readonly VisualElement histo;
        readonly Label axisMin, axisYou, axisMax;
        readonly Label listTitle;
        readonly VisualElement rows;
        readonly Label more;

        List<School> schools = new List<School>();
        bool useSat = true;
        string satText = string.Empty, actText = string.Empty;
        int filter = 1;

        public ScoreView(VisualElement container)
        {
            if (container == null) throw new ArgumentNullException("container");
            root = container;
            root.Clear();

            root.Add(Text("내 점수로 보기", "score-h1"));
            root.Add(Text("점수를 넣으면 학교마다 등록한 학생들의 가운데 50% 점수와 비교해 줘요.", "score-lead"));

            var seg = new VisualElement();
            seg.AddToClassList("seg");
            satTab = new Button(() => SetTab(true)) { text = "SAT" };
            actTab = new Button(() => SetTab(false)) { text = "ACT" };
            satTab.AddToClassList("seg-btn");
            actTab.AddToClassList("seg-btn");
            seg.Add(satTab);
            seg.Add(actTab);
            root.Add(seg);

            var inputBox = new VisualElement();
            inputBox.AddToClassList("score-input-box");
            inputLabel = Text("", "score-input-label");
            inputBox.Add(inputLabel);
            field = new TextField();
            field.AddToClassList("score-field");
            field.isDelayed = false;
            field.keyboardType = TouchScreenKeyboardType.NumberPad;
            field.RegisterValueChangedCallback(OnInput);
            inputBox.Add(field);
            root.Add(inputBox);

            var tileRow = new VisualElement();
            tileRow.AddToClassList("tile-row");
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                var t = new Button(() => { filter = k; Rebuild(); });
                t.AddToClassList("tile");
                if (i == 3) t.AddToClassList("tile-last");
                tileNums[i] = Text("–", "tile-num");
                tileNums[i].style.color = KindColors[i];
                t.Add(tileNums[i]);
                t.Add(Text(KindLabels[i], "tile-label"));
                tiles[i] = t;
                tileRow.Add(t);
            }
            root.Add(tileRow);

            summary = Text("", "score-summary");
            root.Add(summary);

            var hbox = new VisualElement();
            hbox.AddToClassList("box");
            hbox.Add(Text("점수별로 겹치는 학교 수", "box-title"));
            hbox.Add(Text("막대가 높을수록 그 점수가 가운데 50%에 드는 학교가 많아요.", "box-desc"));
            histo = new VisualElement();
            histo.AddToClassList("score-histo");
            hbox.Add(histo);
            var axis = new VisualElement();
            axis.AddToClassList("score-axis");
            axisMin = Text("", "score-axis-label");
            axisYou = Text("", "score-axis-you");
            axisMax = Text("", "score-axis-label");
            axis.Add(axisMin);
            axis.Add(axisYou);
            axis.Add(axisMax);
            hbox.Add(axis);
            root.Add(hbox);

            var lbox = new VisualElement();
            lbox.AddToClassList("box");
            listTitle = Text("", "box-title");
            lbox.Add(listTitle);
            var legend = new VisualElement();
            legend.AddToClassList("score-legend");
            var lband = new VisualElement();
            lband.AddToClassList("legend-band");
            legend.Add(lband);
            legend.Add(Text("가운데 50% 범위", "legend-text"));
            var lmark = new VisualElement();
            lmark.AddToClassList("legend-mark");
            legend.Add(lmark);
            legend.Add(Text("내 점수", "legend-text"));
            lbox.Add(legend);
            rows = new VisualElement();
            lbox.Add(rows);
            more = Text("", "score-more");
            lbox.Add(more);
            root.Add(lbox);

            root.Add(Text(
                "학교가 공개한 Common Data Set C9 기준이에요. 합격자가 아니라 실제 등록한 신입생의 점수라서 합격 가능성을 뜻하지 않아요. " +
                "미국 대학에는 최저 점수가 없어요. 크게 낮음은 하위 25% 점수보다 SAT 100점, ACT 3점 넘게 낮다는 뜻이에요.", "score-caption"));

            UpdateTabs();
            Rebuild();
        }

        public void SetSchools(List<School> list)
        {
            schools = list != null ? list.Where(s => s != null).ToList() : new List<School>();
            Rebuild();
        }

        // ------------------------------------------------------------------ input

        void OnInput(ChangeEvent<string> evt)
        {
            string raw = evt.newValue ?? string.Empty;
            string digits = new string(raw.Where(char.IsDigit).ToArray());
            int max = useSat ? 4 : 2;
            if (digits.Length > max) digits = digits.Substring(0, max);
            if (digits != raw) field.SetValueWithoutNotify(digits);
            if (useSat) satText = digits; else actText = digits;
            Rebuild();
        }

        void SetTab(bool sat)
        {
            if (useSat == sat) return;
            useSat = sat;
            field.SetValueWithoutNotify(useSat ? satText : actText);
            UpdateTabs();
            Rebuild();
        }

        void UpdateTabs()
        {
            satTab.EnableInClassList("active", useSat);
            actTab.EnableInClassList("active", !useSat);
            inputLabel.text = useSat ? "내 SAT 점수 (400–1600)" : "내 ACT 점수 (1–36)";
        }

        float? UserScore()
        {
            string raw = useSat ? satText : actText;
            int v;
            if (string.IsNullOrEmpty(raw) || !int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) return null;
            if (useSat) return (v >= 400 && v <= 1600) ? (float?)v : null;
            return (v >= 1 && v <= 36) ? (float?)v : null;
        }

        static bool TryRange(string text, out float lo, out float hi)
        {
            lo = 0f; hi = 0f;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string[] parts = text.Replace('–', '-').Replace('—', '-').Split('-');
            if (parts.Length != 2) return false;
            float a, b;
            if (!float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out a)) return false;
            if (!float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out b)) return false;
            if (b < a) return false;
            lo = a; hi = b;
            return true;
        }

        // ------------------------------------------------------------------ drawing

        void Rebuild()
        {
            float min = useSat ? SatMin : ActMin;
            float max = useSat ? SatMax : ActMax;
            float step = useSat ? SatStep : ActStep;
            float gap = useSat ? SatGap : ActGap;
            string unit = useSat ? "SAT" : "ACT";
            float? user = UserScore();

            var entries = new List<Entry>();
            int missing = 0;
            foreach (School s in schools)
            {
                string text = s.stats != null ? (useSat ? s.stats.satMid50 : s.stats.actMid50) : null;
                float lo, hi;
                if (TryRange(text, out lo, out hi)) entries.Add(new Entry { School = s, Lo = lo, Hi = hi });
                else missing++;
            }
            entries = entries.OrderByDescending(e => e.Lo).ThenByDescending(e => e.Hi).ToList();

            int[] counts = new int[4];
            foreach (Entry e in entries)
            {
                if (!user.HasValue) continue;
                float v = user.Value;
                e.Kind = v > e.Hi ? 0 : v >= e.Lo ? 1 : v >= e.Lo - gap ? 2 : 3;
                counts[e.Kind]++;
            }

            for (int i = 0; i < 4; i++)
            {
                tileNums[i].text = user.HasValue ? counts[i].ToString(CultureInfo.InvariantCulture) : "–";
                tiles[i].EnableInClassList("active", user.HasValue && filter == i);
            }

            if (entries.Count == 0)
                summary.text = "지금 목록에는 " + unit + " 범위를 공개한 학교가 없어요.";
            else if (!user.HasValue)
                summary.text = "점수를 넣으면 학교마다 어디쯤인지 보여 줘요.";
            else
                summary.text = entries.Count + "곳과 비교했어요." + (missing > 0 ? " " + missing + "곳은 " + unit + " 범위를 공개하지 않아 뺐어요." : string.Empty);

            BuildHistogram(entries, user, min, max, step);

            List<Entry> shown = user.HasValue ? entries.Where(e => e.Kind == filter).ToList() : entries;
            listTitle.text = user.HasValue ? KindLabels[filter] + " 학교 " + shown.Count + "곳" : "전체 " + entries.Count + "곳";
            BuildRows(shown.Take(RowLimit).ToList(), user, min, max);
            more.text = shown.Count > RowLimit ? "그 밖에 " + (shown.Count - RowLimit) + "곳 더 있어요" : "높은 점수 범위부터 정렬했어요";
        }

        void BuildHistogram(List<Entry> entries, float? user, float min, float max, float step)
        {
            histo.Clear();
            int bins = Mathf.RoundToInt((max - min) / step);
            int[] counts = new int[bins];
            int peak = 0;
            for (int i = 0; i < bins; i++)
            {
                float center = min + (i + 0.5f) * step;
                int c = 0;
                foreach (Entry e in entries) if (center >= e.Lo && center <= e.Hi) c++;
                counts[i] = c;
                if (c > peak) peak = c;
            }
            int userBin = user.HasValue ? Mathf.Clamp(Mathf.FloorToInt((user.Value - min) / step), 0, bins - 1) : -1;
            for (int i = 0; i < bins; i++)
            {
                var bar = new VisualElement();
                bar.AddToClassList("score-bin");
                float h = peak > 0 ? HistoHeight * counts[i] / peak : 0f;
                bar.style.height = Mathf.Max(counts[i] > 0 ? 3f : 0f, h);
                bar.style.backgroundColor = i == userBin ? KoText.Ink : BinDim;
                histo.Add(bar);
            }
            axisMin.text = Fmt(min);
            axisMax.text = Fmt(max);
            axisYou.text = user.HasValue ? "내 점수 " + Fmt(user.Value) + (user.Value < min || user.Value > max ? " (범위 밖)" : string.Empty) : string.Empty;
        }

        void BuildRows(List<Entry> list, float? user, float min, float max)
        {
            rows.Clear();
            foreach (Entry e in list)
            {
                var row = new VisualElement();
                row.AddToClassList("score-row");

                var top = new VisualElement();
                top.AddToClassList("score-row-top");
                var name = Text(string.IsNullOrEmpty(e.School.nameKo) ? e.School.name : e.School.nameKo, "score-row-name");
                top.Add(name);
                top.Add(Text(Fmt(e.Lo) + "–" + Fmt(e.Hi), "score-row-range"));
                if (e.Kind >= 0)
                {
                    Label st = Text(KindLabels[e.Kind], "score-row-status");
                    st.style.color = KindColors[e.Kind];
                    top.Add(st);
                }
                row.Add(top);

                var track = new VisualElement();
                track.AddToClassList("score-track");
                var band = new VisualElement();
                band.AddToClassList("score-band");
                band.style.backgroundColor = UiUtil.ParseColor(e.School.colorHex, UiUtil.FallbackAccent);
                float p1 = Pct(e.Lo, min, max), p2 = Pct(e.Hi, min, max);
                band.style.left = Length.Percent(p1);
                band.style.width = Length.Percent(Mathf.Max(2f, p2 - p1));
                track.Add(band);
                if (user.HasValue)
                {
                    var marker = new VisualElement();
                    marker.AddToClassList("score-marker");
                    marker.style.left = Length.Percent(Pct(user.Value, min, max));
                    track.Add(marker);
                }
                row.Add(track);
                rows.Add(row);
            }
        }

        static Label Text(string text, string cls)
        {
            var l = new Label(text ?? string.Empty);
            l.AddToClassList(cls);
            return l;
        }

        static float Pct(float v, float min, float max)
        {
            return Mathf.Clamp01((v - min) / (max - min)) * 100f;
        }

        static string Fmt(float v)
        {
            return Math.Abs(v - Mathf.Round(v)) < 0.001f
                ? Mathf.RoundToInt(v).ToString(CultureInfo.InvariantCulture)
                : v.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
