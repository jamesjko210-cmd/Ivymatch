using System;
using System.Collections.Generic;

namespace IvyMatch
{
    // Models for schools.json schema v2 (CONTRACT section 3/4).
    // Public fields only: Newtonsoft.Json binds them directly. JsonUtility is NOT used.
    // Every number that must come from evidence (CDS, official page) is nullable: null = "no evidence yet".

    [Serializable]
    public class SchoolDatabase
    {
        public const int CurrentSchemaVersion = 2;

        public int schemaVersion;
        public string generatedAt;
        public List<School> schools = new List<School>();
    }

    [Serializable]
    public class School
    {
        public string id;
        public string name;
        public string nameKo;
        public string location;
        public double? lat;         // campus latitude (NCES IPEDS HD), null = unknown
        public double? lng;         // campus longitude
        public int? ipedsId;        // IPEDS UNITID (NCES College Navigator id)
        public int founded;
        public string undergradSize;
        public int? undergradCount; // degree-seeking undergrads (CDS B1/B2) as a number, for size filters. null = unknown
        public string type;       // "private" | "public" | null
        public string group;        // "ivy" | "ivy-plus" | "top50" | null  (list bucket, not a ranking)
        public string cycle;
        public Stats stats = new Stats();
        public Admissions admissions = new Admissions();
        public Cost cost = new Cost();
        public Intl intl = new Intl();
        public Majors majors;       // CDS J1 bachelor's degrees by field. null = not available
        public List<string> tags = new List<string>();
        public string notable;
        public List<string> strategy = new List<string>();
        public List<string> lookFor = new List<string>();
        public List<string> compare = new List<string>();
        public List<SourceRef> sources = new List<SourceRef>();
        public string verifiedAt;   // "yyyy-MM-dd" or null
        public string status;       // "verified" | "pending" | "unverified"
        public string colorHex;     // school colour, e.g. "#a51c30" (logo badge background)
        public string abbr;         // short name for the logo badge, e.g. "UCLA" (1-5 chars). null = first two letters of name

        /// <summary>Text for the logo badge: <see cref="abbr"/> when set, otherwise the name's first two letters.</summary>
        public string LogoText
        {
            get { return string.IsNullOrWhiteSpace(abbr) ? Initials(name) : abbr.Trim(); }
        }

        /// <summary>True when an early (ED/REA) acceptance rate is known.</summary>
        public bool HasEarly
        {
            get { return stats != null && stats.rateEarly.HasValue; }
        }

        /// <summary>True when the overall acceptance rate is known.</summary>
        public bool HasRate
        {
            get { return stats != null && stats.rateOverall.HasValue; }
        }

        /// <summary>True only when status == "verified".</summary>
        public bool IsVerified
        {
            get { return string.Equals(status, "verified", StringComparison.Ordinal); }
        }

        /// <summary>First two characters of the name (logo initials). Empty string for null/empty.</summary>
        public static string Initials(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            string t = name.Trim();
            if (t.Length == 0) return string.Empty;
            return t.Length <= 2 ? t : t.Substring(0, 2);
        }
    }

    [Serializable]
    public class Stats
    {
        public int? applied;
        public int? admitted;
        public int? enrolled;
        public float? rateOverall;   // null = no evidence
        public float? rateEarly;     // null = no evidence
        public float? rateRegular;   // null = no evidence
        public string earlyType;     // "ED" | "REA" | "EA" | null  (primary early plan, for the card badge)
        public string satMid50;
        public string actMid50;
    }

    /// <summary>One application round: ED, ED2, EA, REA or RD.</summary>
    [Serializable]
    public class Round
    {
        public string type;          // "ED" | "ED2" | "EA" | "REA" | "RD"
        public string deadline;      // "yyyy-MM-dd" or null
        public string notification;  // "yyyy-MM-dd" or free text ("mid-December") or null
        public bool? binding;        // ED/ED2 true, EA/REA/RD false, null = unknown
    }

    [Serializable]
    public class Admissions
    {
        public List<Round> rounds = new List<Round>();
        public string testPolicy;       // "required" | "optional" | "blind" | "flexible" | null
        public string testPolicyNote;   // e.g. "Required from fall 2025 entry"
        public int? supplementEssays;   // number of school-specific essays, null = unknown
        public string applicationUrl;   // official admissions page
    }

    [Serializable]
    public class Cost
    {
        public int? costOfAttendance;   // USD per year, total (tuition+fees+room+board), null = unknown
        public int? tuition;            // USD per year, null = unknown
        public bool? needBlindIntl;     // need-blind for international applicants
        public bool? meetsFullNeedIntl; // meets 100% demonstrated need for internationals
        public bool? cssProfile;        // CSS Profile required for aid
        public float? intlAidShare;     // % of international undergrads receiving aid, null = unknown
        public string note;
    }

    [Serializable]
    public class Intl
    {
        public bool? needBlind;         // kept for v1 compatibility; mirrors cost.needBlindIntl
        public float? intlShare;        // % of undergrads who are international, null = unknown
        public int? koreanStudents;     // headcount (all levels unless note says otherwise), null = unknown
        public string toeflMin;         // e.g. "100" or "not required", null = unknown
        public string note;
    }

    /// <summary>CDS J1: share of bachelor's degrees conferred per CIP 2-digit field.</summary>
    [Serializable]
    public class Majors
    {
        public string cdsYear;       // e.g. "2025-26"
        public string period;        // e.g. "2024-07-01..2025-06-30" or null
        public List<MajorShare> bachelor = new List<MajorShare>();
        public string note;
    }

    [Serializable]
    public class MajorShare
    {
        public string cip;           // 2-digit CIP code, e.g. "11"
        public string label;         // CDS label, e.g. "Computer and information sciences"
        public float pct;            // % of bachelor's degrees
    }

    [Serializable]
    public class SourceRef
    {
        public string label;
        public string url;           // may be null
    }
}
