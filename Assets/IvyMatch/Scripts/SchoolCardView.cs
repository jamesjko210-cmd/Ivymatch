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
            DateTime today = DateTime.Today;

            // ---- header ----
            Label logo = Root.Q<Label>("logo");
            if (logo != null)
            {
                string logoText = School.LogoText;
                logo.text = logoText;
                logo.style.fontSize = LogoFontSize(logoText.Length);
                logo.style.whiteSpace = WhiteSpace.NoWrap;
                Color bg = UiUtil.ParseColor(School.colorHex, UiUtil.FallbackAccent);
                logo.style.backgroundColor = bg;
                logo.style.color = ContrastRatio(bg, Color.white) >= ContrastRatio(bg, LogoDarkText) ? Color.white : LogoDarkText;
            }

            UiUtil.SetText(Root.Q<Label>("school-name"), string.IsNullOrEmpty(School.nameKo) ? School.name : School.nameKo);
            string en = School.name ?? string.Empty;
            if (!string.IsNullOrEmpty(School.location)) en += "  /  " + School.location;
            UiUtil.SetText(Root.Q<Label>("school-en"), en);

            Label badge = Root.Q<Label>("early-badge");
            if (badge != null)
            {
                bool has = !string.IsNullOrEmpty(st.earlyType);
                badge.text = has ? st.earlyType : string.Empty;
                UiUtil.SetVisible(badge, has);
            }

            // ---- big number ----
            UiUtil.SetText(Root.Q<Label>("rate-value"), st.rateOverall.HasValue ? UiText.Rate(st.rateOverall.Value) : UiText.Dash);
            UiUtil.SetVisible(Root.Q<Label>("rate-unit"), st.rateOverall.HasValue);
            UiUtil.SetText(Root.Q<Label>("rate-people"), KoText.People(st.rateOverall));
            Root.EnableInClassList("no-rate", !st.rateOverall.HasValue);

            // ---- facts ----
            Round round;
            DateTime date;
            Label dday = Root.Q<Label>("fact-dday");
            if (KoText.NextRound(School, today, out round, out date))
            {
                int days = KoText.DaysUntil(date, today);
                UiUtil.SetText(dday, KoText.DDay(days));
                if (dday != null) dday.EnableInClassList("soon", days <= KoText.SoonDays);
                UiUtil.SetVisible(dday, true);
                UiUtil.SetText(Root.Q<Label>("fact-date"), KoText.Date(date, today) + " " + (round.type ?? string.Empty));
            }
            else
            {
                UiUtil.SetVisible(dday, false);
                UiUtil.SetText(Root.Q<Label>("fact-date"), UiText.Dash);
            }
            Cost cost = School.cost ?? new Cost();
            UiUtil.SetText(Root.Q<Label>("fact-cost"), KoText.Usd(cost.costOfAttendance));
            UiUtil.SetText(Root.Q<Label>("fact-aid"), KoText.NeedBlind(cost.needBlindIntl));

            // ---- footer ----
            string status = KoText.StatusText(School.status);
            UiUtil.SetVisible(Root.Q<VisualElement>("footer-check"), status == null);
            Label src = Root.Q<Label>("footer-source");
            UiUtil.SetText(src, status ?? KoText.CdsLabel(School.cycle));
            if (src != null) src.EnableInClassList("unverified", status != null);
            UiUtil.SetText(Root.Q<Label>("footer-verified"), KoText.CheckedOn(School.verifiedAt));
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
