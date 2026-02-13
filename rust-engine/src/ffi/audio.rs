// 오디오 파형 피크 추출 FFI
// FFmpeg으로 오디오 디코딩 → f32 PCM → 블록별 최대 절대값 계산

use crate::ffi::types::ErrorCode;
use std::ffi::{c_char, CStr};
use std::path::PathBuf;

use ffmpeg_next as ffmpeg;

/// 오디오 피크 데이터 추출 (C# P/Invoke 호출)
///
/// 파일에서 오디오 스트림을 디코딩하고, samples_per_peak 단위로
/// 블록별 최대 절대값(0.0~1.0)을 계산하여 반환한다.
///
/// # 파라미터
/// - file_path: UTF-8 파일 경로
/// - samples_per_peak: 다운샘플 비율 (예: 1024 → 1024 샘플당 1 피크)
/// - out_peaks: 출력 피크 배열 포인터 (f32[], 호출자가 free_audio_peaks로 해제)
/// - out_peak_count: 출력 피크 개수
/// - out_channels: 채널 수
/// - out_sample_rate: 샘플레이트
/// - out_duration_ms: 오디오 총 길이 (ms)
///
/// # 반환값
/// ErrorCode (0=성공)
#[no_mangle]
pub extern "C" fn extract_audio_peaks(
    file_path: *const c_char,
    samples_per_peak: u32,
    out_peaks: *mut *mut f32,
    out_peak_count: *mut u32,
    out_channels: *mut u32,
    out_sample_rate: *mut u32,
    out_duration_ms: *mut i64,
) -> i32 {
    // NULL 검사
    if file_path.is_null() || out_peaks.is_null() || out_peak_count.is_null()
        || out_channels.is_null() || out_sample_rate.is_null() || out_duration_ms.is_null()
    {
        return ErrorCode::NullPointer as i32;
    }

    if samples_per_peak == 0 {
        return ErrorCode::InvalidParam as i32;
    }

    unsafe {
        // 출력 파라미터 초기화
        *out_peaks = std::ptr::null_mut();
        *out_peak_count = 0;
        *out_channels = 0;
        *out_sample_rate = 0;
        *out_duration_ms = 0;

        // UTF-8 경로 변환
        let c_str = CStr::from_ptr(file_path);
        let file_path_str = match c_str.to_str() {
            Ok(s) => s,
            Err(e) => {
                eprintln!("❌ extract_audio_peaks: Invalid UTF-8: {}", e);
                return ErrorCode::InvalidParam as i32;
            }
        };

        let path = PathBuf::from(file_path_str);

        // 피크 추출 실행
        match extract_peaks_internal(&path, samples_per_peak) {
            Ok(result) => {
                *out_channels = result.channels;
                *out_sample_rate = result.sample_rate;
                *out_duration_ms = result.duration_ms;
                *out_peak_count = result.peaks.len() as u32;

                // 피크 데이터를 힙에 할당하고 포인터 반환
                let peaks_box = result.peaks.into_boxed_slice();
                *out_peaks = Box::into_raw(peaks_box) as *mut f32;

                ErrorCode::Success as i32
            }
            Err(e) => {
                eprintln!("❌ extract_audio_peaks: {}", e);
                ErrorCode::Ffmpeg as i32
            }
        }
    }
}

/// 피크 데이터 메모리 해제 (C#에서 호출)
#[no_mangle]
pub extern "C" fn free_audio_peaks(peaks: *mut f32, count: u32) -> i32 {
    if peaks.is_null() {
        return ErrorCode::NullPointer as i32;
    }

    unsafe {
        let slice = std::slice::from_raw_parts_mut(peaks, count as usize);
        let _ = Box::from_raw(slice as *mut [f32]);
    }

    ErrorCode::Success as i32
}

/// 내부 피크 추출 결과
struct AudioPeakResult {
    peaks: Vec<f32>,
    channels: u32,
    sample_rate: u32,
    duration_ms: i64,
}

/// FFmpeg으로 오디오 디코딩 + 피크 계산 (내부 함수)
fn extract_peaks_internal(
    file_path: &PathBuf,
    samples_per_peak: u32,
) -> Result<AudioPeakResult, String> {
    // FFmpeg 초기화
    ffmpeg::init().map_err(|e| format!("FFmpeg init failed: {}", e))?;

    // 파일 열기
    let mut input_ctx = ffmpeg::format::input(file_path)
        .map_err(|e| format!("Failed to open file: {}", e))?;

    // 오디오 스트림 찾기
    let audio_stream = input_ctx
        .streams()
        .best(ffmpeg::media::Type::Audio)
        .ok_or("No audio stream found")?;

    let audio_stream_index = audio_stream.index();
    let codec_params = audio_stream.parameters();

    // Duration 계산
    let duration_ms = if audio_stream.duration() > 0 {
        let tb = audio_stream.time_base();
        (audio_stream.duration() * i64::from(tb.numerator()) * 1000)
            / i64::from(tb.denominator())
    } else if input_ctx.duration() > 0 {
        input_ctx.duration() / 1000
    } else {
        0
    };

    // 오디오 디코더 생성
    let mut context = ffmpeg::codec::context::Context::from_parameters(codec_params)
        .map_err(|e| format!("Failed to create audio context: {}", e))?;

    // 멀티스레딩
    if let Ok(parallelism) = std::thread::available_parallelism() {
        context.set_threading(ffmpeg::threading::Config {
            kind: ffmpeg::threading::Type::Frame,
            count: parallelism.get(),
        });
    }

    let mut decoder = context
        .decoder()
        .audio()
        .map_err(|e| format!("Failed to get audio decoder: {}", e))?;

    let sample_rate = decoder.rate();
    let channels = decoder.channels() as u32;

    // 리샘플러: 원본 포맷 → f32 planar
    let mut resampler = ffmpeg::software::resampling::Context::get(
        decoder.format(),
        decoder.channel_layout(),
        decoder.rate(),
        ffmpeg::format::Sample::F32(ffmpeg::format::sample::Type::Packed),
        decoder.channel_layout(),
        decoder.rate(),
    )
    .map_err(|e| format!("Failed to create resampler: {}", e))?;

    // 피크 계산 버퍼
    let mut peaks: Vec<f32> = Vec::new();
    let mut block_max: f32 = 0.0;
    let mut block_sample_count: u32 = 0;

    // 패킷 처리
    for (stream, packet) in input_ctx.packets() {
        if stream.index() != audio_stream_index {
            continue;
        }

        if decoder.send_packet(&packet).is_err() {
            continue;
        }

        // 디코딩된 프레임 수신
        let mut decoded_frame = ffmpeg::frame::Audio::empty();
        while decoder.receive_frame(&mut decoded_frame).is_ok() {
            // 리샘플링 (f32 packed)
            let mut resampled = ffmpeg::frame::Audio::empty();
            if resampler.run(&decoded_frame, &mut resampled).is_err() {
                continue;
            }

            let data = resampled.data(0);
            let sample_count = resampled.samples();

            // f32 슬라이스로 변환
            let f32_slice = unsafe {
                std::slice::from_raw_parts(
                    data.as_ptr() as *const f32,
                    sample_count * channels as usize,
                )
            };

            // 채널 믹스다운 + 블록별 피크 계산
            for chunk in f32_slice.chunks(channels as usize) {
                // 모노 믹스다운: 모든 채널의 max(abs)
                let sample_abs = chunk.iter()
                    .map(|s| s.abs())
                    .fold(0.0f32, f32::max);

                if sample_abs > block_max {
                    block_max = sample_abs;
                }

                block_sample_count += 1;

                if block_sample_count >= samples_per_peak {
                    // 피크 값 클램핑 (0.0~1.0)
                    peaks.push(block_max.min(1.0));
                    block_max = 0.0;
                    block_sample_count = 0;
                }
            }
        }
    }

    // 마지막 블록 처리
    if block_sample_count > 0 {
        peaks.push(block_max.min(1.0));
    }

    Ok(AudioPeakResult {
        peaks,
        channels,
        sample_rate,
        duration_ms,
    })
}
