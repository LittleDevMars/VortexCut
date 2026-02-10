# Rust 엔진 빌드 스크립트 (Windows PowerShell)

$ErrorActionPreference = "Stop"

Write-Host "=== VortexCut Rust 엔진 빌드 (Windows) ===" -ForegroundColor Green

# Rust 엔진 디렉토리로 이동
Set-Location rust-engine

Write-Host "`n1. Rust 프로젝트 빌드 중..." -ForegroundColor Cyan
cargo build --release

if ($LASTEXITCODE -ne 0) {
    Write-Host "Rust 빌드 실패!" -ForegroundColor Red
    exit 1
}

Write-Host "`n2. DLL 복사 중..." -ForegroundColor Cyan

# 출력 디렉토리 생성
$uiRuntimeDir = "..\VortexCut.UI\runtimes\win-x64\native"
$testBinDir = "..\VortexCut.Tests\bin\Debug\net8.0"

New-Item -ItemType Directory -Force -Path $uiRuntimeDir | Out-Null
New-Item -ItemType Directory -Force -Path $testBinDir | Out-Null

# DLL 복사
Copy-Item "target\release\rust_engine.dll" $uiRuntimeDir -Force
Write-Host "  - VortexCut.UI로 복사 완료" -ForegroundColor Gray

Copy-Item "target\release\rust_engine.dll" $testBinDir -Force
Write-Host "  - VortexCut.Tests로 복사 완료" -ForegroundColor Gray

Write-Host "`n✓ Rust 엔진 빌드 완료!" -ForegroundColor Green

# 원래 디렉토리로 복귀
Set-Location ..
