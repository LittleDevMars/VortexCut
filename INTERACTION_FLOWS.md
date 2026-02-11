# VortexCut 고급 인터랙션 및 UX 흐름도 (High-End UX Guide)

본 문서는 VortexCut의 UI를 단순한 "기능 구현" 수준을 넘어, **상용 소프트웨어 수준의 매끄럽고 고급스러운 사용자 경험(UX)**으로 고도화하기 위한 상세 가이드입니다.
Claude Code가 UI 코드를 작성할 때 이 문서의 **"Micro-interactions(미세 상호작용)"**과 **"Visual Feedback(시각적 피드백)"** 규칙을 따르도록 하십시오.

## 1. UX 핵심 철학: "물 흐르는 듯한 반응 (Fluidity)"

현대적인 UI(2026 트렌드)의 핵심은 **즉각적인 반응**과 **부드러운 전환**입니다.
- **Latencyless Feel**: 무거운 작업(렌더링 등)이 뒤에서 돌더라도, UI 자체는 0.1초 내에 반응해야 합니다 (예: 클릭 시 버튼 눌림 효과 즉시 발생).
- **Motion Meaning**: 모든 애니메이션은 의미가 있어야 합니다. (예: 패널이 열릴 때 내용물이 `Opacity 0 -> 1` 및 `Slide Up`으로 등장).

---

## 2. 주요 워크플로우별 UX 디테일

### 2.1 미디어 가져오기 (Import Workflow)
단순한 파일 열기가 아닌, "보관함으로 데이터를 전송한다"는 느낌을 강조합니다.

1.  **Drag & Drop (외부 탐색기 -> Project Bin)**
    -   **Hover 상태**: 파일을 빈(Bin) 영역 위로 드래그하면, 빈의 테두리가 `ColorAccent`로 빛나며(Glow Effect) "Drop here" 힌트 표시.
    -   **Drop 직후**:
        -   파일 목록에 즉시 "Skeleton UI" (로딩 중인 회색 박스)가 추가됨.
        -   비동기로 썸네일 추출이 완료되면 부드럽게(`Fade In, 300ms`) 실제 이미지로 교체.
2.  **Hover Preview (마우스 오버 미리보기)**
    -   빈(Bin)의 썸네일 위에 마우스를 올리면, 좌우로 마우스를 움직여(Hover Scrub) 해당 클립을 빠르게 탐색 가능해야 합니다.

### 2.2 타임라인 편집 (Timeline Editing)
가장 사용 빈도가 높고 "손맛"이 중요한 영역입니다.

1.  **클립 배치 (Clip Placement)**
    -   **Ghosting**: 빈에서 타임라인으로 드래그 시작 시, 반투명한 클립 아이콘(Ghost)이 커서를 따라다님.
    -   **Snap Guidance**:
        -   클립 경계나 플레이헤드 근처(10px 이내)로 접근하면, 클립이 "자석처럼" 딱 붙음.
        -   동시에 수직선(Vertical Line)이 전체 트랙 높이만큼 `Accent Color`로 반짝임.
2.  **Zooming & Panning**
    -   **Mouse Wheel Zoom**: 휠을 돌릴 때 단계적으로 뚝뚝 끊기지 않고, 부드럽게 보간(Interpolation)되어 확대/축소됩니다.
    -   **Inertia Scrolling**: 팬(Pan, 중간 버튼 드래그) 동작을 멈췄을 때, 관성(Inertia)에 의해 약간 더 미끄러지듯 멈춤.
3.  **Razor Tool (자르기 도구)**
    -   단축키 `C`를 누르면 커서가 면도칼 아이콘으로 변경.
    -   클립 위를 지나갈 때, 자를 예정인 위치에 **빨간색 점선(Red Dashed Line)**이 미리 표시됨 (Hover Feedback).

### 2.3 키프레임 애니메이션 (Keyframing)
정밀함과 직관성이 핵심입니다.

1.  **키프레임 조작**
    -   **Hover Enlargement**: 다이아몬드(키프레임) 위에 마우스를 올리면 크기가 `1.2배` 커지며 선택 용이성을 높임.
    -   **Smart Tooltip**: 드래그 중에는 현재 시간과 값(`00:05:12, Opacity: 50%`)이 툴팁으로 실시간 표시.
2.  **그래프 에디터 (Graph Editor)**
    -   베지어 핸들을 당길 때, 연결된 곡선이 실시간으로 부드럽게 갱신되어야 함 (No Lag).
    -   선택된 구간은 옅은 틴트(`ColorAccent` + `Opacity 0.1`)로 배경 강조.

---

## 3. Avalonia 구현 가이드 (Technical Tips)

고품질 UI 구현을 위해 다음 Avalonia 기능들을 적극 활용하십시오.

### 3.1 스타일 및 애니메이션
- **ControlTheme**: 모든 버튼과 입력 필드는 `ControlTheme`을 사용하여 상태(Normal, PointerOver, Pressed, Disabled)별 스타일을 정의하고, `Transitions` 속성을 사용해 색상/크기 변화를 부드럽게 처리하십시오.
    ```xml
    <Setter Property="Transitions">
        <Transitions>
            <BrushTransition Property="Background" Duration="0:0:0.2"/>
            <TransformOperationsTransition Property="RenderTransform" Duration="0:0:0.1"/>
        </Transitions>
    </Setter>
    ```

### 3.2 성능 최적화 (High Performance)
- **RenderOptions**: 이미지나 비디오 렌더링 시 고품질 보간을 사용하되, 스크롤링 중에는 저품질로 전환하는 **"Dynamic Quality Scaling"** 기법 적용 고려.
    ```xml
    RenderOptions.BitmapInterpolationMode="HighQuality"
    ```
- **Composition API**: 복잡한 애니메이션(블러, 그림자, 비트맵 효과)은 UI 스레드가 아닌 컴포지터(Compositor) 레벨에서 처리하여 60fps 유지.

## 4. UI 고도화 체크리스트

다음 항목들이 구현되었는지 점검하여 "형편없는" UI를 탈피하십시오.

- [ ] **Custom Cursors**: 상황에 맞는 커서 (자르기, 이동, 리사이즈, 핸들 조작) 사용.
- [ ] **Focus Visuals**: 키보드 탭 탐색 시 명확한 포커스 링(Focus Ring) 표시.
- [ ] **Empty States**: 리스트가 비었을 때 단순히 비워두지 말고, 일러스트와 함께 "여기로 파일을 끌어놓으세요" 안내 문구 표시.
- [ ] **Toast Notifications**: 저장 완료, 렌더링 시작/종료 시 우측 하단에 비침습적인 토스트 알림 팝업.

이 문서는 VortexCut의 UI를 프로페셔널한 수준으로 끌어올리는 기준점(Benchmark)이 됩니다.
