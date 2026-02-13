# 썸네일 엔진 리팩토링 계획서

> 참고 자료:
> - [Swifter: Improved Online Video Scrubbing (CHI 2013, Matejka et al.)](https://dl.acm.org/doi/10.1145/2470654.2466149)
> - [FFmpeg Mastery: Extracting Perfect Thumbnails (Sergiu Savva)](https://medium.com/@sergiu.savva/ffmpeg-mastery-extracting-perfect-thumbnails-from-videos-339a4229bb32)
> - [3.8x faster thumbnails with FFmpeg seeking (Sebastian Aigner)](https://sebi.io/posts/2024-12-21-faster-thumbnail-generation-with-ffmpeg-seeking/)

---

## 1. 현재 문제 진단

### 1.1 AS-IS 아키텍처 (세션 도입 전)

```
썸네일 100장 요청:
  [Open file → Init decoder → Init scaler(960x540) → Seek(0ms) → Decode → Scale(120x68) → Close]
  [Open file → Init decoder → Init scaler(960x540) → Seek(700ms) → Decode → Scale(120x68) → Close]
  [Open file → Init decoder → Init scaler(960x540) → Seek(1400ms) → Decode → Scale(120x68) → Close]
  ... × 100회
```

**문제점:**
1. 파일 Open/Close 100회 (각 50-200ms 오버헤드)
2. 960×540으로 디코딩 후 120×68로 재축소 (CPU/메모리 낭비)
3. 매번 키프레임 seek → 같은 GOP 내 반복 디코딩 (아래 상세)

### 1.2 AS-IS 아키텍처 (세션 도입 후, 현재)

```
세션 기반:
  [Open file → Init decoder → Init scaler(120x68)]
  [Seek(0ms)    → Decode forward to PTS ≈ 0ms    → Return frame]
  [Seek(700ms)  → Decode forward to PTS ≈ 700ms  → Return frame]
  [Seek(1400ms) → Decode forward to PTS ≈ 1400ms → Return frame]
  ... × 100회 (같은 디코더 재사용)
  [Close]
```

**해결된 문제:** 파일 Open 1회, 스케일러가 직접 썸네일 크기
**남은 문제:** 매 썸네일마다 Seek 발생 (아래 상세)

### 1.3 핵심 병목: 불필요한 반복 Seek

현재 `decode_frame()`의 순차 판정 기준:

```rust
let is_sequential = timestamp_ms >= self.last_timestamp_ms
    && timestamp_ms <= self.last_timestamp_ms + frame_duration_ms * 2;
// 30fps 기준 frame_duration_ms * 2 ≈ 66ms
```

썸네일 간격은 500~3500ms → **항상 `is_sequential = false`** → 매번 Seek 실행

#### GOP 내 반복 Seek 문제

```
예: H.264 영상, GOP = 2초 (키프레임: 0ms, 2000ms, 4000ms...)
썸네일 간격: 700ms

Thumbnail @   0ms: Seek → 키프레임 0ms    → 디코딩 ~0ms 분량   ← 정상
Thumbnail @ 700ms: Seek → 키프레임 0ms    → 디코딩 ~700ms 분량 ← 0~700ms 중복 디코딩!
Thumbnail @1400ms: Seek → 키프레임 0ms    → 디코딩 ~1400ms 분량 ← 0~1400ms 중복 디코딩!
Thumbnail @2100ms: Seek → 키프레임 2000ms → 디코딩 ~100ms 분량 ← OK (새 GOP)
Thumbnail @2800ms: Seek → 키프레임 2000ms → 디코딩 ~800ms 분량 ← 2000~2800ms 중복!
```

**Forward Decode로 개선 시:**
```
Thumbnail @   0ms: Seek → 0ms     → Return       (0ms 디코딩)
Thumbnail @ 700ms: Forward decode  → Return       (700ms 분량만 디코딩)
Thumbnail @1400ms: Forward decode  → Return       (700ms 분량만 디코딩)
Thumbnail @2100ms: Forward decode  → Return       (700ms 분량만 디코딩)
```

**절약량:** GOP=2초, interval=700ms 기준, 약 50% 디코딩량 감소

---

## 2. 참고 자료 핵심 요약

### 2.1 Swifter (CHI 2013) — 프리페치 썸네일 그리드

- **핵심:** 스크러빙 시 단일 프레임 대신 **프리캐시된 썸네일 그리드**를 표시
- **성능:** YouTube/Netflix/Swift 대비 **씬 탐색 속도 48% 향상**
- **그리드:** 8×8 그리드가 속도/만족도 모두 높은 최적점
- **적용 포인트:**
  - 타임라인 필름스트립 = "항상 표시되는 Swifter 그리드"와 동일 개념
  - 뷰포트 바깥 구간까지 **프리페치** → 스크롤 시 즉시 표시
  - 줌 레벨별 서로 다른 밀도(티어)로 캐싱

### 2.2 FFmpeg Double-SS 기법 — 정확한 프레임 추출

- **Input Seek (`-ss` before `-i`):** 키프레임 기반 고속 seek (부정확)
- **Output Seek (`-ss` after `-i`):** 정확하지만 전체 디코딩 필요 (느림)
- **Double-SS (두 번 seek):**
  ```
  ffmpeg -ss 55.0 -i input.mp4 -ss 0.2 -frames:v 1 output.jpg
  ```
  1단계: 키프레임까지 고속 점프 (55.0초)
  2단계: 거기서 정밀 forward decode (0.2초)
  → **빠르면서 정확한 프레임** 추출

- **우리 Rust 코드에서의 대응:**
  ```rust
  // 현재 구현이 이미 Double-SS와 동일한 원리:
  self.input_ctx.seek(timestamp, ..timestamp)  // ← Input Seek (키프레임으로 점프)
  // → 패킷 읽으며 PTS 확인 (forward decode) // ← Output Seek에 해당
  ```
  **다만**, 매 썸네일마다 seek을 새로 하는 것이 문제

### 2.3 fps 필터 vs Loop+Seek — 3.8배 속도차

| 방식 | 4분 영상 처리 시간 | 원리 |
|------|-------------------|------|
| `fps=1` 필터 | 10.34초 | **모든 프레임** 디코딩 후 필터링 |
| Loop+Seek | 2.68초 | 필요한 위치만 seek → 1장 추출 |
| **비율** | **3.8배** | |

- **우리 상황:** ffmpeg-next Rust 바인딩으로 직접 디코딩하므로 CLI `fps` 필터 미사용
- **동일 원리 적용:** "모든 프레임 순차 디코딩" 대신 "필요한 시점만 seek+decode"
- **BUT:** 같은 GOP 내에서는 forward decode가 seek보다 빠름 (위 1.3 참조)
- **결론: Hybrid 전략** — 가까우면 forward decode, 멀면 seek

### 2.4 Gemini 조언 — 병렬 추출

- 영상을 4개 구간으로 나눠 **4 스레드 병렬 추출**
- **현재:** `SemaphoreSlim(2)` → 동시 2개 세션
- **개선:** 같은 파일에 대해 구간 분할 병렬화는 복잡도 대비 효과 낮음
  (파일별 1세션 유지가 더 안정적, I/O 병목은 디코딩이 아닌 메모리 할당)
- **대안:** 다른 파일의 세션은 이미 병렬 동작 (Semaphore 2개)

---

## 3. TO-BE 아키텍처

### 3.1 개선된 흐름

```
ThumbnailStripService                    Rust Decoder (Session)
      │                                        │
      │ Create(file, 120, 68)                   │
      │ ──────────────────────────────────────→ │ open_with_resolution(120x68)
      │                                        │ scaler: YUV→RGBA @ 120x68
      │                                        │
      │ Generate(0ms)                           │
      │ ──────────────────────────────────────→ │ Seek(0ms) → decode to PTS≈0
      │ ←────── RGBA frame ───────────────────  │
      │                                        │
      │ Generate(700ms)                         │
      │ ──────────────────────────────────────→ │ ★ Forward decode (no seek!)
      │ ←────── RGBA frame ───────────────────  │   → decode to PTS≈700
      │                                        │
      │ Generate(1400ms)                        │
      │ ──────────────────────────────────────→ │ ★ Forward decode (no seek!)
      │ ←────── RGBA frame ───────────────────  │   → decode to PTS≈1400
      │                                        │
      │   ... (시간순 반복) ...                   │
      │                                        │
      │ Dispose()                               │
      │ ──────────────────────────────────────→ │ Drop decoder
```

### 3.2 Forward Decode 판정 로직

```rust
// 현재 (프리뷰 재생 전용):
let is_sequential = ts >= last_ts && ts <= last_ts + frame_duration * 2;

// 개선: 설정 가능한 threshold
let is_forward = ts >= last_ts && ts <= last_ts + self.forward_threshold_ms;

// 핵심 변경: forward일 때도 PTS 확인 수행
let target_info = if is_forward && (ts - last_ts) > frame_duration * 2 {
    // "확장 순차" 구간: seek 안 함, but PTS는 확인
    Some((target_pts, tolerance_pts))
} else if is_forward {
    // "즉시 순차" (한 프레임 차이): 기존 동작 유지
    None
} else {
    // 비순차: seek + PTS 확인
    Some((target_pts, tolerance_pts))
};
```

### 3.3 Threshold 설정

| 사용 맥락 | `forward_threshold_ms` | 이유 |
|-----------|----------------------|------|
| 프리뷰 재생 (기본값) | `frame_duration * 2` (~66ms @30fps) | 정확한 순차 재생 |
| 썸네일 세션 | `10000ms` (10초) | 대부분 GOP < 10초, seek 회피 |

10초 이상 차이나면 seek 하는 것이 효율적 (forward decode로 10초분 패킷 소비 불필요)

---

## 4. 구현 계획

### Phase 1: Forward Decode 최적화 (Rust)

**파일: `rust-engine/src/ffmpeg/decoder.rs`**

1. `Decoder` 구조체에 `forward_threshold_ms: i64` 필드 추가
2. `open()`에서 기본값 = `frame_duration * 2`
3. `pub fn set_forward_threshold(&mut self, ms: i64)` 메서드 추가
4. `decode_frame()` 수정:
   - `is_sequential` → `is_forward` (threshold 사용)
   - seek 판정과 PTS 판정 분리
   - forward 구간에서도 PTS 확인 수행

**파일: `rust-engine/src/ffi/thumbnail.rs`**

5. `thumbnail_session_create`에서 `decoder.set_forward_threshold(10_000)` 호출

### Phase 2: Viewport 프리페치 (C#)

**파일: `VortexCut.UI/Services/ThumbnailStripService.cs`**

6. `BuildTimeOrder()`에서 visible range 우선 생성
   - 현재: 0ms부터 순차
   - 개선: visible range 구간 먼저 → 나머지는 주변부터 확장
7. Viewport 양쪽 **여유 구간(margin)** 프리페치
   - 좌우 viewport 폭의 50% 만큼 추가 프리페치
   - 스크롤 시 즉시 표시 가능

### Phase 3: println! 완전 제거 + 벤치마크

8. Rust 전체 `println!` 감사 → 에러만 `eprintln!`
9. 17분 영상으로 Before/After 벤치마크:
   - 썸네일 생성 총 시간
   - Seek 횟수 카운트
   - 메모리 사용량

---

## 5. 기대 효과

| 지표 | AS-IS (세션 前) | AS-IS (세션 後) | TO-BE (Forward) |
|------|--------------|---------------|----------------|
| 파일 Open | N회 | 1회 | 1회 |
| Seek 횟수 | N회 | N회 | ~N/3회 (GOP 내 skip) |
| 디코딩 해상도 | 960×540 | 120×68 | 120×68 |
| GOP 내 중복 디코딩 | 심각 | 심각 | 없음 |
| println! I/O | 3-4줄/장 | 0 | 0 |

### 수치 예측 (17분 영상, Medium 티어 1500ms 간격, ~680장)

- **Seek 감소:** GOP=2초 기준, 680회 → ~230회 (약 66% 감소)
- **디코딩량:** GOP 내 중복 제거로 총 디코딩 프레임 ~40% 감소
- **총 시간:** 세션 도입으로 이미 3-5배 개선 + Forward로 추가 30-50% 개선

---

## 6. 향후 확장 (별도 스프린트)

### 6.1 Scene-Aware 썸네일 (Swifter 영감)
- 장면 전환점(scene change)에 우선 썸네일 배치
- FFmpeg `select='gt(scene,0.3)'` 필터 로직을 Rust에서 구현
- 눈밭처럼 변화 적은 구간에서도 의미 있는 프레임 선택

### 6.2 타일 스프라이트 (WebVTT 스타일)
- 개별 RGBA 버퍼 대신 하나의 큰 스프라이트 시트로 합성
- GPU 텍스처 1장으로 렌더링 → DrawImage 호출 수 감소
- 메모리 단편화 방지

### 6.3 적응형 밀도
- 장면 변화가 많은 구간: 간격 좁게 (500ms)
- 정적 구간: 간격 넓게 (3000ms)
- 콘텐츠 기반 자동 조절
