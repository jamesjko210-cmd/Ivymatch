using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IvyMatch
{
    /// <summary>
    /// Every user-visible string lives here (CONTRACT section 8) so a later full-Korean switch is a one-file change.
    /// Numbers are always formatted with the invariant culture so "3.6" never becomes "3,6" on a Korean/German device.
    /// </summary>
    public static class UiText
    {
        // ---- Card ----
        public const string RateLabel = "Acceptance rate · 합격률";
        public const string RateUnit = "%";
        public const string EarlyPrefix = "Early";
        public const string Dash = "—";
        public const string Verified = "검증됨";
        public const string Unverified = "검증 전";
        public const string Pending = "검증 중";
        public const string FooterUnchecked = "미확인";
        public const string FooterCheckedSuffix = " 확인";

        // ---- Summary / header / empty ----
        public const string NoMatch = "No schools match this filter · 해당 학교 없음";
        public const string SummaryNoMatch = "No schools match";

        // ---- Filter captions ----
        public const string FilterAll = "All";
        public const string FilterLow = "<5%";
        public const string FilterMid = "5–10%";
        public const string FilterHigh = ">10%";

        // ---- Detail panel ----
        public const string TagEdOffered = "ED offered";
        public const string TagRea = "REA";
        public const string TagEaOffered = "EA offered";
        public const string NoData = "No data · 데이터 없음";
        public const string SourceLinkSuffix = " ↗";
        public const string BulletPrefix = "• ";

        // ---- Formatting helpers (all culture-invariant) ----

        /// <summary>"3.6"</summary>
        public static string Rate(float value)
        {
            return value.ToString("0.0", CultureInfo.InvariantCulture);
        }

        /// <summary>"3.6%"</summary>
        public static string Pct(float value)
        {
            return Rate(value) + RateUnit;
        }

        /// <summary>"54,008" (thousands separator, invariant). Null -> "—".</summary>
        public static string Count(int? value)
        {
            return value.HasValue ? value.Value.ToString("N0", CultureInfo.InvariantCulture) : Dash;
        }

        /// <summary>"$87,450"</summary>
        public static string Usd(int value)
        {
            return "$" + value.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>"2025-11-01" -> "Nov 1"; other text is returned as-is.</summary>
        public static string ShortDate(string iso)
        {
            if (string.IsNullOrWhiteSpace(iso)) return null;
            DateTime d;
            if (DateTime.TryParseExact(iso.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                return d.ToString("MMM d", CultureInfo.InvariantCulture);
            return iso.Trim();
        }

        // ---- v2 info block ----
        public const string InfoDeadlines = "Deadlines · 마감";
        public const string InfoTest = "Tests · 시험";
        public const string InfoCost = "Cost / yr · 연간 비용";
        public const string InfoAid = "Intl aid · 국제학생 지원";
        public const string InfoIntl = "International · 국제학생";
        public const string InfoEssays = "Supplements · 보충 에세이";

        /// <summary>"ED Nov 1 → Dec 15 · RD Jan 1 → Apr 1". Rounds without a deadline are skipped; none -> "—".</summary>
        public static string Deadlines(List<Round> rounds)
        {
            if (rounds == null) return Dash;
            var parts = new List<string>();
            foreach (Round r in rounds)
            {
                if (r == null || string.IsNullOrEmpty(r.type)) continue;
                string dl = ShortDate(r.deadline);
                if (dl == null) continue;
                string n = ShortDate(r.notification);
                parts.Add(n == null ? r.type + " " + dl : r.type + " " + dl + " → " + n);
            }
            return parts.Count == 0 ? Dash : string.Join(" · ", parts);
        }

        /// <summary>"Required · 필수 (Required from fall 2025 entry)"</summary>
        public static string TestPolicy(string policy, string note)
        {
            string head;
            switch (policy)
            {
                case "required": head = "Required · 필수"; break;
                case "optional": head = "Test-optional · 선택"; break;
                case "blind": head = "Test-blind · 미반영"; break;
                case "flexible": head = "Test-flexible · 대체 가능"; break;
                default: head = Dash; break;
            }
            return string.IsNullOrEmpty(note) ? head : head + " (" + note + ")";
        }

        /// <summary>"$87,450 total · tuition $62,000"</summary>
        public static string CostLine(int? coa, int? tuition)
        {
            var parts = new List<string>();
            if (coa.HasValue) parts.Add(Usd(coa.Value) + " total");
            if (tuition.HasValue) parts.Add("tuition " + Usd(tuition.Value));
            return parts.Count == 0 ? Dash : string.Join(" · ", parts);
        }

        /// <summary>"Need-blind · meets full need · CSS Profile · 55% aided"</summary>
        public static string AidLine(bool? needBlind, bool? fullNeed, bool? css, float? aidShare)
        {
            var parts = new List<string>();
            if (needBlind.HasValue) parts.Add(needBlind.Value ? "Need-blind · 재정 무관" : "Need-aware · 재정 고려");
            if (fullNeed.HasValue) parts.Add(fullNeed.Value ? "meets full need" : "partial need");
            if (css.HasValue) parts.Add(css.Value ? "CSS Profile" : "no CSS Profile");
            if (aidShare.HasValue) parts.Add(Rate(aidShare.Value) + "% aided");
            return parts.Count == 0 ? Dash : string.Join(" · ", parts);
        }

        /// <summary>"12.5% intl · 310 Korean · TOEFL 100"</summary>
        public static string IntlLine(float? share, int? korean, string toefl)
        {
            var parts = new List<string>();
            if (share.HasValue) parts.Add(Rate(share.Value) + "% intl");
            if (korean.HasValue) parts.Add(Count(korean) + " Korean · 한국인");
            if (!string.IsNullOrEmpty(toefl)) parts.Add("TOEFL " + toefl);
            return parts.Count == 0 ? Dash : string.Join(" · ", parts);
        }

        /// <summary>"3 essays"</summary>
        public static string EssaysLine(int? count)
        {
            if (!count.HasValue) return Dash;
            return count.Value == 0 ? "None · 없음" : count.Value.ToString(CultureInfo.InvariantCulture) + " essays";
        }

        // ---- Search & filters (CONTRACT section 11) ----
        public const string InfoMajors = "Majors · 전공 (졸업생 비중)";
        public const string SearchPlaceholder = "Search school, city, state · 학교·지역 검색";
        public const string FilterToggle = "Filters · 필터";
        public const string FilterReset = "Reset · 초기화";
        public const string SearchClear = "×";

        public const string CatType = "Type · 유형";
        public const string CatRegion = "Region · 지역";
        public const string CatSize = "Undergrads · 학부생 수";
        public const string CatCost = "Cost / yr · 연간 총비용";
        public const string CatRounds = "Application plan · 지원 전형";
        public const string CatTests = "Tests · 시험 정책";
        public const string CatAid = "Intl aid · 국제학생 재정지원";
        public const string CatMajors = "Majors · 전공 (졸업생 5% 이상)";
        public const string CatSort = "Sort · 정렬";

        // ---- Map view (CONTRACT section 12) ----
        public const string ViewList = "List · 목록";
        public const string ViewMap = "Map · 지도";
        public const string MapAll = "All US · 전체";
        public const string MapHint = "Tap a number to zoom in · 숫자 배지를 누르면 확대됩니다";
        public const string MapSource = "Map: US Census Bureau · Campus locations: NCES IPEDS";
        public const string MapUnavailable = "Map data not found · 지도 데이터를 찾지 못했습니다";

        /// <summary>"Filters · 필터 (3)" when options are active.</summary>
        public static string FilterToggleLabel(int active)
        {
            return active > 0 ? FilterToggle + " (" + active.ToString(CultureInfo.InvariantCulture) + ")" : FilterToggle;
        }

        public static string Label(SchoolType v)
        {
            return v == SchoolType.Private ? "Private · 사립" : "Public · 공립";
        }

        public static string Label(Region v)
        {
            switch (v)
            {
                case Region.Northeast: return "Northeast · 동북부";
                case Region.Midwest: return "Midwest · 중서부";
                case Region.South: return "South · 남부";
                default: return "West · 서부";
            }
        }

        public static string Label(SizeBucket v)
        {
            switch (v)
            {
                case SizeBucket.Under5k: return "< 5k";
                case SizeBucket.From5kTo10k: return "5k–10k";
                case SizeBucket.From10kTo20k: return "10k–20k";
                default: return "20k+";
            }
        }

        public static string Label(CostBucket v)
        {
            switch (v)
            {
                case CostBucket.Under60k: return "< $60k";
                case CostBucket.From60kTo85k: return "$60k–85k";
                case CostBucket.From85kTo95k: return "$85k–95k";
                default: return "$95k+";
            }
        }

        public static string Label(RoundFilter v)
        {
            return v == RoundFilter.RDOnly ? "RD only · 정시만" : v.ToString();
        }

        public static string Label(TestFilter v)
        {
            switch (v)
            {
                case TestFilter.Required: return "Required · 필수";
                case TestFilter.Optional: return "Optional · 선택";
                case TestFilter.Blind: return "Blind · 미반영";
                default: return "Flexible · 대체 가능";
            }
        }

        public static string Label(AidFilter v)
        {
            return v == AidFilter.NeedBlind ? "Need-blind · 재정 무관" : "Meets full need · 100% 지원";
        }

        public static string Label(MajorGroup g)
        {
            return g == null ? string.Empty : g.LabelEn + " · " + g.LabelKo;
        }

        public static string Label(SortOrder v)
        {
            switch (v)
            {
                case SortOrder.RateAsc: return "Acceptance ↑ · 합격률 낮은순";
                case SortOrder.CostAsc: return "Cost ↑ · 총비용 낮은순";
                case SortOrder.SizeAsc: return "Smallest · 규모 작은순";
                case SortOrder.SizeDesc: return "Largest · 규모 큰순";
                case SortOrder.DeadlineSoonest: return "Next deadline · 마감 임박순";
                case SortOrder.IntlShareDesc: return "Most intl · 국제학생 많은순";
                case SortOrder.MajorShareDesc: return "Major share · 전공 비중순";
                default: return "Default · 기본";
            }
        }

        /// <summary>"Computer Science 18.8% · Engineering 14.8% · Business 11.4% (CDS 2025-26 J1)". No data -> "—".</summary>
        public static string MajorsLine(School s)
        {
            if (s == null || !SchoolQuery.HasMajors(s)) return Dash;
            var top = SchoolQuery.TopGroups(s, 3);
            if (top.Count == 0) return Dash;
            string body = string.Join(" · ", top.Select(kv => kv.Key.LabelEn + " " + Rate(kv.Value) + "%"));
            string year = string.IsNullOrEmpty(s.majors.cdsYear) ? "" : " (CDS " + s.majors.cdsYear + " J1)";
            string note = string.IsNullOrEmpty(s.majors.note) ? "" : " — " + s.majors.note;
            return body + year + note;
        }

        /// <summary>" · 3 hidden (no data) · 정보 없음 3개교 제외" or empty.</summary>
        public static string MissingNote(int missing)
        {
            if (missing <= 0) return string.Empty;
            string n = missing.ToString(CultureInfo.InvariantCulture);
            return " · " + n + " hidden (no data) · 정보 없음 " + n + "개교 제외";
        }

        /// <summary>"Class of 2028 · 검증 전"</summary>
        public static string CycleLine(string cycle, string status)
        {
            string tail;
            if (string.Equals(status, "verified", StringComparison.Ordinal)) tail = Verified;
            else if (string.Equals(status, "pending", StringComparison.Ordinal)) tail = Pending;
            else tail = Unverified;
            return string.IsNullOrEmpty(cycle) ? tail : cycle + " · " + tail;
        }

        /// <summary>"Early ED" / "Early REA" / "Early" when the type is unknown.</summary>
        public static string EarlyLabel(string earlyType)
        {
            return string.IsNullOrEmpty(earlyType) ? EarlyPrefix : EarlyPrefix + " " + earlyType;
        }

        /// <summary>"2026.10.20 확인" for "2026-10-20"; any other non-empty text is shown as-is. Null/empty -> "미확인".</summary>
        public static string VerifiedOn(string verifiedAt)
        {
            if (string.IsNullOrWhiteSpace(verifiedAt)) return FooterUnchecked;
            DateTime d;
            if (DateTime.TryParseExact(verifiedAt.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                       DateTimeStyles.None, out d))
                return d.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture) + FooterCheckedSuffix;
            return verifiedAt.Trim() + FooterCheckedSuffix;
        }

        /// <summary>"8 universities · verified 0/8"</summary>
        public static string HeaderSub(int total, int verifiedCount)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} universities · verified {1}/{2}",
                                 total, verifiedCount, total);
        }

        /// <summary>"Showing 50 (42 pending) · Avg 5.4% · Most selective Harvard (3.6%)"</summary>
        public static string Summary(int shown, int pending, float avgRate, string mostSelectiveName, float mostSelectiveRate)
        {
            string head = pending > 0
                ? string.Format(CultureInfo.InvariantCulture, "Showing {0} ({1} pending)", shown, pending)
                : string.Format(CultureInfo.InvariantCulture, "Showing {0}", shown);
            return string.Format(CultureInfo.InvariantCulture,
                                 "{0} · Avg {1}% · Most selective {2} ({3}%)",
                                 head, Rate(avgRate), mostSelectiveName, Rate(mostSelectiveRate));
        }

        /// <summary>"Showing 42 · rates pending"</summary>
        public static string SummaryNoRates(int shown)
        {
            return string.Format(CultureInfo.InvariantCulture, "Showing {0} · rates pending · 합격률 검증 대기", shown);
        }

        /// <summary>"Cambridge, MA · Founded 1636 · ~7,000 undergrad" (empty parts are skipped).</summary>
        public static string DetailSub(string location, int founded, string undergradSize)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(location)) parts.Add(location);
            if (founded > 0) parts.Add("Founded " + founded.ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(undergradSize)) parts.Add(undergradSize);
            return string.Join(" · ", parts);
        }

        /// <summary>"Harvard · 하버드" (or just the English name when no Korean name).</summary>
        public static string DisplayName(string name, string nameKo)
        {
            if (string.IsNullOrEmpty(nameKo)) return name ?? string.Empty;
            return (name ?? string.Empty) + " · " + nameKo;
        }
    }

    /// <summary>Small null-safe UI Toolkit helpers shared by the view classes.</summary>
    internal static class UiUtil
    {
        public static readonly Color FallbackAccent = new Color(0x5b / 255f, 0x8d / 255f, 0xff / 255f, 1f);

        /// <summary>Query on <paramref name="start"/>; if not found, query from the top of its tree. Returns null if absent.</summary>
        public static T Find<T>(VisualElement start, string name) where T : VisualElement
        {
            if (start == null) return null;
            T hit = start.Q<T>(name);
            if (hit != null) return hit;
            VisualElement top = start;
            while (top.parent != null) top = top.parent;
            return top == start ? null : top.Q<T>(name);
        }

        public static void SetText(Label label, string text)
        {
            if (label != null) label.text = text ?? string.Empty;
        }

        /// <summary>
        /// Show/hide with BOTH the "hidden" class and an inline display style, so it works whether the UXML/USS
        /// default is display:none or display:flex.
        /// </summary>
        public static void SetVisible(VisualElement ve, bool visible)
        {
            if (ve == null) return;
            ve.EnableInClassList("hidden", !visible);
            ve.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public static Color ParseColor(string hex, Color fallback)
        {
            Color c;
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out c)) return c;
            return fallback;
        }
    }
}
