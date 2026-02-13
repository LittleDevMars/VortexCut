// Renderer FFI - C# 연동

use crate::rendering::Renderer;
use crate::timeline::Timeline;
use crate::ffmpeg::Decoder;
use crate::ffi::types::ErrorCode;
use std::ffi::{c_void, c_char, CStr};
use std::sync::{Arc, Mutex};
use std::path::PathBuf;

/// Renderer 생성 (Mutex로 감싸서 thread-safe 보장)
#[no_mangle]
pub extern "C" fn renderer_create(timeline: *mut c_void, out_renderer: *mut *mut c_void) -> i32 {
    if timeline.is_null() || out_renderer.is_null() {
        return ErrorCode::NullPointer as i32;
    }

    unsafe {
        // Arc::into_raw()는 *const Mutex<Timeline>을 반환함
        let timeline_arc = Arc::from_raw(timeline as *const Mutex<Timeline>);
        let timeline_clone = Arc::clone(&timeline_arc);

        // 원본 Arc의 소유권 유지 (C#이 관리)
        let _ = Arc::into_raw(timeline_arc);

        let renderer = Renderer::new(timeline_clone);
        // CRITICAL: Renderer를 Mutex로 감싸서 동시 접근 방지
        let renderer_mutex = Box::new(Mutex::new(renderer));
        *out_renderer = Box::into_raw(renderer_mutex) as *mut c_void;

        // 생성 완료
    }

    ErrorCode::Success as i32
}

/// Renderer 파괴
#[no_mangle]
pub extern "C" fn renderer_destroy(renderer: *mut c_void) -> i32 {
    if renderer.is_null() {
        return ErrorCode::NullPointer as i32;
    }

    unsafe {
        // Mutex<Renderer>를 Box로 다시 감싸서 drop
        let _ = Box::from_raw(renderer as *mut Mutex<Renderer>);
        // 파괴 완료
    }

    ErrorCode::Success as i32
}

/// 프레임 렌더링 (Mutex로 동시 접근 방지)
#[no_mangle]
pub extern "C" fn renderer_render_frame(
    renderer: *mut c_void,
    timestamp_ms: i64,
    out_width: *mut u32,
    out_height: *mut u32,
    out_data: *mut *mut u8,
    out_data_size: *mut usize,
) -> i32 {
    if renderer.is_null() || out_width.is_null() || out_height.is_null()
        || out_data.is_null() || out_data_size.is_null() {
        // NULL 포인터
        return ErrorCode::NullPointer as i32;
    }

    unsafe {
        let renderer_mutex = &*(renderer as *const Mutex<Renderer>);

        let mut renderer_ref = match renderer_mutex.try_lock() {
            Ok(r) => r,
            Err(_) => {
                // Mutex busy → 프레임 스킵 (출력 파라미터 초기화)
                *out_width = 0;
                *out_height = 0;
                *out_data = std::ptr::null_mut();
                *out_data_size = 0;
                return ErrorCode::Success as i32;
            }
        };

        match renderer_ref.render_frame(timestamp_ms) {
            Ok(frame) => {
                *out_width = frame.width;
                *out_height = frame.height;
                *out_data_size = frame.data.len();

                let data_box = frame.data.into_boxed_slice();
                *out_data = Box::into_raw(data_box) as *mut u8;

                ErrorCode::Success as i32
            }
            Err(e) => {
                // 에러를 프레임 스킵으로 처리 (C# Exception 방지)
                // render_frame Err는 Timeline lock poison 등 심각한 상황이지만,
                // C#에서 Exception throw → 재생 영구 정지보다는
                // 프레임 스킵(null) 반환이 더 안전
                eprintln!("renderer_render_frame error at {}ms: {}", timestamp_ms, e);
                *out_width = 0;
                *out_height = 0;
                *out_data = std::ptr::null_mut();
                *out_data_size = 0;
                ErrorCode::Success as i32
            }
        }
        // Mutex lock은 여기서 자동으로 해제됨 (MutexGuard drop)
    }
}

/// 재생 모드 설정 (C# 재생 시작/정지 시 호출)
/// playback=1: 재생 모드 (forward_threshold=5000ms, seek 대신 forward decode)
/// playback=0: 스크럽 모드 (forward_threshold=100ms, 즉시 seek)
#[no_mangle]
pub extern "C" fn renderer_set_playback_mode(renderer: *mut c_void, playback: i32) -> i32 {
    if renderer.is_null() {
        return ErrorCode::NullPointer as i32;
    }

    unsafe {
        let renderer_mutex = &*(renderer as *const Mutex<Renderer>);
        match renderer_mutex.try_lock() {
            Ok(mut r) => {
                r.set_playback_mode(playback != 0);
                ErrorCode::Success as i32
            }
            Err(_) => ErrorCode::Success as i32, // busy면 무시 (다음 프레임에서 적용)
        }
    }
}

/// 프레임 캐시 클리어 (클립 편집 시 C#에서 호출)
#[no_mangle]
pub extern "C" fn renderer_clear_cache(renderer: *mut c_void) -> i32 {
    if renderer.is_null() {
        return ErrorCode::NullPointer as i32;
    }

    unsafe {
        let renderer_mutex = &*(renderer as *const Mutex<Renderer>);
        match renderer_mutex.try_lock() {
            Ok(mut r) => {
                r.clear_cache();
                ErrorCode::Success as i32
            }
            Err(_) => ErrorCode::Success as i32, // busy면 무시
        }
    }
}

/// 캐시 통계 조회 (디버깅/모니터링)
#[no_mangle]
pub extern "C" fn renderer_get_cache_stats(
    renderer: *mut c_void,
    out_cached_frames: *mut u32,
    out_cache_bytes: *mut usize,
) -> i32 {
    if renderer.is_null() || out_cached_frames.is_null() || out_cache_bytes.is_null() {
        return ErrorCode::NullPointer as i32;
    }

    unsafe {
        let renderer_mutex = &*(renderer as *const Mutex<Renderer>);
        match renderer_mutex.try_lock() {
            Ok(r) => {
                let (frames, bytes) = r.cache_stats();
                *out_cached_frames = frames;
                *out_cache_bytes = bytes;
                ErrorCode::Success as i32
            }
            Err(_) => {
                *out_cached_frames = 0;
                *out_cache_bytes = 0;
                ErrorCode::Success as i32
            }
        }
    }
}

/// 렌더링된 프레임 데이터 해제
#[no_mangle]
pub extern "C" fn renderer_free_frame_data(data: *mut u8, size: usize) -> i32 {
    if data.is_null() {
        return ErrorCode::NullPointer as i32;
    }

    unsafe {
        let slice = std::slice::from_raw_parts_mut(data, size);
        let _ = Box::from_raw(slice as *mut [u8]);
    }

    ErrorCode::Success as i32
}

/// 비디오 파일 정보 조회 (duration, width, height, fps)
#[no_mangle]
pub extern "C" fn get_video_info(
    file_path: *const c_char,
    out_duration_ms: *mut i64,
    out_width: *mut u32,
    out_height: *mut u32,
    out_fps: *mut f64,
) -> i32 {
    if file_path.is_null() || out_duration_ms.is_null()
        || out_width.is_null() || out_height.is_null() || out_fps.is_null() {
        return ErrorCode::NullPointer as i32;
    }

    unsafe {
        let c_str = CStr::from_ptr(file_path);
        let file_path_str = match c_str.to_str() {
            Ok(s) => s,
            Err(_) => return ErrorCode::InvalidParam as i32,
        };

        let path = PathBuf::from(file_path_str);

        let decoder = match Decoder::open(&path) {
            Ok(d) => d,
            Err(e) => {
                eprintln!("get_video_info: Failed to open: {}", e);
                return ErrorCode::Ffmpeg as i32;
            }
        };

        *out_duration_ms = decoder.duration_ms();
        *out_width = decoder.width();
        *out_height = decoder.height();
        *out_fps = decoder.fps();
    }

    ErrorCode::Success as i32
}

/// 비디오 썸네일 생성 (스탠드얼론 함수 - 레거시, 단일 프레임용)
/// NOTE: 다수 썸네일 생성 시 thumbnail_session_* API 사용 권장
#[no_mangle]
pub extern "C" fn generate_video_thumbnail(
    file_path: *const c_char,
    timestamp_ms: i64,
    thumb_width: u32,
    thumb_height: u32,
    out_width: *mut u32,
    out_height: *mut u32,
    out_data: *mut *mut u8,
    out_data_size: *mut usize,
) -> i32 {
    if file_path.is_null() || out_width.is_null() || out_height.is_null()
        || out_data.is_null() || out_data_size.is_null() {
        return ErrorCode::NullPointer as i32;
    }

    unsafe {
        let c_str = CStr::from_ptr(file_path);
        let file_path_str = match c_str.to_str() {
            Ok(s) => s,
            Err(_) => return ErrorCode::InvalidParam as i32,
        };

        let path = PathBuf::from(file_path_str);

        // 임시 Decoder 생성 (단일 프레임이므로 960x540 기본 해상도)
        let mut decoder = match Decoder::open(&path) {
            Ok(d) => d,
            Err(e) => {
                eprintln!("generate_video_thumbnail: Failed to open: {}", e);
                return ErrorCode::Ffmpeg as i32;
            }
        };

        match decoder.generate_thumbnail(timestamp_ms, thumb_width, thumb_height) {
            Ok(frame) => {
                *out_width = frame.width;
                *out_height = frame.height;
                *out_data_size = frame.data.len();

                let data_box = frame.data.into_boxed_slice();
                *out_data = Box::into_raw(data_box) as *mut u8;

                ErrorCode::Success as i32
            }
            Err(e) => {
                eprintln!("generate_video_thumbnail: Failed at {}ms: {}", timestamp_ms, e);
                ErrorCode::Ffmpeg as i32
            }
        }
    }
}
