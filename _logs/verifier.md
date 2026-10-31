# VERIFIER log

Scripts: `_verify/check.py` (UXML<->C#, JSON<->model, invariants). Run: `python3 -I _verify/check.py`.
No C# compiler in sandbox (`which dotnet mcs csc` -> none). Nothing was compiled.

## 1. Mismatches found
- UXML <-> C#: 50 names defined in UXML, 46 queried in code, 0 queried-but-undefined, 0 element-type mismatches (Label/Button/VisualElement/ScrollView all agree). Defined but unused (informational): card-scroll (now used, see 3), filter-bar, header-title, row-overall.
- JSON <-> model: 0 missing/extra keys at school, stats, intl, source, top level. JSON parses, 8 schools, all rateEarly/rateRegular null, earlyType REA for harvard/yale/princeton and ED for the other 5, status unverified + verifiedAt null everywhere, all compare ids resolve. rate/applied/admitted/enrolled/sat/act (plus name, location, tags, tc->colorHex, founded, size, notable, strategy, lookFor, compare) equal the `schools` array in _ref HTML: 0 diffs.
- USS <-> code: every class the code toggles/adds that matters exists in USS (hidden, narrow, selected, active, dim, tag, blue, section-item, compare-chip, current, compare-name, compare-rate, source-link). Added by code but never styled (informational, harmless duplicates): bullet, compare-card, compare-row (inline styles set), highlight, link, source. USS classes never set by code but used in UXML: all (header, card parts, etc.). `.unity-label` is a built-in Unity class.
- USS sanity: no unsupported properties (no box-shadow/gap/grid/text-shadow/filter/outline/z-index). Properties used are all valid USS (letter-spacing, white-space, transition-*, opacity, border-width shorthand, -unity-font-definition). `var()` vars declared in `:root`. No edit needed.
- C# read-through: usings cover all types (Newtonsoft.Json, UnityEngine.Networking, System.IO, System.Text, System.Runtime.CompilerServices for TaskAwaiter, System.Globalization, System.Linq). Every School/Stats/SchoolRepository member called exists with matching signature. `async void Start()` is wrapped in try/catch. No `?.` / `??` on UnityEngine.Object (only on VisualElement / string / plain classes). Length.Percent used correctly. Instantiate() returns TemplateContainer, handled with card-root lookup + fallback child. Filter: Low <5, Mid 5<=r<=10 inclusive, High >10. Empty-list paths guarded (Average only after Count==0 return; best = list[0]; FirstOrDefault). No local-name scope conflicts found.
- Result: 0 real mismatches in the contract-level checks. Only the two requested changes below were applied.

## 2. Counts
Names checked 50 UXML / 46 queried; mismatched 0; fixed 0 (none needed). Requested items applied: 2.

## 3. Edits made
- Assets/IvyMatch/Scripts/DashboardController.cs: new field `ScrollView cardScroll;` (~line 33); `cardScroll = uiRoot.Q<ScrollView>("card-scroll");` in BindUi (~line 113); in SelectSchool (~line 355) `if (cardScroll != null) cardScroll.scrollOffset = Vector2.zero;` (item 6, null-safe).
- Assets/StreamingAssets/schools.json: dartmouth strategy[0] now ends with " (unverified)" (line ~389). lookFor[0] "93% of admits in top 10%" is also an unsourced claim copied from the HTML; left untouched (not requested).

## 4. Could NOT verify
- Compilation (no C# compiler / Unity). All C# checks were by reading.
- USS import (Unity may print unknown-property warnings; none expected), actual layout (TemplateContainer wrapper as flex item at 390x844 and desktop), Korean glyphs.

## 5. Remaining risks for first Unity run
1. Compile errors are possible anywhere; read the Console first. Highest-value places: UnityWebRequestAwaitExtensions (custom GetAwaiter on UnityWebRequestAsyncOperation; if Unity 6 already provides awaitable support there should still be no ambiguity, but watch for CS0121), SchoolCardView Q<..>()?.EnableInClassList.
2. Missing `Resources/Fonts/NotoSansKR-Regular` makes the USS `resource()` log a warning and fall back to the default font; Korean text shows as boxes until the font is added.
3. PanelSettings must have the default runtime Theme Style Sheet assigned, or buttons and labels look unstyled (README step 5 covers this).
4. IL2CPP / managed code stripping on Android/iOS: Newtonsoft reflection-populates model fields (e.g. Intl, schemaVersion are never read by code). If stripping is High, set Managed Stripping Level to Low/Minimal or add a link.xml preserving the Assembly-CSharp IvyMatch types, otherwise fields may silently stay default on device.
5. Selecting a card resets the card list to the top, but the detail panel sits above the ScrollView so on small phones it still pushes the card list down; moving the panel inside card-scroll is the real fix if that is not enough.
6. `narrow` threshold (600) is in logical panel units after Scale With Screen Size (ref 390x844, match 0.5): on wide desktop windows the scaled width may differ from the window pixel width.
7. Disabling and re-enabling the GameObject rebuilds the UIDocument tree and drops the bindings (known, in README).
8. Data caveat: Dartmouth lookFor[0] "93% of admits in top 10%" and Cornell/Penn/Columbia qualitative ED claims remain unsourced.
