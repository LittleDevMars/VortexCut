// FFmpeg Decoder 모듈 (ffmpeg-next 사용)

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

/// 비디오 디코더
pub struct Decoder {
    input_ctx: ffmpeg::format::context::Input,
    video_stream_index: usize,
    decoder: ffmpeg::codec::decoder::Video,
    scaler: ffmpeg::software::scaling::Context,
    width: u32,
    height: u32,
    fps: f64,
    duration_ms: i64,
}

impl Decoder {
    /// 비디오 파일 열기
    pub fn open(file_path: &Path) -> Result<Self, String> {
        // FFmpeg 초기화
        ffmpeg::init().map_err(|e| format!("FFmpeg init failed: {}", e))?;

        // 입력 파일 열기
        let input_ctx = ffmpeg::format::input(&file_path)
            .map_err(|e| format!("Failed to open file: {}", e))?;

        // 비디오 스트림 찾기
        let video_stream = input_ctx
            .streams()
            .best(ffmpeg::media::Type::Video)
            .ok_or("No video stream found")?;

        let video_stream_index = video_stream.index();

        // 디코더 찾기 및 생성
        let context_decoder = ffmpeg::codec::context::Context::from_parameters(video_stream.parameters())
            .map_err(|e| format!("Failed to create context: {}", e))?;

        let decoder = context_decoder
            .decoder()
            .video()
            .map_err(|e| format!("Failed to get video decoder: {}", e))?;

        // 비디오 정보 추출
        let width = decoder.width();
        let height = decoder.height();

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

        // Scaler 생성 (YUV -> RGBA 변환)
        let scaler = ffmpeg::software::scaling::Context::get(
            decoder.format(),
            width,
            height,
            ffmpeg::format::Pixel::RGBA,
            width,
            height,
            ffmpeg::software::scaling::Flags::BILINEAR,
        )
        .map_err(|e| format!("Failed to create scaler: {}", e))?;

        Ok(Self {
            input_ctx,
            video_stream_index,
            decoder,
            scaler,
            width,
            height,
            fps,
            duration_ms,
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
        // Seek to timestamp
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
}
