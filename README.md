# IvyMatch Unity — 아이비리그 입시 대시보드 (UI Toolkit)

웹 대시보드(`ivy_league_admissions_2nd.html`)의 "카드 격자 + 합격률 큰 숫자 + 막대" 화면을 Unity UI Toolkit 런타임 UI로 옮긴 프로젝트입니다. 세로(portrait) 모바일 기준이며 PC 에디터에서도 깨지지 않게 만들었습니다.

- 카드마다 막대 3줄: **전체 / 얼리(ED·REA) / 정시(RD)**. 막대 척도는 모든 카드에서 0~20% 고정입니다.
- 카드 헤더 둘째 줄: 입학 연도 + 검증 상태 (예: `Class of 2028 · 검증 전`).
- 카드 하단: 확인일 + 출처 레이블.
- 카드를 누르면 상세 패널(통계, 태그, 출처)과 "전략 / 평가 포인트 / 비교" 3개 섹션이 열립니다.

> 이 프로젝트는 Unity 에디터 없이 작성되어 아직 컴파일/실행 검증을 하지 않았습니다. 처음 열었을 때 Console 창에 경고나 오류가 나오면 아래 "알려진 제한"을 먼저 확인하세요.

---

## 폴더 구조

```
ivy-unity/
  README.md
  CONTRACT.md                       # 설계 계약서 (요소 이름·스키마 규격)
  Packages/manifest.json            # Newtonsoft.Json 의존성 포함
  Assets/
    StreamingAssets/schools.json    # 번들 데이터 (8개 학교)
    IvyMatch/
      Scripts/
        SchoolData.cs               # 데이터 모델
        SchoolRepository.cs         # 로드 / 원격 갱신
        DashboardController.cs      # MonoBehaviour (UIDocument 옆에 부착)
        SchoolCardView.cs           # 카드 1장 바인딩
        DetailPanelView.cs          # 상세 패널 바인딩
        RateFilter.cs               # 필터 enum + 적용 함수
        UiText.cs                   # 모든 UI 문자열 상수
      UI/
        Dashboard.uxml
        SchoolCard.uxml
        Dashboard.uss
```

---

## 1. 프로젝트 만들기

1. Unity Hub 에서 **New project** 를 누릅니다.
2. 에디터 버전은 **Unity 6000.0 LTS** 를 고릅니다. (2022.3 LTS 에서도 동작하도록 작성했습니다.)
3. 템플릿은 **2D (Built-In Render Pipeline)** 를 고릅니다. URP 는 필요 없습니다.
4. 프로젝트 이름을 정하고 **Create project** 를 누릅니다.

## 2. 파일 덮어쓰기와 Newtonsoft.Json 설치

### 2-1. 파일 복사

1. 만든 프로젝트 폴더를 탐색기(Finder)로 엽니다. (Unity 에서 Project 창 아무 곳 우클릭 → *Show in Explorer*)
2. 이 zip 의 `Assets/` 폴더 안 내용을 프로젝트의 `Assets/` 에 그대로 복사합니다. (`StreamingAssets`, `IvyMatch` 두 폴더가 생깁니다.)
3. `Packages/manifest.json` 은 두 가지 중 하나를 선택합니다.
   - **새 프로젝트라면**: zip 의 `manifest.json` 으로 덮어써도 됩니다.
   - **이미 다른 패키지를 쓰는 프로젝트라면**: 덮어쓰지 말고 기존 파일의 `"dependencies"` 안에 아래 한 줄만 추가합니다.
     ```json
     "com.unity.nuget.newtonsoft-json": "3.2.1",
     ```
4. Unity 창으로 돌아오면 자동으로 임포트/컴파일이 진행됩니다. 끝날 때까지 기다립니다.

### 2-2. Newtonsoft.Json 을 Package Manager 로 설치하는 방법 (manifest 를 수정하지 않았거나 설치가 안 된 경우)

1. 메뉴 **Window → Package Manager** 를 엽니다.
2. 왼쪽 위 **+** 버튼을 누르고 **Add package by name…** 을 선택합니다.
3. Name 칸에 정확히 아래 이름을 입력합니다.
   ```
   com.unity.nuget.newtonsoft-json
   ```
4. Version 칸은 비워두거나 `3.2.1` 을 입력하고 **Add** 를 누릅니다.
5. 설치가 끝나면 Package Manager 목록의 *In Project* 에 **Newtonsoft Json** 이 보입니다.
6. Console 에 `The type or namespace name 'Newtonsoft' could not be found` 오류가 남아 있으면 에디터를 한 번 재시작합니다.

> 이 프로젝트는 `JsonUtility` 를 쓰지 않습니다. (`float?` 같은 null 허용 값을 지원하지 않기 때문입니다.) 그래서 Newtonsoft 가 반드시 필요합니다.

## 3. 실행 — 씬 설정 필요 없음

씬·UIDocument·PanelSettings 는 **코드가 자동으로 만듭니다.** 직접 끌어다 놓을 것이 없습니다.

1. 프로젝트를 열면 `Assets/Scenes/IvyMatch.unity` 가 자동 생성되고 Build Settings 첫 번째 씬으로 등록됩니다. (Untitled 씬에 저장 안 된 작업이 있으면 건너뛰고 Console 에 안내가 뜹니다 → 메뉴 **IvyMatch → Setup Scene**)
2. Game 창 해상도를 **Portrait 390×844** (또는 Simulator) 로 맞추고 **Play**.
3. 8개 학교 카드가 보이면 성공입니다. (어떤 씬에서 Play 해도 `IvyMatchBootstrap` 이 대시보드를 만들어 줍니다.)

동작 원리:

- `Scripts/IvyMatchBootstrap.cs` — 씬 로드 직후 `DashboardController` 가 없으면 카메라, PanelSettings(390×844, 런타임 테마), UIDocument, DashboardController 를 만듭니다.
- UI 파일은 `Assets/IvyMatch/Resources/UI/` 에 있어야 합니다 (`Dashboard.uxml`, `Dashboard.uss`, `SchoolCard.uxml`, `IvyRuntimeTheme.tss`). 빌드에 포함되려면 `Resources` 안이어야 합니다.
- 씬에 직접 DashboardController 를 배치해도 됩니다. 빈 칸(UIDocument·PanelSettings·Card Template)은 Resources 에서 자동으로 채웁니다.
- `Editor/IvyMatchSceneSetup.cs` — 씬 생성/빌드 등록 담당 (에디터 전용, 빌드에 안 들어감).

UI Toolkit 을 처음 쓰는 분을 위한 요점:

- 화면 구조는 `Dashboard.uxml`(HTML 같은 역할), 모양은 `Dashboard.uss`(CSS 같은 역할), 동작은 `DashboardController.cs`(JavaScript 같은 역할)입니다.
- 코드는 UXML 의 `name="..."` 속성으로 요소를 찾습니다. (예: `Q<Label>("summary-bar")`) UXML 에서 name 을 바꾸면 코드도 같이 바꿔야 합니다. 이름 목록은 `CONTRACT.md` §6 에 있습니다.
- UXML/USS 는 Project 창에서 더블클릭하면 **UI Builder** 로 열립니다.

## 5. 한글 폰트 (Noto Sans KR)

한글을 제대로 보이게 하려면 폰트 파일을 직접 넣어야 합니다. 폰트가 없어도 영어는 정상 표시되고, 한글만 네모(□)로 보이거나 기본 폰트로 대체됩니다. UI 문자열은 "영어 · 한글" 형태라서 폰트가 없어도 화면 이해는 가능합니다.

1. Google Fonts 의 **Noto Sans KR** (SIL Open Font License, 상업적 사용 가능)을 다운로드합니다.
2. 압축을 풀어 `NotoSansKR-Regular.ttf` 를 찾습니다. (가변 폰트만 있는 경우 `NotoSansKR-VariableFont_wght.ttf` 를 `NotoSansKR-Regular.ttf` 로 이름을 바꾸어 쓰거나, static 폰트 묶음에서 Regular 를 고릅니다.)
3. 파일을 **반드시 아래 경로**에 넣습니다.

   ```
   Assets/IvyMatch/Resources/Fonts/NotoSansKR-Regular.ttf
   ```

   `Resources` 폴더가 없으면 `Assets/IvyMatch/` 아래에 직접 만듭니다. (폴더 이름은 대소문자까지 정확히 `Resources`)

4. 이유(경로 주의): `IvyMatchBootstrap.LoadKoreanFont()` 가 `Resources.Load("Fonts/NotoSansKR-Regular")` 로 찾습니다. 폰트가 없으면 OS 한글 폰트(맑은 고딕 등)로 대체하므로 에디터에서는 한글이 보이지만, **폰에서는 기기 폰트에 의존하므로 앱 빌드 전에는 꼭 넣으세요.** 그래서 `Assets/IvyMatch/Fonts/` 같은 일반 폴더에 넣으면 찾지 못합니다. (`Assets/IvyMatch/Fonts/.gitkeep` 은 폴더 자리 표시용이며 폰트 위치가 아닙니다.)
5. 폰트를 넣은 뒤 Project 창에서 `.ttf` 를 선택해 Inspector 의 **Character** 가 **Dynamic** 인지 확인합니다. (기본값)
6. Play 를 다시 눌러 한글(`합격률`, `검증 전` 등)이 보이는지 확인합니다. 그래도 네모로 보이면 Unity 를 재시작해 임포트를 새로고침하세요.

## 6. Android / iOS 빌드 체크리스트

- [ ] **File → Build Profiles**(Unity 6) 또는 **Build Settings**(2022.3)에서 플랫폼을 Android 또는 iOS 로 전환합니다.
- [ ] **Player Settings → Resolution and Presentation → Default Orientation = Portrait** 로 고정하고, *Allowed Orientations* 에서 Landscape 는 끕니다.
- [ ] `Assets/StreamingAssets/schools.json` 이 프로젝트에 있는지 확인합니다. (폴더 이름은 정확히 `StreamingAssets`.) Android 에서는 파일이 APK 안에 들어가므로 `File` API 대신 `UnityWebRequest` 로 읽으며, 코드(`SchoolRepository`)가 이미 처리합니다.
- [ ] **인터넷 권한은 `Remote Json Url` 을 쓸 때만** 필요합니다. 쓰지 않으면 켜지 마세요.
  - Android: *Player Settings → Other Settings → Internet Access = Require*
  - iOS: 별도 설정은 없지만 URL 은 반드시 **HTTPS** 여야 합니다. (App Transport Security)
- [ ] Build Settings 첫 씬이 `Assets/Scenes/IvyMatch.unity` 인지 확인합니다.
- [ ] Android 는 *Minimum API Level* 을 Unity 가 권장하는 값으로 두고, IL2CPP + ARM64 를 사용합니다.
- [ ] 릴리스 빌드에서 값이 비어 보이면 *Player Settings → Other Settings → Managed Stripping Level* 을 **Low** 로 낮춰 봅니다. (IL2CPP 코드 제거로 JSON 모델 필드가 지워지는 경우를 대비)
- [ ] 실기기에서 한글 폰트가 보이는지(5번), 카드 1열 레이아웃(폭 600 미만에서 `narrow` 클래스 적용)이 맞는지 확인합니다.

## 7. 데이터 갱신 절차

데이터는 모두 `schools.json` 한 파일에 있습니다. (스키마 v2, 50개교 — 아이비 8 + Ivy+ 7 + 상위권 35)

**현재 상태 (2026-10-22)**: 50개교 중 49개교가 `verified` 입니다. UVA 는 CDS 숫자가 들어갔지만 지원 마감일을 확인하지 못해 `pending` 입니다. 전공 비율(CDS J1)은 49개교에 있고, Wake Forest 는 공식 J1 표가 80.5%까지만 채워져 있어 비워 두었습니다. 각 학교의 Common Data Set(대부분 2025-26, Boston College·Northeastern 은 2024-25)과 공식 입학처·재정지원 페이지에서 확인한 값만 들어 있고, 출처 URL 은 학교별 `sources` 에 있습니다. 지원 라운드 날짜는 2027년 가을 입학 주기 기준이며, 공식 페이지가 "Mid-December" 처럼 모호하게만 적은 발표일은 `null` 입니다. 확인하지 못한 값은 `null` 이며 카드에는 `—`로 표시됩니다. 필드 설명은 `CONTRACT.md` §3 에 있습니다.

학교별로 채워야 하는 것 (출처 → 필드):

| 출처 | 채울 필드 |
|---|---|
| Common Data Set **C1** | `stats.applied / admitted / enrolled`, `stats.rateOverall` |
| CDS **C21–C22** | `stats.rateEarly / rateRegular`, `admissions.rounds[].deadline / notification` |
| CDS **C8** | `admissions.testPolicy` (+ 학교 입학처 공지로 `testPolicyNote`) |
| CDS **C9** | `stats.satMid50 / actMid50` |
| CDS **G** (비용) | `cost.costOfAttendance / tuition` |
| CDS **H6** + 학교 재정지원 페이지 | `cost.needBlindIntl / meetsFullNeedIntl / cssProfile / intlAidShare` |
| CDS **B2** / Open Doors | `intl.intlShare / koreanStudents` |
| 학교 입학처 페이지 | `admissions.supplementEssays / applicationUrl`, `intl.toeflMin` |
| CDS **J1** (학사 학위 분야별 비율) | `majors.bachelor[]` (CIP 2자리 코드별 %) |
| NCES IPEDS HD (기관 정보 파일) | `lat`, `lng`, `ipedsId` (지도 위치) |
| CDS **B1/B2** 학부생 수 | `undergradSize`(표시용 문자열), `undergradCount`(규모 필터용 숫자) |

1. **`schools.json` 수정 또는 교체**
   - 수치는 공식 자료(예: 각 대학의 Common Data Set)로 확인한 값만 넣습니다.
   - 근거 없는 값은 **절대 추정해서 넣지 말고 `null`** 로 둡니다. v2에서는 모든 숫자 필드가 `null` 을 허용합니다.
   - 날짜는 `"2025-11-01"` 형식. 상세 패널에는 `ED Nov 1 → Dec 15` 처럼 표시됩니다.
   - 카드 왼쪽 위 로고 배지는 `abbr`(학교 약칭, 1-5자)를 `colorHex`(학교 대표색) 배경에 표시합니다. `abbr` 가 없으면 이름 앞 2글자를 씁니다. 글자색(흰색/검정)은 배경과의 대비가 더 큰 쪽으로 자동 선택됩니다.
2. **검증 상태 갱신**
   - `status`: `"unverified"` → `"pending"` → `"verified"`
   - `verifiedAt`: 확인한 날짜 `"2026-10-20"` 형식 (카드에는 `2026.10.20 확인` 으로 표시)
   - `sources`: `[{ "label": "Harvard CDS 2024-25", "url": "https://..." }]` 처럼 출처 레이블과 URL
   - 최상위 `generatedAt` 날짜도 갱신합니다.
3. **앱에 반영**
   - *번들로 반영*: 새 `schools.json` 을 `Assets/StreamingAssets/` 에 덮어쓰고 다시 빌드합니다.
   - *원격으로 반영(앱 재배포 없이)*: 새 `schools.json` 을 HTTPS 서버(GitHub Pages, S3 등)에 올리고, 그 주소를 `DashboardController` 의 **Remote Json Url** 에 넣어 한 번 빌드합니다. 이후에는 파일만 교체하면 앱이 시작할 때 내려받습니다.
4. 앱의 동작 방식
   - 시작하면 먼저 기기에 저장된 원격 갱신본(`Application.persistentDataPath/schools.json`)을 읽고, 없으면 번들본을 읽습니다.
   - `Remote Json Url` 이 있으면 다운로드 → JSON 검증 → 저장 후 화면을 다시 그립니다.
   - 네트워크가 없거나 JSON 이 잘못되었으면 조용히 실패하고 기존 데이터를 계속 씁니다.
   - 잘못된 원격본이 저장되어 문제가 되면 앱 데이터를 지우거나 `persistentDataPath` 의 `schools.json` 을 삭제하면 번들본으로 돌아갑니다.

---

## 알려진 제한

- **통계는 공식 CDS 기준이지만 매년 바뀝니다.** 2026-10-21 에 확인한 값이며, UVA 는 아직 `pending` 입니다. 비용(`cost`)은 대부분 CDS G1 의 등록금+필수 수수료+기숙사·식비 합계이고, 일부 학교는 2026-27 학년도 금액입니다. 기준은 학교별 `cost.note` 에 적혀 있습니다.
- **정시(RD) 합격률은 50개교 모두 `null` 입니다.** 공식적으로 발표하는 학교가 없어 계산하지 않았습니다. 얼리 합격률은 CDS C21/C22 나 학교 공식 발표에 지원자·합격자 수가 모두 있을 때만 들어 있습니다. 값이 없는 막대는 길이 0 과 `—` 로 표시되고 행이 흐리게(dim) 보입니다.
- 전략/평가 포인트 문구(`strategy`, `lookFor`, `notable`)에는 출처 없는 서술(예: Dartmouth 얼리 합격률 언급)이 웹 원본에서 그대로 옮겨져 있습니다. 숫자 검증과 별개로 아직 정리하지 않았습니다.
- 입학 연도(`cycle`)는 사용한 CDS 연도를 따릅니다. 대부분 `Class of 2029` 이고, Boston College·Northeastern 은 `Class of 2028` 입니다.
- **한글 폰트는 포함되어 있지 않습니다.** 5번 절차대로 직접 넣어야 합니다.
- UI Toolkit 런타임 UI 는 `Resources` 폰트가 없을 때 에러 없이 기본 폰트로 대체합니다. 한글이 □ 로 나오는 것은 폰트 문제입니다.
- 이 코드는 Unity 에디터에서 컴파일/실행해 보지 못한 상태입니다. 첫 실행 시 USS 의 "unknown property" 경고나 오타성 오류가 있을 수 있습니다.
- GameObject 를 비활성화했다가 다시 켜면 UIDocument 가 화면 트리를 새로 만들어 버튼 연결이 풀립니다. 이 앱은 단일 씬에서 항상 켜 두는 것을 전제로 합니다.
- 상세 패널은 카드 목록과 같은 ScrollView 안에 있어 함께 스크롤됩니다. 마우스/시뮬레이터에서는 드래그로 스크롤되고(`DragScrollManipulator`), 10px 이상 끌면 카드 선택으로 처리하지 않습니다.

---

## 검색·필터

상단 검색창은 학교 영문·한글 이름, 약칭, 도시·주로 찾습니다. "Filters · 필터" 버튼을 누르면 유형, 지역, 학부생 수, 연간 총비용, 지원 전형, 시험 정책, 국제학생 재정지원, 전공, 정렬을 고를 수 있습니다. 같은 항목 안의 선택은 "또는", 항목끼리는 "그리고"로 묶입니다. 켜진 조건의 값이 없는 학교는 결과에서 빠지고 요약 줄에 개수가 표시됩니다. 전공 필터는 해당 분야 졸업생 비중이 5% 이상인 학교를 보여 주고 비중 순으로 정렬합니다. 동작 규칙은 `CONTRACT.md` §11 에 있습니다.

## 미국 지도

필터 줄의 "Map · 지도" 버튼을 누르면 목록 대신 미국 지도가 보입니다. 지도에는 현재 검색·필터 결과에 남은 학교만 학교색 약칭 배지로 표시됩니다. 가까운 학교들은 숫자 배지로 묶이고, 숫자를 누르면 확대됩니다. 끝까지 확대해도 겹치면 원형으로 펼쳐집니다. 위쪽 버튼으로 동북부·중서부·남부·서부로 바로 이동할 수 있습니다. 학교 배지를 누르면 지도 아래에 상세 정보가 열립니다.

지도는 미국 인구조사국의 공개 주 경계 자료로 만들었고, 인터넷이나 API 키 없이 동작합니다. 학교 좌표는 미국 교육부 NCES 의 IPEDS 기관 정보 파일에서 가져왔습니다. 지도를 다시 만드는 방법은 `CONTRACT.md` §12 에 있습니다.

