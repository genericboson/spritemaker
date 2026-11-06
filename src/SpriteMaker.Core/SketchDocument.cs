namespace SpriteMaker.Core;

/// <summary>그림판 캔버스의 편집 로직(브러시, 채우기, 되돌리기). UI 와 무관하게 테스트할 수 있다.</summary>
public sealed class SketchDocument
{
    private const int MaxHistory = 40;
    private readonly Stack<uint[]> _undo = new();
    private readonly Stack<uint[]> _redo = new();

    public PixelImage Image { get; private set; }

    public SketchDocument(int width = 512, int height = 512)
    {
        Image = new PixelImage(width, height);
    }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public bool IsEmpty => !Image.Pixels.Any(ColorUtil.IsOpaque);

    /// <summary>편집 직전에 호출해 되돌리기 지점을 남긴다.</summary>
    public void Checkpoint()
    {
        _undo.Push((uint[])Image.Pixels.Clone());
        if (_undo.Count > MaxHistory)
        {
            var keep = _undo.Take(MaxHistory).Reverse().ToArray();
            _undo.Clear();
            foreach (var s in keep) _undo.Push(s);
        }
        _redo.Clear();
    }

    public bool Undo()
    {
        if (_undo.Count == 0) return false;
        _redo.Push((uint[])Image.Pixels.Clone());
        Array.Copy(_undo.Pop(), Image.Pixels, Image.Pixels.Length);
        return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0) return false;
        _undo.Push((uint[])Image.Pixels.Clone());
        Array.Copy(_redo.Pop(), Image.Pixels, Image.Pixels.Length);
        return true;
    }

    public void Clear()
    {
        Checkpoint();
        Array.Clear(Image.Pixels);
    }

    /// <summary>다른 이미지를 캔버스에 맞춰(비율 유지, 가운데 정렬) 불러온다.</summary>
    public void Load(PixelImage source)
    {
        Checkpoint();
        Array.Clear(Image.Pixels);
        double scale = Math.Min((double)Image.Width / source.Width, (double)Image.Height / source.Height);
        int w = Math.Max(1, (int)(source.Width * scale)), h = Math.Max(1, (int)(source.Height * scale));
        int ox = (Image.Width - w) / 2, oy = (Image.Height - h) / 2;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                uint c = source[Math.Min(source.Width - 1, (int)(x / scale)), Math.Min(source.Height - 1, (int)(y / scale))];
                Image[ox + x, oy + y] = ColorUtil.IsOpaque(c) ? c | 0xFF000000u : 0u;
            }
    }

    /// <summary>원형 브러시로 선을 긋는다. color 가 투명이면 지우개.</summary>
    public PixelRect StrokeLine(int x0, int y0, int x1, int y1, int size, uint color)
    {
        double radius = Math.Max(0.5, size / 2.0);
        int steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
        for (int i = 0; i <= steps; i++)
        {
            double t = steps == 0 ? 0 : (double)i / steps;
            Stamp(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, radius, color);
        }
        int r = (int)Math.Ceiling(radius) + 1;
        int minX = Math.Max(0, Math.Min(x0, x1) - r), minY = Math.Max(0, Math.Min(y0, y1) - r);
        int maxX = Math.Min(Image.Width, Math.Max(x0, x1) + r + 1), maxY = Math.Min(Image.Height, Math.Max(y0, y1) + r + 1);
        return new PixelRect(minX, minY, Math.Max(0, maxX - minX), Math.Max(0, maxY - minY));
    }

    private void Stamp(double cx, double cy, double radius, uint color)
    {
        int x0 = (int)Math.Floor(cx - radius), x1 = (int)Math.Ceiling(cx + radius);
        int y0 = (int)Math.Floor(cy - radius), y1 = (int)Math.Ceiling(cy + radius);
        double r2 = radius * radius;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if (!Image.InBounds(x, y)) continue;
                double dx = x + 0.5 - (cx + 0.5), dy = y + 0.5 - (cy + 0.5);
                if (dx * dx + dy * dy <= r2) Image[x, y] = color;
            }
    }

    /// <summary>같은 색으로 4방향 연결된 영역을 채운다. 바뀐 픽셀 수를 돌려준다.</summary>
    public int FloodFill(int x, int y, uint color)
    {
        if (!Image.InBounds(x, y)) return 0;
        uint target = Image[x, y];
        if (target == color) return 0;

        int changed = 0;
        var stack = new Stack<(int X, int Y)>();
        stack.Push((x, y));
        while (stack.Count > 0)
        {
            var (px, py) = stack.Pop();
            int left = px;
            while (left > 0 && Image[left - 1, py] == target) left--;
            int right = px;
            while (right < Image.Width - 1 && Image[right + 1, py] == target) right++;
            for (int xx = left; xx <= right; xx++)
            {
                if (Image[xx, py] != target) continue;
                Image[xx, py] = color;
                changed++;
                if (py > 0 && Image[xx, py - 1] == target) stack.Push((xx, py - 1));
                if (py < Image.Height - 1 && Image[xx, py + 1] == target) stack.Push((xx, py + 1));
            }
        }
        return changed;
    }

    public uint PickColor(int x, int y) => Image.InBounds(x, y) ? Image[x, y] : 0u;
}
