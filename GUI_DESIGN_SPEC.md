# VortexCut GUI 상세 설계 명세서 (2026 Modern Edition)

본 문서는 **2026년형 현대적인 크로스 플랫폼 영상 편집기**인 VortexCut의 GUI 레이아웃과 디자인 명세를 정의합니다.
UI/UX 디자인은 **Kdenlive의 직관적인 4분할 레이아웃**을 계승하되, 최신 디자인 트렌드(Deep Dark Mode, Glassmorphism, Icon-centric)를 반영하여 전문가 수준의 사용성을 제공하는 것을 목표로 합니다.

## 1. 디자인 철학 (Design Philosophy)

- **Deep Dark Aesthetic**: 장시간 작업 시 눈의 피로를 최소화하기 위해 `#121212` (Base)와 `#1E1E1E` (Panel) 배경색을 사용합니다.
- **Visual Hierarchy**: 중요한 정보(타임코드, 선택된 클립)는 Accent Color (`#007ACC`)로 강조하고, 보조 정보는 감도 낮은 회색(`TextSecondary`)으로 처리합니다.
- **Clean & Flat**: 불필요한 그라데이션과 3D 효과를 배제하고, 깔끔한 평면 디자인과 미묘한 테두리(`1px`)로 구획을 나눕니다.
- **Icon-First**: 텍스트 레이블을 최소화하고 직관적인 벡터 아이콘(SVG)을 사용하여 공간 효율성을 높입니다.

---

## 2. 전체 레이아웃 (Global Layout)

화면은 크게 **Top Bar**, **Middle Workspace**, **Bottom Timeline**, **Status Bar**로 구성됩니다.

```
+-----------------------------------------------------------------------+
|  [Menu Bar] File  Edit  View  Project  Tool  Help          [Layout ▾] |
+------------------+----------------------------------+-----------------+
|                  |                                  |                 |
|  [1. Project Bin]|      [2. Source Monitor]         | [4. Inspector]  |
|  (Media / FX)    |      (Original Clip Preview)     | (Properties)    |
|                  |                                  |                 |
|   List / Grid    +----------------------------------+   Audio Mixer   |
|                  |                                  |                 |
|                  |      [3. Program Monitor]        |   Color Grade   |
|                  |      (Main Timeline Output)      |                 |
|                  |                                  |                 |
+------------------+----------------------------------+-----------------+
|                                                                       |
|  [5. Timeline Toolbar]  [Timecode 00:00:15:00]                        |
|                                                                       |
|  V1 [ Clip A ][ Clip B ]--------------------------------------------  |
|  V2 [ Overlay Text ]------------------------------------------------  |
|  A1 [ Audio Track 1 ]-----------------------------------------------  |
|  A2 [ Background Music ]--------------------------------------------  |
|                                                                       |
+-----------------------------------------------------------------------+
|  Status: Ready | GPU: 12% | RAM: 4.2GB              [Render Queue]    |
+-----------------------------------------------------------------------+
```

---

## 3. 상세 패널 명세 (Panel Specifications)

### 3.1. 상단 메뉴 및 툴바 (Top Bar)
- **위치**: 최상단 (Height: 40px)
- **배경**: `#252526`
- **구성요소**:
  - **App Logo**: 좌측 상단. Accent Color 배지.
  - **Menu Bar**: File, Edit, View, Project, Tool, Help.
  - **Workspace Switcher (우측)**: Editing, Color, Audio, Effects 탭 버튼.
- **주요 동작**:
  - `Ctrl+N`: 새 프로젝트
  - `Ctrl+O`: 프로젝트 열기
  - `Ctrl+S`: 저장

### 3.2. 프로젝트 빈 (Project Bin)
- **위치**: 좌측 상단 (Width: 15~20%)
- **기능**: 미디어 파일 관리 및 효과 탐색기.
- **탭 구성 (하단 또는 상단 탭)**:
  1.  **Project Bin**: 임포트된 비디오, 오디오, 이미지 리스트.
  2.  **Effects**: 비디오/오디오 효과 라이브러리 (검색창 포함).
  3.  **Transitions**: 화면 전환 효과.
- **주요 UI 요소**:
  - **검색창**: 상단 고정. 필터링 기능.
  - **보기 모드**: 리스트 보기 (상세 정보) / 그리드 보기 (큰 썸네일).
  - **Import 버튼**: `+` 아이콘 강조.
- **단축키**:
  - `Add Clip`: `Ctrl+I`

### 3.3. 모니터 패널 (Monitors)
- **위치**: 중앙 상단 (Width: 40~50%)
- **구조**: 듀얼 뷰 (Split Left/Right) 또는 탭 방식 전환.
  1.  **Source Monitor (Clip Monitor)**: Project Bin에서 더블 클릭한 원본 소스 확인. `I`(In), `O`(Out) 점 설정.
  2.  **Program Monitor**: 타임라인의 현재 재생 위치(Playhead) 화면 출력.
- **공통 제어바 (하단)**:
  - **Timecode**: `00:00:00:00` (클릭하여 직접 입력 이동 가능).
  - **Transport**: `Prev Frame`, `Play/Pause`, `Next Frame`.
  - **Jog/Shuttle**: 슬라이더로 재생 속도 조절.
  - **Loop**: 구간 반복 토글.
- **단축키**:
  - `Space`: 재생/정지
  - `J/K/L`: 뒤로 감기 / 정지 / 앞으로 감기 (반복 누름 시 배속)
  - `Left/Right`: 1 프레임 이동

### 3.4. 인스펙터 및 믹서 (Right Panel)
- **위치**: 우측 상단 (Width: 20~25%)
- **기능**: 선택된 객체의 속성 제어. 아코디언 또는 탭 구조 추천.
- **탭 구성**:
  1.  **Inspector (Properties)**:
      - **Video**: Opacity, Position (X,Y), Scale, Rotation.
      - **Audio**: Volume, Pan.
      - **Effect Stack**: 적용된 효과 목록 (순서 변경 가능), 효과별 파라미터.
  2.  **Audio Mixer**: 트랙별 볼륨 레벨 미터 및 페이더.
  3.  **Color**: 간단한 색 보정 휠(Lift, Gamma, Gain).
- **인터랙션**:
  - 숫자 필드는 드래그하여 값 조절 가능 (Scrubbable inputs).

### 3.5. 타임라인 (Timeline)
- **위치**: 하단 전체 (Height: 30~40%)
- **기능**: 멀티 트랙 비선형 편집(NLE).
- **구조**:
  - **Toolbar (상단)**:
    - **Select Tool (`V`)**: 기본 선택.
    - **Razor Tool (`C`)**: 자르기.
    - **Spacer Tool (`M`)**: 트랙 밀기.
    - **Snapping (`S`)**: 자석 기능 토글.
  - **Track Header (좌측)**:
    - 트랙 이름 (V1, A1 등).
    - Toggle Visibility (눈 아이콘), Lock (자물쇠), Mute/Solo.
  - **Track Area (우측)**:
    - **Playhead**: 현재 재생 위치 표시선 (빨간색).
    - **Wuler (눈금자)**: 시간 표시. 클릭 시 Playhead 이동.
    - **Clips**: 직사각형 블록. 썸네일(시작/끝)과 파형(Audio) 표시.
      - Opacity/Volume 조정 핸들(라인) 오버레이.
- **단축키**:
  - `Ctrl+X/C/V`: 컷/복사/붙여넣기
  - `Del`: 리플 삭제 (빈 공간 없이 삭제)

### 3.5.1. 타임라인 동작 원칙 (Timeline Behavior Rules) - *New*
HCI 연구 및 최신 NLE 표준에 따른 타임라인 인터랙션 규칙을 정의합니다.

1.  **시맨틱 줌 (Semantic Zooming)**:
    -   **Zoom Out (축소)**: 클립을 단순한 색상 블록으로 표시. 텍스트 레이블만 최소한으로 노출하여 전체적인 구조 파악에 집중.
    -   **Zoom In (확대)**: 클립 내부에 **프레임 썸네일(Start/End)**과 **오디오 파형(Waveform)**을 렌더링. 마커 텍스트와 세부 싱크 포인트 표시.

2.  **직접 조작 피드백 (Direct Manipulation Feedback)**:
    -   **Snapping**: 클립 이동 시 다른 클립의 끝이나 플레이헤드에 근접하면 자석처럼 붙으며, **수직 가이드라인(Accent Color)**이 일시적으로 나타남.
    -   **Ripple/Roll Edit**:
        -   클립 사이를 드래그할 때 영향을 받는 인접 클립들이 실시간으로 밀리거나 당겨지는 애니메이션 제공.
        -   편집 모드에 따라 커서 아이콘 변경 (Ripple: `][`, Roll: `<|>`).

3.  **성능 최적화 시각화 (Performance Visualization)**:
    -   **Proxy Indicator**: 고해상도 영상을 프록시(저해상도 대체본)로 재생 중일 때, 뷰어 또는 클립 헤더에 `PROXY` 배지 표시.
    -   **Background Render Bar**: 타임라인 상단 시간자(Ruler) 아래에 렌더링 상태 표시 줄 (Red: 렌더링 필요, Green: 렌더링 완료).

---

## 4. 시각적 스타일 가이드 (Visual Style Guide)

### 4.1. 색상 (Color Palette)
| 이름 | 색상코드 | 용도 |
|---|---|---|
| **Background** | `#121212` | 메인 윈도우 배경 |
| **Panel BG** | `#1E1E1E` | 패널 컨테이너 배경 |
| **Border** | `#3E3E42` | 패널 경계선, 버튼 테두리 |
| **Text Main** | `#E1E1E1` | 주요 텍스트, 메뉴 |
| **Text Muted** | `#858585` | 비활성 텍스트, 레이블 |
| **Accent** | `#007ACC` | 선택, 포커스, 활성 상태 (Blue) |
| **Warning** | `#D7BA7D` | 주의, 경고 (Yellow/Orange) |
| **Error** | `#F48771` | 오류, 삭제 (Red) |

### 4.2. 타이포그래피 (Typography)
- **Font Family**: `Segoe UI` (Windows), `Inter` (Cross-platform), `Consolas` (Timecode).
- **Size**:
  - 기본 텍스트: `12px` or `13px`
  - 헤더/제목: `11px` (Bold, Uppercase)
  - 타임코드: `14px` (Monospace)

### 4.3. 아이콘 (Iconography)
- `Fluent System Icons` 또는 `Material Design Icons` 스타일 사용.
- `16x16` 또는 `20x20` 크기, 1px 스트로크.
- 채워진 아이콘(Filled)은 상태 On, 곽선(Outline)은 상태 Off를 표현.

본 문서는 Claude Code 및 AI 에이전트가 VortexCut의 UI를 구현하고 이해하는 데 있어 기준으로 사용됩니다.
