using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace IvyMatch
{
    /// <summary>
    /// "My scores" screen. The student types an SAT and/or ACT score; the screen shows
    /// (1) a histogram of how many schools' middle-50% range (CDS C9) includes each score, with the student's bin marked, and
    /// (2) one range bar per school with the student's marker and an inside / below / above label.
    /// Data is the published 25th-75th percentile of ENROLLED students. It is not an admit range and not a prediction.
    /// </summary>
    public class ScoreView
    {
        const float SatMin = 1300f, SatMax = 1600f, SatStep = 20f;
        const float ActMin = 28f, ActMax = 36f, ActStep = 1f;
        const float HistoHeight = 70f;

        static readonly Color BinDim = new Color(1f, 1f, 1f, 0.16f);
        static readonly Color BinUser = new Color(0.357f, 0.553f, 1f, 1f);
        static readonly Color FarBelow = new Color(0.929f, 0.384f, 0.384f, 1f);
        static readonly Color Below = new Color(1f, 0.690f, 0.267f, 1f);
        static readonly Color Inside = new Color(0.357f, 0.553f, 1f, 1f);
        static readonly Color Above = new Color(0.322f, 0.839f, 0.553f, 1f);

        class Entry
        {
            public School School;
            public float Lo, Hi;
        }

        readonly VisualElement root;
        readonly TextField satField;
        readonly TextField actField;
        readonly Button satTab;
        readonly Button actTab;
        readonly Label summary;
        readonly VisualElement histo;
        readonly VisualElement histoAxis;
        readonly VisualElement rows;

        List<School> schools = new List<School>();
        bool useSat = true;

        public ScoreView(VisualElement container)
        {
            if (container == null) throw new ArgumentNullException("container");
            root = container;
            root.Clear();
            root.AddToClassList("score-view");

            var title = new Label("My scores · 내 점수");
            title.AddToClassList("score-title");
            root.Add(title);

            var inputs = new VisualElement();
            inputs.AddToClassList("score-inputs");
            satField = MakeField("SAT (400–1600)", 4, TouchScreenKeyboardType.NumberPad);
            actField = MakeField("ACT (1–36)", 2, TouchScreenKeyboardType.NumberPad);
            inputs.Add(satField);
            inputs.Add(actField);
            root.Add(inputs);

            var tabs = new VisualElement();
            tabs.AddToClassList("score-tabs");
            satTab = new Button(() => SetTab(true));
            satTab.text = "Compare SAT";
            satTab.AddToClassList("filter-btn");
            actTab = new Button(() => SetTab(false));
            actTab.text = "Compare ACT";
            actTab.AddToClassList("filter-btn");
            tabs.Add(satTab);
            tabs.Add(actTab);
            root.Add(tabs);

            summary = new Label();
            summary.AddToClassList("score-summary");
            root.Add(summary);

            var histoTitle = new Label("Schools whose middle 50% includes each score · 각 점수를 중간 50%에 포함하는 학교 수");
            histoTitle.AddToClassList("score-caption");
            root.Add(histoTitle);

            histo = new VisualElement();
            histo.AddToClassList("score-histo");
            root.Add(histo);

            histoAxis = new VisualElement();
            histoAxis.AddToClassList("score-histo-axis");
            root.Add(histoAxis);

            rows = new VisualElement();
            rows.AddToClassList("score-rows");
            root.Add(rows);

            var note = new Label(
                "Official Common Data Set (C9): the middle 50% of ENROLLED first-year students, not admits. " +
                "Average = inside the school's middle 50%. High = above it. Below = under it. Far below = more than 100 SAT or 3 ACT points under it. " +
                "No U.S. school sets a minimum score, so this shows position, not eligibility. · 합격자가 아니라 등록자 기준이며, 자격 판정이나 합격 예측이 아닙니다.");
            note.AddToClassList("score-caption");
            root.Add(note);


            UpdateTabs();
            Rebuild();
        }

        // ------------------------------------------------------------------ public API

        public void SetSchools(List<School> list)
        {
            schools = list != null ? list.Where(s => s != null).ToList() : new List<School>();
            Rebuild();
        }

        // ------------------------------------------------------------------ input

        TextField MakeField(string label, int maxLen, TouchScreenKeyboardType keyboard)
        {
            var f = new TextField(label);
            f.AddToClassList("score-field");
            f.maxLength = maxLen;
            f.isDelayed = false;
            f.keyboardType = keyboard;
            f.RegisterValueChangedCallback(evt => Rebuild());
            return f;
        }

        void SetTab(bool sat)
        {
            if (useSat == sat) return;
            useSat = sat;
            UpdateTabs();
            Rebuild();
        }

        void UpdateTabs()
        {
            satTab.EnableInClassList("active", useSat);
            actTab.EnableInClassList("active", !useSat);
        }

        float? UserScore()
        {
            string raw = (useSat ? satField.value : actField.value) ?? string.Empty;
            raw = raw.Trim();
            if (raw.Length == 0) return null;
            int v;
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) return null;
            if (useSat) return (v >= 400 && v <= 1600) ? (float?)v : null;
            return (v >= 1 && v <= 36) ? (float?)v : null;
        }

        // ------------------------------------------------------------------ parsing

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

        List<Entry> Entries(out int missing)
        {
            var list = new List<Entry>();
            missing = 0;
            foreach (School s in schools)
            {
                string text = s.stats != null ? (useSat ? s.stats.satMid50 : s.stats.actMid50) : null;
                float lo, hi;
                if (TryRange(text, out lo, out hi)) list.Add(new Entry { School = s, Lo = lo, Hi = hi });
                else missing++;
            }
            return list.OrderByDescending(e => e.Lo).ThenByDescending(e => e.Hi).ToList();
        }

        // ------------------------------------------------------------------ drawing

        void Rebuild()
        {
            if (rows == null) return;
            float min = useSat ? SatMin : ActMin;
            float max = useSat ? SatMax : ActMax;
            float step = useSat ? SatStep : ActStep;
            string unit = useSat ? "SAT" : "ACT";

            int missing;
            List<Entry> entries = Entries(out missing);
            float? user = UserScore();

            BuildSummary(entries, missing, user, unit);
            BuildHistogram(entries, user, min, max, step);
            BuildRows(entries, user, min, max);
        }

        void BuildSummary(List<Entry> entries, int missing, float? user, string unit)
        {
            if (entries.Count == 0)
            {
                summary.text = "No " + unit + " ranges in the current list. · 현재 목록에 " + unit + " 자료가 없습니다.";
                return;
            }
            string tail = missing > 0
                ? "\n" + missing + " school(s) left out: no " + unit + " range published. · " + missing + "개교는 " + unit + " 구간 미공개로 제외"
                : string.Empty;
            if (!user.HasValue)
            {
                summary.text = "Enter your " + unit + " score above. · " + unit + " 점수를 입력하세요." + tail;
                return;
            }
            int far = 0, below = 0, inside = 0, above = 0;
            float gap = useSat ? 100f : 3f;
            foreach (Entry e in entries)
            {
                if (user.Value < e.Lo - gap) far++;
                else if (user.Value < e.Lo) below++;
                else if (user.Value > e.Hi) above++;
                else inside++;
            }
            summary.text = unit + " " + Fmt(user.Value) + ": high at " + above + " · average at " + inside +
                           " · below at " + below + " · far below at " + far + " (of " + entries.Count + " schools)" + tail;
        }

        void BuildHistogram(List<Entry> entries, float? user, float min, float max, float step)
        {
            histo.Clear();
            histoAxis.Clear();
            int bins = Mathf.RoundToInt((max - min) / step);
            if (bins <= 0) return;

            int[] counts = new int[bins];
            int peak = 0;
            for (int i = 0; i < bins; i++)
            {
                float center = min + (i + 0.5f) * step;
                int c = 0;
                foreach (Entry e in entries)
                    if (center >= e.Lo && center <= e.Hi) c++;
                counts[i] = c;
                if (c > peak) peak = c;
            }

            int userBin = -1;
            if (user.HasValue)
                userBin = Mathf.Clamp(Mathf.FloorToInt((user.Value - min) / step), 0, bins - 1);

            for (int i = 0; i < bins; i++)
            {
                var col = new VisualElement();
                col.AddToClassList("score-bin");
                var bar = new VisualElement();
                bar.AddToClassList("score-bin-bar");
                float h = peak > 0 ? HistoHeight * counts[i] / peak : 0f;
                bar.style.height = Mathf.Max(counts[i] > 0 ? 2f : 0f, h);
                bar.style.backgroundColor = i == userBin ? BinUser : BinDim;
                col.Add(bar);
                histo.Add(col);
            }

            var left = new Label(Fmt(min));
            var mid = new Label(user.HasValue ? "You: " + Fmt(user.Value) + (user.Value < min ? " (below scale)" : user.Value > max ? " (above scale)" : string.Empty) : string.Empty);
            var right = new Label(Fmt(max));
            left.AddToClassList("score-axis-label");
            mid.AddToClassList("score-axis-label");
            mid.AddToClassList("score-axis-user");
            right.AddToClassList("score-axis-label");
            histoAxis.Add(left);
            histoAxis.Add(mid);
            histoAxis.Add(right);
        }

        void BuildRows(List<Entry> entries, float? user, float min, float max)
        {
            rows.Clear();
            foreach (Entry e in entries)
            {
                var row = new VisualElement();
                row.AddToClassList("score-row");

                var top = new VisualElement();
                top.AddToClassList("score-row-top");
                var name = new Label(e.School.name + "  " + Fmt(e.Lo) + "–" + Fmt(e.Hi));
                name.AddToClassList("score-row-name");
                top.Add(name);

                if (user.HasValue)
                {
                    string status;
                    Color color;
                    float gap = useSat ? 100f : 3f;
                    if (user.Value < e.Lo - gap) { status = "Far below · 크게 낮음"; color = FarBelow; }
                    else if (user.Value < e.Lo) { status = "Below · 낮음"; color = Below; }
                    else if (user.Value > e.Hi) { status = "High · 높음"; color = Above; }
                    else { status = "Average · 평균"; color = Inside; }
                    var st = new Label(status);
                    st.AddToClassList("score-row-status");
                    st.style.color = color;
                    top.Add(st);
                }
                row.Add(top);

                var track = new VisualElement();
                track.AddToClassList("score-track");

                var band = new VisualElement();
                band.AddToClassList("score-band");
                Color bg = UiUtil.ParseColor(e.School.colorHex, UiUtil.FallbackAccent);
                band.style.backgroundColor = bg;
                float p1 = Pct(e.Lo, min, max), p2 = Pct(e.Hi, min, max);
                band.style.left = Length.Percent(p1);
                band.style.width = Length.Percent(Mathf.Max(1.5f, p2 - p1));
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
