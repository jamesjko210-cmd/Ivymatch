# LOGIC agent log

## 1. Done
Written (all namespace IvyMatch, block-style, no `#nullable`):
- Assets/IvyMatch/Scripts/RateFilter.cs      - `enum RateFilter` + `static class RateFilters` { Apply(IEnumerable<School>, RateFilter) -> List<School>; Label(this RateFilter) }
- Assets/IvyMatch/Scripts/UiText.cs          - `static class UiText` (constants + culture-invariant format helpers) and `internal static class UiUtil` (Find<T>, SetText, SetVisible, ParseColor)
- Assets/IvyMatch/Scripts/SchoolCardView.cs
- Assets/IvyMatch/Scripts/DetailPanelView.cs
- Assets/IvyMatch/Scripts/DashboardController.cs
- README.md (Korean, CONTRACT section 9 items 1-7 + "데이터 갱신 절차" + "알려진 제한")
- No data/UI files touched. Read data-agent.md, ui-agent.md, SchoolData.cs, SchoolRepository.cs, Dashboard.uxml, SchoolCard.uxml, Dashboard.uss (selectors only) and reconciled (see 3).
- NOT compiled (no Unity/C# compiler in this sandbox).

## 2. Element names queried via Q / Find (diff against UXML)
Dashboard.uxml, from DashboardController (uiRoot.Q):
  root (VisualElement, falls back to rootVisualElement), card-grid (VisualElement, required),
  header-sub (Label), summary-bar (Label), empty-state (Label),
  filter-all / filter-low / filter-mid / filter-high (Button),
  detail-panel (VisualElement), detail-close (Button),
  btn-strategy / btn-lookfor / btn-compare (Button)
Dashboard.uxml, from DetailPanelView (Find<T>: panel.Q, else top-of-tree Q):
  detail-title, detail-sub, stat-applied, stat-admitted, stat-enrolled, stat-sat, stat-act, detail-notable (Label);
  detail-tags, detail-sources, section-strategy, section-lookfor, section-compare (VisualElement);
  btn-strategy, btn-lookfor, btn-compare (Button; only to toggle `active`)
SchoolCard.uxml, from SchoolCardView (cardRoot.Q) + controller:
  card-root (Q on the TemplateContainer; fallback = first child),
  logo, school-name, school-cycle, rate-label, rate-value, rate-unit, text-overall, text-early, text-regular,
  label-early, early-badge, footer-verified, footer-source (Label);
  bar-overall, bar-early, bar-regular, row-early, row-regular (VisualElement)
NOT queried (unused by code): header-title, filter-bar, card-scroll, row-overall.
Every queried name exists in the UI agent's UXML with the type listed. Missing elements are skipped silently (Debug.LogWarning only for filter/section buttons, detail-close, detail-panel; LogError for card-grid / UIDocument / cardTemplate).

## 3. Classes toggled / added
Toggled (EnableInClassList): `hidden` (detail-panel, empty-state, section-*, early-badge; ALSO inline style.display set to Flex/None so it works regardless of USS defaults),
  `narrow` (uiRoot and the element named "root", width < 600), `selected` (card-root), `active` (filter-* buttons; btn-strategy/lookfor/compare), `dim` (row-early / row-regular when value null).
Added on dynamic children (AddToClassList):
  tag (+ `blue` for ED offered / REA / EA offered chip),
  bullet + section-item (strategy/lookfor lines; whiteSpace Normal inline),
  compare-row (inline flex-row wrap), compare-card + compare-chip (+ highlight + current for the school itself), compare-name, compare-rate,
  source + source-link (+ link when url present).
Class-name reconciliation: the task text said bullet / compare-card / highlight; Dashboard.uss defines section-item / compare-chip / .current / source-link. I add BOTH sets so either styling works. USS has no rules for: bullet, compare-card, highlight, compare-row, source, link (harmless).

## 4. Model fields read (diff against SchoolData.cs - all exist there)
School: id, name, nameKo, location, founded, undergradSize, cycle, notable, tags, strategy, lookFor, compare, sources, verifiedAt, status, colorHex, IsVerified
Stats (via school.stats, null-guarded): applied, admitted, enrolled, rateOverall, rateEarly (float?), rateRegular (float?), earlyType, satMid50, actMid50
SourceRef: label, url
SchoolDatabase: schools
Not read: Intl, HasEarly, School.Initials (local MakeInitials used instead so card code does not depend on an optional helper).
SchoolRepository used: `new SchoolRepository()`, `LoadAsync()`, `TryRefreshAsync(string)`. (LastError/LastSource unused.)

## 5. Decisions / assumptions
- `RateFilter` enum cannot share a name with a static class, so helpers are `RateFilters.Apply/Label` (Label is also an extension method). Verifier: grep for `RateFilters`, not `RateFilter.Apply`.
- Card instantiation: the TemplateContainer wrapper itself is added to card-grid (Dashboard.uss has `.card-grid > TemplateContainer` rules making the wrapper the flex item); card-root is found inside it for binding/click. Re-render removes the wrappers (not card-grid.Clear(), so other children of card-grid are safe).
- Section/containers get Clear() then refilled (UI agent confirmed they are empty in UXML). When a list is empty, a "No data · 데이터 없음" line is shown instead of a blank box.
- Show() hides all 3 sections; the caller then calls ShowSection. Controller default after select = "strategy". On re-render (filter change / data refresh) the open section is preserved.
- Selection is kept by id across filter changes if the school is still visible; otherwise detail is hidden and selection cleared. Empty filter result -> empty-state visible, summary "No schools match", detail hidden, no exception.
- Pending status shows "검증 중" on the card (CONTRACT only lists 검증 전/검증됨); anything not verified/pending shows "검증 전".
- detail-title = "Name · 한글명" (nameKo appended when present). Card school-name = name only.
- verifiedAt "yyyy-MM-dd" is shown as "yyyy.MM.dd 확인" (matches CONTRACT example); unparsable text is shown as-is + " 확인".
- header-sub = "{total} universities · verified {n}/{total}" (total from data, not hard-coded 8). UI agent's CONTRACT text says "검증 상태 n/8"; task text said English "verified" - followed the task text.
- empty-state text = UiText.NoMatch ("No schools match this filter · 해당 학교 없음") overrides the UXML's initial Korean sentence ("조건에 맞는 학교가 없습니다").
- Filter button captions are NOT set by code (UXML already has All / <5% / 5–10% / >10%); RateFilters.Label returns identical strings.
- Logo text colour is set inline: dark (#0c0e12-ish) when background luma > 0.6, else white (data-agent says colorHex values are light pastels; USS default is white).
- All numbers formatted with CultureInfo.InvariantCulture ("3.6", never "3,6").
- Source URLs open only if http/https (Application.OpenURL).
- Controller binds in Start (UIDocument's tree is not guaranteed ready in our OnEnable). Disable/enable of the GameObject rebuilds the UIDocument tree and loses bindings (documented in README limits).
- `RefreshAsync()` is public (Task<bool>) so a future refresh button can call it; Start uses it when remoteJsonUrl non-empty && autoRefreshOnStart.
- After the first LoadAsync, a refresh re-calls LoadAsync (reads the new persistent copy); an empty/failed reload keeps old data.
- Avoided `?.` on UnityEngine.Object subclasses. `?.` is used only on VisualElement (plain C#) in SchoolCardView for row-early/row-regular.

## 6. For other agents / verifier
- UI: nothing to change required. Optional USS additions if wanted: `.compare-row`, `.tag` already exists; `.bullet`/`.compare-card`/`.highlight` have no rules (duplicates of section-item/compare-chip/current).
- UI suggestion kept open (not implemented): on phones the open detail panel sits above card-scroll and pushes cards down; could set `card-scroll.scrollOffset = Vector2.zero` on select, or move the panel inside the ScrollView.
- DATA: no changes needed. Relies on SchoolRepository.LoadAsync never returning null (handles null anyway).

## 7. CONTRACT change requests
- None required. Notes: (a) helper class name `RateFilters` (enum/class name clash); (b) extra public members: DashboardController.RefreshAsync(), DetailPanelView.CurrentSection / SectionStrategy|SectionLookFor|SectionCompare constants, UiText helper methods.

## 8. Remaining
- Compile + run in Unity (first run: check Console for USS warnings and C# compile errors; none could be tested here).
- Verify visually: `.card-grid > TemplateContainer` wrapper sizing at 390x844 and at desktop width; narrow toggle threshold uses panel logical units after Scale With Screen Size.
- Korean font file must be added by the user (README section 5).
- Optional: scroll-to-top / panel placement on phones; EditMode tests for RateFilters.Apply boundaries (5.0 -> Mid, 10.0 -> Mid, 10.01 -> High).
