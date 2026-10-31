# DATA agent log

## 1. Done
- Assets/StreamingAssets/schools.json: 8 schools generated programmatically from the HTML `schools` array (no hand-typing; node eval of the array). Validated with python json.load.
- Assets/IvyMatch/Scripts/SchoolData.cs, SchoolRepository.cs
- Packages/manifest.json (newtonsoft-json 3.2.1 + modules ui, uielements, unitywebrequest, jsonserialize)

## 2. Decisions
- id order = HTML order: harvard, yale, princeton, columbia, upenn, brown, dartmouth, cornell. `name` for upenn is "Penn" (as in HTML), nameKo "펜실베이니아(유펜)".
- colorHex = HTML `tc` (light pastel tones, e.g. #7eb8ff) -> logo text on these backgrounds should be DARK for contrast.
- earlyType: REA harvard/yale/princeton; ED others. rateEarly/rateRegular null everywhere. intl {null,null}. status "unverified", verifiedAt null, sources [{"Dashboard v2 (unverified)", null}], cycle "Class of 2028".
- Model fields are initialised to empty lists/objects; Parse() also normalises nulls and defaults status to "unverified".
- DATA-QUALITY FLAG (not changed, copied verbatim): HTML Dartmouth strategy[0] says "ED acceptance rate ~17-19% vs ~4% RD" and lookFor[0] says "93% of admits in top 10%". These are unsourced claims in prose; do not turn them into rateEarly/rateRegular. Cornell/Penn/Columbia strategy text claims ED "boosts" chances (qualitative). Verification step later.
- Also: Princeton notable says "No early decision - only REA" (consistent with earlyType REA).

## 3. Public API (for UI / LOGIC)
namespace IvyMatch
- class SchoolDatabase { int schemaVersion; string generatedAt; List<School> schools; }
- class School { string id, name, nameKo, location; int founded; string undergradSize, cycle; Stats stats; Intl intl; List<string> tags; string notable; List<string> strategy, lookFor, compare; List<SourceRef> sources; string verifiedAt, status, colorHex;
    bool HasEarly {get;}  // stats.rateEarly.HasValue
    bool IsVerified {get;} // status == "verified"
    static string Initials(string name) // first 2 chars (trimmed); "" for null/empty }
- class Stats { int applied, admitted, enrolled; float rateOverall; float? rateEarly; float? rateRegular; string earlyType; string satMid50, actMid50; }
- class Intl { bool? needBlind; string note; }
- class SourceRef { string label; string url; }
- class SchoolRepository (instance, not static)
    const string FileName = "schools.json"
    string LastError { get; }     // null on success; see notes
    string LastSource { get; }    // "persistent" | "streaming" | "none"
    static string PersistentPath, StreamingPath
    Task<SchoolDatabase> LoadAsync()   // never throws; on total failure returns EMPTY db (schools.Count==0, non-null) + LastError
    Task<bool> TryRefreshAsync(string url) // never throws; false on empty url/network/parse error; saves atomically (tmp + move)
    static SchoolDatabase Parse(string json) // throws InvalidDataException on empty/invalid/no schools
- static class UnityWebRequestAwaitExtensions { Task<UnityWebRequest> AsTask(this UnityWebRequestAsyncOperation); TaskAwaiter<UnityWebRequest> GetAwaiter(this UnityWebRequestAsyncOperation) }  -> `await req.SendWebRequest();`

## 4. Notes for other agents
- LOGIC: usage `var db = await new SchoolRepository().LoadAsync(); if (db.schools.Count == 0) show error/empty.` Call from `async void Start()` (Unity main-thread context, safe for UI). Keep one repository instance if you want LastError after TryRefreshAsync. LoadAsync may leave LastError set to "persistent copy unusable: ..." even when it succeeded via streaming (informational warning only).
- LOGIC: rateEarly/rateRegular are null for all 8 now -> bars must render "—" and 0 width without exceptions. Nullable access via `.HasValue`/`.Value`.
- LOGIC: verifiedAt is a string ("yyyy-MM-dd") or null; footer: null -> "미확인". Source label: sources.Count>0 ? sources[0].label : "".
- LOGIC: RateFilter must use stats.rateOverall (<5 / 5-10 / >10).
- LOGIC: compare ids may reference ids not in the list only if data is edited; look up defensively (the 4 in each list are all present, and the school itself is the first entry of its own compare list).
- UI: colorHex is a pastel; parse with ColorUtility.TryParseHtmlString. Initials via School.Initials(name).
- Newtonsoft package must be in manifest (done); asmdef not used, so scripts compile in Assembly-CSharp which references it automatically.
- Not compiled here (no Unity available); code written against Unity 6 / 2022.3 APIs (UnityWebRequest.Result, req.timeout, completed event).

## 5. CONTRACT change requests
- None required. Minor additions beyond contract: SchoolRepository.LastSource, PersistentPath/StreamingPath, static Parse, UnityWebRequestAwaitExtensions.

## 6. Remaining
- Real verification of all stats and filling rateEarly/rateRegular/verifiedAt/sources (next phase; update schools.json only with sourced numbers).
- Optional: add Assembly Definition if project grows.
