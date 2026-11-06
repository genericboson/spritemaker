namespace SpriteMaker.Core;

public enum Facing { Down, Up, Right, Left }

public sealed record AnimationClip(string Name, IReadOnlyList<PixelImage> Frames, double Fps, bool Loop);

public sealed record AnimationOptions
{
    public double WalkFps { get; init; } = 8;
    public double AttackFps { get; init; } = 12;
    public uint SlashColor { get; init; } = 0xFFFFFFFFu;
    public uint SlashEdgeColor { get; init; } = 0xFF9FD8FFu;
}

/// <summary>
/// 모든 프레임은 CellSize×CellSize 정사각형. 발 기준점(FootX, FootY)이 모든 프레임에서 같은 자리여서
/// 고닷에서 이 점을 노드 원점에 맞추면 타일 위에 서 있게 된다.
/// </summary>
public sealed record AnimationSet(IReadOnlyList<AnimationClip> Clips, int CellSize, int FootX, int FootY)
{
    public AnimationClip Get(string name) => Clips.First(c => c.Name == name);
}

public static class Animator
{
    private readonly record struct Pose(int BodyDx = 0, int BodyDy = 0, int FarDx = 0, int FarDy = 0, int NearDx = 0, int NearDy = 0);

    public static AnimationSet Generate(CharacterViews views, AnimationOptions options)
    {
        int h = views.Front.Height;
        int maxW = new[] { views.Front.Width, views.Back.Width, views.Side.Width }.Max();
        int pad = Math.Max(4, h / 3);
        int cell = Math.Max(h, maxW) + pad * 2;
        cell += cell % 2;
        int footX = cell / 2;
        int footY = cell - Math.Max(2, pad / 3);

        var ctx = new Context(cell, footX, footY, h, options);
        var clips = new List<AnimationClip>();

        var walkDown = FrontBackWalk(ctx, views.Front);
        var walkUp = FrontBackWalk(ctx, views.Back);
        var walkRight = SideWalk(ctx, views.Side);
        var walkLeft = walkRight.Select(f => f.FlipHorizontal()).ToList();

        clips.Add(new AnimationClip("idle_down", [walkDown[0]], 1, true));
        clips.Add(new AnimationClip("idle_up", [walkUp[0]], 1, true));
        clips.Add(new AnimationClip("idle_right", [walkRight[1]], 1, true));
        clips.Add(new AnimationClip("idle_left", [walkLeft[1]], 1, true));

        clips.Add(new AnimationClip("walk_down", walkDown, options.WalkFps, true));
        clips.Add(new AnimationClip("walk_up", walkUp, options.WalkFps, true));
        clips.Add(new AnimationClip("walk_right", walkRight, options.WalkFps, true));
        clips.Add(new AnimationClip("walk_left", walkLeft, options.WalkFps, true));

        var attackRight = Attack(ctx, views.Side, Facing.Right);
        clips.Add(new AnimationClip("attack_down", Attack(ctx, views.Front, Facing.Down), options.AttackFps, false));
        clips.Add(new AnimationClip("attack_up", Attack(ctx, views.Back, Facing.Up), options.AttackFps, false));
        clips.Add(new AnimationClip("attack_right", attackRight, options.AttackFps, false));
        clips.Add(new AnimationClip("attack_left", attackRight.Select(f => f.FlipHorizontal()).ToList(), options.AttackFps, false));

        return new AnimationSet(clips, cell, footX, footY);
    }

    private sealed record Context(int Cell, int FootX, int FootY, int SpriteHeight, AnimationOptions Options)
    {
        public int Lift => Math.Max(1, SpriteHeight / 16);
        public int Stride => Math.Max(1, SpriteHeight / 12);
    }

    /// <summary>앞/뒤 걷기: 서기 → 왼발 들기 → 서기 → 오른발 들기, 발을 들 때 몸이 1px 올라간다.</summary>
    private static List<PixelImage> FrontBackWalk(Context c, PosedView v)
    {
        int lift = c.Lift + 1;
        return
        [
            Compose(c, v, new Pose()),
            Compose(c, v, new Pose(BodyDy: -1, FarDy: -lift)),
            Compose(c, v, new Pose()),
            Compose(c, v, new Pose(BodyDy: -1, NearDy: -lift)),
        ];
    }

    /// <summary>옆 걷기: 다리 벌림(접지) → 교차(뒷발 들기) → 반대로 벌림 → 교차(앞발 들기).</summary>
    private static List<PixelImage> SideWalk(Context c, PosedView v)
    {
        int s = c.Stride, lift = c.Lift;
        return
        [
            Compose(c, v, new Pose(BodyDy: 0, FarDx: -s, NearDx: s)),
            Compose(c, v, new Pose(BodyDy: -1, FarDy: -lift - 1)),
            Compose(c, v, new Pose(BodyDy: 0, FarDx: s, NearDx: -s)),
            Compose(c, v, new Pose(BodyDy: -1, NearDy: -lift - 1)),
        ];
    }

    /// <summary>공격: 뒤로 젖힘 → 앞으로 내지르며 베기 → 잔상 → 복귀.</summary>
    private static List<PixelImage> Attack(Context c, PosedView v, Facing facing)
    {
        var (dx, dy) = facing switch
        {
            Facing.Down => (0, 1),
            Facing.Up => (0, -1),
            Facing.Right => (1, 0),
            _ => (-1, 0),
        };
        bool slashBehind = facing == Facing.Up;
        int lunge = Math.Max(2, c.SpriteHeight / 12);

        PixelImage Frame(int step, double? slashFrom)
        {
            var pose = new Pose(BodyDx: dx * step, BodyDy: dy * step, FarDx: dx * step / 2, FarDy: dy * step / 2,
                NearDx: dx * step / 2, NearDy: dy * step / 2);
            var img = new PixelImage(c.Cell, c.Cell);
            if (slashFrom is { } fromBehind && slashBehind) DrawSlash(c, v, img, facing, fromBehind, pose);
            DrawPose(c, v, img, pose);
            if (slashFrom is { } fromFront && !slashBehind) DrawSlash(c, v, img, facing, fromFront, pose);
            return img;
        }

        return
        [
            Frame(-1, null),
            Frame(lunge, 0.0),
            Frame(lunge / 2, 0.55),
            Frame(0, null),
        ];
    }

    private static PixelImage Compose(Context c, PosedView v, Pose p)
    {
        var img = new PixelImage(c.Cell, c.Cell);
        DrawPose(c, v, img, p);
        return img;
    }

    private static void DrawPose(Context c, PosedView v, PixelImage target, Pose p)
    {
        int ox = c.FootX - v.CenterX;
        int oy = c.FootY - v.Height;
        target.Draw(v.LegFar.Image, ox + v.LegFar.X + p.FarDx, oy + v.LegFar.Y + p.FarDy);
        target.Draw(v.LegNear.Image, ox + v.LegNear.X + p.NearDx, oy + v.LegNear.Y + p.NearDy);
        target.Draw(v.Torso.Image, ox + v.Torso.X + p.BodyDx, oy + v.Torso.Y + p.BodyDy);
        target.Draw(v.Head.Image, ox + v.Head.X + p.BodyDx, oy + v.Head.Y + p.BodyDy);
    }

    /// <summary>
    /// 바라보는 방향을 중심으로 140° 호를 그린다. sweepFrom 은 0(전체)~1 사이로, 호의 앞쪽을 지워 잔상을 만든다.
    /// </summary>
    private static void DrawSlash(Context c, PosedView v, PixelImage target, Facing facing, double sweepFrom, Pose p)
    {
        double centerX = c.FootX + p.BodyDx;
        double centerY = c.FootY - v.Height * 0.5 + p.BodyDy;
        double radius = Math.Max(v.Width * 0.75, v.Height * 0.55);
        double baseAngle = facing switch
        {
            Facing.Down => Math.PI / 2,
            Facing.Up => -Math.PI / 2,
            Facing.Right => 0,
            _ => Math.PI,
        };
        double half = 70 * Math.PI / 180;
        double start = baseAngle - half + 2 * half * sweepFrom;
        double end = baseAngle + half;
        int thickness = sweepFrom > 0 ? 1 : 2;

        int steps = (int)Math.Ceiling(radius * 2 * half * 2);
        for (int i = 0; i <= steps; i++)
        {
            double a = start + (end - start) * i / steps;
            for (int t = 0; t < thickness + 1; t++)
            {
                double r = radius - t;
                int x = (int)Math.Round(centerX + Math.Cos(a) * r);
                int y = (int)Math.Round(centerY + Math.Sin(a) * r);
                if (!target.InBounds(x, y)) continue;
                target[x, y] = t == thickness ? c.Options.SlashEdgeColor : c.Options.SlashColor;
            }
        }
    }
}
