using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace IvyMatch
{
    /// <summary>Korean-first strings and small date helpers for the light redesign.</summary>
    public static class KoText
    {
        public static readonly Color Ink = new Color(0x17 / 255f, 0x20 / 255f, 0x33 / 255f, 1f);
        public static readonly Color Muted = new Color(0x5B / 255f, 0x64 / 255f, 0x74 / 255f, 1f);
        public static readonly Color Ok = new Color(0x0A / 255f, 0x7A / 255f, 0x55 / 255f, 1f);
        public static readonly Color Warn = new Color(0xC2 / 255f, 0x41 / 255f, 0x0C / 255f, 1f);
        public static readonly Color Danger = new Color(0xB4 / 255f, 0x23 / 255f, 0x18 / 255f, 1f);

        /// <summary>Deadlines within this many days are shown in the warning colour.</summary>
        public const int SoonDays = 14;

        public static string HeaderTitle(int total)
        {
            return "미국 대학 " + total.ToString(CultureInfo.InvariantCulture) + "곳";
        }

        public static string HeaderSub(int verified)
        {
            return verified.ToString(CultureInfo.InvariantCulture) + "곳은 학교 공식 자료로 확인했어요";
        }

        /// <summary>"지원자 100명 중 약 4명 합격"</summary>
        public static string People(float? rate)
        {
            if (!rate.HasValue) return "합격률 공식 자료 없음";
            int n = Mathf.RoundToInt(rate.Value);
            if (n < 1) return "지원자 100명 중 1명 미만 합격";
            return "지원자 100명 중 약 " + n.ToString(CultureInfo.InvariantCulture) + "명 합격";
        }

        public static bool TryDate(string iso, out DateTime d)
        {
            d = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(iso)) return false;
            return DateTime.TryParseExact(iso.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d);
        }

        /// <summary>"11월 1일", or "2027년 1월 1일" outside the current year.</summary>
        public static string Date(DateTime d, DateTime today)
        {
            string md = d.Month.ToString(CultureInfo.InvariantCulture) + "월 " + d.Day.ToString(CultureInfo.InvariantCulture) + "일";
            return d.Year == today.Year ? md : d.Year.ToString(CultureInfo.InvariantCulture) + "년 " + md;
        }

        public static int DaysUntil(DateTime d, DateTime today)
        {
            return (int)Math.Round((d.Date - today.Date).TotalDays);
        }

        /// <summary>"D-1", "D-day", or "마감" for a past date.</summary>
        public static string DDay(int days)
        {
            if (days < 0) return "마감";
            if (days == 0) return "D-day";
            return "D-" + days.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Earliest round with a deadline on or after today.</summary>
        public static bool NextRound(School s, DateTime today, out Round round, out DateTime date)
        {
            round = null;
            date = DateTime.MaxValue;
            if (s == null || s.admissions == null || s.admissions.rounds == null) return false;
            foreach (Round r in s.admissions.rounds)
            {
                DateTime d;
                if (r == null || !TryDate(r.deadline, out d)) continue;
                if (d.Date < today.Date) continue;
                if (round == null || d < date) { round = r; date = d; }
            }
            return round != null;
        }

        public static string RoundName(string type)
        {
            switch (type)
            {
                case "ED": return "ED 조기 지원 (합격 시 등록)";
                case "ED2": return "ED2 2차 조기 지원";
                case "EA": return "EA 조기 지원";
                case "REA": return "REA 조기 지원";
                case "RD": return "RD 정시 지원";
                default: return type ?? string.Empty;
            }
        }

        /// <summary>"CDS 2025-26" for "Class of 2029".</summary>
        public static string CdsLabel(string cycle)
        {
            if (string.IsNullOrEmpty(cycle)) return "공식 자료";
            string digits = string.Empty;
            foreach (char c in cycle) if (char.IsDigit(c)) digits += c;
            int year;
            if (digits.Length != 4 || !int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out year)) return "공식 자료";
            int start = year - 4;
            return "CDS " + start.ToString(CultureInfo.InvariantCulture) + "-" + ((start + 1) % 100).ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>"10월 21일 확인"; null -> "확인 전".</summary>
        public static string CheckedOn(string verifiedAt)
        {
            DateTime d;
            if (!TryDate(verifiedAt, out d)) return "확인 전";
            return d.Month.ToString(CultureInfo.InvariantCulture) + "월 " + d.Day.ToString(CultureInfo.InvariantCulture) + "일 확인";
        }

        public static string StatusText(string status)
        {
            if (string.Equals(status, "verified", StringComparison.Ordinal)) return null;
            if (string.Equals(status, "pending", StringComparison.Ordinal)) return "검증 중";
            return "검증 전";
        }

        public static string Usd(int? v)
        {
            return v.HasValue ? "$" + v.Value.ToString("N0", CultureInfo.InvariantCulture) : UiText.Dash;
        }

        public static string Pct(float? v)
        {
            return v.HasValue ? UiText.Rate(v.Value) + "%" : UiText.Dash;
        }

        public static string NeedBlind(bool? v)
        {
            if (!v.HasValue) return UiText.Dash;
            return v.Value ? "재정 무관" : "재정 고려";
        }

        public static string YesNo(bool? v)
        {
            if (!v.HasValue) return null;
            return v.Value ? "예" : "아니요";
        }

        public static string TestPolicy(string policy)
        {
            switch (policy)
            {
                case "required": return "시험 점수 필수 (SAT 또는 ACT)";
                case "optional": return "시험 점수 선택 제출";
                case "blind": return "시험 점수 반영 안 함";
                case "flexible": return "다른 시험으로 대체 가능";
                default: return null;
            }
        }

        public static string Toefl(string toefl)
        {
            if (string.IsNullOrWhiteSpace(toefl)) return null;
            string t = toefl.Trim();
            if (t.IndexOf("not required", StringComparison.OrdinalIgnoreCase) >= 0) return "필요 없음";
            return t;
        }

        public static string TypeName(string type)
        {
            if (type == "private") return "사립";
            if (type == "public") return "공립";
            return null;
        }

        /// <summary>First source whose label contains any keyword, else null.</summary>
        public static SourceRef FindSource(School s, params string[] keywords)
        {
            if (s == null || s.sources == null) return null;
            foreach (string k in keywords)
                foreach (SourceRef src in s.sources)
                    if (src != null && !string.IsNullOrEmpty(src.url) && !string.IsNullOrEmpty(src.label)
                        && src.label.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)
                        return src;
            return null;
        }

        /// <summary>Opens only http/https URLs.</summary>
        public static void OpenUrl(string url)
        {
            Uri u;
            if (Uri.TryCreate(url, UriKind.Absolute, out u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps))
                Application.OpenURL(u.AbsoluteUri);
            else
                Debug.LogWarning("[IvyMatch] Refusing to open non-http(s) URL: " + url);
        }
    }
}
