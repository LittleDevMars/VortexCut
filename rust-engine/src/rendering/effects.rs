// 이펙트 엔진 — RGBA 픽셀 연산 (Brightness, Contrast, Saturation, Temperature)

use std::collections::HashMap;

/// 클립별 이펙트 파라미터 (-1.0 ~ 1.0, 0=원본)
#[derive(Debug, Clone)]
pub struct EffectParams {
    pub brightness: f32,
    pub contrast: f32,
    pub saturation: f32,
    pub temperature: f32,
}

impl Default for EffectParams {
    fn default() -> Self {
        Self {
            brightness: 0.0,
            contrast: 0.0,
            saturation: 0.0,
            temperature: 0.0,
        }
    }
}

impl EffectParams {
    /// 모든 값이 기본값(0)인지 확인 — true이면 이펙트 연산 건너뜀
    pub fn is_default(&self) -> bool {
        self.brightness.abs() < 0.001
            && self.contrast.abs() < 0.001
            && self.saturation.abs() < 0.001
            && self.temperature.abs() < 0.001
    }
}

/// 클립별 이펙트 저장소
pub type EffectStore = HashMap<u64, EffectParams>;

/// RGBA 버퍼에 이펙트 적용 (in-place)
/// data: RGBA 픽셀 배열 (4 bytes per pixel)
pub fn apply_effects(data: &mut [u8], width: u32, height: u32, params: &EffectParams) {
    if params.is_default() {
        return;
    }

    let pixel_count = (width * height) as usize;
    if data.len() < pixel_count * 4 {
        return;
    }

    let brightness_offset = params.brightness * 255.0;
    let contrast_factor = 1.0 + params.contrast;
    let saturation_factor = 1.0 + params.saturation;

    // Temperature: warm(+) = R+, B-, cool(-) = R-, B+
    let temp_r = params.temperature * 30.0;
    let temp_b = -params.temperature * 30.0;

    for i in 0..pixel_count {
        let idx = i * 4;
        let mut r = data[idx] as f32;
        let mut g = data[idx + 1] as f32;
        let mut b = data[idx + 2] as f32;
        // Alpha (idx+3) 는 변경하지 않음

        // 1. Brightness: 단순 오프셋
        if brightness_offset.abs() > 0.1 {
            r += brightness_offset;
            g += brightness_offset;
            b += brightness_offset;
        }

        // 2. Contrast: 128 기준 스케일링
        if (contrast_factor - 1.0).abs() > 0.001 {
            r = 128.0 + (r - 128.0) * contrast_factor;
            g = 128.0 + (g - 128.0) * contrast_factor;
            b = 128.0 + (b - 128.0) * contrast_factor;
        }

        // 3. Saturation: luminance 기준 조정
        if (saturation_factor - 1.0).abs() > 0.001 {
            // BT.709 가중치
            let lum = 0.2126 * r + 0.7152 * g + 0.0722 * b;
            r = lum + (r - lum) * saturation_factor;
            g = lum + (g - lum) * saturation_factor;
            b = lum + (b - lum) * saturation_factor;
        }

        // 4. Temperature: R/B 채널 오프셋
        if temp_r.abs() > 0.1 {
            r += temp_r;
            b += temp_b;
        }

        // Clamp 0-255
        data[idx] = r.clamp(0.0, 255.0) as u8;
        data[idx + 1] = g.clamp(0.0, 255.0) as u8;
        data[idx + 2] = b.clamp(0.0, 255.0) as u8;
    }
}
