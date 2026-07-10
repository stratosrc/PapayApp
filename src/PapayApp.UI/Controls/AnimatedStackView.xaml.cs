namespace PapayApp.UI.Controls;

public partial class AnimatedStackView : ContentView
{
    private const uint AnimationLength = 2000;
    private const uint AnimationRate = 16;

    private bool _isAnimating;
    private int _animationRunId;

    public AnimatedStackView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SetCardPerspective();
    }

    private void SetCardPerspective()
    {
        PositionCard(BackCard, 44, 0.92);
        PositionCard(MiddleCard, 0, 0.96);
        PositionCard(FrontCard, -44, 1);
    }

    private static void PositionCard(VisualElement card, double y, double scale)
    {
        card.RotationX = 0;
        card.Rotation = 45;
        card.TranslationY = y;
        card.Scale = scale;
        card.AnchorX = 0.5;
        card.AnchorY = 0.5;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        if (_isAnimating)
        {
            return;
        }

        _isAnimating = true;
        _animationRunId++;
        _ = StartAnimationLoopAsync(_animationRunId);
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        _isAnimating = false;
        this.AbortAnimation("FrontCard");
        this.AbortAnimation("MiddleCard");
        this.AbortAnimation("BackCard");
    }

    private async Task StartAnimationLoopAsync(int runId)
    {
        await Task.WhenAll(
            StartCardAnimationAsync(FrontCard, "FrontCard", -44, 1, 0, runId),
            StartCardAnimationAsync(MiddleCard, "MiddleCard", 0, 0.96, 100, runId),
            StartCardAnimationAsync(BackCard, "BackCard", 44, 0.92, 200, runId));
    }

    private async Task StartCardAnimationAsync(
        VisualElement card,
        string animationName,
        double baseY,
        double baseScale,
        int delay,
        int runId)
    {
        await Task.Delay(delay);

        if (!_isAnimating || runId != _animationRunId)
        {
            return;
        }

        var animation = new Animation(progress =>
        {
            var frame = GetFrame(progress);
            card.TranslationY = baseY + frame.YOffset;
            card.Opacity = frame.Opacity;
            card.Scale = baseScale * frame.Scale;
        });

        animation.Commit(
            this,
            animationName,
            AnimationRate,
            AnimationLength,
            Easing.Linear,
            (_, _) => ResetCard(card, baseY, baseScale),
            () => _isAnimating && runId == _animationRunId);
    }

    private static void ResetCard(VisualElement card, double baseY, double baseScale)
    {
        card.TranslationY = baseY;
        card.Opacity = 1;
        card.Scale = baseScale;
    }

    private static AnimationFrame GetFrame(double progress)
    {
        var frames = new[]
        {
            new AnimationFrame(0.00, 0, 1, 1),
            new AnimationFrame(0.11, 6, 1, 1.02),
            new AnimationFrame(0.34, 118, 0, 0.82),
            new AnimationFrame(0.48, -118, 0, 0.82),
            new AnimationFrame(0.57, 0, 1, 1),
            new AnimationFrame(0.61, 26, 1, 1.02),
            new AnimationFrame(0.74, -9, 1, 0.99),
            new AnimationFrame(0.87, 3, 1, 1),
            new AnimationFrame(1.00, 0, 1, 1)
        };

        for (var i = 0; i < frames.Length - 1; i++)
        {
            var start = frames[i];
            var end = frames[i + 1];

            if (progress < start.Percent || progress > end.Percent)
            {
                continue;
            }

            var localProgress = (progress - start.Percent) / (end.Percent - start.Percent);
            var easedProgress = EaseFrame(start.Percent, end.Percent, localProgress);

            return new AnimationFrame(
                progress,
                Lerp(start.YOffset, end.YOffset, easedProgress),
                Lerp(start.Opacity, end.Opacity, easedProgress),
                Lerp(start.Scale, end.Scale, easedProgress));
        }

        return frames[^1];
    }

    private static double EaseFrame(double startPercent, double endPercent, double progress)
    {
        if (startPercent >= 0.11 && endPercent <= 0.34)
        {
            return Easing.CubicIn.Ease(progress);
        }

        if (startPercent >= 0.48 && endPercent <= 0.57)
        {
            return Easing.CubicOut.Ease(progress);
        }

        if (startPercent >= 0.57)
        {
            return Easing.SpringOut.Ease(progress);
        }

        return Easing.CubicInOut.Ease(progress);
    }

    private static double Lerp(double start, double end, double progress)
    {
        return start + ((end - start) * progress);
    }

    private readonly record struct AnimationFrame(double Percent, double YOffset, double Opacity, double Scale);
}
