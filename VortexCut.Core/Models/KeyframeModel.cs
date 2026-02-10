namespace VortexCut.Core.Models;

/// <summary>
/// 키프레임 보간 타입 (After Effects 스타일)
/// </summary>
public enum InterpolationType
{
    Linear,      // 선형 보간
    Bezier,      // 베지어 곡선 (Direction Handles 사용)
    EaseIn,      // 가속 (Ease In)
    EaseOut,     // 감속 (Ease Out)
    EaseInOut,   // 가속+감속 (Ease In-Out)
    Hold         // 계단식 (다음 키프레임까지 값 유지)
}

/// <summary>
/// 베지어 핸들 (After Effects Direction Handle)
/// </summary>
public class BezierHandle
{
    public double TimeOffset { get; set; }  // 시간 오프셋 (초)
    public double ValueOffset { get; set; } // 값 오프셋

    public BezierHandle(double timeOffset = 0, double valueOffset = 0)
    {
        TimeOffset = timeOffset;
        ValueOffset = valueOffset;
    }
}

/// <summary>
/// 키프레임 (단일 키프레임)
/// </summary>
public class Keyframe
{
    public double Time { get; set; }  // 시간 (초 단위)
    public double Value { get; set; } // 속성 값
    public InterpolationType Interpolation { get; set; } = InterpolationType.Linear;

    // 베지어 핸들 (Direction Handles)
    public BezierHandle? InHandle { get; set; }   // 진입 핸들
    public BezierHandle? OutHandle { get; set; }  // 진출 핸들

    public Keyframe(double time, double value, InterpolationType interpolation = InterpolationType.Linear)
    {
        Time = time;
        Value = value;
        Interpolation = interpolation;
    }
}

/// <summary>
/// 키프레임 시스템 (애니메이션 관리)
/// </summary>
public class KeyframeSystem
{
    public List<Keyframe> Keyframes { get; } = new List<Keyframe>();

    /// <summary>
    /// 키프레임 추가됨 이벤트
    /// </summary>
    public event Action<Keyframe>? OnKeyframeAdded;

    /// <summary>
    /// 키프레임 제거됨 이벤트
    /// </summary>
    public event Action<Keyframe>? OnKeyframeRemoved;

    /// <summary>
    /// 키프레임 추가
    /// </summary>
    public void AddKeyframe(double time, double value, InterpolationType interpolation = InterpolationType.Linear)
    {
        var keyframe = new Keyframe(time, value, interpolation);
        Keyframes.Add(keyframe);
        Keyframes.Sort((a, b) => a.Time.CompareTo(b.Time)); // 시간순 정렬
        OnKeyframeAdded?.Invoke(keyframe);
    }

    /// <summary>
    /// 키프레임 제거
    /// </summary>
    public void RemoveKeyframe(Keyframe keyframe)
    {
        if (Keyframes.Remove(keyframe))
        {
            OnKeyframeRemoved?.Invoke(keyframe);
        }
    }

    /// <summary>
    /// 키프레임 업데이트 (시간/값 변경)
    /// </summary>
    public void UpdateKeyframe(Keyframe keyframe, double newTime, double newValue)
    {
        keyframe.Time = newTime;
        keyframe.Value = newValue;
        Keyframes.Sort((a, b) => a.Time.CompareTo(b.Time)); // 재정렬
    }

    /// <summary>
    /// 모든 키프레임 제거
    /// </summary>
    public void ClearKeyframes()
    {
        var keyframesToRemove = Keyframes.ToList();
        Keyframes.Clear();

        foreach (var kf in keyframesToRemove)
        {
            OnKeyframeRemoved?.Invoke(kf);
        }
    }

    /// <summary>
    /// 주어진 시간의 보간된 값 계산
    /// </summary>
    public double Interpolate(double currentTime)
    {
        if (Keyframes.Count == 0)
            return 0;

        if (Keyframes.Count == 1)
            return Keyframes[0].Value;

        // 범위 밖: 첫/마지막 키프레임 값 반환
        if (currentTime <= Keyframes[0].Time)
            return Keyframes[0].Value;

        if (currentTime >= Keyframes[^1].Time)
            return Keyframes[^1].Value;

        // 두 키프레임 사이에서 보간
        for (int i = 0; i < Keyframes.Count - 1; i++)
        {
            var kf1 = Keyframes[i];
            var kf2 = Keyframes[i + 1];

            if (currentTime >= kf1.Time && currentTime <= kf2.Time)
            {
                double t = (currentTime - kf1.Time) / (kf2.Time - kf1.Time);

                return kf1.Interpolation switch
                {
                    InterpolationType.Linear => InterpolateLinear(kf1.Value, kf2.Value, t),
                    InterpolationType.EaseIn => InterpolateEaseIn(kf1.Value, kf2.Value, t),
                    InterpolationType.EaseOut => InterpolateEaseOut(kf1.Value, kf2.Value, t),
                    InterpolationType.EaseInOut => InterpolateEaseInOut(kf1.Value, kf2.Value, t),
                    InterpolationType.Hold => kf1.Value, // 계단식
                    InterpolationType.Bezier => InterpolateBezier(kf1, kf2, t),
                    _ => InterpolateLinear(kf1.Value, kf2.Value, t)
                };
            }
        }

        return Keyframes[^1].Value;
    }

    /// <summary>
    /// 선형 보간
    /// </summary>
    private double InterpolateLinear(double v1, double v2, double t)
    {
        return v1 + t * (v2 - v1);
    }

    /// <summary>
    /// Ease In 보간 (가속)
    /// </summary>
    private double InterpolateEaseIn(double v1, double v2, double t)
    {
        return v1 + Math.Pow(t, 2) * (v2 - v1);
    }

    /// <summary>
    /// Ease Out 보간 (감속)
    /// </summary>
    private double InterpolateEaseOut(double v1, double v2, double t)
    {
        return v1 + (1 - Math.Pow(1 - t, 2)) * (v2 - v1);
    }

    /// <summary>
    /// Ease In-Out 보간 (가속+감속)
    /// </summary>
    private double InterpolateEaseInOut(double v1, double v2, double t)
    {
        double easedT = t < 0.5
            ? 2 * t * t
            : 1 - Math.Pow(-2 * t + 2, 2) / 2;

        return v1 + easedT * (v2 - v1);
    }

    /// <summary>
    /// 베지어 보간 (Direction Handles 사용)
    /// Cubic Bezier: P0 (kf1) → P1 (OutHandle) → P2 (InHandle) → P3 (kf2)
    /// </summary>
    private double InterpolateBezier(Keyframe kf1, Keyframe kf2, double t)
    {
        double p0 = kf1.Value;
        double p3 = kf2.Value;

        // P1: OutHandle이 있으면 사용, 없으면 1/3 지점
        double p1 = kf1.OutHandle != null
            ? kf1.Value + kf1.OutHandle.ValueOffset
            : p0 + (p3 - p0) / 3;

        // P2: InHandle이 있으면 사용, 없으면 2/3 지점
        double p2 = kf2.InHandle != null
            ? kf2.Value + kf2.InHandle.ValueOffset
            : p3 - (p3 - p0) / 3;

        // Cubic Bezier 공식
        return Math.Pow(1 - t, 3) * p0 +
               3 * Math.Pow(1 - t, 2) * t * p1 +
               3 * (1 - t) * Math.Pow(t, 2) * p2 +
               Math.Pow(t, 3) * p3;
    }
}
