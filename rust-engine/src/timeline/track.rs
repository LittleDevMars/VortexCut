// 트랙 모듈 - 클립들을 담는 레이어

use super::clip::{VideoClip, AudioClip};

/// 비디오 트랙
#[derive(Debug, Clone)]
pub struct VideoTrack {
    pub id: u64,
    pub index: usize,  // 트랙 순서 (0 = 최하단)
    pub clips: Vec<VideoClip>,
    pub enabled: bool,
}

impl VideoTrack {
    /// 새 비디오 트랙 생성
    pub fn new(id: u64, index: usize) -> Self {
        Self {
            id,
            index,
            clips: Vec::new(),
            enabled: true,
        }
    }

    /// 클립 추가
    pub fn add_clip(&mut self, clip: VideoClip) {
        self.clips.push(clip);
        // 시작 시간 기준으로 정렬
        self.clips.sort_by_key(|c| c.start_time_ms);
    }

    /// 클립 제거
    pub fn remove_clip(&mut self, clip_id: u64) -> Option<VideoClip> {
        if let Some(index) = self.clips.iter().position(|c| c.id == clip_id) {
            Some(self.clips.remove(index))
        } else {
            None
        }
    }

    /// 특정 시간에 활성화된 클립 찾기
    pub fn get_clip_at_time(&self, time_ms: i64) -> Option<&VideoClip> {
        if !self.enabled {
            return None;
        }

        self.clips.iter().find(|clip| clip.contains_time(time_ms))
    }

    /// 클립 ID로 찾기
    pub fn get_clip_by_id(&self, clip_id: u64) -> Option<&VideoClip> {
        self.clips.iter().find(|c| c.id == clip_id)
    }

    /// 클립 ID로 찾기 (mutable)
    pub fn get_clip_by_id_mut(&mut self, clip_id: u64) -> Option<&mut VideoClip> {
        self.clips.iter_mut().find(|c| c.id == clip_id)
    }
}

/// 오디오 트랙
#[derive(Debug, Clone)]
pub struct AudioTrack {
    pub id: u64,
    pub index: usize,
    pub clips: Vec<AudioClip>,
    pub enabled: bool,
    pub muted: bool,
}

impl AudioTrack {
    /// 새 오디오 트랙 생성
    pub fn new(id: u64, index: usize) -> Self {
        Self {
            id,
            index,
            clips: Vec::new(),
            enabled: true,
            muted: false,
        }
    }

    /// 클립 추가
    pub fn add_clip(&mut self, clip: AudioClip) {
        self.clips.push(clip);
        self.clips.sort_by_key(|c| c.start_time_ms);
    }

    /// 클립 제거
    pub fn remove_clip(&mut self, clip_id: u64) -> Option<AudioClip> {
        if let Some(index) = self.clips.iter().position(|c| c.id == clip_id) {
            Some(self.clips.remove(index))
        } else {
            None
        }
    }

    /// 특정 시간에 활성화된 클립들 찾기 (오디오는 여러 클립 동시 재생 가능)
    pub fn get_clips_at_time(&self, time_ms: i64) -> Vec<&AudioClip> {
        if !self.enabled || self.muted {
            return Vec::new();
        }

        self.clips
            .iter()
            .filter(|clip| clip.contains_time(time_ms))
            .collect()
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::path::PathBuf;

    #[test]
    fn test_video_track_add_clip() {
        let mut track = VideoTrack::new(1, 0);

        let clip1 = VideoClip::new(1, PathBuf::from("test1.mp4"), 0, 5000);
        let clip2 = VideoClip::new(2, PathBuf::from("test2.mp4"), 5000, 3000);

        track.add_clip(clip1);
        track.add_clip(clip2);

        assert_eq!(track.clips.len(), 2);
        assert_eq!(track.clips[0].id, 1);
        assert_eq!(track.clips[1].id, 2);
    }

    #[test]
    fn test_video_track_remove_clip() {
        let mut track = VideoTrack::new(1, 0);
        let clip = VideoClip::new(1, PathBuf::from("test.mp4"), 0, 5000);
        track.add_clip(clip);

        assert_eq!(track.clips.len(), 1);

        let removed = track.remove_clip(1);
        assert!(removed.is_some());
        assert_eq!(track.clips.len(), 0);

        let not_found = track.remove_clip(999);
        assert!(not_found.is_none());
    }

    #[test]
    fn test_video_track_get_clip_at_time() {
        let mut track = VideoTrack::new(1, 0);

        let clip1 = VideoClip::new(1, PathBuf::from("test1.mp4"), 0, 5000);
        let clip2 = VideoClip::new(2, PathBuf::from("test2.mp4"), 5000, 3000);

        track.add_clip(clip1);
        track.add_clip(clip2);

        let clip_at_2000 = track.get_clip_at_time(2000);
        assert!(clip_at_2000.is_some());
        assert_eq!(clip_at_2000.unwrap().id, 1);

        let clip_at_6000 = track.get_clip_at_time(6000);
        assert!(clip_at_6000.is_some());
        assert_eq!(clip_at_6000.unwrap().id, 2);

        let clip_at_9000 = track.get_clip_at_time(9000);
        assert!(clip_at_9000.is_none());
    }

    #[test]
    fn test_track_disabled() {
        let mut track = VideoTrack::new(1, 0);
        let clip = VideoClip::new(1, PathBuf::from("test.mp4"), 0, 5000);
        track.add_clip(clip);

        track.enabled = false;

        let result = track.get_clip_at_time(2000);
        assert!(result.is_none());
    }
}
