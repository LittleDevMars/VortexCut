using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using VortexCut.UI.ViewModels;

namespace VortexCut.UI.Views;

public partial class InspectorView : UserControl
{
    private Slider? _brightnessSlider;
    private Slider? _contrastSlider;
    private Slider? _saturationSlider;
    private Slider? _temperatureSlider;
    private TextBlock? _brightnessValueText;
    private TextBlock? _contrastValueText;
    private TextBlock? _saturationValueText;
    private TextBlock? _temperatureValueText;

    // 프로그래밍적 슬라이더 값 설정 시 이벤트 무시용 플래그
    private bool _isUpdatingSliders;

    public InspectorView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        SetupColorSliders();
    }

    private void SetupColorSliders()
    {
        _brightnessSlider = this.FindControl<Slider>("BrightnessSlider");
        _contrastSlider = this.FindControl<Slider>("ContrastSlider");
        _saturationSlider = this.FindControl<Slider>("SaturationSlider");
        _temperatureSlider = this.FindControl<Slider>("TemperatureSlider");
        _brightnessValueText = this.FindControl<TextBlock>("BrightnessValueText");
        _contrastValueText = this.FindControl<TextBlock>("ContrastValueText");
        _saturationValueText = this.FindControl<TextBlock>("SaturationValueText");
        _temperatureValueText = this.FindControl<TextBlock>("TemperatureValueText");

        // Slider ValueChanged 이벤트 연결
        if (_brightnessSlider != null) _brightnessSlider.ValueChanged += OnEffectSliderChanged;
        if (_contrastSlider != null) _contrastSlider.ValueChanged += OnEffectSliderChanged;
        if (_saturationSlider != null) _saturationSlider.ValueChanged += OnEffectSliderChanged;
        if (_temperatureSlider != null) _temperatureSlider.ValueChanged += OnEffectSliderChanged;

        // Reset 버튼
        var resetButton = this.FindControl<Button>("ResetEffectsButton");
        if (resetButton != null) resetButton.Click += OnResetEffectsClick;

        // SelectedClip 변경 감지 → 슬라이더 값 동기화
        if (DataContext is MainViewModel mainVm)
        {
            mainVm.Timeline.PropertyChanged += OnTimelinePropertyChanged;
        }
    }

    private void OnTimelinePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "SelectedClip")
        {
            SyncSlidersToClip();
        }
    }

    /// <summary>
    /// 선택된 클립의 이펙트 값을 슬라이더에 동기화
    /// </summary>
    private void SyncSlidersToClip()
    {
        _isUpdatingSliders = true;
        try
        {
            var mainVm = DataContext as MainViewModel;
            var clip = mainVm?.Timeline.SelectedClip;

            if (clip == null)
            {
                SetSliderValue(_brightnessSlider, 0);
                SetSliderValue(_contrastSlider, 0);
                SetSliderValue(_saturationSlider, 0);
                SetSliderValue(_temperatureSlider, 0);
            }
            else
            {
                // 클립의 이펙트 값을 슬라이더에 반영 (-1.0~1.0 → -100~100)
                SetSliderValue(_brightnessSlider, clip.Brightness * 100.0);
                SetSliderValue(_contrastSlider, clip.Contrast * 100.0);
                SetSliderValue(_saturationSlider, clip.Saturation * 100.0);
                SetSliderValue(_temperatureSlider, clip.Temperature * 100.0);
            }

            UpdateValueTexts();
        }
        finally
        {
            _isUpdatingSliders = false;
        }
    }

    private static void SetSliderValue(Slider? slider, double value)
    {
        if (slider != null)
            slider.Value = Math.Round(value);
    }

    private void UpdateValueTexts()
    {
        if (_brightnessValueText != null && _brightnessSlider != null)
            _brightnessValueText.Text = ((int)_brightnessSlider.Value).ToString();
        if (_contrastValueText != null && _contrastSlider != null)
            _contrastValueText.Text = ((int)_contrastSlider.Value).ToString();
        if (_saturationValueText != null && _saturationSlider != null)
            _saturationValueText.Text = ((int)_saturationSlider.Value).ToString();
        if (_temperatureValueText != null && _temperatureSlider != null)
            _temperatureValueText.Text = ((int)_temperatureSlider.Value).ToString();
    }

    /// <summary>
    /// 이펙트 슬라이더 값 변경 → Rust에 전달 + 프리뷰 갱신
    /// </summary>
    private void OnEffectSliderChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingSliders) return;

        var mainVm = DataContext as MainViewModel;
        var clip = mainVm?.Timeline.SelectedClip;
        if (clip == null || mainVm == null) return;

        // 슬라이더 값 → ClipModel (-100~100 → -1.0~1.0)
        double brightness = (_brightnessSlider?.Value ?? 0) / 100.0;
        double contrast = (_contrastSlider?.Value ?? 0) / 100.0;
        double saturation = (_saturationSlider?.Value ?? 0) / 100.0;
        double temperature = (_temperatureSlider?.Value ?? 0) / 100.0;

        clip.Brightness = brightness;
        clip.Contrast = contrast;
        clip.Saturation = saturation;
        clip.Temperature = temperature;

        UpdateValueTexts();

        // Rust Renderer에 이펙트 전달 (캐시 클리어 포함)
        mainVm.ProjectService.SetClipEffects(
            clip.Id,
            (float)brightness,
            (float)contrast,
            (float)saturation,
            (float)temperature);

        // 프리뷰 갱신
        mainVm.Preview.RenderFrameAsync(mainVm.Timeline.CurrentTimeMs);
    }

    /// <summary>
    /// Reset All 버튼 → 모든 이펙트 0으로 초기화
    /// </summary>
    private void OnResetEffectsClick(object? sender, RoutedEventArgs e)
    {
        var mainVm = DataContext as MainViewModel;
        var clip = mainVm?.Timeline.SelectedClip;
        if (clip == null || mainVm == null) return;

        clip.Brightness = 0;
        clip.Contrast = 0;
        clip.Saturation = 0;
        clip.Temperature = 0;

        SyncSlidersToClip();

        mainVm.ProjectService.SetClipEffects(clip.Id, 0, 0, 0, 0);
        mainVm.Preview.RenderFrameAsync(mainVm.Timeline.CurrentTimeMs);
    }
}
