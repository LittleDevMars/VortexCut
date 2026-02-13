// ThumbnailSession FFI - 세션 기반 썸네일 생성
// 디코더를 한 번 열고, 여러 timestamp에 대해 순차/랜덤 디코딩
// 매번 Decoder를 새로 생성하던 기존 방식 대비:
//   - 파일 Open/Close 1회 (기존: N회)
//   - 스케일러가 직접 썸네일 해상도로 출력 (기존: 960x540 → nearest-neighbor 다운스케일)

use crate::ffmpeg::decoder::{Decoder, DecodeResult};
use crate::ffi::types::ErrorCode;
use std::ffi::{c_char, CStr};
use std::path::PathBuf;

/// 썸네일 세션 (Decoder를 유지하며 여러 프레임 생성)
pub struct ThumbnailSession {
    decoder: Decoder,
}

/// 썸네일 세션 생성
/// - file_path: UTF-8 인코딩된 파일 경로
/// - thumb_width/height: 썸네일 출력 해상도 (스케일러가 이 크기로 직접 디코딩)
/// - out_session: 세션 핸들 (caller가 소유, thumbnail_session_destroy로 해제)
/// - out_duration_ms: 비디오 총 길이 (ms)
/// - out_fps: 비디오 FPS
#[no_mangle]
pub extern "C" fn thumbnail_session_create(
    file_path: *const c_char,
    thumb_width: u32,
    thumb_height: u32,
    out_session: *mut *mut ThumbnailSession,
    out_duration_ms: *mut i64,
    out_fps: *mut f64,
) -> i32 {
    if file_path.is_null() || out_session.is_null()
        || out_duration_ms.is_null() || out_fps.is_null()
    {
        return ErrorCode::NullPointer as i32;
    }

    unsafe {
        let c_str = CStr::from_ptr(file_path);
        let file_path_str = match c_str.to_str() {
            Ok(s) => s,
            Err(_) => return ErrorCode::InvalidParam as i32,
        };

        let path = PathBuf::from(file_path_str);

        // 썸네일 해상도로 직접 디코딩 (960x540 거치지 않음)
        let mut decoder = match Decoder::open_with_resolution(&path, thumb_width, thumb_height) {
            Ok(d) => d,
            Err(e) => {
                eprintln!("thumbnail_session_create: Failed to open decoder: {}", e);
                return ErrorCode::Ffmpeg as i32;
            }
        };

        // Forward decode 임계값 10초: GOP 내 불필요한 seek 방지
        // 썸네일은 시간순 생성 → 대부분 forward decode로 처리
        decoder.set_forward_threshold(10_000);

        *out_duration_ms = decoder.duration_ms();
        *out_fps = decoder.fps();

        let session = Box::new(ThumbnailSession {
            decoder,
        });

        *out_session = Box::into_raw(session);
    }

    ErrorCode::Success as i32
}

/// 세션에서 특정 timestamp의 썸네일 생성
/// - 디코더가 이미 열려있으므로 파일 Open/Close 오버헤드 없음
/// - 시간순 호출 시 forward decode 활용 (seek 최소화)
/// - out_data: RGBA 바이트 배열 (caller가 renderer_free_frame_data로 해제)
#[no_mangle]
pub extern "C" fn thumbnail_session_generate(
    session: *mut ThumbnailSession,
    timestamp_ms: i64,
    out_width: *mut u32,
    out_height: *mut u32,
    out_data: *mut *mut u8,
    out_data_size: *mut usize,
) -> i32 {
    if session.is_null() || out_width.is_null() || out_height.is_null()
        || out_data.is_null() || out_data_size.is_null()
    {
        return ErrorCode::NullPointer as i32;
    }

    unsafe {
        let session = &mut *session;

        // decode_frame → 스케일러가 이미 thumb 해상도이므로 추가 다운스케일 불필요
        let frame = match session.decoder.decode_frame(timestamp_ms) {
            Ok(DecodeResult::Frame(f)) => f,
            Ok(DecodeResult::EndOfStream(f)) => f,
            Ok(DecodeResult::FrameSkipped) => {
                // seek 실패 → 빈 프레임 반환 (C# 측에서 스킵 처리)
                *out_width = 0;
                *out_height = 0;
                *out_data = std::ptr::null_mut();
                *out_data_size = 0;
                return ErrorCode::Success as i32;
            }
            Ok(DecodeResult::EndOfStreamEmpty) => {
                *out_width = 0;
                *out_height = 0;
                *out_data = std::ptr::null_mut();
                *out_data_size = 0;
                return ErrorCode::Success as i32;
            }
            Err(e) => {
                eprintln!("thumbnail_session_generate: decode failed at {}ms: {}", timestamp_ms, e);
                *out_width = 0;
                *out_height = 0;
                *out_data = std::ptr::null_mut();
                *out_data_size = 0;
                return ErrorCode::Ffmpeg as i32;
            }
        };

        *out_width = frame.width;
        *out_height = frame.height;
        *out_data_size = frame.data.len();

        // 데이터를 힙에 할당하고 포인터 반환
        let data_box = frame.data.into_boxed_slice();
        *out_data = Box::into_raw(data_box) as *mut u8;
    }

    ErrorCode::Success as i32
}

/// 썸네일 세션 파괴
#[no_mangle]
pub extern "C" fn thumbnail_session_destroy(session: *mut ThumbnailSession) -> i32 {
    if session.is_null() {
        return ErrorCode::NullPointer as i32;
    }

    unsafe {
        let _ = Box::from_raw(session);
    }

    ErrorCode::Success as i32
}
