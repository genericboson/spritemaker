using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SpriteMaker.Core;

public sealed record SheetRow(AnimationClip Clip, int Row);

public sealed record SpriteSheet(PixelImage Image, int CellSize, IReadOnlyList<SheetRow> Rows);

public sealed record ExportResult(string PngPath, string FramesPath, string ScenePath, string MetaPath);

/// <summary>
/// Godot 4 용으로 내보낸다.
/// - {name}.png        : 애니메이션 하나가 한 줄인 스프라이트시트
/// - {name}_frames.tres: SpriteFrames 리소스(AtlasTexture 로 시트를 잘라 씀)
/// - {name}.tscn       : 발밑이 원점인 AnimatedSprite2D (텍스처 필터 Nearest)
/// - {name}.json       : 칸 크기·발 기준점·애니메이션 목록
/// 세 파일을 같은 폴더(프로젝트 안 어디든)에 두면 상대 경로로 서로를 찾는다.
/// </summary>
public static class GodotExporter
{
    public static SpriteSheet BuildSheet(AnimationSet set)
    {
        int columns = set.Clips.Max(c => c.Frames.Count);
        var image = new PixelImage(columns * set.CellSize, set.Clips.Count * set.CellSize);
        var rows = new List<SheetRow>();
        for (int row = 0; row < set.Clips.Count; row++)
        {
            var clip = set.Clips[row];
            for (int col = 0; col < clip.Frames.Count; col++)
                image.Draw(clip.Frames[col], col * set.CellSize, row * set.CellSize);
            rows.Add(new SheetRow(clip, row));
        }
        return new SpriteSheet(image, set.CellSize, rows);
    }

    public static string BuildSpriteFramesTres(SpriteSheet sheet, string texturePath)
    {
        var sb = new StringBuilder();
        sb.Append("[gd_resource type=\"SpriteFrames\" format=3]\n\n");
        sb.Append($"[ext_resource type=\"Texture2D\" path=\"{texturePath}\" id=\"1_sheet\"]\n\n");

        var ids = new Dictionary<(int Row, int Col), string>();
        foreach (var row in sheet.Rows)
            for (int col = 0; col < row.Clip.Frames.Count; col++)
            {
                string id = $"AtlasTexture_{row.Clip.Name}_{col}";
                ids[(row.Row, col)] = id;
                sb.Append($"[sub_resource type=\"AtlasTexture\" id=\"{id}\"]\n");
                sb.Append("atlas = ExtResource(\"1_sheet\")\n");
                sb.Append($"region = Rect2({col * sheet.CellSize}, {row.Row * sheet.CellSize}, {sheet.CellSize}, {sheet.CellSize})\n\n");
            }

        sb.Append("[resource]\n");
        sb.Append("animations = [");
        for (int i = 0; i < sheet.Rows.Count; i++)
        {
            var row = sheet.Rows[i];
            if (i > 0) sb.Append(", ");
            sb.Append("{\n\"frames\": [");
            for (int col = 0; col < row.Clip.Frames.Count; col++)
            {
                if (col > 0) sb.Append(", ");
                sb.Append($"{{\n\"duration\": 1.0,\n\"texture\": SubResource(\"{ids[(row.Row, col)]}\")\n}}");
            }
            sb.Append("],\n");
            sb.Append($"\"loop\": {(row.Clip.Loop ? "true" : "false")},\n");
            sb.Append($"\"name\": &\"{row.Clip.Name}\",\n");
            sb.Append($"\"speed\": {row.Clip.Fps.ToString("0.0###", CultureInfo.InvariantCulture)}\n}}");
        }
        sb.Append("]\n");
        return sb.ToString();
    }

    public static string BuildScene(string nodeName, string framesPath, AnimationSet set)
    {
        // centered=true 이므로 칸 중심이 원점. 발 기준점이 원점에 오도록 offset 을 준다.
        int offsetX = set.CellSize / 2 - set.FootX;
        int offsetY = set.CellSize / 2 - set.FootY;
        var sb = new StringBuilder();
        sb.Append("[gd_scene format=3]\n\n");
        sb.Append($"[ext_resource type=\"SpriteFrames\" path=\"{framesPath}\" id=\"1_frames\"]\n\n");
        sb.Append($"[node name=\"{nodeName}\" type=\"AnimatedSprite2D\"]\n");
        sb.Append("texture_filter = 1\n");
        sb.Append("sprite_frames = ExtResource(\"1_frames\")\n");
        sb.Append("animation = &\"idle_down\"\n");
        sb.Append($"offset = Vector2({offsetX}, {offsetY})\n");
        return sb.ToString();
    }

    public static ExportResult Export(AnimationSet set, string directory, string baseName)
    {
        if (string.IsNullOrWhiteSpace(baseName)) throw new ArgumentException("이름이 비어 있습니다.", nameof(baseName));
        Directory.CreateDirectory(directory);

        var sheet = BuildSheet(set);
        string pngName = baseName + ".png";
        string framesName = baseName + "_frames.tres";
        string sceneName = baseName + ".tscn";
        string metaName = baseName + ".json";

        var result = new ExportResult(
            Path.Combine(directory, pngName),
            Path.Combine(directory, framesName),
            Path.Combine(directory, sceneName),
            Path.Combine(directory, metaName));

        PngEncoder.Save(sheet.Image, result.PngPath);
        File.WriteAllText(result.FramesPath, BuildSpriteFramesTres(sheet, pngName), new UTF8Encoding(false));
        File.WriteAllText(result.ScenePath, BuildScene(ToNodeName(baseName), framesName, set), new UTF8Encoding(false));

        var meta = new
        {
            cellSize = set.CellSize,
            foot = new { x = set.FootX, y = set.FootY },
            animations = sheet.Rows.Select(r => new
            {
                name = r.Clip.Name,
                row = r.Row,
                frames = r.Clip.Frames.Count,
                fps = r.Clip.Fps,
                loop = r.Clip.Loop,
            }),
        };
        File.WriteAllText(result.MetaPath, JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));
        return result;
    }

    private static string ToNodeName(string baseName)
    {
        var chars = baseName.Where(ch => char.IsLetterOrDigit(ch) || ch == '_').ToArray();
        return chars.Length == 0 ? "Character" : new string(chars);
    }
}
