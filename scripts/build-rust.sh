#!/bin/bash
# Rust 엔진 빌드 스크립트 (macOS/Linux)

set -e

echo "=== VortexCut Rust 엔진 빌드 (macOS/Linux) ==="

# Rust 엔진 디렉토리로 이동
cd rust-engine

echo ""
echo "1. Rust 프로젝트 빌드 중..."
cargo build --release

echo ""
echo "2. 라이브러리 복사 중..."

# 출력 디렉토리 생성
UI_RUNTIME_DIR="../VortexCut.UI/runtimes/osx-x64/native"
TEST_BIN_DIR="../VortexCut.Tests/bin/Debug/net8.0"

mkdir -p "$UI_RUNTIME_DIR"
mkdir -p "$TEST_BIN_DIR"

# 플랫폼 감지 및 라이브러리 복사
if [[ "$OSTYPE" == "darwin"* ]]; then
    # macOS
    cp target/release/librust_engine.dylib "$UI_RUNTIME_DIR/"
    cp target/release/librust_engine.dylib "$TEST_BIN_DIR/"
    echo "  - VortexCut.UI로 복사 완료 (dylib)"
    echo "  - VortexCut.Tests로 복사 완료 (dylib)"
elif [[ "$OSTYPE" == "linux-gnu"* ]]; then
    # Linux
    UI_RUNTIME_DIR="../VortexCut.UI/runtimes/linux-x64/native"
    mkdir -p "$UI_RUNTIME_DIR"
    cp target/release/librust_engine.so "$UI_RUNTIME_DIR/"
    cp target/release/librust_engine.so "$TEST_BIN_DIR/"
    echo "  - VortexCut.UI로 복사 완료 (so)"
    echo "  - VortexCut.Tests로 복사 완료 (so)"
fi

echo ""
echo "✓ Rust 엔진 빌드 완료!"

# 원래 디렉토리로 복귀
cd ..
