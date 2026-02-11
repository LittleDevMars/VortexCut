# VortexCut UI 구조 및 디자인 설명서

이 문서는 멀티모달 기능이 없는 환경(예: Claude Code)에서 VortexCut 애플리케이션의 UI/UX를 이해하기 위해 작성되었습니다. 코드(`MainWindow.axaml`, `ModernTheme.axaml` 등)를 기반으로 분석한 시각적 구조와 레이아웃을 설명합니다.

## 1. 전체적인 테마 및 스타일 (Visual Theme)

- **테마 (Theme)**: "Deep Dark" 스타일로, VS Code와 Kdenlive에서 영감을 받았습니다. 장시간 작업 시 눈의 피로를 줄이기 위해 어두운 회색조 배경을 사용합니다.
- **주요 색상 (Color Palette)**:
  - **배경 (Backgrounds)**:
    - 기본 배경 (`BgBase`): `#121212` (매우 어두운 회색)
    - 패널 배경 (`BgPanel`): `#1E1E1E` (어두운 회색)
    - 헤더 배경 (`BgHeader`): `#252526`
  - **강조색 (Accent)**: `#007ACC` (VS Code Blue 계열)
  - **텍스트 (Text)**:
    - 기본 (`TextPrimary`): `#CCCCCC` (밝은 회색)
    - 밝음 (`TextBright`): `#FFFFFF` (흰색)
    - 보조 (`TextSecondary`): `#969696` (중간 회색)
- **폰트**: `Segoe UI`, `Inter`, `Sans-Serif`를 우선순위로 사용합니다.

## 2. 메인 윈도우 레이아웃 (Layout Structure)

애플리케이션은 크게 **상단 헤더(메뉴)**와 **작업 공간(Workspace)**으로 나뉩니다. 작업 공간은 다시 **상단 패널(Bin/Monitor)**과 **하단 패널(Timeline)**로 분할되어 있습니다.

### 2.1. 타이틀 바 및 메뉴 (Title Bar & Menu)
- **위치**: 최상단 (Height: 38px)
- **구성**:
  - **로고**: "VortexCut" 텍스트 (Accent 색상 배경의 뱃지 형태)
  - **메뉴바**: File, Edit, View, Help
  - **프로젝트 정보**: 우측에 현재 프로젝트 이름 표시

### 2.2. 상단 작업 영역 (Upper Pane)
좌측부터 우측으로 4개의 구획으로 나뉘어 있습니다. 각 구획은 `GridSplitter`로 크기 조절이 가능합니다.

1.  **Project Bin (프로젝트 보관함)**
    - **위치**: 좌측 (Column 0)
    - **역할**: 불러온 미디어 파일(비디오, 오디오 등)을 리스트 형태로 표시합니다.
    - **구성**:
      - 헤더: "PROJECT BIN"
      - 리스트 아이템: 썸네일(아이콘), 파일명, 해상도, 길이(ms) 정보 포함.

2.  **Clip Monitor (소스 모니터)**
    - **위치**: 중앙 좌측 (Column 2)
    - **역할**: Project Bin에서 선택한 원본 소스를 미리보기 합니다.
    - **구성**:
      - 헤더: "CLIP MONITOR"
      - 내용: 현재 "No Clip Selected" 텍스트가 기본 표시됨. 검은색 배경.

3.  **Program Monitor (프로그램 모니터)**
    - **위치**: 중앙 우측 (Column 4)
    - **역할**: 타임라인에서 편집된 최종 영상을 미리보기 합니다.
    - **구성**:
      - 헤더: "PROGRAM MONITOR"
      - 화면: 영상 미리보기 이미지 (`Image` 컨트롤)
      - **제어 패널 (Controls)** (하단):
        - **타임코드**: `00:00:00:00` (청록색 텍스트)
        - **재생 제어**: 이전 프레임(⏮), 재생/일시정지(▶ - Accent 색상 버튼), 다음 프레임(⏭)
        - **추가 도구**: 설정(⚙), 전체화면(⛶)

4.  **Inspector (속성 관리자)**
    - **위치**: 우측 (Column 6)
    - **역할**: 선택한 클립의 속성 및 효과를 조정합니다.
    - **구성**:
      - 헤더: "INSPECTOR"
      - **Properties**: Scale(크기), Position(위치) 입력 필드.
      - **Effects**: 적용된 효과 리스트 (예: Color Correction, Transform).

### 2.3. 하단 작업 영역 - 타임라인 (Timeline)
- **위치**: 하단 전체 (Row 2)
- **헤더 구성**:
  - 타이틀: "TIMELINE"
  - **툴바 (Toolbar)**:
    - ✂ Razor (자르기)
    - 🧲 Snap (자석 효과)
    - \+ Add Marker (마커 추가)
  - **상태 정보 (Stats)**:
    - Clips: 현재 클립 개수
    - FPS: 프레임 레이트 (예: 30)
- **타임라인 캔버스**:
  - 트랙(Track)들이 수직으로 나열되고, 시간 축이 수평으로 뻗어있는 영역입니다.
  - 여기에 비디오/오디오 클립들이 배치됩니다.

## 3. UI 컴포넌트 스타일 (Component Styling)
- **패널 헤더**: 짙은 배경(`BgHeader`)에 대문자 볼드체 타이틀로 구분감을 줍니다.
- **버튼**:
  - 일반 버튼: 짙은 회색 배경, 1px 테두리.
  - 툴 버튼: 배경이 투명하고 아이콘 위주, 마우스 오버 시 밝아짐.
  - 중요 버튼(재생 등): Accent 색상(`#007ACC`) 사용.
- **입력 필드 (TextBox)**: 배경이 약간 밝은 회색(`#252526`), 포커스 시 Accent 색상 테두리.

이 문서를 통해 UI를 직접 볼 수 없는 환경에서도 VortexCut의 구조와 기능을 파악하고 코드를 수정할 수 있습니다.
