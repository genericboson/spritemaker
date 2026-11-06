using SpriteMaker.Core;

namespace SpriteMaker.Tests;

public class PipelineTests
{
    private static readonly string[] ExpectedClips =
    [
        "idle_down", "idle_up", "idle_right", "idle_left",
        "walk_down", "walk_up", "walk_right", "walk_left",
        "attack_down", "attack_up", "attack_right", "attack_left",
    ];

    [Theory]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(32)]
    [InlineData(48)]
    public void Pixelize_HitsTargetHeight(int height)
    {
        var sprite = Pixelizer.Pixelize(SampleSketch.Create(), new PixelizeOptions { TargetHeight = height });
        Assert.Equal(height, sprite.Height);
        Assert.InRange(sprite.Width, height / 2, height * 2);
    }

    [Fact]
    public void Pixelize_RespectsPaletteLimit()
    {
        var sprite = Pixelizer.Pixelize(SampleSketch.Create(), new PixelizeOptions { MaxColors = 6, AddOutline = false });
        int distinct = sprite.Pixels.Where(ColorUtil.IsOpaque).Distinct().Count();
        Assert.InRange(distinct, 2, 6);
    }

    [Fact]
    public void Pixelize_LineArtGetsFilled()
    {
        var sprite = Pixelizer.Pixelize(SampleSketch.Create(lineArtOnly: true), new PixelizeOptions { AddOutline = false });
        var bounds = sprite.OpaqueBounds()!.Value;
        // 닫힌 선 안쪽이 채워져 실루엣 면적의 대부분이 불투명해야 한다
        double fill = (double)sprite.CountOpaque() / (bounds.Width * bounds.Height);
        Assert.True(fill > 0.5, $"채움 비율 {fill:0.00}");
    }

    [Fact]
    public void Pixelize_EmptySketchThrows()
    {
        Assert.Throws<InvalidOperationException>(() => Pixelizer.Pixelize(new PixelImage(64, 64), new PixelizeOptions()));
    }

    [Fact]
    public void BodyAnalyzer_FindsNeckAndCrotch()
    {
        var sprite = Pixelizer.Pixelize(SampleSketch.Create(), new PixelizeOptions { TargetHeight = 32 });
        var layout = BodyAnalyzer.Detect(sprite);
        // 샘플: 머리 30~240 / 몸통 250~380 / 다리 380~477 (512 기준, 전체 높이 약 447)
        Assert.InRange(layout.HeadBottom, 13, 18);
        Assert.InRange(layout.HipY, 22, 27);
        Assert.InRange(layout.CenterX, sprite.Width / 2 - 2, sprite.Width / 2 + 2);
    }

    [Fact]
    public void Back_HidesFaceWithHair()
    {
        var sprite = Pixelizer.Pixelize(SampleSketch.Create(), new PixelizeOptions { TargetHeight = 32 });
        var result = SpritePipeline.Generate(sprite, new AnimationOptions());
        var backHead = result.Views.Back.Head.Image;
        // 앞모습 머리에는 눈(잉크) 이 있지만 뒷모습 안쪽에는 없어야 한다
        bool HasInkInside(PixelImage img) =>
            Enumerable.Range(0, img.Height).Any(y => Enumerable.Range(0, img.Width).Any(x =>
                img.IsOpaqueAt(x, y) && !Outliner.IsEdge(img, x, y) && ColorUtil.Luminance(img[x, y]) < 0.2));
        Assert.True(HasInkInside(result.Views.Front.Head.Image));
        Assert.False(HasInkInside(backHead));
    }

    [Fact]
    public void Generate_ProducesAllClipsWithConsistentCells()
    {
        var result = SpritePipeline.Generate(SampleSketch.Create(), new PixelizeOptions(), new AnimationOptions());
        var set = result.Animations;
        Assert.Equal(ExpectedClips, set.Clips.Select(c => c.Name));
        foreach (var clip in set.Clips)
        {
            Assert.NotEmpty(clip.Frames);
            Assert.All(clip.Frames, f => { Assert.Equal(set.CellSize, f.Width); Assert.Equal(set.CellSize, f.Height); });
        }
        Assert.All(set.Clips.Where(c => c.Name.StartsWith("walk") || c.Name.StartsWith("attack")),
            c => Assert.Equal(4, c.Frames.Count));
    }

    [Fact]
    public void Walk_FramesActuallyDiffer()
    {
        var set = SpritePipeline.Generate(SampleSketch.Create(), new PixelizeOptions(), new AnimationOptions()).Animations;
        foreach (var name in new[] { "walk_down", "walk_up", "walk_right", "walk_left" })
        {
            var frames = set.Get(name).Frames;
            Assert.NotEqual(frames[0].Pixels, frames[1].Pixels);
            Assert.NotEqual(frames[1].Pixels, frames[3].Pixels);
        }
    }

    [Fact]
    public void Walk_FeetStayOnBaseline()
    {
        var set = SpritePipeline.Generate(SampleSketch.Create(), new PixelizeOptions(), new AnimationOptions()).Animations;
        foreach (var name in new[] { "idle_down", "walk_down", "walk_up", "walk_right" })
        {
            // 서 있는 첫 프레임의 가장 아래 불투명 행이 발 기준선이어야 한다
            var bounds = set.Get(name).Frames[0].OpaqueBounds()!.Value;
            Assert.Equal(set.FootY, bounds.Bottom);
        }
    }

    [Fact]
    public void Left_IsMirrorOfRight()
    {
        var set = SpritePipeline.Generate(SampleSketch.Create(), new PixelizeOptions(), new AnimationOptions()).Animations;
        var right = set.Get("walk_right").Frames;
        var left = set.Get("walk_left").Frames;
        for (int i = 0; i < right.Count; i++)
            Assert.Equal(right[i].FlipHorizontal().Pixels, left[i].Pixels);
    }

    [Fact]
    public void Attack_StrikeFrameHasSlash()
    {
        var options = new AnimationOptions();
        var set = SpritePipeline.Generate(SampleSketch.Create(), new PixelizeOptions(), options).Animations;
        foreach (var name in new[] { "attack_down", "attack_up", "attack_right", "attack_left" })
        {
            var clip = set.Get(name);
            Assert.False(clip.Loop);
            Assert.Contains(options.SlashColor, clip.Frames[1].Pixels);
            Assert.DoesNotContain(options.SlashColor, clip.Frames[0].Pixels);
        }
    }

    [Fact]
    public void Export_WritesGodotFiles()
    {
        var set = SpritePipeline.Generate(SampleSketch.Create(), new PixelizeOptions(), new AnimationOptions()).Animations;
        string dir = Environment.GetEnvironmentVariable("SPRITEMAKER_SAMPLE_OUT")
                     ?? Path.Combine(Path.GetTempPath(), "spritemaker_test_" + Guid.NewGuid().ToString("N"));
        var result = GodotExporter.Export(set, dir, "hero");
        PngEncoder.Save(SampleSketch.Create(), Path.Combine(dir, "hero_sketch.png"));

        var png = File.ReadAllBytes(result.PngPath);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png[..4]);

        string tres = File.ReadAllText(result.FramesPath);
        Assert.StartsWith("[gd_resource type=\"SpriteFrames\"", tres);
        foreach (var name in ExpectedClips) Assert.Contains($"\"name\": &\"{name}\"", tres);
        Assert.Contains("path=\"hero.png\"", tres);

        string scene = File.ReadAllText(result.ScenePath);
        Assert.Contains("type=\"AnimatedSprite2D\"", scene);
        Assert.True(File.Exists(result.MetaPath));
    }

    [Fact]
    public void Png_RoundTripsSize()
    {
        var img = new PixelImage(3, 2);
        img[1, 1] = 0xFFFF0000u;
        var bytes = PngEncoder.Encode(img);
        // IHDR 의 width/height (빅엔디언)
        Assert.Equal(3, bytes[16] << 24 | bytes[17] << 16 | bytes[18] << 8 | bytes[19]);
        Assert.Equal(2, bytes[20] << 24 | bytes[21] << 16 | bytes[22] << 8 | bytes[23]);
    }
}
