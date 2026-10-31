using System.Collections.Generic;
using System.Linq;

namespace IvyMatch
{
    /// <summary>Acceptance-rate filter buckets (CONTRACT section 8).</summary>
    public enum RateFilter
    {
        All,
        Low,    // rate < 5
        Mid,    // 5 <= rate <= 10
        High    // rate > 10
    }

    /// <summary>
    /// Helpers for <see cref="RateFilter"/>. (A static class cannot share the enum's name,
    /// so call these as RateFilters.Apply(...) / RateFilters.Label(...) or as extension methods.)
    /// </summary>
    public static class RateFilters
    {
        /// <summary>
        /// Returns the schools that match the filter, preserving order.
        /// Never throws: a null source yields an empty list; null entries are skipped.
        /// </summary>
        public static List<School> Apply(IEnumerable<School> schools, RateFilter filter)
        {
            if (schools == null) return new List<School>();

            switch (filter)
            {
                // Schools with no rate yet (null) only appear under "All".
                case RateFilter.Low:
                    return schools.Where(s => s != null && s.HasRate && s.stats.rateOverall.Value < 5f).ToList();
                case RateFilter.Mid:
                    return schools.Where(s => s != null && s.HasRate
                                              && s.stats.rateOverall.Value >= 5f && s.stats.rateOverall.Value <= 10f).ToList();
                case RateFilter.High:
                    return schools.Where(s => s != null && s.HasRate && s.stats.rateOverall.Value > 10f).ToList();
                default:
                    return schools.Where(s => s != null).ToList();
            }
        }

        /// <summary>Button caption for the filter ("All", "&lt;5%", "5–10%", "&gt;10%").</summary>
        public static string Label(this RateFilter filter)
        {
            switch (filter)
            {
                case RateFilter.Low: return UiText.FilterLow;
                case RateFilter.Mid: return UiText.FilterMid;
                case RateFilter.High: return UiText.FilterHigh;
                default: return UiText.FilterAll;
            }
        }
    }
}
