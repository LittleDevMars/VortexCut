// 자막 오버레이 — RGBA 비트맵 알파 블렌딩
// C#에서 텍스트를 RGBA 비트맵으로 렌더링 → FFI로 전달 → Export 시 프레임 위에 합성

/// 단일 자막 오버레이 (시간 범위 + RGBA 비트맵)
pub struct SubtitleOverlay {
    /// 표시 시작 시간 (ms)
    pub start_ms: i64,
    /// 표시 끝 시간 (ms)
    pub end_ms: i64,
    /// 비트맵 좌상단 X (비디오 프레임 기준)
    pub x: i32,
    /// 비트맵 좌상단 Y
    pub y: i32,
    /// 비트맵 너비
    pub width: u32,
    /// 비트맵 높이
    pub height: u32,
    /// RGBA 비트맵 데이터 (width * height * 4 bytes)
    pub rgba_data: Vec<u8>,
}

/// 자막 오버레이 목록 (FFI에서 생성/해제)
pub struct SubtitleOverlayList {
    pub overlays: Vec<SubtitleOverlay>,
}

impl SubtitleOverlayList {
    pub fn new() -> Self {
        Self { overlays: Vec::new() }
    }

    /// 특정 시간에 활성인 오버레이 찾기
    pub fn get_active(&self, timestamp_ms: i64) -> Option<&SubtitleOverlay> {
        self.overlays.iter().find(|o| timestamp_ms >= o.start_ms && timestamp_ms < o.end_ms)
    }
}

/// RGBA 프레임 위에 RGBA 자막 오버레이를 알파 블렌딩
/// frame_rgba: 비디오 프레임 (width * height * 4), 결과가 in-place로 기록됨
pub fn blend_overlay_rgba(
    frame_rgba: &mut [u8],
    frame_width: u32,
    frame_height: u32,
    overlay: &SubtitleOverlay,
) {
    let fw = frame_width as i32;
    let fh = frame_height as i32;
    let ow = overlay.width as i32;
    let oh = overlay.height as i32;

    for oy in 0..oh {
        let fy = overlay.y + oy;
        if fy < 0 || fy >= fh { continue; }

        for ox in 0..ow {
            let fx = overlay.x + ox;
            if fx < 0 || fx >= fw { continue; }

            let overlay_idx = ((oy * ow + ox) * 4) as usize;
            let frame_idx = ((fy * fw + fx) * 4) as usize;

            if overlay_idx + 3 >= overlay.rgba_data.len() { continue; }
            if frame_idx + 3 >= frame_rgba.len() { continue; }

            let sa = overlay.rgba_data[overlay_idx + 3] as u32;
            if sa == 0 { continue; } // 완전 투명 — 스킵

            let sr = overlay.rgba_data[overlay_idx] as u32;
            let sg = overlay.rgba_data[overlay_idx + 1] as u32;
            let sb = overlay.rgba_data[overlay_idx + 2] as u32;

            if sa == 255 {
                // 완전 불투명 — 직접 복사
                frame_rgba[frame_idx] = sr as u8;
                frame_rgba[frame_idx + 1] = sg as u8;
                frame_rgba[frame_idx + 2] = sb as u8;
                frame_rgba[frame_idx + 3] = 255;
            } else {
                // 알파 블렌딩: out = src * alpha + dst * (1 - alpha)
                let da = 255 - sa;
                let dr = frame_rgba[frame_idx] as u32;
                let dg = frame_rgba[frame_idx + 1] as u32;
                let db = frame_rgba[frame_idx + 2] as u32;

                frame_rgba[frame_idx] = ((sr * sa + dr * da) / 255) as u8;
                frame_rgba[frame_idx + 1] = ((sg * sa + dg * da) / 255) as u8;
                frame_rgba[frame_idx + 2] = ((sb * sa + db * da) / 255) as u8;
                frame_rgba[frame_idx + 3] = 255;
            }
        }
    }
}

/// YUV420P → RGBA 변환 (자막 블렌딩용)
pub fn yuv420p_to_rgba(yuv_data: &[u8], width: u32, height: u32) -> Vec<u8> {
    let w = width as usize;
    let h = height as usize;
    let y_size = w * h;
    let uv_size = (w / 2) * (h / 2);

    if yuv_data.len() < y_size + uv_size * 2 {
        // 데이터 부족 — 검은 RGBA 프레임 반환
        return vec![0u8; w * h * 4];
    }

    let y_plane = &yuv_data[..y_size];
    let u_plane = &yuv_data[y_size..y_size + uv_size];
    let v_plane = &yuv_data[y_size + uv_size..];

    let mut rgba = vec![0u8; w * h * 4];

    for row in 0..h {
        for col in 0..w {
            let y_val = y_plane[row * w + col] as i32;
            let u_val = u_plane[(row / 2) * (w / 2) + col / 2] as i32 - 128;
            let v_val = v_plane[(row / 2) * (w / 2) + col / 2] as i32 - 128;

            let r = (y_val + ((359 * v_val) >> 8)).clamp(0, 255);
            let g = (y_val - ((88 * u_val + 183 * v_val) >> 8)).clamp(0, 255);
            let b = (y_val + ((454 * u_val) >> 8)).clamp(0, 255);

            let idx = (row * w + col) * 4;
            rgba[idx] = r as u8;
            rgba[idx + 1] = g as u8;
            rgba[idx + 2] = b as u8;
            rgba[idx + 3] = 255;
        }
    }

    rgba
}

/// RGBA → YUV420P 변환 (블렌딩 후 인코딩용)
pub fn rgba_to_yuv420p(rgba: &[u8], width: u32, height: u32) -> Vec<u8> {
    let w = width as usize;
    let h = height as usize;
    let y_size = w * h;
    let uv_size = (w / 2) * (h / 2);

    let mut yuv = vec![0u8; y_size + uv_size * 2];

    // Y plane (BT.601)
    for row in 0..h {
        for col in 0..w {
            let idx = (row * w + col) * 4;
            let r = rgba[idx] as i32;
            let g = rgba[idx + 1] as i32;
            let b = rgba[idx + 2] as i32;
            let y = ((66 * r + 129 * g + 25 * b + 128) >> 8) + 16;
            yuv[row * w + col] = y.clamp(16, 235) as u8;
        }
    }

    // U, V planes (2x2 서브샘플링, BT.601)
    let u_offset = y_size;
    let v_offset = y_size + uv_size;

    for row in (0..h).step_by(2) {
        for col in (0..w).step_by(2) {
            let mut r_sum = 0i32;
            let mut g_sum = 0i32;
            let mut b_sum = 0i32;

            // 2x2 블록 평균
            for dy in 0..2 {
                for dx in 0..2 {
                    let py = (row + dy).min(h - 1);
                    let px = (col + dx).min(w - 1);
                    let idx = (py * w + px) * 4;
                    r_sum += rgba[idx] as i32;
                    g_sum += rgba[idx + 1] as i32;
                    b_sum += rgba[idx + 2] as i32;
                }
            }

            let r = r_sum / 4;
            let g = g_sum / 4;
            let b = b_sum / 4;

            let uv_idx = (row / 2) * (w / 2) + col / 2;
            let u = ((-38 * r - 74 * g + 112 * b + 128) >> 8) + 128;
            let v = ((112 * r - 94 * g - 18 * b + 128) >> 8) + 128;
            yuv[u_offset + uv_idx] = u.clamp(0, 255) as u8;
            yuv[v_offset + uv_idx] = v.clamp(0, 255) as u8;
        }
    }

    yuv
}
