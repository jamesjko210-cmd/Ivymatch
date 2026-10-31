using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace IvyMatch
{
    // Search + multi-filter + sort over the school list (CONTRACT section 11).
    // Pure C#: no UnityEngine dependency, so it can be tested outside the editor.
    //
    // Semantics:
    //  - Text search matches name, nameKo, abbr, location and id (case/space/punctuation-insensitive).
    //  - Inside one category the chosen options are OR-ed ("ED or EA"); categories are AND-ed.
    //    Exception: the two intl-aid options are AND-ed (a school must satisfy every chosen aid condition).
    //  - A school whose field is null is excluded while that category is active, and counted in
    //    QueryResult.MissingData so the UI can say "n schools hidden (no data)".

    public enum SchoolType { Private, Public }
    public enum Region { Northeast, Midwest, South, West }
    public enum SizeBucket { Under5k, From5kTo10k, From10kTo20k, Over20k }
    public enum CostBucket { Under60k, From60kTo85k, From85kTo95k, Over95k }
    public enum RoundFilter { ED, ED2, EA, REA, RDOnly }
    public enum TestFilter { Required, Optional, Blind, Flexible }
    public enum AidFilter { NeedBlind, MeetsFullNeed }
    public enum SortOrder { Default, RateAsc, CostAsc, SizeAsc, SizeDesc, DeadlineSoonest, IntlShareDesc, MajorShareDesc }

    /// <summary>A group of CIP 2-digit fields shown as one major filter chip.</summary>
    public sealed class MajorGroup
    {
        public readonly string Key;
        public readonly string LabelEn;
        public readonly string LabelKo;
        public readonly string[] Cips;

        public MajorGroup(string key, string en, string ko, params string[] cips)
        {
            Key = key; LabelEn = en; LabelKo = ko; Cips = cips;
        }
    }

    public sealed class QueryResult
    {
        public List<School> Items = new List<School>();
        /// <summary>Schools that matched everything else but were hidden because an active filter's field is null.</summary>
        public int MissingData;
    }

    public class SchoolQuery
    {
        /// <summary>A major filter matches a school when the group's share of bachelor's degrees is at least this (%).</summary>
        public const float MajorMinShare = 5f;

        public static readonly MajorGroup[] MajorGroups =
        {
            new MajorGroup("cs", "Computer Science", "컴퓨터", "11"),
            new MajorGroup("eng", "Engineering", "공학", "14", "15"),
            new MajorGroup("math", "Math & Physical Sci", "수학·물리과학", "27", "40", "41"),
            new MajorGroup("bio", "Life Sciences", "생명과학", "26"),
            new MajorGroup("health", "Health", "보건·의료", "51"),
            new MajorGroup("bus", "Business", "경영", "52"),
            new MajorGroup("soc", "Social Sciences", "사회과학", "45"),
            new MajorGroup("psych", "Psychology", "심리학", "42"),
            new MajorGroup("hum", "Humanities", "인문학", "05", "16", "23", "24", "38", "39", "54"),
            new MajorGroup("arts", "Arts & Architecture", "예술·건축", "50", "04"),
            new MajorGroup("comm", "Communication", "커뮤니케이션", "09", "10"),
            new MajorGroup("inter", "Interdisciplinary", "융합전공", "30"),
        };

        static readonly Dictionary<string, Region> StateRegion = BuildStateRegions();

        public string Text = "";
        public readonly HashSet<SchoolType> Types = new HashSet<SchoolType>();
        public readonly HashSet<Region> Regions = new HashSet<Region>();
        public readonly HashSet<SizeBucket> Sizes = new HashSet<SizeBucket>();
        public readonly HashSet<CostBucket> Costs = new HashSet<CostBucket>();
        public readonly HashSet<RoundFilter> Rounds = new HashSet<RoundFilter>();
        public readonly HashSet<TestFilter> Tests = new HashSet<TestFilter>();
        public readonly HashSet<AidFilter> Aid = new HashSet<AidFilter>();
        public readonly HashSet<string> Majors = new HashSet<string>(); // MajorGroup.Key
        public SortOrder Sort = SortOrder.Default;

        /// <summary>Number of active filter options (search text not included).</summary>
        public int ActiveCount
        {
            get
            {
                return Types.Count + Regions.Count + Sizes.Count + Costs.Count + Rounds.Count
                       + Tests.Count + Aid.Count + Majors.Count;
            }
        }

        public bool IsEmpty
        {
            get { return ActiveCount == 0 && string.IsNullOrWhiteSpace(Text) && Sort == SortOrder.Default; }
        }

        public void Clear()
        {
            Text = "";
            Types.Clear(); Regions.Clear(); Sizes.Clear(); Costs.Clear();
            Rounds.Clear(); Tests.Clear(); Aid.Clear(); Majors.Clear();
            Sort = SortOrder.Default;
        }

        /// <summary>Filters and sorts. Never throws; null source gives an empty result. Input order is the default order.</summary>
        public QueryResult Apply(IEnumerable<School> schools, DateTime today)
        {
            var result = new QueryResult();
            if (schools == null) return result;

            string needle = Normalize(Text);
            var matched = new List<School>();
            foreach (School s in schools)
            {
                if (s == null) continue;
                if (needle.Length > 0 && !MatchesText(s, needle)) continue;

                bool missing;
                if (!MatchesFilters(s, out missing))
                {
                    if (missing) result.MissingData++;
                    continue;
                }
                matched.Add(s);
            }

            result.Items = SortList(matched, today);
            return result;
        }

        // ------------------------------------------------------------------ text

        static bool MatchesText(School s, string needle)
        {
            foreach (string field in new[] { s.name, s.nameKo, s.abbr, s.location, s.id })
                if (!string.IsNullOrEmpty(field) && Normalize(field).Contains(needle)) return true;
            return false;
        }

        /// <summary>Lower-case, drop spaces and punctuation ("W&amp;M" -> "wm", "St. Louis" -> "stlouis").</summary>
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var sb = new StringBuilder(text.Length);
            foreach (char c in text.Normalize(NormalizationForm.FormC))
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            return sb.ToString();
        }

        // ------------------------------------------------------------------ filters

        /// <summary>
        /// True when every active category accepts the school. When it fails only because a needed field is
        /// null (and nothing else rejected it), <paramref name="missing"/> is true.
        /// </summary>
        bool MatchesFilters(School s, out bool missing)
        {
            missing = false;
            bool rejected = false;

            Check(Types, TypeOf(s), ref rejected, ref missing);
            Check(Regions, RegionOf(s), ref rejected, ref missing);
            Check(Sizes, SizeOf(s), ref rejected, ref missing);
            Check(Costs, CostOf(s), ref rejected, ref missing);

            if (Rounds.Count > 0)
            {
                HashSet<RoundFilter> offered = RoundsOf(s);
                if (offered.Count == 0) missing = true;
                else if (!Rounds.Overlaps(offered)) rejected = true;
            }

            if (Tests.Count > 0)
            {
                TestFilter? t = TestOf(s);
                if (!t.HasValue) missing = true;
                else if (!Tests.Contains(t.Value)) rejected = true;
            }

            if (Aid.Count > 0)
            {
                Cost c = s.cost ?? new Cost();
                foreach (AidFilter a in Aid)
                {
                    bool? v = a == AidFilter.NeedBlind ? c.needBlindIntl : c.meetsFullNeedIntl;
                    if (!v.HasValue) missing = true;
                    else if (!v.Value) rejected = true;
                }
            }

            if (Majors.Count > 0)
            {
                if (!HasMajors(s)) missing = true;
                else if (!Majors.Any(k => GroupShare(s, k) >= MajorMinShare)) rejected = true;
            }

            if (rejected) { missing = false; return false; }
            return !missing;
        }

        static void Check<T>(HashSet<T> wanted, T? actual, ref bool rejected, ref bool missing) where T : struct
        {
            if (wanted.Count == 0) return;
            if (!actual.HasValue) missing = true;
            else if (!wanted.Contains(actual.Value)) rejected = true;
        }

        // ------------------------------------------------------------------ field accessors (null = unknown)

        public static SchoolType? TypeOf(School s)
        {
            if (s.type == "private") return SchoolType.Private;
            if (s.type == "public") return SchoolType.Public;
            return null;
        }

        /// <summary>US Census region from the 2-letter state at the end of "City, ST".</summary>
        public static Region? RegionOf(School s)
        {
            if (string.IsNullOrEmpty(s.location)) return null;
            string loc = s.location.Trim();
            int comma = loc.LastIndexOf(',');
            string st = (comma >= 0 ? loc.Substring(comma + 1) : loc).Trim().ToUpperInvariant();
            return RegionOfState(st);
        }

        /// <summary>US Census region of a 2-letter state code ("MA" -> Northeast), or null.</summary>
        public static Region? RegionOfState(string state)
        {
            Region r;
            return state != null && StateRegion.TryGetValue(state.Trim().ToUpperInvariant(), out r) ? r : (Region?)null;
        }

        public static SizeBucket? SizeOf(School s)
        {
            if (!s.undergradCount.HasValue) return null;
            int n = s.undergradCount.Value;
            if (n < 5000) return SizeBucket.Under5k;
            if (n < 10000) return SizeBucket.From5kTo10k;
            if (n < 20000) return SizeBucket.From10kTo20k;
            return SizeBucket.Over20k;
        }

        public static CostBucket? CostOf(School s)
        {
            int? coa = s.cost != null ? s.cost.costOfAttendance : null;
            if (!coa.HasValue) return null;
            int v = coa.Value;
            if (v < 60000) return CostBucket.Under60k;
            if (v < 85000) return CostBucket.From60kTo85k;
            if (v < 95000) return CostBucket.From85kTo95k;
            return CostBucket.Over95k;
        }

        /// <summary>Round filters a school satisfies. "RD only" = it has rounds but none of them early.</summary>
        public static HashSet<RoundFilter> RoundsOf(School s)
        {
            var set = new HashSet<RoundFilter>();
            if (s.admissions == null || s.admissions.rounds == null) return set;
            bool any = false, early = false;
            foreach (Round r in s.admissions.rounds)
            {
                if (r == null || string.IsNullOrEmpty(r.type)) continue;
                any = true;
                switch (r.type)
                {
                    case "ED": set.Add(RoundFilter.ED); early = true; break;
                    case "ED2": set.Add(RoundFilter.ED2); early = true; break;
                    case "EA": set.Add(RoundFilter.EA); early = true; break;
                    case "REA": set.Add(RoundFilter.REA); early = true; break;
                }
            }
            if (any && !early) set.Add(RoundFilter.RDOnly);
            return set;
        }

        public static TestFilter? TestOf(School s)
        {
            switch (s.admissions != null ? s.admissions.testPolicy : null)
            {
                case "required": return TestFilter.Required;
                case "optional": return TestFilter.Optional;
                case "blind": return TestFilter.Blind;
                case "flexible": return TestFilter.Flexible;
                default: return null;
            }
        }

        public static bool HasMajors(School s)
        {
            return s.majors != null && s.majors.bachelor != null && s.majors.bachelor.Count > 0;
        }

        public static MajorGroup GroupByKey(string key)
        {
            return MajorGroups.FirstOrDefault(g => g.Key == key);
        }

        /// <summary>Sum of the school's bachelor's shares (%) over the group's CIP codes. 0 when unknown.</summary>
        public static float GroupShare(School s, string groupKey)
        {
            MajorGroup g = GroupByKey(groupKey);
            if (g == null || !HasMajors(s)) return 0f;
            float sum = 0f;
            foreach (MajorShare m in s.majors.bachelor)
                if (m != null && Array.IndexOf(g.Cips, m.cip) >= 0) sum += m.pct;
            return sum;
        }

        /// <summary>Groups ordered by share, largest first, only those with a share above zero.</summary>
        public static List<KeyValuePair<MajorGroup, float>> TopGroups(School s, int count)
        {
            return MajorGroups
                .Select(g => new KeyValuePair<MajorGroup, float>(g, GroupShare(s, g.Key)))
                .Where(kv => kv.Value > 0f)
                .OrderByDescending(kv => kv.Value)
                .Take(count)
                .ToList();
        }

        /// <summary>Earliest round deadline on or after <paramref name="today"/>, or null.</summary>
        public static DateTime? NextDeadline(School s, DateTime today)
        {
            if (s.admissions == null || s.admissions.rounds == null) return null;
            DateTime? best = null;
            foreach (Round r in s.admissions.rounds)
            {
                DateTime d;
                if (r == null || string.IsNullOrEmpty(r.deadline)) continue;
                if (!DateTime.TryParseExact(r.deadline.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) continue;
                if (d < today.Date) continue;
                if (!best.HasValue || d < best.Value) best = d;
            }
            return best;
        }

        // ------------------------------------------------------------------ sort

        List<School> SortList(List<School> list, DateTime today)
        {
            SortOrder sort = Sort;
            if (sort == SortOrder.Default && Majors.Count > 0) sort = SortOrder.MajorShareDesc;

            // Stable sort; unknown values always go last.
            switch (sort)
            {
                case SortOrder.RateAsc:
                    return OrderNullsLast(list, s => s.stats != null ? s.stats.rateOverall : null, false);
                case SortOrder.CostAsc:
                    return OrderNullsLast(list, s => s.cost != null ? (float?)s.cost.costOfAttendance : null, false);
                case SortOrder.SizeAsc:
                    return OrderNullsLast(list, s => (float?)s.undergradCount, false);
                case SortOrder.SizeDesc:
                    return OrderNullsLast(list, s => (float?)s.undergradCount, true);
                case SortOrder.IntlShareDesc:
                    return OrderNullsLast(list, s => s.intl != null ? s.intl.intlShare : null, true);
                case SortOrder.DeadlineSoonest:
                    return OrderNullsLast(list, s =>
                    {
                        DateTime? d = NextDeadline(s, today);
                        return d.HasValue ? (float?)(d.Value - today.Date).TotalDays : null;
                    }, false);
                case SortOrder.MajorShareDesc:
                    if (Majors.Count == 0) return list;
                    return OrderNullsLast(list, s => HasMajors(s) ? (float?)Majors.Sum(k => GroupShare(s, k)) : null, true);
                default:
                    return list;
            }
        }

        static List<School> OrderNullsLast(List<School> list, Func<School, float?> key, bool descending)
        {
            var known = list.Where(s => key(s).HasValue);
            var ordered = descending ? known.OrderByDescending(s => key(s).Value) : known.OrderBy(s => key(s).Value);
            return ordered.Concat(list.Where(s => !key(s).HasValue)).ToList();
        }

        static Dictionary<string, Region> BuildStateRegions()
        {
            var d = new Dictionary<string, Region>();
            foreach (string st in "CT ME MA NH RI VT NJ NY PA".Split(' ')) d[st] = Region.Northeast;
            foreach (string st in "IL IN IA KS MI MN MO NE ND OH SD WI".Split(' ')) d[st] = Region.Midwest;
            foreach (string st in "DE DC FL GA MD NC SC VA WV AL KY MS TN AR LA OK TX".Split(' ')) d[st] = Region.South;
            foreach (string st in "AZ CO ID MT NV NM UT WY AK CA HI OR WA".Split(' ')) d[st] = Region.West;
            return d;
        }
    }
}
