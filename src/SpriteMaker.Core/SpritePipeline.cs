namespace SpriteMaker.Core;

public sealed record GenerationResult(
    PixelImage Sprite,
    BodyLayout Layout,
    CharacterViews Views,
    AnimationSet Animations);

/// <summary>스케치 → 도트 스프라이트 → 부위 분석 → 방향별 뷰 → 애니메이션.</summary>
public static class SpritePipeline
{
    public static PixelImage Pixelize(PixelImage sketch, PixelizeOptions options) =>
        Pixelizer.Pixelize(sketch, options);

    public static GenerationResult Generate(
        PixelImage sprite,
        AnimationOptions animationOptions,
        BodyLayout? layoutOverride = null)
    {
        var layout = (layoutOverride ?? BodyAnalyzer.Detect(sprite)).ClampTo(sprite);
        var views = ViewBuilder.Build(sprite, layout);
        var animations = Animator.Generate(views, animationOptions);
        return new GenerationResult(sprite, layout, views, animations);
    }

    public static GenerationResult Generate(
        PixelImage sketch,
        PixelizeOptions pixelizeOptions,
        AnimationOptions animationOptions,
        BodyLayout? layoutOverride = null) =>
        Generate(Pixelize(sketch, pixelizeOptions), animationOptions, layoutOverride);
}
