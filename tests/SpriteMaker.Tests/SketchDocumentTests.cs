using SpriteMaker.Core;

namespace SpriteMaker.Tests;

public class SketchDocumentTests
{
    private const uint Red = 0xFFFF0000u;
    private const uint Blue = 0xFF0000FFu;

    [Fact]
    public void Stroke_PaintsAlongLine()
    {
        var doc = new SketchDocument(64, 64);
        doc.StrokeLine(10, 10, 50, 10, 3, Red);
        Assert.Equal(Red, doc.Image[10, 10]);
        Assert.Equal(Red, doc.Image[30, 10]);
        Assert.Equal(Red, doc.Image[50, 10]);
        Assert.Equal(0u, doc.Image[30, 20]);
    }

    [Fact]
    public void FloodFill_StopsAtLines()
    {
        var doc = new SketchDocument(32, 32);
        doc.StrokeLine(16, 0, 16, 31, 2, Red);
        int changed = doc.FloodFill(2, 2, Blue);
        Assert.True(changed > 0);
        Assert.Equal(Blue, doc.Image[2, 30]);
        Assert.Equal(0u, doc.Image[30, 2]);
    }

    [Fact]
    public void FloodFill_SameColorIsNoop()
    {
        var doc = new SketchDocument(8, 8);
        Assert.Equal(0, doc.FloodFill(1, 1, 0u));
    }

    [Fact]
    public void UndoRedo_RestoresState()
    {
        var doc = new SketchDocument(16, 16);
        doc.Checkpoint();
        doc.StrokeLine(1, 1, 1, 1, 1, Red);
        Assert.False(doc.IsEmpty);
        Assert.True(doc.Undo());
        Assert.True(doc.IsEmpty);
        Assert.True(doc.Redo());
        Assert.Equal(Red, doc.Image[1, 1]);
    }

    [Fact]
    public void Load_FitsAndCenters()
    {
        var doc = new SketchDocument(100, 100);
        var src = new PixelImage(10, 20);
        Array.Fill(src.Pixels, Red);
        doc.Load(src);
        var b = doc.Image.OpaqueBounds()!.Value;
        Assert.Equal(100, b.Height);
        Assert.Equal(50, b.Width);
        Assert.Equal(25, b.X);
    }
}
