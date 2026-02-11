# VortexCut

> **Rust** 렌더링 엔진 + **C# Avalonia** UI 기반 크로스 플랫폼 영상 편집 프로그램

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)]()
[![Rust](https://img.shields.io/badge/Rust-2021-orange)]()
[![.NET](https://img.shields.io/badge/.NET-8.0-blue)]()
[![License](https://img.shields.io/badge/license-MIT-green)]()

## 프로젝트 개요

VortexCut은 고성능 **Rust 렌더링 엔진**(ffmpeg-next)과 현대적인 **C# Avalonia UI**를 결합한 영상 편집 소프트웨어입니다.

### 주요 특징

- 🚀 **고성능 렌더링**: Rust + FFmpeg 기반 네이티브 렌더링 엔진
- 🎨 **현대적인 UI**: C# Avalonia로 구현된 크로스 플랫폼 UI
- 📝 **타임라인 편집**: 멀티 트랙 비디오/오디오 편집
- 📜 **자막 지원**: SRT/ASS 자막, Whisper 자동 자막 생성
- 🎵 **오디오 처리**: 볼륨 조정, 페이드, TTS 통합
- ✨ **고급 효과**: 트랜지션, 필터, 색보정

### 기술 스택

| 구성 요소 | 기술 |
|----------|------|
| 렌더링 엔진 | Rust 2021, ffmpeg-next 8.0 |
| UI 프레임워크 | C# .NET 8, Avalonia UI 11 |
| 연동 방식 | FFI (P/Invoke) |
| 타겟 플랫폼 | Windows, macOS |

## 프로젝트 구조

```
VortexCut/
├── rust-engine/              # Rust 렌더링 엔진 (cdylib)
│   ├── src/
│   │   ├── ffi/              # FFI 인터페이스
│   │   ├── ffmpeg/           # FFmpeg 래퍼
│   │   ├── timeline/         # 타임라인 엔진
│   │   ├── rendering/        # 렌더링 파이프라인
│   │   └── subtitle/         # 자막 처리
│   └── Cargo.toml
├── VortexCut.Core/           # C# 공통 모델
├── VortexCut.Interop/        # Rust-C# P/Invoke 레이어
├── VortexCut.UI/             # Avalonia UI
├── VortexCut.Tests/          # C# 단위 테스트
└── docs/
    ├── TECHSPEC.md           # 기술 명세서
    └── ARCHITECTURE.md       # 아키텍처 문서
```

## 빌드 방법

### 필수 요구사항

- **Rust** 1.70 이상 ([설치 링크](https://rustup.rs/))
- **.NET SDK** 8.0 이상 ([설치 링크](https://dotnet.microsoft.com/download))
- **FFmpeg** 개발 라이브러리 (나중에 필요)

### 1. Rust 엔진 빌드

```bash
cd rust-engine
cargo build --release
```

생성된 DLL:
- Windows: `target/release/rust_engine.dll`
- macOS: `target/release/librust_engine.dylib`

### 2. C# 프로젝트 빌드

```bash
# 솔루션 빌드
dotnet build VortexCut.sln -c Release

# 테스트 실행
dotnet test VortexCut.Tests
```

### 3. 통합 빌드 (권장)

**Windows (PowerShell)**:
```powershell
.\scripts\build-all.ps1
```

**macOS/Linux (Bash)**:
```bash
chmod +x scripts/build-all.sh
./scripts/build-all.sh
```

## 개발 시작하기

### 1. 저장소 클론

```bash
git clone https://github.com/your-username/VortexCut.git
cd VortexCut
```

### 2. 의존성 설치

```bash
# Rust 의존성
cd rust-engine
cargo fetch

# .NET 의존성
cd ..
dotnet restore
```

### 3. 개발 환경 설정

**Visual Studio Code** (권장):
- Rust Analyzer 확장 설치
- C# Dev Kit 확장 설치

**Visual Studio 2022**:
- Rust 플러그인 설치 (선택사항)
- .NET 8 워크로드 설치

### 4. FFI 테스트 실행

```bash
# Rust 빌드
cd rust-engine
cargo build --release

# DLL 복사
cp target/release/rust_engine.dll ../VortexCut.Tests/bin/Debug/net8.0/

# 테스트 실행
cd ..
dotnet test VortexCut.Tests
```

## 현재 상태

### ✅ Phase 2E 완료 (2026-02-10) - 전문가급 타임라인
- [x] **Rust FFI 렌더링 엔진** - FFmpeg 통합, Timeline 엔진, 프레임 렌더링
- [x] **C# Avalonia UI** - Kdenlive 스타일 4-패널 레이아웃
- [x] **타임라인 편집 22가지 기능**
  - DaVinci Resolve 스타일 UI (그라데이션, 그림자, 60FPS 애니메이션)
  - After Effects 키프레임 시스템 (6가지 보간, F9 단축키, J/K 네비게이션)
  - Kdenlive 편집 도구 (Snap with time delta, In/Out points, track mute/solo)
  - SMPTE 타임코드, Playhead auto-scroll, 링크 클립, 색상 라벨
  - 15+ 키보드 단축키, 성능 모니터 (FPS 카운터)

### 🚧 진행 중
- [ ] 메모리 관리 개선 (RenderService 프레임 크기 검증, finalizer)
- [ ] 이벤트 누수 수정 (TimelineCanvas 구독 해제)
- [ ] 테스트 환경 의존성 제거 (Mock 기반 테스트)

### 📋 계획
- [ ] 자막 편집 기능
- [ ] 고급 효과 시스템 (필터, 블러, 색보정)
- [ ] 내보내기 최적화 (병렬 렌더링)

## 문서

- [TECHSPEC.md](docs/TECHSPEC.md) - 기술 명세서
- [CLAUDE.md](CLAUDE.md) - Claude 사용 가이드

## 기여 방법

기여를 환영합니다! 다음 단계를 따라주세요:

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 라이선스

이 프로젝트는 MIT 라이선스 하에 배포됩니다.

## 제작자

- **Claude Sonnet 4.5** - AI 개발 어시스턴트
- **사용자** - 프로젝트 설계 및 디렉션

## 참고 자료

- [Rust FFI Omnibus](http://jakegoulding.com/rust-ffi-omnibus/)
- [ffmpeg-next GitHub](https://github.com/zmwangx/rust-ffmpeg)
- [Avalonia UI Docs](https://docs.avaloniaui.net/)
- [Microsoft P/Invoke Docs](https://docs.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke)

---

**마지막 업데이트**: 2026-02-10
