# VortexCut 기술 명세서 (Technical Specification)

> **작성일**: 2026-02-10
> **버전**: 0.1.0
> **상태**: 초기 개발 단계

## 1. 개요

VortexCut은 **Rust 렌더링 엔진**(rusty_ffmpeg)과 **C# Avalonia UI**를 결합한 크로스 플랫폼 영상 편집 프로그램입니다.

### 1.1 목표
- 고성능 영상 렌더링 (Rust + FFmpeg)
- 현대적이고 직관적인 UI (C# Avalonia)
- Windows 및 macOS 지원

### 1.2 기술 스택
| 구성 요소 | 기술 |
|----------|------|
| 렌더링 엔진 | Rust 2021, rusty_ffmpeg 0.13 |
| UI 프레임워크 | C# .NET 8, Avalonia UI 11 |
| 연동 방식 | FFI (Foreign Function Interface) via P/Invoke |
| 빌드 시스템 | Cargo (Rust), MSBuild/.NET CLI (C#) |

## 2. 아키텍처

### 2.1 전체 구조

```
┌─────────────────────────────────────────────┐
│         VortexCut.UI (C# Avalonia)         │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐   │
│  │ViewModels│ │  Views   │ │ Controls │   │
│  └────┬─────┘ └──────────┘ └──────────┘   │
│       │                                     │
│  ┌────▼──────────────────────────┐         │
│  │  VortexCut.Interop (P/Invoke) │         │
│  └────┬──────────────────────────┘         │
└───────┼─────────────────────────────────────┘
        │ FFI Boundary (C ABI)
┌───────▼─────────────────────────────────────┐
│      Rust Engine (rust_engine.dll)         │
│  ┌───────┐ ┌─────────┐ ┌──────────┐       │
│  │  FFI  │ │Timeline │ │ Renderer │       │
│  └───┬───┘ └────┬────┘ └────┬─────┘       │
│      │          │            │              │
│  ┌───▼──────────▼────────────▼────┐        │
│  │     FFmpeg (rusty_ffmpeg)      │        │
│  └────────────────────────────────┘        │
└─────────────────────────────────────────────┘
```

### 2.2 프로젝트 구조

```
VortexCut/
├── rust-engine/              # Rust 렌더링 엔진
│   ├── src/
│   │   ├── ffi/              # FFI 인터페이스
│   │   ├── ffmpeg/           # FFmpeg 래퍼
│   │   ├── timeline/         # 타임라인 엔진
│   │   ├── rendering/        # 렌더링 파이프라인
│   │   └── subtitle/         # 자막 처리
│   └── Cargo.toml
├── VortexCut.Core/           # C# 공통 모델
├── VortexCut.Interop/        # Rust-C# P/Invoke
├── VortexCut.UI/             # Avalonia UI
└── VortexCut.Tests/          # C# 단위 테스트
```

## 3. FFI (Foreign Function Interface) 설계

### 3.1 핵심 원칙

1. **Opaque Pointer 패턴**: Rust 객체를 `*mut c_void` 포인터로 전달
2. **C ABI 사용**: `extern "C"` 로 C 호출 규약 준수
3. **에러 코드 반환**: `i32` 에러 코드 + `out` 파라미터 패턴
4. **명시적 메모리 관리**: Rust가 할당, C#이 해제 함수 호출

### 3.2 FFI 타입 정의

**Rust 측** ([rust-engine/src/ffi/types.rs](rust-engine/src/ffi/types.rs)):
```rust
#[repr(C)]
pub struct CClip {
    pub id: u64,
    pub start_time_ms: i64,
    pub duration_ms: i64,
    pub file_path: *const c_char,
}
```

**C# 측** ([VortexCut.Interop/Types/NativeTypes.cs](VortexCut.Interop/Types/NativeTypes.cs)):
```csharp
[StructLayout(LayoutKind.Sequential)]
public struct NativeClip {
    public ulong Id;
    public long StartTimeMs;
    public long DurationMs;
    public IntPtr FilePath;  // UTF-8 string
}
```

### 3.3 메모리 관리

| 타입 | 할당 | 해제 | 방법 |
|------|------|------|------|
| Timeline/Clip 객체 | Rust | C# 호출 | `timeline_destroy(ptr)` FFI |
| 문자열 | Rust | C# 호출 | `string_free(ptr)` FFI |
| 프레임 데이터 | Rust | C# 호출 | `frame_free(ptr)` FFI |

**C# SafeHandle 패턴**:
```csharp
public class TimelineHandle : SafeHandle {
    protected override bool ReleaseHandle() {
        return NativeMethods.timeline_destroy(handle) == 0;
    }
}
```

### 3.4 에러 핸들링

**Rust → C# 에러 전파**:
- Rust: `Result<T>` → i32 에러 코드 변환
- C#: 에러 코드 확인 → `RustException` 발생

에러 코드:
- `0`: SUCCESS
- `1`: NULL_PTR
- `2`: INVALID_PARAM
- `3`: FFMPEG
- `4`: IO
- `99`: UNKNOWN

## 4. 주요 기능

### 4.1 타임라인 기반 편집

**기능:**
- 멀티 트랙 비디오/오디오 편집
- 드래그 앤 드롭으로 클립 배치
- 클립 트림 (시작/끝 조정)
- 클립 이동 및 순서 변경

**구현:**
- Rust: [rust-engine/src/timeline/timeline.rs](rust-engine/src/timeline/timeline.rs)
- C#: [VortexCut.UI/Controls/TimelineControl.axaml](VortexCut.UI/Controls/TimelineControl.axaml)

### 4.2 자막 편집

**기능:**
- SRT/ASS 자막 파일 임포트/내보내기
- 자막 수동 추가/편집
- Whisper 음성 인식 자동 자막 생성

**구현:**
- Rust: [rust-engine/src/subtitle/](rust-engine/src/subtitle/)
- C#: [VortexCut.UI/Views/SubtitleView.axaml](VortexCut.UI/Views/SubtitleView.axaml)

### 4.3 오디오 처리

**기능:**
- 오디오 볼륨 조정
- 페이드 인/아웃
- TTS (Text-to-Speech) 통합 (선택사항)

**구현:**
- Rust: [rust-engine/src/rendering/audio_mixer.rs](rust-engine/src/rendering/audio_mixer.rs)
- C#: [VortexCut.UI/Views/AudioView.axaml](VortexCut.UI/Views/AudioView.axaml)

### 4.4 고급 효과

**기능:**
- 트랜지션 (페이드, 디졸브 등)
- 필터 (색보정, 블러 등)
- 색보정 (밝기, 대비, 채도)

**구현:**
- Rust: [rust-engine/src/timeline/effect.rs](rust-engine/src/timeline/effect.rs)
- C#: [VortexCut.UI/Views/EffectsView.axaml](VortexCut.UI/Views/EffectsView.axaml)

## 5. 데이터 모델

### 5.1 Project

```csharp
public class Project {
    public string Name { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double Fps { get; set; }
    public Timeline Timeline { get; set; }
    public List<MediaFile> MediaFiles { get; set; }
}
```

### 5.2 Timeline

```csharp
public class Timeline {
    public List<Track> VideoTracks { get; set; }
    public List<Track> AudioTracks { get; set; }
    public List<SubtitleTrack> SubtitleTracks { get; set; }
}
```

### 5.3 Clip

```csharp
public class Clip {
    public Guid Id { get; set; }
    public Guid MediaFileId { get; set; }
    public long StartTimeMs { get; set; }
    public long DurationMs { get; set; }
    public long TrimStartMs { get; set; }
    public long TrimEndMs { get; set; }
    public Transition? Transition { get; set; }
    public List<Effect> Effects { get; set; }
}
```

## 6. 렌더링 파이프라인

### 6.1 프리뷰 렌더링

```
┌──────────┐     ┌────────────┐     ┌────────────┐
│ Timeline │ ──> │  Renderer  │ ──> │ WriteableBitmap │
└──────────┘     │ (Rust FFI) │     │   (C# UI)   │
                 └────────────┘     └─────────────┘
                       │
                  ┌────▼────┐
                  │ FFmpeg  │
                  │ Decoder │
                  └─────────┘
```

**성능 최적화:**
- FrameCache (LRU 캐싱)
- Background Rendering (미리 렌더링)
- Proxy Files (저해상도 프록시)

### 6.2 최종 렌더링 (내보내기)

```
Timeline → Renderer → Compositor → Encoder → Output File
              ↓           ↓            ↓
          FFmpeg     AudioMixer    H.264/H.265
```

**지원 포맷:**
- 비디오: H.264, H.265, VP9
- 오디오: AAC, Opus
- 컨테이너: MP4, MKV, WebM

## 7. 빌드 시스템

### 7.1 Rust 빌드

```bash
cd rust-engine
cargo build --release
```

출력: `target/release/rust_engine.dll` (Windows) 또는 `librust_engine.dylib` (macOS)

### 7.2 C# 빌드

```bash
dotnet build VortexCut.sln -c Release
```

### 7.3 통합 빌드

**Windows** ([scripts/build-rust.ps1](scripts/build-rust.ps1)):
```powershell
cargo build --release
Copy-Item target\release\rust_engine.dll VortexCut.UI\runtimes\win-x64\native\
dotnet build VortexCut.sln -c Release
```

**macOS** ([scripts/build-rust.sh](scripts/build-rust.sh)):
```bash
cargo build --release
cp target/release/librust_engine.dylib VortexCut.UI/runtimes/osx-x64/native/
dotnet build VortexCut.sln -c Release
```

## 8. 개발 로드맵

### Phase 1: 핵심 인프라 (4-6주)

- [x] 프로젝트 구조 생성
- [ ] FFI 기본 구조 (types.rs, timeline.rs)
- [ ] Timeline 엔진 기본 구현
- [ ] FFmpeg Decoder
- [ ] C# P/Invoke 래퍼
- [ ] 간단한 Avalonia UI
- [ ] 단일 프레임 렌더링 테스트

### Phase 2: 편집 기능 확장 (6-8주)

- [ ] 멀티 트랙 지원
- [ ] Compositor (레이어 합성)
- [ ] AudioMixer (오디오 믹싱)
- [ ] 기본 트랜지션
- [ ] FrameCache (성능 최적화)
- [ ] 타임라인 UI (드래그 앤 드롭)
- [ ] 실시간 프리뷰
- [ ] Undo/Redo
- [ ] 프로젝트 저장/불러오기

### Phase 3: 고급 기능 (8-10주)

- [ ] Subtitle 파서/렌더러
- [ ] Whisper 통합
- [ ] TTS 통합
- [ ] 고급 효과 (색보정, 필터)
- [ ] Export Pipeline
- [ ] 자막 편집 UI
- [ ] 효과 패널 UI
- [ ] 내보내기 설정 UI

## 9. 테스트 전략

### 9.1 Rust 테스트

```bash
cd rust-engine
cargo test
```

### 9.2 C# 테스트

```bash
dotnet test VortexCut.Tests
```

### 9.3 통합 테스트

1. UI 실행
2. 비디오 파일 열기
3. 타임라인에 클립 추가
4. 프레임 렌더링 확인
5. 프로젝트 저장/불러오기

## 10. 성능 목표

| 항목 | 목표 |
|------|------|
| 프리뷰 재생 | 30fps @ 1080p |
| 메모리 사용 | < 2GB (일반적인 프로젝트) |
| 렌더링 속도 | 실시간 대비 0.5x ~ 2x |
| UI 반응성 | < 100ms (모든 작업) |

## 11. 보안 고려사항

- **메모리 안전성**: Rust의 소유권 시스템 활용
- **Unsafe 코드 최소화**: FFI 경계에만 제한
- **입력 검증**: 모든 사용자 입력 검증 (파일 경로, 파라미터 등)
- **에러 전파**: Panic 대신 Result 사용

## 12. 참고 자료

- [Rust FFI Omnibus](http://jakegoulding.com/rust-ffi-omnibus/)
- [rusty_ffmpeg GitHub](https://github.com/CCExtractor/rusty_ffmpeg)
- [Avalonia UI Docs](https://docs.avaloniaui.net/)
- [Microsoft P/Invoke Docs](https://docs.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke)

---

**마지막 업데이트**: 2026-02-10
**작성자**: Claude Sonnet 4.5
