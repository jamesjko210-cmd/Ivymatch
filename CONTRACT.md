# IvyMatch Unity — 설계 계약서 (CONTRACT.md)

감독(Supervisor)이 고정한 규격. 모든 에이전트는 이 문서를 따른다. 변경이 필요하면 자기 로그에 "CONTRACT 변경 요청"으로 적고 감독에게 보고한다. 임의로 바꾸지 않는다.

## 0. 목표
- 웹 대시보드(ivy_league_admissions_2nd.html)의 "카드 격자 + 합격률 큰 숫자 + 막대" 형태를 Unity UI Toolkit으로 재구현.
- 막대는 **전체 / 얼리(ED·REA) / 정시(RD)** 3줄. 척도는 모든 카드에서 0~20% 고정.
- 카드 헤더 두 번째 줄 = 입학 연도 + 출처판 (예: "Class of 2028 · 검증 전").
- 카드 하단 = 확인일 + 출처 레이블.
- 추후 Android/iOS 앱으로 빌드. 세로(portrait) 모바일 기준, PC에서도 깨지지 않게.

## 1. 환경
- Unity 6000.0 LTS 기준(2022.3 LTS 호환되게 작성). UI Toolkit 런타임.
- JSON: `com.unity.nuget.newtonsoft-json` (Newtonsoft.Json). `JsonUtility` 사용 금지(nullable/null 미지원).
- 네임스페이스: `IvyMatch`.
- C# 규칙: `#nullable enable` 사용 안 함(Unity 호환). nullable 수치는 `float?`/`int?` 사용. 문자열 null 허용.
- 한글 표시: USS에서 `-unity-font-definition: resource("Fonts/NotoSansKR-Regular")`를 쓰되, 폰트 파일은 사용자가 넣는다(README에 안내). 폰트가 없어도 영어는 표시되어야 하므로 UI 문자열은 **영어 기본 + 한글 보조**로 작성(예: "Acceptance rate · 합격률").

## 2. 폴더 구조 (zip 루트 = 프로젝트 루트)
```
ivy-unity/
  README.md
  CONTRACT.md
  Packages/manifest.json            # newtonsoft 의존성 포함한 최소 manifest
  Assets/
    StreamingAssets/schools.json
    IvyMatch/
      Scripts/
        SchoolData.cs               # 모델
        SchoolRepository.cs         # 로드/갱신
        DashboardController.cs      # MonoBehaviour, UIDocument에 부착
        SchoolCardView.cs           # 카드 1장 바인딩
        DetailPanelView.cs          # 상세 패널 바인딩
        RateFilter.cs               # 필터 enum + 적용
      UI/
        Dashboard.uxml
        SchoolCard.uxml
        Dashboard.uss
      Fonts/.gitkeep                # NotoSansKR 넣는 자리
```

## 3. 데이터 스키마 (schools.json) — v2

최상위:
```json
{ "schemaVersion": 2, "generatedAt": "2026-10-21", "schools": [ { ...School } ] }
```
School (v2에서 추가된 필드는 `+` 표시):
```json
{
  "id": "harvard",
  "name": "Harvard",
  "nameKo": "하버드",
  "location": "Cambridge, MA",
+ "lat": 42.374471, "lng": -71.118313,   // 캠퍼스 좌표 (NCES IPEDS HD LATITUDE/LONGITUD). 지도 표시용
+ "ipedsId": 166027,                     // IPEDS UNITID (College Navigator id)
  "founded": 1636,
  "undergradSize": "~7,000 undergrad",
+ "undergradCount": 7110,             // 학부 재학생 수(CDS B1/B2) 숫자. 규모 필터용, null = 미확인
+ "type": "private",                 // "private" | "public" | null
+ "group": "ivy",                    // "ivy" | "ivy-plus" | "top50"  (목록 묶음, 순위 아님)
  "cycle": "Class of 2028",
  "stats": {
    "applied": 54008, "admitted": 1971, "enrolled": 1665,   // v2: 모두 int? (null 허용)
    "rateOverall": 3.6,                                      // v2: float? (null = 근거 없음)
    "rateEarly": null, "rateRegular": null,
    "earlyType": "REA",                                      // 카드 배지용 대표 얼리 플랜
    "satMid50": "1580", "actMid50": "36"
  },
+ "admissions": {
+   "rounds": [                                              // 지원 라운드. 마감·발표일은 "yyyy-MM-dd"
+     { "type": "REA", "deadline": "2025-11-01", "notification": "2025-12-12", "binding": false },
+     { "type": "RD",  "deadline": "2026-01-01", "notification": "2026-03-27", "binding": false }
+   ],
+   "testPolicy": "required",         // "required" | "optional" | "blind" | "flexible" | null
+   "testPolicyNote": "Required from fall 2025 entry",
+   "supplementEssays": 5,            // 학교별 보충 에세이 개수, null = 미확인
+   "applicationUrl": "https://college.harvard.edu/admissions"
+ },
+ "cost": {
+   "costOfAttendance": 87450,        // USD/년 총비용(등록금+기숙사+식비 등), null = 미확인
+   "tuition": 59320,
+   "needBlindIntl": true,            // 국제학생 need-blind 여부
+   "meetsFullNeedIntl": true,        // 국제학생 need 100% 충족 여부
+   "cssProfile": true,               // 지원 신청에 CSS Profile 필요 여부
+   "intlAidShare": null,             // 국제학생 중 지원 받는 비율(%)
+   "note": null
+ },
  "intl": {
    "needBlind": true,                // v1 호환. 로더가 cost.needBlindIntl 과 동기화
+   "intlShare": 14.9,                // 학부 국제학생 비율(%)
+   "koreanStudents": null,           // 한국인 재학생 수 (Open Doors / 학교 통계)
+   "toeflMin": null,                 // "100" | "not required" | null
    "note": null
  },
+ "majors": {                         // CDS J1 학사 학위 분야별 비율. null = 자료 없음
+   "cdsYear": "2025-26", "period": "2024-07-01..2025-06-30",
+   "bachelor": [ { "cip": "45", "label": "Social sciences", "pct": 27.0 } ],   // CIP 2자리, 비율 큰 순
+   "note": null
+ },
  "tags": ["Research", "Liberal Arts", "STEM"],
  "notable": "...",
  "strategy": ["..."], "lookFor": ["..."], "compare": ["harvard", "yale"],
  "sources": [ { "label": "Harvard CDS 2025-26", "url": "https://..." } ],
  "verifiedAt": null,
  "status": "unverified",             // "verified" | "pending" | "unverified"
  "colorHex": "#a51c30",              // 학교 대표색. 카드 로고 배지 배경
+ "abbr": "HU"                        // 로고 배지 약칭(1-5자). null 이면 이름 앞 2글자
}
```
규칙:
- **근거 없는 숫자는 `null`.** 추정값·기억값 금지. 새로 추가된 42개교는 이름·위치·설립연도·공립/사립·얼리 플랜 종류만 들어 있고 숫자는 전부 `null`, `status`는 `"pending"`.
- `rounds[].type` ∈ `ED | ED2 | EA | REA | RD`. `binding`은 ED/ED2만 true.
- `earlyType`은 `rounds`에서 ED > REA > EA 순으로 고른 대표값. 카드 배지에만 쓴다.
- `rateOverall`이 `null`인 학교는 카드에 `—`로 표시되고, 합격률 필터(<5% 등)에서는 제외되며 "All"에만 나온다.
- v1 파일도 그대로 읽힌다(없는 필드는 null/빈 리스트로 채움).
- `group`은 순위가 아니라 목록 묶음이다. 앱 어디에도 순위 숫자를 표시하지 않는다.

## 4. C# 모델 (SchoolData.cs)
```csharp
namespace IvyMatch {
  [Serializable] public class SchoolDatabase { int schemaVersion; string generatedAt; List<School> schools; }
  [Serializable] public class School { id, name, nameKo, location, founded(int), undergradSize, int? undergradCount, type, group, cycle,
      Stats stats, Admissions admissions, Cost cost, Intl intl, Majors majors, List<string> tags, notable,
      List<string> strategy, lookFor, compare, List<SourceRef> sources, verifiedAt, status, colorHex, abbr }
  [Serializable] public class Stats { int? applied, admitted, enrolled; float? rateOverall, rateEarly, rateRegular; string earlyType, satMid50, actMid50; }
  [Serializable] public class Round { string type, deadline, notification; bool? binding; }
  [Serializable] public class Admissions { List<Round> rounds; string testPolicy, testPolicyNote; int? supplementEssays; string applicationUrl; }
  [Serializable] public class Cost { int? costOfAttendance, tuition; bool? needBlindIntl, meetsFullNeedIntl, cssProfile; float? intlAidShare; string note; }
  [Serializable] public class Intl { bool? needBlind; float? intlShare; int? koreanStudents; string toeflMin, note; }
  [Serializable] public class Majors { string cdsYear, period; List<MajorShare> bachelor; string note; }
  [Serializable] public class MajorShare { string cip, label; float pct; }
  [Serializable] public class SourceRef { string label; string url; }
}
```
편의 메서드: `School.HasRate`, `School.HasEarly`, `School.IsVerified`, `School.LogoText`(abbr, 없으면 이름 앞 2글자).

## 5. SchoolRepository.cs
- `Task<SchoolDatabase> LoadAsync()`:
  1. `Application.persistentDataPath/schools.json`이 있으면 우선 사용(원격 갱신본).
  2. 없으면 `StreamingAssets/schools.json`. Android는 `UnityWebRequest`로 읽어야 함(파일 API 불가). 다른 플랫폼은 `File.ReadAllText`.
- `Task<bool> TryRefreshAsync(string url)`: `UnityWebRequest.Get` → 성공 시 JSON 파싱 검증 → persistentDataPath에 저장 → true. 실패/파싱 오류 시 false, 기존 데이터 유지. 네트워크 없으면 조용히 false.
- 원격 URL은 `DashboardController`의 인스펙터 필드 `remoteJsonUrl`(기본 빈 문자열 = 갱신 안 함).

## 6. UXML 요소 이름 (name 속성) — 코드의 `Q<>()`와 1:1
### Dashboard.uxml
| name | 타입 | 용도 |
|---|---|---|
| `root` | VisualElement | 최상위. 컨트롤러가 너비에 따라 `narrow` 클래스 토글 |
| `header-title` | Label | "Ivy League Admissions" |
| `header-sub` | Label | "8 universities · 검증 상태 n/8" |
| `filter-bar` | VisualElement | 버튼 컨테이너 |
| `filter-all` / `filter-low` / `filter-mid` / `filter-high` | Button | All / <5% / 5–10% / >10% |
| `summary-bar` | Label | "Showing 8 · Avg 5.4% · Most selective Harvard (3.6%)" |
| `card-scroll` | ScrollView | 카드 격자 스크롤 |
| `card-grid` | VisualElement | flex-wrap row. 카드 인스턴스가 여기 들어감 |
| `detail-panel` | VisualElement | 기본 `display: none`. 카드 선택 시 표시 |
| `detail-title` | Label | 학교명 |
| `detail-sub` | Label | location · founded · size |
| `detail-close` | Button | 닫기 |
| `stat-applied` / `stat-admitted` / `stat-enrolled` / `stat-sat` / `stat-act` | Label | 값 |
| `detail-tags` | VisualElement | 태그 칩 컨테이너 |
| `detail-notable` | Label | notable |
| `btn-strategy` / `btn-lookfor` / `btn-compare` | Button | 섹션 토글 |
| `section-strategy` / `section-lookfor` / `section-compare` | VisualElement | 각 섹션 컨테이너(기본 none). 컨트롤러가 Label/칩을 채움 |
| `detail-sources` | VisualElement | 출처 링크 목록 |
| `empty-state` | Label | 필터 결과 0개일 때 표시. 기본 none |

v2 추가 (상세 패널 info 블록): `info-grid`, `info-deadlines`, `info-test`, `info-cost`, `info-aid`, `info-intl`, `info-essays`, `info-majors` (모두 Label, `info-grid`만 VisualElement)

검색·필터 (§11): `search-bar`(검색창 줄), `active-filters`(적용 중 조건 칩, 기본 none), `filter-panel`(필터 패널, 기본 none, `card-scroll` 안). 세 요소 모두 빈 VisualElement 이고 `FilterPanelView` 가 코드로 채운다.

지도 (§12): `view-list` / `view-map`(Button, `filter-bar` 안 목록·지도 전환), `map-view`(빈 VisualElement, 기본 none, `card-scroll` 안 `detail-panel` 위. `UsMapView` 가 코드로 채운다).

### SchoolCard.uxml (템플릿, 루트 1개)
| name | 타입 | 용도 |
|---|---|---|
| `card-root` | VisualElement | 클릭 영역. 클래스 `card`, 선택 시 `selected` |
| `logo` | Label | `abbr` 약칭(없으면 이름 앞 2글자). 배경색 = colorHex, 글자색은 배경 밝기에 따라 흰색/검정. 글자 수에 따라 15/13/11/10px |
| `school-name` | Label | name |
| `school-cycle` | Label | `cycle · 검증 전/검증됨` |
| `rate-label` | Label | "Acceptance rate · 합격률" |
| `rate-value` | Label | "3.6" |
| `rate-unit` | Label | "%" |
| `row-overall` / `row-early` / `row-regular` | VisualElement | 각 막대 행 |
| `bar-overall` / `bar-early` / `bar-regular` | VisualElement | 채움 막대. 코드가 `style.width = Length.Percent(min(rate/20*100,100))` |
| `label-early` | Label | "Early ED" 또는 "Early REA" |
| `text-overall` / `text-early` / `text-regular` | Label | "3.6%" 또는 "—" |
| `footer-verified` | Label | "2026.10.20 확인" 또는 "미확인" |
| `footer-source` | Label | 첫 source label |
| `early-badge` | Label | "ED"/"REA" 칩 |

## 7. USS 클래스 (Dashboard.uss)
- 색 변수는 `:root`에 `--bg`, `--surface`, `--surface2`, `--border`, `--text`, `--muted`, `--accent`로 정의. HTML 다크 테마 값 그대로(`#0c0e12`, `#141720`, `#1c2030`, `#e8eaf0`, `#7a8099`, `#5b8dff`). UI Toolkit USS 변수 문법: `--bg: #0c0e12;` 선언, `var(--bg)` 사용.
- `.card` 기본 너비 260px, `flex-grow: 1`, `margin: 8px`. `.narrow .card { width: 100%; }`(세로폰).
- `.card.selected { border-color: var(--accent); }`
- `.filter-btn`, `.filter-btn.active`
- `.bar-track`(높이 6px, 둥근), `.bar-fill`, `.bar-fill.overall`/`.early`/`.regular` 색 3단계(`#85B7EB`, `#185FA5`, `#B5D4F4`). 트랙 폭이 0일 때도 레이아웃 유지.
- `.tag`, `.tag.blue`, `.hidden { display: none; }`
- USS에는 미디어쿼리가 없다. 반응형은 컨트롤러가 `root` 너비 < 600px이면 `narrow` 클래스 추가.
- `-unity-font-definition`은 `.root`에 한 번만. 폰트 리소스가 없으면 Unity가 기본 폰트로 폴백한다(에러 아님).

## 8. 컨트롤러 동작 (DashboardController.cs)
- `[SerializeField] UIDocument document; [SerializeField] VisualTreeAsset cardTemplate; [SerializeField] string remoteJsonUrl = "";`
- `Start()`: 로드 → 카드 렌더 → 필터 버튼 바인딩 → 원격 URL 있으면 `TryRefreshAsync` 후 성공 시 재렌더.
- 필터: `RateFilter { All, Low(<5), Mid(5~10), High(>10) }`. 결과 0개면 `empty-state` 표시, `summary-bar`는 "No schools match"로. **빈 리스트에서 예외 금지**(HTML 원본 버그).
- 카드 클릭: 같은 카드 재클릭 = 선택 해제. 선택 시 `detail-panel` 표시 + `selected` 클래스.
- 상세 패널 3버튼: 눌린 섹션만 표시, 나머지 none. compare 섹션은 `compare` 배열의 학교 4개를 작은 칩 행으로(name · rateOverall%).
- 반응형: `root.RegisterCallback<GeometryChangedEvent>` 에서 너비 확인 → `narrow` 토글.
- 모든 UI 문자열은 상수로 모아 `UiText` static class에 둔다(추후 한글 전환 쉽게).
- 카드는 데이터베이스가 바뀔 때만 한 번 만든다(`CardGridPresenter.Rebuild`). 필터·검색·정렬은 카드를 숨기거나 보이고, 보이는 순서가 바뀐 경우에만 `BringToFront` 로 순서를 바꾼다(`CardGridPresenter.Show`). 선택 표시는 숨은 카드까지 포함해 매 렌더마다 맞춘다.
- 스크롤: 터치는 ScrollView 기본 터치 스크롤(관성 포함). 마우스·펜 드래그는 `DragScrollManipulator` 가 처리하고, 놓을 때 속도가 있으면 관성으로 미끄러진다. 폭 600 미만(`narrow`)에서는 세로 스크롤바를 숨기고, 넓은 화면에서는 `Dashboard.uss` 의 얇은 오버레이 스크롤바(화살표 없음, 폭 4px)를 쓴다.

## 9. README 필수 내용
1. Unity Hub에서 6000.0 LTS, 2D(URP 불필요) 템플릿으로 빈 프로젝트 생성.
2. 이 zip의 `Assets/`, `Packages/manifest.json`을 덮어쓰기(기존 manifest는 병합: `com.unity.nuget.newtonsoft-json` 한 줄만 추가해도 됨).
3. (개정) PanelSettings 는 `IvyMatchBootstrap` 이 런타임 생성: Scale With Screen Size, 390×844, Match 0(폭 기준), 테마 = Resources/UI/IvyRuntimeTheme.tss.
4. (개정) 씬 수동 구성 불필요. UI 에셋 경로는 `Assets/IvyMatch/Resources/UI/`, 씬은 에디터 스크립트가 `Assets/Scenes/IvyMatch.unity` 로 생성.
5. 한글 폰트: Noto Sans KR OFL 다운로드 → `Assets/IvyMatch/Fonts/NotoSansKR-Regular.ttf` 로 넣고 Resources 로드 경로 설명(`Assets/IvyMatch/Resources/Fonts/...`로 두어야 `resource()` 동작).
6. Android/iOS 빌드 체크리스트(Portrait 고정, StreamingAssets 포함 확인, 인터넷 권한은 remoteJsonUrl 쓸 때만).
7. 데이터 갱신 절차: `schools.json` 교체 → `status`/`verifiedAt` 갱신 → 원격 URL에 배포.

## 10. 에이전트 로그
각 에이전트는 `/home/claude/ivy-unity/_logs/<agent>.md`에 (1) 한 일, (2) 결정 사항, (3) 다른 에이전트가 알아야 할 것, (4) CONTRACT 변경 요청, (5) 남은 일 을 남긴다. 다른 에이전트의 로그가 있으면 시작 전에 읽는다.

## 11. 검색·필터 (SchoolQuery.cs / FilterPanelView.cs)
- 처리 순서: 합격률 버튼(`RateFilter`) → `SchoolQuery.Apply(list, DateTime.Today)` → 카드 렌더. `SchoolQuery` 는 UnityEngine 의존이 없는 순수 C# 이다.
- 검색어: `name`, `nameKo`, `abbr`, `location`, `id` 에서 찾는다. 대소문자·공백·기호 무시("w&m" = "W&M"). 입력이 150ms 멈춘 뒤에 다시 거른다(`FilterPanelView.SearchDebounceMs`).
- 카테고리와 옵션:
  - 유형 사립/공립 (`type`)
  - 지역 동북부/중서부/남부/서부 (`location` 끝의 주 약자 → 미국 인구조사국 4개 권역)
  - 학부생 수 <5k / 5k–10k / 10k–20k / 20k+ (`undergradCount`)
  - 연간 총비용 <$60k / $60k–85k / $85k–95k / $95k+ (`cost.costOfAttendance`)
  - 지원 전형 ED / ED2 / EA / REA / 정시만 (`admissions.rounds`. 정시만 = 얼리 라운드 없음)
  - 시험 정책 required / optional / blind / flexible
  - 국제학생 재정 Need-blind / 100% 지원 (`cost.needBlindIntl`, `cost.meetsFullNeedIntl`)
  - 전공: CIP 묶음 12개(`SchoolQuery.MajorGroups`). 해당 묶음의 졸업생 비중이 `MajorMinShare`(5%) 이상이면 일치
- 같은 카테고리 안의 옵션은 OR, 카테고리끼리는 AND. 예외로 국제학생 재정 두 옵션은 AND.
- 켜진 카테고리의 값이 `null` 인 학교는 결과에서 빠지고, 요약 줄에 "n hidden (no data) · 정보 없음 n개교 제외"로 표시한다.
- 정렬: 기본(목록 순), 합격률 낮은순, 총비용 낮은순, 규모 작은순/큰순, 마감 임박순(오늘 이후 가장 이른 마감), 국제학생 많은순, 전공 비중순. 값이 없는 학교는 항상 맨 뒤. 전공 필터를 켜고 정렬이 기본이면 전공 비중순으로 정렬한다.
- 상세 패널 `info-majors`: 비중 상위 3개 묶음과 CDS 연도(`UiText.MajorsLine`).

## 12. 미국 지도 (UsMapData.cs / MapLayout.cs / UsMapView.cs)
- 데이터: `Assets/IvyMatch/Resources/Map/us_states.json` (TextAsset, 경로 `Map/us_states`). 미국 인구조사국 cartographic boundary 파일(퍼블릭 도메인)의 본토 48개 주 + DC 경계를 Albers 등적원추도법(USGS: 기준위도 29.5°/45.5°, 원점 23°N 96°W, 구면)으로 투영하고 단순화해 0..1 로 정규화한 것. 다시 만들 때는 `Tools/build_us_map.py` 를 쓴다(`python -I Tools/build_us_map.py <shp 폴더> Assets/StreamingAssets/schools.json Assets/IvyMatch/Resources/Map/us_states.json <미리보기.png>`).
- 학교 위치: `schools.json` 의 `lat`/`lng` 를 `UsMapData.Project` 가 같은 투영식으로 계산한다. 투영 상수는 지도 JSON 의 `projection` 에 들어 있으므로 C# 과 Python 이 항상 같다.
- 화면: `filter-bar` 의 List/Map 버튼으로 전환. 지도에는 합격률 버튼 + 검색·필터 결과와 같은 학교만 표시한다. 학교가 있는 주는 조금 밝게 칠한다.
- 마커: 학교 약칭 배지(학교색 배경, 대비 큰 글자색). 화면에서 겹치는 마커(타원 36×22px 안)는 개수 배지로 묶는다. 묶음을 누르면 그 학교들이 보이도록 확대(최대 14배)하고, 더 확대할 수 없으면 원형으로 펼치고 가운데에서 선을 그어 준다.
- 지역 버튼: All US / Northeast / Midwest / South / West. 각 권역에 속한 주들의 경계 상자로 확대한다.
- 학교 배지를 누르면 카드 클릭과 같이 선택/선택 해제되고, 상세 패널이 지도 아래에 열리며 그 위치로 스크롤한다.

