# 썸네일 Filmstrip 시스템 설계 문서

## 1. 문제 정의

### 1.1 프로 NLE의 Filmstrip
DaVinci Resolve, Premiere Pro, Final Cut Pro 등의 NLE는 타임라인 클립에 **빈틈 없는 연속 썸네일**(Filmstrip)을 표시한다.
각 프레임이 클립 폭을 빈틈없이 채우며, 사용자는 한눈에 영상 내용을 파악할 수 있다.

### 1.2 VortexCut 초기 문제
1. **썸네일을 시간 위치에 개별 배치** → 간격이 생겨 빈틈 발생
2. **긴 영상에서 간격이 뷰포트보다 큼** → 썸네일이 아예 안 보임 (17분 영상, 51초 간격)
3. **줌 레벨 무관하게 동일 간격** → 확대 시 더 큰 빈틈

---

## 2. 아키텍처 개요

```
┌─────────────────────┐    ┌──────────────────────┐    ┌─────────────────────┐
│  ThumbnailStripService │──→│  Rust FFI (FFmpeg)    │──→│  ClipCanvasPanel     │
│  - LRU 캐시           │    │  generate_video_      │    │  DrawThumbnailStrip() │
│  - 배경 비동기 생성    │    │  thumbnail()          │    │  연속 타일링 렌더링    │
│  - 3티어 해상도       │    └──────────────────────┘    └─────────────────────┘
└─────────────────────┘
```

### 핵심 분리: "생성"과 "렌더링"은 독립
- **생성 (ThumbnailStripService)**: 시간 간격으로 프레임을 디코딩하여 캐시에 저장
- **렌더링 (ClipCanvasPanel)**: 슬롯 단위로 연속 타일링, 가장 가까운 캐시된 프레임 사용

이 분리 덕분에:
- 생성이 완료되지 않아도 부분 표시 가능 (점진적 로딩)
- 줌 레벨이 바뀌어도 기존 캐시 활용 가능
- 메모리 예산 내에서 최적의 품질 보장

### 2.1 주요 퍼블릭 API 스케치

구체적인 타입/네임스페이스는 구현에서 조정 가능하지만, 큰 흐름은 다음과 같다.

```csharp
// 1) Service 쪽: 생성 + 캐시 조회
public sealed class ThumbnailStripService
{
    // (mediaPath, tier) 단위로 스트립을 관리
    public ThumbnailStrip GetOrRequestStrip(string mediaPath, ThumbnailTier tier);

    // 타임라인 편집 이벤트에 의해 특정 구간 무효화
    public void InvalidateRange(string mediaPath, long startMs, long endMs);
}

public sealed class ThumbnailStrip
{
    public string MediaPath { get; }
    public ThumbnailTier Tier { get; }
    public IReadOnlyList<CachedThumbnail> Thumbnails { get; } // 시간순 정렬
}

public sealed class CachedThumbnail
{
    public long TimeMs { get; }
    public int Width { get; }
    public int Height { get; }
    public byte[] RgbaData { get; }
}

// 2) ClipCanvasPanel 쪽: 슬롯 기반 렌더링
// - Zoom/TrackHeight/Clip 구간을 기준으로 슬롯을 계산하고
// - 각 슬롯의 중심 시간에 대해 FindNearestThumbnail() 수행
```

이 API 스케치는 **"서비스는 썸네일 리스트만 제공, 렌더러는 슬롯 계산/그리기만 담당"** 이라는 책임 분리를 구현자가 바로 이해할 수 있도록 돕기 위한 것이다.

---

## 3. 티어 시스템 (왜 3단계인가?)

### 3.1 줌 레벨과 썸네일 해상도의 관계

축소 상태에서 고해상도 썸네일은 낭비고, 확대 상태에서 저해상도는 흐리다.
`_pixelsPerMs` (1ms당 픽셀 수)를 기준으로 3단계로 분류:

| 티어 | 줌 조건 | 썸네일 크기 | 최대 간격 | 최대 장수 (1시간 기준) | 메모리 |
|------|---------|------------|----------|---------------------|--------|
| Low | < 0.05 px/ms | 80×45 | 10초 | ~360장 | ~5MB |
| Medium | 0.05~0.3 px/ms | 120×68 | 5초 | ~720장 | ~24MB |
| High | > 0.3 px/ms | 160×90 | 2초 | ~1800장 | ~104MB |

### 3.2 간격 계산 공식

```csharp
// Math.Clamp(기본간격, 최소간격, 최대간격)
Low:    Math.Clamp(durationMs / 10,  3000, 10000)  // 3~10초
Medium: Math.Clamp(durationMs / 20,  2000,  5000)  // 2~5초
High:   Math.Clamp(durationMs / 500, 1000,  2000)  // 1~2초
```

**왜 Math.Clamp인가?**
- `Math.Max(최소, duration/N)` 만 쓰면 → 긴 영상에서 간격이 무한히 커짐 (버그 원인)
- `Math.Clamp`로 상한을 걸면 → 17분 영상도 5초(Medium) 간격 = 204장으로 제한

### 3.3 이전 버그: Math.Max만 사용

```
17분 영상 (1,020,953ms), Medium 티어:
  Math.Max(2000, 1020953/20) = 51,047ms (51초 간격!)

  100% 줌에서 슬롯 폭 ≈ 106px
  51초 × 0.09 px/ms = 4,594px 간격
  → 뷰포트(~1400px)에 1개도 안 들어옴
  → 모든 슬롯이 같은 썸네일 반복
```

수정 후:
```
Math.Clamp(1020953/20, 2000, 5000) = 5,000ms (5초 간격)
  → 204장 생성, 슬롯마다 다른 프레임 표시
```

---

## 4. 연속 타일링 렌더링 (DrawThumbnailStrip)

### 4.1 이전 방식: 시간 기반 개별 배치
```
클립: [  thumb@0s  ----빈틈----  thumb@2s  ----빈틈----  thumb@4s  ]
```
각 썸네일을 `timeMs × _pixelsPerMs` 위치에 놓음 → 빈틈 발생

### 4.2 현재 방식: 슬롯 기반 연속 타일링
```
클립: [thumb|thumb|thumb|thumb|thumb|thumb|thumb|thumb|thumb]
       0s    1s    2s    3s    4s    5s    6s    7s    8s
```

1. 클립 시작부터 끝까지 `slotWidth` (트랙 높이 × 16:9) 단위로 슬롯 배치  
   - `slotWidth`는 트랙 높이(30~200px)와 연동되므로, 트랙 높이/줌(`_pixelsPerMs`)이 바뀔 때마다 재계산 필요  
   - 슬롯 수 = `ClipWidth / slotWidth` 이며, 줌이 커지면 슬롯 수가 늘어나 더 촘촘한 Filmstrip을 제공
2. 각 슬롯의 중심 시간 계산
3. **이진 탐색**(FindNearestThumbnail)으로 가장 가까운 캐시된 프레임 검색
4. 해당 프레임을 슬롯에 꽉 채워 그림

### 4.3 왜 이진 탐색인가?

썸네일 리스트는 시간순 정렬되어 있으므로 O(log N) 탐색이 가능.
매 프레임 렌더링 시 슬롯 수십 개 × O(log N) = 매우 빠름.

```csharp
// 슬롯 중심 시간에 가장 가까운 썸네일 찾기
private static CachedThumbnail? FindNearestThumbnail(
    List<CachedThumbnail> thumbs, long timeMs)
{
    // 이진 탐색: lo/hi 사이를 좁혀가며 가장 가까운 것 선택
    int lo = 0, hi = thumbs.Count - 1;
    while (lo < hi - 1) { ... }
    return diffLo <= diffHi ? thumbs[lo] : thumbs[hi];
}
```

---

## 5. 메모리 관리

### 5.1 LRU 캐시

```
전체 예산: 256MB / 최대 200개 스트립
키: (파일 경로, 티어)
```

- 캐시 히트 시 LRU 순서 갱신 (LinkedList 뒤로 이동)
- 캐시 미스 시 메모리 예산 확인 → 초과 시 가장 오래된 스트립 제거
- 줌 레벨 변경 시 다른 티어가 요청되면 새 스트립 생성 (기존 티어는 캐시에 유지)
- **같은 파일을 사용하는 여러 클립**은 `(파일 경로, 티어)` 키를 통해 **하나의 스트립을 공유**  
  - 예: 하나의 원본 영상이 타임라인 곳곳에 나뉘어 있어도, 썸네일 디코딩은 파일 단위로 한 번만 수행

### 5.2 메모리 예산 계산 예시

| 시나리오 | 장수 | 크기/장 | 총 메모리 |
|---------|------|--------|----------|
| 10초 클립, Medium | 5장 | 33KB | 0.2MB |
| 17분 클립, Medium | 204장 | 33KB | 6.7MB |
| 1시간 클립, Medium | 720장 | 33KB | 23.8MB |
| 1시간 클립, High | 1800장 | 58KB | 104MB |

### 5.3 타임라인 편집 이벤트별 무효화 규칙

타임라인 편집에 따라 어떤 구간의 썸네일을 무효화(Invaldation) 할지 미리 정의해 두면, 구현과 디버깅이 쉬워진다.

- **클립 트림(양 끝 조정)**  
  - 대상: 해당 클립이 차지하는 시간 구간 `[oldStart, oldEnd]` 와 새로운 구간 `[newStart, newEnd]` 의 합집합  
  - 처리: `InvalidateRange(mediaPath, min(oldStart, newStart), max(oldEnd, newEnd))`
- **클립 이동**  
  - 대상: 원래 위치 `[oldStart, oldEnd]` 와 새 위치 `[newStart, newEnd]`  
  - 처리: 두 구간 모두 `InvalidateRange` 호출 (겹치면 내부에서 중복 제거)
- **클립 삭제**  
  - 대상: 삭제된 클립 구간 전체 `[start, end]`  
  - 처리: `InvalidateRange(mediaPath, start, end)`  
  - 단, 같은 파일을 쓰는 다른 클립이 있으면 해당 구간에서만 무효화
- **속도 변화(향후)**  
  - 대상: 속도가 바뀐 구간 전체 (실제 표시되는 프레임이 달라지므로)  
  - 처리: 해당 구간 `InvalidateRange`

이 규칙은 `ThumbnailStripService.InvalidateRange()` 구현의 기준이 된다.

### 5.4 동시성 & 스레드 모델

```csharp
SemaphoreSlim(2) // FFmpeg 디코더 동시 최대 2개
```

- FFmpeg 디코더는 CPU/메모리를 많이 사용하므로 동시 2개로 제한, 나머지는 큐에서 대기
- 썸네일 생성은 `Task.Run` 등 백그라운드 스레드에서 수행하지만,  
  **`OnThumbnailReady` 콜백에서 UI 갱신(예: `InvalidateVisual()`)은 반드시 Avalonia UI 스레드로 마샬링**해야 한다.
  - 예: `Dispatcher.UIThread.Post(() => clipCanvas.InvalidateVisual())`
- 현재 생성 순서는 **요청 순 FIFO**를 기본으로 하되, 추후 “가시 영역 우선 생성” 최적화를 추가할 수 있다 (아래 8장 참고).

---

## 6. 점진적 표시 (Progressive Loading)

1. `GetOrRequestStrip()` 호출 시 빈 스트립 즉시 반환 + 배경 생성 시작
2. 각 썸네일 생성 완료 시 `OnThumbnailReady` 콜백 → UI 스레드에서 `InvalidateVisual()` 호출
3. 다음 렌더링 사이클에서 새로 추가된 썸네일 포함하여 타일링
4. 사용자는 점진적으로 채워지는 Filmstrip을 볼 수 있음

```
t=0s: [로딩중...로딩중...로딩중...]
t=1s: [thumb0|로딩중...|로딩중...]  ← 0초 프레임 완료
t=2s: [thumb0|thumb2|로딩중...]     ← 2초 프레임 완료
t=3s: [thumb0|thumb2|thumb4]        ← 전체 완료
```

---

## 7. 파일 구조

| 파일 | 역할 |
|------|------|
| `ThumbnailStripService.cs` | 생성 + LRU 캐시 관리 |
| `ClipCanvasPanel.cs: DrawThumbnailStrip()` | 연속 타일링 렌더링 |
| `ClipCanvasPanel.cs: FindNearestThumbnail()` | 이진 탐색 유틸리티 |
| `rust-engine/src/ffi/renderer.rs: generate_video_thumbnail()` | FFmpeg 프레임 디코딩 |
| `RenderService.cs: GenerateThumbnail()` | Rust FFI 래퍼 |

---

## 8. 향후 개선 가능성

1. **적응형 간격**: 줌 레벨과 슬롯 폭을 기반으로 최적 간격 자동 계산
2. **가시 영역 우선 생성**: 현재 보이는 뷰포트 범위의 썸네일을 먼저 생성
3. **디스크 캐시**: 프로젝트 파일과 함께 썸네일 캐시 저장 → 재실행 시 즉시 표시
4. **WebP 압축**: 메모리 대신 디스크 캐시 시 압축 형식 사용

---

## 9. 테스트 시나리오 (리그레션 체크리스트)

1. **짧은 클립 (10초, 1080p)**  
   - Low/Medium/High 티어 각각에서 썸네일 장수와 메모리 사용량이 표(5.2)와 유사한지 확인  
   - 줌 인/아웃/트랙 높이 변경 시 Filmstrip이 빈틈 없이 유지되는지 확인
2. **중간 길이 클립 (5분)**  
   - Medium 티어에서 간격이 `2~5초` 범위로 유지되는지 (Math.Clamp) 확인  
   - 빠르게 스크럽/줌을 반복해도 크래시/메모리 폭증이 없는지 확인
3. **긴 클립 (17분, 버그 재현 케이스)**  
   - Medium 티어에서 간격이 **5초 근처**로 제한되고, 뷰포트 안에 여러 썸네일이 보이는지 확인  
   - 예전처럼 1개 썸네일만 반복되는 현상이 없는지 확인
4. **1시간 클립**  
   - Medium/High 티어에서 썸네일 장수·메모리 사용량이 표(5.2)의 범위를 넘지 않는지 확인  
   - 연속 재생(플레이헤드 이동) 중에도 스크롤/줌에 즉각 반응하는지 확인
5. **타임라인 편집 시나리오**  
   - 클립 트림/이동/삭제 후 해당 구간의 썸네일만 다시 생성되는지 (전체 재생성 아님) 확인  
   - 같은 파일을 쓰는 여러 클립에서, 한 클립 편집이 다른 클립의 Filmstrip 공유를 깨뜨리지 않는지 확인
6. **에러/예외 상황**  
   - 손상된 파일/미지원 코덱/파일 미존재 시, 플레이스홀더 표시 + 로그만 남기고 UI가 안정적으로 유지되는지 확인

---

## 10. 프리뷰/렌더링 엔진 선택 전략 (OpenCV vs FFmpeg)

VortexCut의 Filmstrip/프리뷰/최종 렌더링 파이프라인은 FFmpeg 기반 Rust 엔진을 중심으로 설계되어 있지만,  
향후 OpenCV를 도입하거나 하이브리드 구성을 고려할 때 다음과 같은 선택지가 있다.

1. **개발 속도 + 프리뷰 품질 중시 (가장 일반적인 패턴)**  
   - **전략**: OpenCV로 프리뷰(실시간 표시, 간단한 효과), FFmpeg로 최종 렌더링(고품질 인코딩)  
   - 장점:  
     - OpenCV의 빠른 프로토타이핑, 다양한 이미지 처리 기능을 활용해 UI/프리뷰 개발 속도가 빠름  
     - 최종 출력은 FFmpeg가 담당하므로 코덱/컨테이너 호환성이 좋음  
   - Filmstrip 영향:  
     - 썸네일/Filmstrip은 계속 FFmpeg 기반으로 두고, 프리뷰 파이프라인만 OpenCV를 섞는 구성이 자연스럽다.

2. **최고 성능 + 복잡한 효과 체인 중시 (필터 그래프 위주)**  
   - **전략**: FFmpeg `filter_complex` 중심으로 전체 파이프라인 구성 (프리뷰/최종 렌더 모두 FFmpeg)  
   - 장점:  
     - 필터 체인이 한 번 구성되면, GPU/멀티스레딩 최적화를 FFmpeg에 위임 가능  
     - 복잡한 컬러그레이딩/합성/트랜지션을 순수 필터 그래프로 표현 가능  
   - Filmstrip 영향:  
     - 현재 설계(FFmpeg 썸네일)를 그대로 확장하면 되며, OpenCV 의존성은 최소화/제거 가능.

3. **OpenCV 기능(DNN, Tracking 등)이 필수인 경우 (하이브리드)**  
   - **전략**: OpenCV + FFmpeg 하이브리드  
     - FFmpeg: 디코딩/인코딩/타임라인 합성, Filmstrip 썸네일 생성  
     - OpenCV: 오브젝트 트래킹, 딥러닝 기반 분석, 모션 추정 등 고급 비전 기능  
   - 장점:  
     - AI/트래킹 기반 기능을 NLE에 녹여 넣기 쉬움  
   - Filmstrip 영향:  
     - 썸네일 자체는 여전히 FFmpeg 기반으로 유지하되,  
       OpenCV 결과(트래킹 박스, 분석 메타데이터)를 Filmstrip 위 오버레이로 렌더링하는 식의 확장이 가능하다.

4. **구현 단순성 최우선 (의존성 최소화)**  
   - **전략**: FFmpeg만 사용 (OpenCV는 필요 시 유틸리티 수준으로만 사용하거나 아예 도입하지 않음)  
   - 장점:  
     - 배포 용이, 바이너리 크기/의존성 감소  
     - Rust + FFmpeg 조합만 관리하면 되므로 빌드/디버깅이 단순  
   - Filmstrip 영향:  
     - 현재 문서에서 설계한 **FFmpeg 기반 썸네일/Filmstrip 시스템**만으로 완결된 구조를 유지할 수 있다.

> **현 단계(Phase 3)**: VortexCut은 Rust+FFmpeg 엔진을 중심으로 설계되어 있으므로,  
> 기본 전략은 **4번(FFmpeg 중심)** 을 따르되, 향후 기능 요구에 따라 1/3번 방향으로 확장 가능성을 열어둔다.

---

## 11. Proxy + Caching 전략과 Visible Range Lazy Loading

타임라인 프리뷰의 **체감 성능**을 높이기 위해, 썸네일 Filmstrip과 별개로 다음 두 축의 최적화를 사용한다.

### 11.1 Proxy + Caching 조합

1. **Python FastMovieMaker 단계 (선행 실험)**  
   - 전략: 기존 Python FastMovieMaker 프로젝트에 먼저 프록시 기능을 넣어 개념을 검증한다.  
   - 구현 아이디어:  
     - MoviePy/FFmpeg를 이용해 원본 고해상도 영상에서 **저해상도 Proxy 클립**(예: 1/2~1/4 스케일, 저비트레이트)을 생성  
     - 타임라인/프리뷰/Filmstrip 모두 Proxy 파일을 우선 사용하고, 최종 렌더에서만 원본을 참조  
   - 목적:  
     - Proxy 워크플로우의 UX/성능 이득을 먼저 Python 환경에서 검증한 뒤, Rust 백엔드로 옮길 때 설계를 재사용.

2. **Rust 백엔드 단계 (VortexCut 본체)**  
   - 전략: Rust+FFmpeg 엔진에 **프록시 자동 생성/관리 로직**을 추가한다.  
   - 주요 포인트:  
     - Media 임포트 시, 백그라운드에서 Proxy 파일(저해상도/저비트레이트)을 생성  
     - 타임라인/프리뷰/Filmstrip/썸네일 생성 시 **가능하면 Proxy를 사용**, 원본은 최종 렌더링/정밀 작업에만 사용  
     - Proxy/원본 매핑 정보를 프로젝트 메타데이터에 저장 (예: `MediaItem`에 `ProxyPath` 추가)  
   - 캐싱과의 조합:  
     - Proxy를 사용하면 단일 프레임 디코딩 비용이 감소하므로, 5장에서 설명한 **Frame/Thumbnail 캐시**와 시너지가 크다.  
     - LRU 캐시의 **메모리 예산은 동일하게 유지**하되, 같은 예산으로 더 많은 시간 범위를 커버할 수 있다.

요약하면, Proxy + Caching 조합은  
- **Python FastMovieMaker에서 먼저 실험 → VortexCut Rust 백엔드에 이식**  
순으로 진행하며, Filmstrip/프리뷰 모두 Proxy 우선 전략을 취한다.

### 11.2 Visible Range Lazy Loading (가시 구간 지연 로딩)

모든 프레임/썸네일을 한꺼번에 준비하는 대신, **현재 화면에 보이는 구간(visible range)** 에 대해서만 우선적으로 디코딩/캐싱하는 전략이다.

1. **Avalonia 쪽 (UI 계층)**  
   - 타임라인 컨트롤(`TimelineCanvas`/`ClipCanvasPanel`)의 스크롤/리사이즈 시점에  
     현재 뷰포트의 시간 범위 `[visibleStartMs, visibleEndMs]` 를 계산한다.  
   - 이 값을 **`VisibleChanged` 스타일의 이벤트** 또는 ViewModel 속성(예: `TimelineViewModel.VisibleRange`)으로 노출한다.

2. **Rust 쪽 (백엔드 FFI)**  
   - UI에서 전달한 `[visibleStartMs, visibleEndMs]` 범위를 기반으로,  
     해당 구간에 필요한 프리뷰 프레임/썸네일만 우선 디코딩한다.  
   - 5장에서 정의한 LRU 캐시/PreRenderService와 결합하여,  
     - **가시 구간 ±α(look-ahead)** 범위만 집중적으로 렌더링  
     - 화면 밖 구간은 나중에 요청이 올 때까지 지연(Lazy) 생성

3. **Filmstrip과의 연계**  
   - Filmstrip 슬롯 계산 시, 슬롯의 시간 범위가 `[visibleStartMs, visibleEndMs]` 외부에 크게 벗어나는 경우  
     - 썸네일 요청 우선순위를 낮추거나  
     - 간단한 플레이스홀더만 보여주고, 실제 프레임은 나중에 채워 넣는다.  
   - 이로 인해, 긴 타임라인에서도 **현 시점 근처만 “선명하고 즉각적으로” 보이고, 나머지는 점진적으로 채워지는 UX**를 구현할 수 있다.

이 섹션(11장)은 Phase 3D “타임라인 렌더링 최적화” 설계 시,  
Proxy 워크플로우와 Visible Range 기반 Lazy Loading을 통합하기 위한 상위 전략을 정의한다.

---

*최종 업데이트: 2026-02-12*
