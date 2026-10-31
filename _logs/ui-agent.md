# UI agent log

## 1. Done
- Assets/IvyMatch/UI/Dashboard.uxml, SchoolCard.uxml, Dashboard.uss written. Both UXML files parse as valid XML.
- No C# written. No contract element renamed.

## 2. name= attributes used (verifier: diff against C# Q<>() strings)
Dashboard.uxml (30):
root, header-title, header-sub, filter-bar, filter-all, filter-low, filter-mid, filter-high,
summary-bar, empty-state, detail-panel, detail-title, detail-sub, detail-close,
stat-applied, stat-admitted, stat-enrolled, stat-sat, stat-act, detail-tags, detail-notable,
btn-strategy, btn-lookfor, btn-compare, section-strategy, section-lookfor, section-compare,
detail-sources, card-scroll, card-grid

SchoolCard.uxml (20):
card-root, logo, school-name, school-cycle, rate-label, rate-value, rate-unit,
row-overall, bar-overall, text-overall,
row-early, bar-early, text-early, label-early,
row-regular, bar-regular, text-regular,
footer-verified, footer-source, early-badge

All 50 names from CONTRACT §6 are present, each exactly once. Unnamed (not queried): the "Overall · 전체" and "Regular RD · 정시" row labels, stat box captions, "Sources · 출처" heading.

Types: filter-*, btn-*, detail-close = Button. header-*, summary-bar, empty-state, detail-title/sub/notable, stat-*, card Labels = Label. card-scroll = ScrollView. All others VisualElement.

## 3. For the controller (LOGIC agent)
Classes to toggle:
- `hidden` on: detail-panel, empty-state, section-strategy/lookfor/compare. All start hidden in UXML.
- `narrow` on `root` (width < 600).
- `selected` on card-root.
- `active` on filter-all/low/mid/high. filter-all starts active; move it on click.
- Optional: `active` on btn-strategy/lookfor/compare (styled like the active pill); `dim` on a `row-early`/`row-regular` when the value is null (opacity 0.5).
- Set logo `style.backgroundColor` from colorHex. The USS default is the accent color.
- Set bar widths with `style.width = Length.Percent(...)`. USS default is 0 and the track keeps its layout.

Optional helper classes in the USS for dynamically created children: `.tag`, `.tag.blue` (detail-tags chips), `.section-item` (strategy/lookfor bullet Label), `.compare-chip` (+ `.current`) containing Labels `.compare-name` / `.compare-rate`, `.source-link` (detail-sources Label).

Section containers and detail-sources/detail-tags are EMPTY in UXML. Clear() is safe. The "Sources · 출처" heading is a separate sibling Label.

Card instantiation: `cardTemplate.Instantiate()` wraps card-root in a TemplateContainer, and `CloneTree(grid)` does not. Both work. The USS has `.card-grid > TemplateContainer` rules so the wrapper acts as the flex item (276px basis, flex-grow 1; 100% when narrow). Prefer `Q<VisualElement>("card-root")` inside the instance for the click callback and `selected` toggle.

SchoolCard.uxml has no `<Style>` tag. Dashboard.uss reaches the cards through the Dashboard.uxml root. If a card is previewed alone in UI Builder it will be unstyled, which is expected.

## 4. Decisions
- `.hidden` is the last rule in the USS so it wins over the display defaults above it.
- Button chrome removed (`background-color`, `border-width`, `border-color` set on base, :hover, :active, :focus) so Unity's grey theme doesn't show through.
- `.unity-label { margin:0; padding:0; color }` resets Unity's default label spacing.
- Font definition is on `.root` only. Missing font gives the default-font fallback (documented in a USS comment).
- Extra CSS vars beyond the contract: --border2, --accent-soft, --accent-line.
- Narrow mode sets card side margins to 0 and puts the 20px gutter on card-grid padding. Without this, width:100% plus margin 8px would overflow horizontally by 16px.
- Detail panel sits between empty-state and card-scroll (not inside the scroll), as in the HTML reference.
- ScrollView: `mode="Vertical"` and `horizontal-scroller-visibility="Hidden"`.

## 5. CONTRACT change requests
None required. Suggestion for Supervisor/LOGIC: on phones an open detail-panel above the card list pushes the cards down. If this is a problem, the controller can call `card-scroll.scrollOffset = Vector2.zero` on open. Or the panel can be moved into card-scroll above card-grid, since Q<> by name is recursive and no name changes.

## 6. Remaining / unverified
- Not opened in Unity (no editor here). Everything was written against documented USS behavior: var() on :root, transition-*, TemplateContainer type selector. First Unity run should check for USS "unknown property" warnings in the Console.
- Korean text depends on the user adding Resources/Fonts/NotoSansKR-Regular.ttf.
