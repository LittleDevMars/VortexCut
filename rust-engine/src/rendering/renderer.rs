// 렌더링 엔진 - Timeline을 실제 프레임으로 렌더링

use crate::timeline::{Timeline, VideoClip};
use crate::ffmpeg::{Decoder, Frame as DecoderFrame};
use std::collections::HashMap;
use std::sync::{Arc, Mutex};

/// 렌더링된 프레임 데이터
pub struct RenderedFrame {
    pub width: u32,
    pub height: u32,
    pub data: Vec<u8>, // RGBA 포맷
    pub timestamp_ms: i64,
}

/// 비디오 렌더러
pub struct Renderer {
    timeline: Arc<Mutex<Timeline>>,
    decoder_cache: HashMap<String, Decoder>, // 파일 경로 -> Decoder
}

impl Renderer {
    /// 새 렌더러 생성
    pub fn new(timeline: Arc<Mutex<Timeline>>) -> Self {
        Self {
            timeline,
            decoder_cache: HashMap::new(),
        }
    }

    /// 특정 시간의 프레임 렌더링 (UI 레벨 스케일링 최적화)
    pub fn render_frame(&mut self, timestamp_ms: i64) -> Result<RenderedFrame, String> {
        // Timeline 데이터 복사 (lock 해제를 위해)
        let clips_to_render = {
            let timeline = self.timeline.lock()
                .map_err(|e| format!("Failed to lock timeline: {}", e))?;

            println!("🎬 Rust Renderer: render_frame(timestamp_ms={})", timestamp_ms);
            println!("   Timeline: {}x{}, video_tracks={}", timeline.width, timeline.height, timeline.video_tracks.len());

            let mut clips = Vec::new();

            // 비디오 트랙들을 순회하며 렌더링할 클립 수집
            for (idx, track) in timeline.video_tracks.iter().enumerate() {
                println!("   Track[{}]: enabled={}, clips={}", idx, track.enabled, track.clips.len());

                if !track.enabled {
                    continue;
                }

                // 트랙의 모든 클립 출력
                for clip in &track.clips {
                    println!("     Clip ID={}: start_ms={}, duration_ms={}, end_ms={}",
                        clip.id, clip.start_time_ms, clip.duration_ms, clip.end_time_ms());
                }

                // 현재 timestamp에 해당하는 클립 찾기
                if let Some(clip) = track.get_clip_at_time(timestamp_ms) {
                    println!("   ✅ Found clip ID={} at timestamp {}", clip.id, timestamp_ms);
                    // 클립의 source timestamp 계산
                    if let Some(source_time_ms) = clip.timeline_to_source_time(timestamp_ms) {
                        println!("   ✅ Source time: {}", source_time_ms);
                        clips.push((clip.clone(), source_time_ms));
                    }
                } else {
                    println!("   ⚠️ No clip found at timestamp {}", timestamp_ms);
                }
            }

            println!("   📊 Total clips to render: {}", clips.len());

            clips
        }; // timeline lock 해제

        // 클립이 없으면 검은색 프레임 반환 (960x540)
        if clips_to_render.is_empty() {
            let width = 960;
            let height = 540;
            return Ok(RenderedFrame {
                width,
                height,
                data: vec![0u8; (width * height * 4) as usize],
                timestamp_ms,
            });
        }

        // 첫 번째 클립 렌더링 (스케일링 없이 디코더 출력 그대로 반환)
        let (clip, source_time_ms) = &clips_to_render[0];
        println!("   🎞️ Decoding clip ID={}, source_time_ms={}", clip.id, source_time_ms);

        match self.decode_clip_frame(clip, *source_time_ms) {
            Ok(frame) => {
                println!("   ✅ Decoded frame: {}x{}, data.len()={}", frame.width, frame.height, frame.data.len());
                println!("   🚀 Returning frame without scaling (UI will handle scaling)");

                // 디코더 출력을 그대로 반환 (960x540 RGBA)
                Ok(RenderedFrame {
                    width: frame.width,
                    height: frame.height,
                    data: frame.data,
                    timestamp_ms,
                })
            },
            Err(e) => {
                println!("   ❌ Decode error: {}", e);
                Err(e)
            }
        }
    }

    /// 클립의 프레임 디코딩 (캐시 사용)
    fn decode_clip_frame(&mut self, clip: &VideoClip, source_time_ms: i64) -> Result<DecoderFrame, String> {
        let file_path = clip.file_path.to_string_lossy().to_string();
        println!("   📂 File path: {}", file_path);

        // 디코더가 캐시에 없으면 생성
        if !self.decoder_cache.contains_key(&file_path) {
            println!("   🔧 Opening decoder for file...");
            match Decoder::open(&clip.file_path) {
                Ok(decoder) => {
                    println!("   ✅ Decoder opened: {}x{}, fps={:.2}",
                        decoder.width(), decoder.height(), decoder.fps());
                    self.decoder_cache.insert(file_path.clone(), decoder);
                }
                Err(e) => {
                    println!("   ❌ Failed to open decoder: {}", e);
                    return Err(e);
                }
            }
        } else {
            println!("   ♻️ Using cached decoder");
        }

        // 디코더에서 프레임 가져오기
        println!("   🎬 Getting decoder from cache...");
        let decoder = self.decoder_cache.get_mut(&file_path)
            .ok_or("Decoder not found in cache")?;

        println!("   🎬 Calling decoder.decode_frame({})...", source_time_ms);
        let result = decoder.decode_frame(source_time_ms);

        match &result {
            Ok(frame) => println!("   ✅ decoder.decode_frame() returned OK: {}x{}", frame.width, frame.height),
            Err(e) => println!("   ❌ decoder.decode_frame() returned ERR: {}", e),
        }

        result
    }

    /// 프레임을 output에 합성 (크기가 다르면 스케일링)
    fn composite_frame(&self, source: &[u8], dest: &mut [u8], src_width: u32, src_height: u32, dest_width: u32, dest_height: u32) {
        println!("      🎨 composite_frame START");
        let src_size = (src_width * src_height * 4) as usize;
        let dest_size = (dest_width * dest_height * 4) as usize;

        // 크기 검증
        if source.len() != src_size {
            println!("   ⚠️ Warning: Source buffer size mismatch! Expected {}, got {}", src_size, source.len());
            return;
        }
        if dest.len() != dest_size {
            println!("   ⚠️ Warning: Dest buffer size mismatch! Expected {}, got {}", dest_size, dest.len());
            return;
        }

        if src_width == dest_width && src_height == dest_height {
            // 크기가 같으면 단순 복사
            println!("      📋 Same size, copying directly...");
            dest.copy_from_slice(source);
            println!("   ✅ Composite: Same size, copied directly");
        } else {
            println!("   🔧 Composite: Scaling {}x{} -> {}x{} ({} pixels)",
                src_width, src_height, dest_width, dest_height, dest_width * dest_height);

            // 간단한 최근접 이웃 스케일링 (Nearest Neighbor Scaling)
            let total_pixels = dest_width * dest_height;
            let mut processed = 0u32;

            for y in 0..dest_height {
                for x in 0..dest_width {
                    let src_x = (x as f64 * src_width as f64 / dest_width as f64) as u32;
                    let src_y = (y as f64 * src_height as f64 / dest_height as f64) as u32;

                    if src_x < src_width && src_y < src_height {
                        let src_idx = ((src_y * src_width + src_x) * 4) as usize;
                        let dst_idx = ((y * dest_width + x) * 4) as usize;

                        if src_idx + 4 <= source.len() && dst_idx + 4 <= dest.len() {
                            dest[dst_idx..dst_idx + 4].copy_from_slice(&source[src_idx..src_idx + 4]);
                        }
                    }

                    processed += 1;
                    // 진행률 표시 (10% 단위)
                    if processed % (total_pixels / 10) == 0 {
                        let percent = (processed * 100) / total_pixels;
                        println!("      ⏳ Scaling progress: {}%", percent);
                    }
                }
            }
            println!("   ✅ Composite: Scaling completed");
        }
        println!("      🎨 composite_frame END");
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::path::PathBuf;

    #[test]
    fn test_renderer_create() {
        let timeline = Arc::new(Mutex::new(Timeline::new(1920, 1080, 30.0)));
        let _renderer = Renderer::new(timeline);
    }

    #[test]
    fn test_renderer_with_real_video() {
        let video_path = PathBuf::from(r"C:\Users\USER\Videos\드론 대응 2.75인치 로켓 '비궁'으로 유도키트 개발, 사우디 기술협력 추진.mp4");

        if !video_path.exists() {
            println!("⚠️ Test video file not found, skipping test");
            return;
        }

        println!("\n=== Renderer Test ===");

        // 1. Timeline 생성
        let timeline = Arc::new(Mutex::new(Timeline::new(1920, 1080, 30.0)));

        // 2. 비디오 트랙 추가
        let track_id = {
            let mut tl = timeline.lock().unwrap();
            tl.add_video_track()
        };
        println!("✅ Video track created: ID={}", track_id);

        // 3. 클립 추가
        let clip_id = {
            let mut tl = timeline.lock().unwrap();
            tl.add_video_clip(track_id, video_path.clone(), 0, 5000)
                .expect("Failed to add video clip")
        };
        println!("✅ Video clip added: ID={}", clip_id);

        // 4. Renderer 생성
        let mut renderer = Renderer::new(timeline.clone());
        println!("✅ Renderer created");

        // 5. 프레임 렌더링 테스트
        let timestamps = [0i64, 1000, 2000];
        for timestamp in timestamps {
            println!("\n🎬 Rendering frame at {}ms...", timestamp);
            match renderer.render_frame(timestamp) {
                Ok(frame) => {
                    println!("   ✅ Frame rendered: {}x{}", frame.width, frame.height);
                    println!("   Data size: {} bytes", frame.data.len());

                    // 검증
                    assert_eq!(frame.width, 1920);
                    assert_eq!(frame.height, 1080);
                    assert_eq!(frame.data.len(), (1920 * 1080 * 4) as usize);

                    // 첫 10픽셀 확인
                    let pixels: Vec<u8> = frame.data.iter().take(40).copied().collect();
                    println!("   First 10 pixels (RGBA): {:?}", pixels);
                }
                Err(e) => {
                    panic!("❌ Failed to render frame at {}ms: {}", timestamp, e);
                }
            }
        }

        println!("\n✅ All renderer tests passed!");
    }
}
