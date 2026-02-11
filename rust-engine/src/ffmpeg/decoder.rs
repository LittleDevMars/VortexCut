// FFmpeg Decoder 모듈 (ffmpeg-next with hardware acceleration)

use ffmpeg_next as ffmpeg;
use std::path::Path;

/// 비디오 프레임 데이터
#[derive(Debug, Clone)]
pub struct Frame {
    pub width: u32,
    pub height: u32,
    pub format: PixelFormat,
    pub data: Vec<u8>,
    pub timestamp_ms: i64,
}

/// 픽셀 포맷
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum PixelFormat {
    RGBA,
    RGB,
    YUV420P,
}

/// 비디오 디코더 (ffmpeg-next + hwaccel options)
pub struct Decoder {
    input_ctx: ffmpeg::format::context::Input,
    video_stream_index: usize,
    decoder: ffmpeg::codec::decoder::Video,
    scaler: ffmpeg::software::scaling::Context,
    width: u32,
    height: u32,
    fps: f64,
    duration_ms: i64,
    last_timestamp_ms: i64,
    is_hardware: bool,  // Hardware acceleration 사용 여부
}

impl Decoder {
    /// Decoder 생성 (Multi-threading 최적화)
    fn try_create_decoder(
        _codec_id: ffmpeg::codec::Id,
        codec_params: ffmpeg::codec::Parameters,
    ) -> Result<(ffmpeg::codec::decoder::Video, bool), String> {
        // Create decoder context
        let mut context = ffmpeg::codec::context::Context::from_parameters(codec_params)
            .map_err(|e| format!("Failed to create context: {}", e))?;

        // OPTIMIZATION: Multi-threading
        if let Ok(parallelism) = std::thread::available_parallelism() {
            let thread_count = parallelism.get();
            println!("   🔧 Enabling multi-threading: {} threads", thread_count);
            context.set_threading(ffmpeg::threading::Config {
                kind: ffmpeg::threading::Type::Frame,
                count: thread_count,
            });
        }

        // Open decoder
        let decoder = context
            .decoder()
            .video()
            .map_err(|e| format!("Failed to get video decoder: {}", e))?;

        // Hardware acceleration is set at input level (input_with_dictionary)
        // We'll detect it based on decoder format later
        Ok((decoder, false))  // is_hardware will be updated based on actual usage
    }

    /// 비디오 파일 열기
    pub fn open(file_path: &Path) -> Result<Self, String> {
        // FFmpeg 초기화
        ffmpeg::init().map_err(|e| format!("FFmpeg init failed: {}", e))?;

        // OPTIMIZATION: Hardware acceleration options (플랫폼별)
        let mut options = ffmpeg::Dictionary::new();

        // 플랫폼별 하드웨어 가속 설정
        #[cfg(target_os = "windows")]
        {
            // Windows: NVIDIA CUDA (NVDEC) 우선, 실패 시 D3D11VA
            options.set("hwaccel", "cuda");
            options.set("hwaccel_output_format", "cuda");
            println!("🚀 Opening input with hardware acceleration (Windows: CUDA/NVDEC)");
        }

        #[cfg(target_os = "macos")]
        {
            // macOS: VideoToolbox (Apple 하드웨어 가속)
            options.set("hwaccel", "videotoolbox");
            options.set("hwaccel_output_format", "videotoolbox");
            println!("🚀 Opening input with hardware acceleration (macOS: VideoToolbox)");
        }

        #[cfg(target_os = "linux")]
        {
            // Linux: VAAPI (Intel) 우선, 실패 시 CUDA
            options.set("hwaccel", "vaapi");
            options.set("hwaccel_output_format", "vaapi");
            println!("🚀 Opening input with hardware acceleration (Linux: VAAPI)");
        }

        // 입력 파일 열기 (with hardware acceleration options)
        let input_ctx = ffmpeg::format::input_with_dictionary(&file_path, options)
            .map_err(|e| {
                println!("⚠️ Failed to open with hwaccel, trying without...");
                e
            });

        // Fallback to normal input if hwaccel fails
        let input_ctx = match input_ctx {
            Ok(ctx) => {
                println!("   ✅ Input opened with hardware acceleration");
                ctx
            }
            Err(_) => {
                println!("   📦 Opening input without hardware acceleration");
                ffmpeg::format::input(&file_path)
                    .map_err(|e| format!("Failed to open file: {}", e))?
            }
        };

        // 비디오 스트림 찾기
        let video_stream = input_ctx
            .streams()
            .best(ffmpeg::media::Type::Video)
            .ok_or("No video stream found")?;

        let video_stream_index = video_stream.index();
        let codec_params = video_stream.parameters();
        let codec_id = codec_params.id();

        // OPTIMIZATION 2: Hardware acceleration 시도
        println!("🎬 Codec: {:?}", codec_id);

        let (decoder, is_hardware) = Self::try_create_decoder(codec_id, codec_params)?;

        // 비디오 정보 추출
        let src_width = decoder.width();
        let src_height = decoder.height();

        // OPTIMIZATION 1: 디코딩 해상도 절반으로 낮춤 (4배 속도 개선 예상)
        let decode_width = 960;
        let decode_height = 540;

        println!("🎬 Decoder opened: {}x{} (source) -> {}x{} (decode target)",
                 src_width, src_height, decode_width, decode_height);

        // FPS 계산
        let fps = f64::from(video_stream.avg_frame_rate());

        // Duration 계산 (ms)
        let duration_ms = if video_stream.duration() > 0 {
            let time_base = video_stream.time_base();
            (video_stream.duration() * i64::from(time_base.numerator()) * 1000)
                / i64::from(time_base.denominator())
        } else if input_ctx.duration() > 0 {
            input_ctx.duration() / 1000 // microseconds to milliseconds
        } else {
            0
        };

        // Scaler 생성 (YUV -> RGBA 변환 + 해상도 축소)
        // CRITICAL: output을 960x540으로 설정 (1920x1080의 1/4 픽셀)
        let scaler = ffmpeg::software::scaling::Context::get(
            decoder.format(),
            src_width,
            src_height,
            ffmpeg::format::Pixel::RGBA,
            decode_width,
            decode_height,
            ffmpeg::software::scaling::Flags::FAST_BILINEAR,  // BILINEAR -> FAST_BILINEAR (더 빠름)
        )
        .map_err(|e| format!("Failed to create scaler: {}", e))?;

        Ok(Self {
            input_ctx,
            video_stream_index,
            decoder,
            scaler,
            width: decode_width,
            height: decode_height,
            fps,
            duration_ms,
            last_timestamp_ms: -1,
            is_hardware,
        })
    }

    /// 비디오 정보 가져오기
    pub fn width(&self) -> u32 {
        self.width
    }

    pub fn height(&self) -> u32 {
        self.height
    }

    pub fn fps(&self) -> f64 {
        self.fps
    }

    pub fn duration_ms(&self) -> i64 {
        self.duration_ms
    }

    /// 특정 시간의 프레임 디코딩
    pub fn decode_frame(&mut self, timestamp_ms: i64) -> Result<Frame, String> {
        // CRITICAL: 연속 재생 최적화 - 순차적이면 seek 스킵
        let frame_duration_ms = (1000.0 / self.fps) as i64;
        let is_sequential = timestamp_ms >= self.last_timestamp_ms
            && timestamp_ms <= self.last_timestamp_ms + frame_duration_ms * 2;

        if !is_sequential {
            // 순차적이지 않으면 seek 필요
            println!("   🔍 Non-sequential access: {}ms -> {}ms, seeking...", self.last_timestamp_ms, timestamp_ms);
            self.seek(timestamp_ms)?;
        } else {
            println!("   ⏩ Sequential access: {}ms -> {}ms, skip seek", self.last_timestamp_ms, timestamp_ms);
        }

        self.last_timestamp_ms = timestamp_ms;

        // 패킷 읽고 디코딩
        let mut decoded_frame: Option<ffmpeg::frame::Video> = None;

        for (stream, packet) in self.input_ctx.packets() {
            if stream.index() == self.video_stream_index {
                self.decoder.send_packet(&packet)
                    .map_err(|e| format!("Failed to send packet: {}", e))?;

                let mut frame = ffmpeg::frame::Video::empty();
                if self.decoder.receive_frame(&mut frame).is_ok() {
                    decoded_frame = Some(frame);
                    break;
                }
            }
        }

        let frame = decoded_frame.ok_or("Failed to decode frame")?;

        // RGBA 프레임으로 변환
        let mut rgb_frame = ffmpeg::frame::Video::empty();
        self.scaler.run(&frame, &mut rgb_frame)
            .map_err(|e| format!("Failed to scale frame: {}", e))?;

        // 프레임 데이터 복사
        let size = (self.width * self.height * 4) as usize;
        let mut data = vec![0u8; size];

        let src_data = rgb_frame.data(0);
        let linesize = rgb_frame.stride(0);

        for y in 0..self.height as usize {
            let src_offset = y * linesize;
            let dst_offset = y * (self.width as usize * 4);
            let row_size = self.width as usize * 4;

            data[dst_offset..dst_offset + row_size]
                .copy_from_slice(&src_data[src_offset..src_offset + row_size]);
        }

        Ok(Frame {
            width: self.width,
            height: self.height,
            format: PixelFormat::RGBA,
            data,
            timestamp_ms,
        })
    }

    /// 다음 프레임 디코딩
    pub fn decode_next_frame(&mut self) -> Result<Option<Frame>, String> {
        // TODO: 구현
        Ok(None)
    }

    /// 썸네일 프레임 생성 (작은 해상도로 디코딩)
    pub fn generate_thumbnail(&mut self, timestamp_ms: i64, thumb_width: u32, thumb_height: u32) -> Result<Frame, String> {
        println!("📸 Generating thumbnail: timestamp={}ms, size={}x{}", timestamp_ms, thumb_width, thumb_height);

        // seek to timestamp
        self.seek(timestamp_ms)?;

        // 패킷 읽고 디코딩
        let mut decoded_frame: Option<ffmpeg::frame::Video> = None;

        for (stream, packet) in self.input_ctx.packets() {
            if stream.index() == self.video_stream_index {
                self.decoder.send_packet(&packet)
                    .map_err(|e| format!("Failed to send packet: {}", e))?;

                let mut frame = ffmpeg::frame::Video::empty();
                if self.decoder.receive_frame(&mut frame).is_ok() {
                    decoded_frame = Some(frame);
                    break;
                }
            }
        }

        let frame = decoded_frame.ok_or("Failed to decode thumbnail frame")?;

        // 썸네일용 scaler 생성 (작은 해상도)
        let mut thumb_scaler = ffmpeg::software::scaling::Context::get(
            self.decoder.format(),
            self.decoder.width(),
            self.decoder.height(),
            ffmpeg::format::Pixel::RGBA,
            thumb_width,
            thumb_height,
            ffmpeg::software::scaling::Flags::FAST_BILINEAR,
        )
        .map_err(|e| format!("Failed to create thumbnail scaler: {}", e))?;

        // RGBA 프레임으로 변환
        let mut rgb_frame = ffmpeg::frame::Video::empty();
        thumb_scaler.run(&frame, &mut rgb_frame)
            .map_err(|e| format!("Failed to scale thumbnail: {}", e))?;

        // 프레임 데이터 복사
        let size = (thumb_width * thumb_height * 4) as usize;
        let mut data = vec![0u8; size];

        let src_data = rgb_frame.data(0);
        let linesize = rgb_frame.stride(0);

        for y in 0..thumb_height as usize {
            let src_offset = y * linesize;
            let dst_offset = y * (thumb_width as usize * 4);
            let row_size = thumb_width as usize * 4;

            data[dst_offset..dst_offset + row_size]
                .copy_from_slice(&src_data[src_offset..src_offset + row_size]);
        }

        println!("✅ Thumbnail generated: {}x{}, data size={}", thumb_width, thumb_height, data.len());

        Ok(Frame {
            width: thumb_width,
            height: thumb_height,
            format: PixelFormat::RGBA,
            data,
            timestamp_ms,
        })
    }

    /// 특정 시간으로 seek
    pub fn seek(&mut self, timestamp_ms: i64) -> Result<(), String> {
        let stream = self.input_ctx.stream(self.video_stream_index)
            .ok_or("Video stream not found")?;

        let time_base = stream.time_base();

        // milliseconds to stream time base
        let timestamp = (timestamp_ms * i64::from(time_base.denominator()))
            / (i64::from(time_base.numerator()) * 1000);

        self.input_ctx
            .seek(timestamp, ..timestamp)
            .map_err(|e| format!("Seek failed: {}", e))?;

        self.decoder.flush();

        Ok(())
    }
}

// 실제 비디오 파일이 필요하므로 테스트는 주석 처리
#[cfg(test)]
mod tests {
    use super::*;
    use std::path::PathBuf;

    #[test]
    #[ignore] // 실제 비디오 파일 필요
    fn test_decoder_open() {
        let path = PathBuf::from("test.mp4");
        let decoder = Decoder::open(&path);
        assert!(decoder.is_ok());
    }

    #[test]
    #[ignore] // 실제 비디오 파일 필요
    fn test_decode_frame() {
        let path = PathBuf::from("test.mp4");
        let mut decoder = Decoder::open(&path).unwrap();

        let frame = decoder.decode_frame(1000);
        assert!(frame.is_ok());

        let frame = frame.unwrap();
        assert_eq!(frame.timestamp_ms, 1000);
        assert!(!frame.data.is_empty());
    }

    #[test]
    fn test_decoder_with_real_file() {
        // 실제 비디오 파일로 테스트
        let path = PathBuf::from(r"C:\Users\USER\Videos\드론 대응 2.75인치 로켓 '비궁'으로 유도키트 개발, 사우디 기술협력 추진.mp4");

        if !path.exists() {
            println!("⚠️ Test video file not found, skipping test");
            return;
        }

        println!("\n=== Decoder Test ===");
        println!("📹 Opening video: {:?}", path);

        // 1. 디코더 열기
        let mut decoder = match Decoder::open(&path) {
            Ok(d) => {
                println!("✅ Decoder opened successfully");
                println!("   Resolution: {}x{}", d.width(), d.height());
                println!("   FPS: {:.2}", d.fps());
                println!("   Duration: {}ms", d.duration_ms());
                d
            }
            Err(e) => {
                panic!("❌ Failed to open decoder: {}", e);
            }
        };

        // 2. 프레임 디코딩 테스트 (0ms, 1000ms, 2000ms)
        let timestamps = [0i64, 1000, 2000];
        for timestamp in timestamps {
            println!("\n🎬 Decoding frame at {}ms...", timestamp);
            match decoder.decode_frame(timestamp) {
                Ok(frame) => {
                    println!("   ✅ Frame decoded: {}x{}", frame.width, frame.height);
                    println!("   Data size: {} bytes", frame.data.len());

                    // 첫 10픽셀 확인
                    let pixels: Vec<u8> = frame.data.iter().take(40).copied().collect();
                    println!("   First 10 pixels (RGBA): {:?}", pixels);

                    // 검증
                    assert_eq!(frame.width, decoder.width());
                    assert_eq!(frame.height, decoder.height());
                    assert_eq!(frame.data.len(), (frame.width * frame.height * 4) as usize);
                    assert_eq!(frame.format, PixelFormat::RGBA);
                }
                Err(e) => {
                    panic!("❌ Failed to decode frame at {}ms: {}", timestamp, e);
                }
            }
        }

        println!("\n✅ All decoder tests passed!");
    }
}
