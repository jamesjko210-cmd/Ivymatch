using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace IvyMatch
{
    /// <summary>
    /// Binds one instantiated SchoolCard.uxml (its "card-root" element) to one <see cref="School"/>.
    /// All element lookups are null-tolerant: a missing element is skipped, never thrown on.
    /// </summary>
    public class SchoolCardView
    {
        /// <summary>Bars use a fixed 0-20% scale on every card (CONTRACT section 0).</summary>
        public const float BarScaleMax = 20f;

        public School School { get; private set; }
        public VisualElement Root { get; private set; }
        public bool IsSelected { get; private set; }

        readonly Action<School> onClick;
        Vector2 pressPosition;

        public SchoolCardView(VisualElement cardRoot, School school, Action<School> onClick)
        {
            if (cardRoot == null) throw new ArgumentNullException("cardRoot");
            if (school == null) throw new ArgumentNullException("school");

            Root = cardRoot;
            School = school;
            this.onClick = onClick;

            Bind();
            Root.RegisterCallback<PointerDownEvent>(e => pressPosition = e.position, TrickleDown.TrickleDown);
            Root.RegisterCallback<ClickEvent>(OnClickEvent);
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            Root.EnableInClassList("selected", selected);
        }

        void OnClickEvent(ClickEvent evt)
        {
            // A release after a scroll drag is not a tap: selecting would jump the list back to the top.
            if (((Vector2)evt.position - pressPosition).magnitude > DragScrollManipulator.DragThreshold) return;
            if (onClick != null) onClick(School);
        }

        void Bind()
        {
            Stats st = School.stats ?? new Stats();

            // ---- header ----
            Label logo = Root.Q<Label>("logo");
            if (logo != null)
            {
                string logoText = School.LogoText;
                logo.text = logoText;
                // Longer abbreviations ("UCLA", "WashU") get a smaller font so they fit the 42px badge on one line.
                logo.style.fontSize = LogoFontSize(logoText.Length);
                logo.style.whiteSpace = WhiteSpace.NoWrap;
                Color bg = UiUtil.ParseColor(School.colorHex, UiUtil.FallbackAccent);
                logo.style.backgroundColor = bg;
                // White or dark text, whichever contrasts more with the school colour (WCAG contrast ratio).
                logo.style.color = ContrastRatio(bg, Color.white) >= ContrastRatio(bg, LogoDarkText) ? Color.white : LogoDarkText;
            }

            UiUtil.SetText(Root.Q<Label>("school-name"), School.name);
            UiUtil.SetText(Root.Q<Label>("school-cycle"), UiText.CycleLine(School.cycle, School.status));

            // ---- big number ----
            UiUtil.SetText(Root.Q<Label>("rate-label"), UiText.RateLabel);
            UiUtil.SetText(Root.Q<Label>("rate-value"), st.rateOverall.HasValue ? UiText.Rate(st.rateOverall.Value) : UiText.Dash);
            Root.EnableInClassList("no-rate", !st.rateOverall.HasValue);
            UiUtil.SetText(Root.Q<Label>("rate-unit"), UiText.RateUnit);

            // ---- three bars: overall / early / regular ----
            SetBar(Root.Q<VisualElement>("bar-overall"), Root.Q<Label>("text-overall"), st.rateOverall);
            SetBar(Root.Q<VisualElement>("bar-early"), Root.Q<Label>("text-early"), st.rateEarly);
            SetBar(Root.Q<VisualElement>("bar-regular"), Root.Q<Label>("text-regular"), st.rateRegular);

            // Dashboard.uss: ".bar-row.dim" fades rows that have no data.
            Root.Q<VisualElement>("row-overall")?.EnableInClassList("dim", !st.rateOverall.HasValue);
            Root.Q<VisualElement>("row-early")?.EnableInClassList("dim", !st.rateEarly.HasValue);
            Root.Q<VisualElement>("row-regular")?.EnableInClassList("dim", !st.rateRegular.HasValue);

            UiUtil.SetText(Root.Q<Label>("label-early"), UiText.EarlyLabel(st.earlyType));

            // ---- early badge ----
            Label badge = Root.Q<Label>("early-badge");
            if (badge != null)
            {
                bool has = !string.IsNullOrEmpty(st.earlyType);
                badge.text = has ? st.earlyType : string.Empty;
                UiUtil.SetVisible(badge, has);
            }

            // ---- footer ----
            UiUtil.SetText(Root.Q<Label>("footer-verified"), UiText.VerifiedOn(School.verifiedAt));

            string source = string.Empty;
            if (School.sources != null && School.sources.Count > 0 && School.sources[0] != null)
                source = School.sources[0].label ?? string.Empty;
            UiUtil.SetText(Root.Q<Label>("footer-source"), source);
        }

        static void SetBar(VisualElement bar, Label text, float? rate)
        {
            if (rate.HasValue)
            {
                float pct = Mathf.Clamp(rate.Value / BarScaleMax * 100f, 0f, 100f);
                if (bar != null) bar.style.width = Length.Percent(pct);
                UiUtil.SetText(text, UiText.Pct(rate.Value));
            }
            else
            {
                if (bar != null) bar.style.width = Length.Percent(0f);
                UiUtil.SetText(text, UiText.Dash);
            }
        }

        static readonly Color LogoDarkText = new Color(0.047f, 0.055f, 0.071f, 1f);

        /// <summary>WCAG 2 contrast ratio (1..21) between two opaque colours.</summary>
        public static float ContrastRatio(Color a, Color b)
        {
            float la = RelativeLuminance(a), lb = RelativeLuminance(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
        }

        static float RelativeLuminance(Color c)
        {
            return 0.2126f * Linear(c.r) + 0.7152f * Linear(c.g) + 0.0722f * Linear(c.b);
        }

        static float Linear(float v)
        {
            return v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
        }

        /// <summary>Badge font size by text length: 1-2 chars 15px (USS default), 3 chars 13px, 4 chars 11px, 5+ chars 10px.</summary>
        public static float LogoFontSize(int length)
        {
            if (length <= 2) return 15f;
            if (length == 3) return 13f;
            if (length == 4) return 11f;
            return 10f;
        }
    }
}
