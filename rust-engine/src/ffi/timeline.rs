// Timeline FFI 함수
// C#에서 Timeline을 생성/관리하기 위한 FFI 인터페이스

use std::ffi::{CStr, CString};
use std::os::raw::c_char;
use std::path::PathBuf;
use std::ptr;

use crate::timeline::Timeline;
use super::types::{ERROR_SUCCESS, ERROR_NULL_PTR, ERROR_INVALID_PARAM};

/// Timeline 생성
#[no_mangle]
pub extern "C" fn timeline_create(
    width: u32,
    height: u32,
    fps: f64,
    out_timeline: *mut *mut Timeline,
) -> i32 {
    if out_timeline.is_null() {
        return ERROR_NULL_PTR;
    }

    if width == 0 || height == 0 || fps <= 0.0 {
        return ERROR_INVALID_PARAM;
    }

    let timeline = Box::new(Timeline::new(width, height, fps));

    unsafe {
        *out_timeline = Box::into_raw(timeline);
    }

    ERROR_SUCCESS
}

/// Timeline 파괴 (메모리 해제)
#[no_mangle]
pub extern "C" fn timeline_destroy(timeline: *mut Timeline) -> i32 {
    if timeline.is_null() {
        return ERROR_NULL_PTR;
    }

    unsafe {
        let _ = Box::from_raw(timeline);
    }

    ERROR_SUCCESS
}

/// 비디오 트랙 추가
#[no_mangle]
pub extern "C" fn timeline_add_video_track(
    timeline: *mut Timeline,
    out_track_id: *mut u64,
) -> i32 {
    if timeline.is_null() || out_track_id.is_null() {
        return ERROR_NULL_PTR;
    }

    let timeline = unsafe { &mut *timeline };
    let track_id = timeline.add_video_track();

    unsafe {
        *out_track_id = track_id;
    }

    ERROR_SUCCESS
}

/// 오디오 트랙 추가
#[no_mangle]
pub extern "C" fn timeline_add_audio_track(
    timeline: *mut Timeline,
    out_track_id: *mut u64,
) -> i32 {
    if timeline.is_null() || out_track_id.is_null() {
        return ERROR_NULL_PTR;
    }

    let timeline = unsafe { &mut *timeline };
    let track_id = timeline.add_audio_track();

    unsafe {
        *out_track_id = track_id;
    }

    ERROR_SUCCESS
}

/// 비디오 클립 추가
#[no_mangle]
pub extern "C" fn timeline_add_video_clip(
    timeline: *mut Timeline,
    track_id: u64,
    file_path: *const c_char,
    start_time_ms: i64,
    duration_ms: i64,
    out_clip_id: *mut u64,
) -> i32 {
    if timeline.is_null() || file_path.is_null() || out_clip_id.is_null() {
        return ERROR_NULL_PTR;
    }

    if duration_ms <= 0 {
        return ERROR_INVALID_PARAM;
    }

    let timeline = unsafe { &mut *timeline };

    let path_str = unsafe {
        match CStr::from_ptr(file_path).to_str() {
            Ok(s) => s,
            Err(_) => return ERROR_INVALID_PARAM,
        }
    };

    let path = PathBuf::from(path_str);

    match timeline.add_video_clip(track_id, path, start_time_ms, duration_ms) {
        Some(clip_id) => {
            unsafe {
                *out_clip_id = clip_id;
            }
            ERROR_SUCCESS
        }
        None => ERROR_INVALID_PARAM, // 트랙을 찾을 수 없음
    }
}

/// 오디오 클립 추가
#[no_mangle]
pub extern "C" fn timeline_add_audio_clip(
    timeline: *mut Timeline,
    track_id: u64,
    file_path: *const c_char,
    start_time_ms: i64,
    duration_ms: i64,
    out_clip_id: *mut u64,
) -> i32 {
    if timeline.is_null() || file_path.is_null() || out_clip_id.is_null() {
        return ERROR_NULL_PTR;
    }

    if duration_ms <= 0 {
        return ERROR_INVALID_PARAM;
    }

    let timeline = unsafe { &mut *timeline };

    let path_str = unsafe {
        match CStr::from_ptr(file_path).to_str() {
            Ok(s) => s,
            Err(_) => return ERROR_INVALID_PARAM,
        }
    };

    let path = PathBuf::from(path_str);

    match timeline.add_audio_clip(track_id, path, start_time_ms, duration_ms) {
        Some(clip_id) => {
            unsafe {
                *out_clip_id = clip_id;
            }
            ERROR_SUCCESS
        }
        None => ERROR_INVALID_PARAM,
    }
}

/// 비디오 클립 제거
#[no_mangle]
pub extern "C" fn timeline_remove_video_clip(
    timeline: *mut Timeline,
    track_id: u64,
    clip_id: u64,
) -> i32 {
    if timeline.is_null() {
        return ERROR_NULL_PTR;
    }

    let timeline = unsafe { &mut *timeline };

    if timeline.remove_video_clip(track_id, clip_id) {
        ERROR_SUCCESS
    } else {
        ERROR_INVALID_PARAM
    }
}

/// 오디오 클립 제거
#[no_mangle]
pub extern "C" fn timeline_remove_audio_clip(
    timeline: *mut Timeline,
    track_id: u64,
    clip_id: u64,
) -> i32 {
    if timeline.is_null() {
        return ERROR_NULL_PTR;
    }

    let timeline = unsafe { &mut *timeline };

    if timeline.remove_audio_clip(track_id, clip_id) {
        ERROR_SUCCESS
    } else {
        ERROR_INVALID_PARAM
    }
}

/// 타임라인 총 길이 가져오기 (ms)
#[no_mangle]
pub extern "C" fn timeline_get_duration(
    timeline: *const Timeline,
    out_duration_ms: *mut i64,
) -> i32 {
    if timeline.is_null() || out_duration_ms.is_null() {
        return ERROR_NULL_PTR;
    }

    let timeline = unsafe { &*timeline };

    unsafe {
        *out_duration_ms = timeline.duration_ms();
    }

    ERROR_SUCCESS
}

/// 비디오 트랙 개수 가져오기
#[no_mangle]
pub extern "C" fn timeline_get_video_track_count(
    timeline: *const Timeline,
    out_count: *mut usize,
) -> i32 {
    if timeline.is_null() || out_count.is_null() {
        return ERROR_NULL_PTR;
    }

    let timeline = unsafe { &*timeline };

    unsafe {
        *out_count = timeline.video_tracks.len();
    }

    ERROR_SUCCESS
}

/// 오디오 트랙 개수 가져오기
#[no_mangle]
pub extern "C" fn timeline_get_audio_track_count(
    timeline: *const Timeline,
    out_count: *mut usize,
) -> i32 {
    if timeline.is_null() || out_count.is_null() {
        return ERROR_NULL_PTR;
    }

    let timeline = unsafe { &*timeline };

    unsafe {
        *out_count = timeline.audio_tracks.len();
    }

    ERROR_SUCCESS
}
