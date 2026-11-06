using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using SpriteMaker.Core;

namespace SpriteMaker.App;

public partial class MainWindow : Window
{
    private static readonly uint[] PresetColors =
    [
        0xFF202020, 0xFFFFFFFF, 0xFF8A8F98, 0xFFF2C9A0, 0xFFD9A066, 0xFF8F563B,
        0xFF6B3E26, 0xFFF4D35E, 0xFFE4572E, 0xFFAC3232, 0xFFF29BC1, 0xFF8E5CC2,
        0xFF3A6FD8, 0xFF5FCDE4, 0xFF2E3550, 0xFF6ABE30, 0xFF37946E, 0xFF4B692F,
    ];

    private readonly SketchDocument _doc = new(512, 512);
    private readonly WriteableBitmap _sketchBitmap;
    private readonly DispatcherTimer _regenTimer;
    private readonly DispatcherTimer _animTimer;

    private uint _color = 0xFF202020;
    private Point? _lastPoint;
    private bool _stroking;

    private GenerationResult? _result;
    private BodyLayout? _manualLayout;
    private bool _updatingSliders;
    private AnimationClip? _clip;
    private int _frame;

    public MainWindow()
    {
        InitializeComponent();
        _sketchBitmap = BitmapConvert.ToBitmap(_doc.Image);
        SketchImage.Source = _sketchBitmap;

        _regenTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _regenTimer.Tick += (_, _) => { _regenTimer.Stop(); Regenerate(); };
        _animTimer = new DispatcherTimer();
        _animTimer.Tick += (_, _) => AdvanceFrame();

        BuildPalette();
        SetColor(_color);
        StatusText.Text = "캔버스에 캐릭터 앞모습을 그려 보세요.";

        // 시작 인자로 받은 이미지(예: 탐색기 "연결 프로그램")를 열어 둔다
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && File.Exists(args[1]))
            Loaded += (_, _) => OpenImage(args[1]);
    }

    private void OpenImage(string path)
    {
        try
        {
            _doc.Load(BitmapConvert.Load(path));
            _manualLayout = null;
            RefreshSketch();
            StatusText.Text = $"불러옴: {System.IO.Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StatusText.Text = "이미지를 불러오지 못했습니다: " + ex.Message;
        }
    }

    // ---------------------------------------------------------------- 색

    private void BuildPalette()
    {
        foreach (var c in PresetColors)
        {
            var swatch = new Button
            {
                Width = 26, Height = 26, Margin = new Thickness(2), Padding = new Thickness(0),
                Background = new SolidColorBrush(ToMediaColor(c)), Tag = c, ToolTip = ColorUtil.ToHex(c),
            };
            swatch.Click += (s, _) => SetColor((uint)((Button)s).Tag);
            PalettePanel.Children.Add(swatch);
        }
    }

    private void SetColor(uint color)
    {
        _color = color | 0xFF000000u;
        CurrentColorSwatch.Background = new SolidColorBrush(ToMediaColor(_color));
        HexBox.Text = ColorUtil.ToHex(_color);
    }

    private void HexBox_LostFocus(object sender, RoutedEventArgs e) => ApplyHex();

    private void HexBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) ApplyHex();
    }

    private void ApplyHex()
    {
        if (ColorUtil.TryParseHex(HexBox.Text, out var c)) SetColor(c);
        else HexBox.Text = ColorUtil.ToHex(_color);
    }

    private static Color ToMediaColor(uint c) =>
        Color.FromArgb(ColorUtil.A(c), ColorUtil.R(c), ColorUtil.G(c), ColorUtil.B(c));

    // ---------------------------------------------------------------- 그리기

    private (int X, int Y) ToPixel(MouseEventArgs e)
    {
        var p = e.GetPosition(SketchImage);
        return ((int)Math.Floor(p.X * _doc.Image.Width / SketchImage.ActualWidth),
                (int)Math.Floor(p.Y * _doc.Image.Height / SketchImage.ActualHeight));
    }

    private void Sketch_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var (x, y) = ToPixel(e);
        if (PickerTool.IsChecked == true)
        {
            uint picked = _doc.PickColor(x, y);
            if (ColorUtil.IsOpaque(picked)) SetColor(picked);
            return;
        }
        if (FillTool.IsChecked == true)
        {
            _doc.Checkpoint();
            if (_doc.FloodFill(x, y, _color) > 0) RefreshSketch();
            return;
        }

        _doc.Checkpoint();
        _stroking = true;
        SketchImage.CaptureMouse();
        _lastPoint = new Point(x, y);
        StrokeTo(x, y);
    }

    private void Sketch_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_stroking || e.LeftButton != MouseButtonState.Pressed) return;
        var (x, y) = ToPixel(e);
        StrokeTo(x, y);
    }

    private void Sketch_MouseUp(object sender, MouseButtonEventArgs e) => EndStroke();

    private void Sketch_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!SketchImage.IsMouseCaptured) EndStroke();
    }

    private void StrokeTo(int x, int y)
    {
        var from = _lastPoint ?? new Point(x, y);
        uint color = EraserTool.IsChecked == true ? ColorUtil.Transparent : _color;
        var dirty = _doc.StrokeLine((int)from.X, (int)from.Y, x, y, (int)BrushSize.Value, color);
        BitmapConvert.CopyRegion(_doc.Image, _sketchBitmap, dirty);
        _lastPoint = new Point(x, y);
    }

    private void EndStroke()
    {
        if (!_stroking) return;
        _stroking = false;
        _lastPoint = null;
        SketchImage.ReleaseMouseCapture();
        ScheduleRegenerate();
    }

    private void RefreshSketch()
    {
        BitmapConvert.CopyRegion(_doc.Image, _sketchBitmap, new PixelRect(0, 0, _doc.Image.Width, _doc.Image.Height));
        ScheduleRegenerate();
    }

    private void Undo_Click(object sender, RoutedEventArgs e) { if (_doc.Undo()) RefreshSketch(); }
    private void Redo_Click(object sender, RoutedEventArgs e) { if (_doc.Redo()) RefreshSketch(); }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _doc.Clear();
        _manualLayout = null;
        RefreshSketch();
    }

    private void LoadImage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "이미지|*.png;*.jpg;*.jpeg;*.bmp;*.gif|모든 파일|*.*" };
        if (dlg.ShowDialog(this) == true) OpenImage(dlg.FileName);
    }

    private void SaveSketch_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "PNG|*.png", FileName = NameBox.Text + "_sketch.png" };
        if (dlg.ShowDialog(this) != true) return;
        PngEncoder.Save(_doc.Image, dlg.FileName);
        StatusText.Text = "스케치를 저장했습니다: " + dlg.FileName;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is TextBox) return;
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        switch (e.Key)
        {
            case Key.Z when ctrl: Undo_Click(sender, e); e.Handled = true; break;
            case Key.Y when ctrl: Redo_Click(sender, e); e.Handled = true; break;
            case Key.B when !ctrl: PenTool.IsChecked = true; break;
            case Key.E when !ctrl: EraserTool.IsChecked = true; break;
            case Key.G when !ctrl: FillTool.IsChecked = true; break;
            case Key.I when !ctrl: PickerTool.IsChecked = true; break;
        }
    }

    // ---------------------------------------------------------------- 생성

    private void Option_Changed(object sender, RoutedEventArgs e)
    {
        // 크기가 바뀌면 이전 수동 분할은 의미가 없어진다
        if (sender == HeightBox || sender == OutlineBox) _manualLayout = null;
        ScheduleRegenerate();
    }

    private void ScheduleRegenerate()
    {
        if (!IsLoaded) return;
        _regenTimer.Stop();
        _regenTimer.Start();
    }

    private PixelizeOptions CurrentPixelizeOptions() => new()
    {
        TargetHeight = int.Parse((string)((ComboBoxItem)HeightBox.SelectedItem).Content),
        MaxColors = (int)ColorsSlider.Value,
        AddOutline = OutlineBox.IsChecked == true,
        FillEnclosed = FillEnclosedBox.IsChecked == true,
    };

    private void Regenerate()
    {
        if (_doc.IsEmpty)
        {
            _result = null;
            SpritePreview.Source = null;
            GuideCanvas.Children.Clear();
            AnimPreview.Source = null;
            ClipBox.ItemsSource = null;
            ExportButton.IsEnabled = false;
            _animTimer.Stop();
            return;
        }

        try
        {
            var sprite = SpritePipeline.Pixelize(_doc.Image, CurrentPixelizeOptions());
            _result = SpritePipeline.Generate(sprite, new AnimationOptions(), _manualLayout);
        }
        catch (Exception ex)
        {
            StatusText.Text = "변환 실패: " + ex.Message;
            return;
        }

        ShowSprite(_result);
        ShowClips(_result.Animations);
        ExportButton.IsEnabled = true;
        StatusText.Text = $"스프라이트 {_result.Sprite.Width}×{_result.Sprite.Height}px, 프레임 칸 {_result.Animations.CellSize}px";
    }

    private void ShowSprite(GenerationResult r)
    {
        var sprite = r.Sprite;
        int scale = Math.Max(2, Math.Min(240 / sprite.Width, 240 / sprite.Height));
        SpritePreview.Source = BitmapConvert.ToBitmap(sprite);
        SpritePreview.Width = GuideCanvas.Width = sprite.Width * scale;
        SpritePreview.Height = GuideCanvas.Height = sprite.Height * scale;

        _updatingSliders = true;
        HeadSlider.Maximum = sprite.Height - 1;
        HipSlider.Maximum = sprite.Height - 1;
        CenterSlider.Maximum = sprite.Width - 1;
        HeadSlider.Value = r.Layout.HeadBottom;
        HipSlider.Value = r.Layout.HipY;
        CenterSlider.Value = r.Layout.CenterX;
        _updatingSliders = false;

        GuideCanvas.Children.Clear();
        AddGuide(0, r.Layout.HeadBottom * scale, sprite.Width * scale, r.Layout.HeadBottom * scale, Brushes.Red);
        AddGuide(0, r.Layout.HipY * scale, sprite.Width * scale, r.Layout.HipY * scale, Brushes.LimeGreen);
        AddGuide(r.Layout.CenterX * scale, r.Layout.HipY * scale, r.Layout.CenterX * scale, sprite.Height * scale, Brushes.Gold);
    }

    private void AddGuide(double x1, double y1, double x2, double y2, Brush brush) =>
        GuideCanvas.Children.Add(new Line
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = brush, StrokeThickness = 1.5,
            StrokeDashArray = [4, 2], Opacity = 0.9,
        });

    private void Layout_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingSliders || _result is null) return;
        _manualLayout = new BodyLayout((int)HeadSlider.Value, (int)HipSlider.Value, (int)CenterSlider.Value);
        ScheduleRegenerate();
    }

    private void AutoLayout_Click(object sender, RoutedEventArgs e)
    {
        _manualLayout = null;
        Regenerate();
    }

    // ---------------------------------------------------------------- 애니메이션 미리보기

    private void ShowClips(AnimationSet set)
    {
        string? selected = (ClipBox.SelectedItem as AnimationClip)?.Name ?? "walk_down";
        ClipBox.ItemsSource = set.Clips;
        ClipBox.DisplayMemberPath = nameof(AnimationClip.Name);
        ClipBox.SelectedItem = set.Clips.FirstOrDefault(c => c.Name == selected) ?? set.Clips[0];

        int scale = Math.Max(1, 208 / set.CellSize);
        AnimPreview.Width = AnimPreview.Height = set.CellSize * scale;
    }

    private void ClipBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _clip = ClipBox.SelectedItem as AnimationClip;
        _frame = 0;
        _animTimer.Stop();
        if (_clip is null) return;
        ClipInfo.Text = $"{_clip.Frames.Count}프레임 · {_clip.Fps:0.#} fps · {(_clip.Loop ? "반복" : "1회")}";
        AnimPreview.Source = BitmapConvert.ToBitmap(_clip.Frames[0]);
        _animTimer.Interval = TimeSpan.FromSeconds(1.0 / Math.Max(1, _clip.Fps));
        _animTimer.Start();
    }

    private void AdvanceFrame()
    {
        if (_clip is null) return;
        _frame = (_frame + 1) % (_clip.Frames.Count + (_clip.Loop ? 0 : 3)); // 1회짜리는 잠깐 쉬었다 반복
        AnimPreview.Source = BitmapConvert.ToBitmap(_clip.Frames[Math.Min(_frame, _clip.Frames.Count - 1)]);
    }

    // ---------------------------------------------------------------- 내보내기

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_result is null) return;
        string name = NameBox.Text.Trim();
        if (name.Length == 0 || name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
        {
            StatusText.Text = "파일 이름으로 쓸 수 없는 이름입니다.";
            return;
        }

        var dlg = new OpenFolderDialog { Title = "내보낼 폴더 (고닷 프로젝트 안)" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var files = GodotExporter.Export(_result.Animations, dlg.FolderName, name);
            StatusText.Text = $"내보냄: {dlg.FolderName}\n{System.IO.Path.GetFileName(files.PngPath)}, " +
                              $"{System.IO.Path.GetFileName(files.FramesPath)}, {System.IO.Path.GetFileName(files.ScenePath)}";
        }
        catch (Exception ex)
        {
            StatusText.Text = "내보내기 실패: " + ex.Message;
        }
    }
}
