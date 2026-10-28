using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Drawing2D;

namespace NipkgCreator;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal static class RepositoryGenerator
{
    public static (string FeedsPath, string OutputPath) Resolve(string selectedPath)
    {
        if (string.IsNullOrWhiteSpace(selectedPath))
            throw new ArgumentException("Select a package root, _Src, or feeds folder.");

        var selected = new DirectoryInfo(Path.GetFullPath(selectedPath.Trim().Trim('"')));
        if (!selected.Exists)
            throw new DirectoryNotFoundException("Selected folder does not exist or is inaccessible: " + selected.FullName);

        DirectoryInfo root;
        string feeds;

        if (selected.Name.Equals("feeds", StringComparison.OrdinalIgnoreCase))
        {
            var parent = selected.Parent ?? throw new IOException("Cannot determine package root.");
            root = parent.Name.Equals("_Src", StringComparison.OrdinalIgnoreCase)
                ? parent.Parent ?? throw new IOException("Cannot determine package root.")
                : parent;
            feeds = selected.FullName;
        }
        else if (selected.Name.Equals("_Src", StringComparison.OrdinalIgnoreCase))
        {
            root = selected.Parent ?? throw new IOException("Cannot determine package root.");
            feeds = Path.Combine(selected.FullName, "feeds");
        }
        else
        {
            root = selected;
            feeds = Path.Combine(selected.FullName, "_Src", "feeds");
        }

        if (!Directory.Exists(feeds))
            throw new DirectoryNotFoundException("Feeds folder not found: " + feeds +
                ". Select a package root containing _Src\\feeds, _Src, or feeds.");

        return (feeds, Path.Combine(root.FullName, "nipkg.ini"));
    }

    public static string GetOutputPath(string selectedPath) => Resolve(selectedPath).OutputPath;

    public static (string Content, int Count) Generate(string feedsPath)
    {
        var resolved = Resolve(feedsPath);
        feedsPath = resolved.FeedsPath;
        var folders = Directory.GetDirectories(feedsPath).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToArray();
        if (folders.Length == 0) throw new InvalidOperationException("No repository folders found.");
        var sections = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in folders)
        {
            var name = Path.GetFileName(path).Replace('.', '-');
            if (name.IndexOfAny(new[] {'\r', '\n', '[', ']', '='}) >= 0)
                throw new InvalidOperationException("Invalid repository name: " + name);
            if (!seen.Add(name)) throw new InvalidOperationException("Duplicate normalized repository name: " + name);
            sections.Add($"[nipkg.repo.{name}]\r\nenabled=true\r\nname={name}\r\nuri={path}");
        }
        return (string.Join("\r\n\r\n", sections) + "\r\n", folders.Length);
    }

    public static void Save(string path, string content)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new IOException("Output directory not found.");
        var temp = Path.Combine(directory, ".nipkg-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temp, content, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}


internal static class Palette
{
    public static readonly Color Canvas = Color.FromArgb(244, 247, 253);
    public static readonly Color Ink = Color.FromArgb(20, 39, 68);
    public static readonly Color Secondary = Color.FromArgb(94, 112, 142);
    public static readonly Color Navy = Color.FromArgb(11, 46, 90);
    public static readonly Color Blue = Color.FromArgb(14, 106, 230);
    public static readonly Color BlueDark = Color.FromArgb(12, 70, 180);
    public static readonly Color Green = Color.FromArgb(16, 170, 93);
    public static readonly Color GreenDark = Color.FromArgb(10, 134, 76);
    public static readonly Color Stroke = Color.FromArgb(211, 225, 243);
    public static readonly Color SoftBlue = Color.FromArgb(226, 239, 255);
    public static readonly Color SoftGreen = Color.FromArgb(225, 248, 234);
}

internal static class Shapes
{
    public static System.Drawing.Drawing2D.GraphicsPath Rounded(Rectangle bounds, int radius)
    {
        var result = new System.Drawing.Drawing2D.GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter < 2) { result.AddRectangle(bounds); return result; }
        result.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        result.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        result.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        result.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        result.CloseFigure();
        return result;
    }
}

internal enum ButtonGlyph { Folder, Eye, Create, Refresh, Copy, Open }

internal sealed class ModernButton : Control
{
    private bool hovered;
    private bool pressed;
    public Color Accent { get; set; } = Palette.Blue;
    public bool Filled { get; set; }
    public ButtonGlyph Glyph { get; set; }
    public ModernButton(string text, ButtonGlyph glyph, bool filled, Color accent)
    {
        Text = text;
        Glyph = glyph;
        Filled = filled;
        Accent = accent;
        Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold);
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.PushButton;
        AccessibleName = text;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.Selectable, true);
        MinimumSize = new Size(80, 34);
    }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Palette.Canvas);
        var area = new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
        using var shape = Shapes.Rounded(area, 9);
        var color = !Enabled ? Color.FromArgb(227, 232, 240) :
                    Filled ? pressed ? ControlPaint.Dark(Accent, 0.18f) :
                    hovered ? ControlPaint.Light(Accent, 0.13f) : Accent :
                    hovered ? Palette.SoftBlue : Color.White;
        using (var background = new SolidBrush(color)) g.FillPath(background, shape);
        using (var border = new Pen(Filled ? color : hovered ? Palette.Blue : Palette.Stroke, 1.25f))
            g.DrawPath(border, shape);
        var foreground = !Enabled ? Palette.Secondary : Filled ? Color.White :
                         Accent == Palette.Green ? Palette.GreenDark : Palette.BlueDark;
        DrawGlyph(g, new Rectangle(14, (Height - 19) / 2, 19, 19), foreground);
        TextRenderer.DrawText(g, Text, Font, new Rectangle(40, 0, Math.Max(0, Width - 45), Height),
            foreground, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis |
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        if (Focused)
        {
            using var focus = new Pen(Filled ? Color.White : Palette.Blue, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
            g.DrawPath(focus, shape);
        }
    }
    private void DrawGlyph(Graphics g, Rectangle r, Color color)
    {
        int x = r.Left, y = r.Top;
        using var p = new Pen(color, 1.8f) { StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round, LineJoin = System.Drawing.Drawing2D.LineJoin.Round };
        switch (Glyph)
        {
            case ButtonGlyph.Folder:
            case ButtonGlyph.Open:
                g.DrawLines(p, new[] { new Point(x+1,y+6), new Point(x+7,y+6), new Point(x+9,y+8),
                    new Point(x+18,y+8), new Point(x+18,y+17), new Point(x+1,y+17), new Point(x+1,y+6) });
                if (Glyph == ButtonGlyph.Open) g.DrawLine(p,x+7,y+12,x+14,y+12);
                break;
            case ButtonGlyph.Eye:
                g.DrawEllipse(p, x+1, y+5, 17, 10);
                using (var dot = new SolidBrush(color)) g.FillEllipse(dot, x+8, y+9, 4, 4);
                break;
            case ButtonGlyph.Create:
                using (var brush = new SolidBrush(color))
                    g.FillPolygon(brush, new[] { new Point(x+5,y+2), new Point(x+17,y+10), new Point(x+5,y+17) });
                break;
            case ButtonGlyph.Refresh:
                g.DrawArc(p, x+3, y+3, 13, 13, 35, 265);
                g.DrawLines(p, new[] { new Point(x+15,y+4), new Point(x+17,y+9), new Point(x+12,y+8) });
                break;
            case ButtonGlyph.Copy:
                g.DrawRectangle(p,x+5,y+5,12,12);
                g.DrawLines(p,new[] { new Point(x+2,y+13),new Point(x+2,y+2),new Point(x+13,y+2) });
                break;
        }
    }
}

internal sealed class SurfaceCard : Panel
{
    public Color Fill { get; set; } = Color.White;
    public Color Outline { get; set; } = Palette.Stroke;
    public SurfaceCard()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        BackColor = Color.White;
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Palette.Canvas);
        using var shape = Shapes.Rounded(new Rectangle(1, 1, Math.Max(1, Width-3), Math.Max(1, Height-3)), 13);
        using var brush = new SolidBrush(Fill);
        using var line = new Pen(Outline, 1);
        g.FillPath(brush, shape);
        g.DrawPath(line, shape);
    }
}

internal sealed class GradientHeader : Panel
{
    private readonly Bitmap? logo;
    public GradientHeader(Bitmap? logo)
    {
        this.logo = logo;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Math.Max(1, Width-1), Math.Max(1, Height-1));
        g.Clear(Parent?.BackColor ?? Palette.Canvas);
        using var shape = Shapes.Rounded(rect, 17);
        using (var gradient = new System.Drawing.Drawing2D.LinearGradientBrush(rect,
            Palette.Navy, Palette.Blue, System.Drawing.Drawing2D.LinearGradientMode.Horizontal))
            g.FillPath(gradient, shape);
        var state = g.Save();
        g.SetClip(shape);
        using (var wave = new System.Drawing.Drawing2D.GraphicsPath())
        {
            wave.AddBezier(Width - 340, Height, Width - 220, Height - 68,
                           Width - 130, Height - 45, Width, Height - 32);
            wave.AddLine(Width, Height);
            wave.CloseFigure();
            using var brush = new SolidBrush(Color.FromArgb(82, 30, 194, 255));
            g.FillPath(brush, wave);
        }
        using (var wave = new System.Drawing.Drawing2D.GraphicsPath())
        {
            wave.AddBezier(Width - 210, Height, Width - 125, Height - 75,
                           Width - 68, Height - 62, Width, Height - 73);
            wave.AddLine(Width, Height);
            wave.CloseFigure();
            using var brush = new SolidBrush(Color.FromArgb(145, 34, 211, 128));
            g.FillPath(brush, wave);
        }
        g.Restore(state);
        if (logo != null) g.DrawImage(logo, new Rectangle(20, 22, 70, 70));
        using var titleFont = new Font("Segoe UI", 19f, FontStyle.Bold);
        using var subFont = new Font("Segoe UI", 10.5f, FontStyle.Regular);
        TextRenderer.DrawText(g, "NI Package Repository Creator", titleFont,
            new Rectangle(107, 20, Math.Max(20, Width-125), 43), Color.White,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, "Create nipkg.ini from NI package feeds in seconds", subFont,
            new Rectangle(109, 66, Math.Max(20, Width-127), 30), Color.FromArgb(221,239,255),
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

internal sealed class MainForm : Form
{
    private readonly TextBox source = new();
    private readonly TextBox output = new();
    private readonly RichTextBox preview = new();
    private readonly Label status = new();
    private readonly Label previewLabel = new();
    private readonly SurfaceCard statusCard = new();
    private readonly ModernButton copyButton = new("Copy", ButtonGlyph.Copy, false, Palette.Blue);
    private readonly System.Windows.Forms.Timer resolver = new() { Interval = 380 };
    private bool customOutput;

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public MainForm()
    {
        Text = "Nipkg Creator | NI Repository Configuration";
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Font;
        MinimumSize = new Size(860, 670);
        Size = new Size(1050, 745);
        BackColor = Palette.Canvas;
        ForeColor = Palette.Ink;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application; }
        catch { Icon = SystemIcons.Application; }
        Bitmap? logo = null;
        try { logo = Icon.ToBitmap(); } catch { /* native icon fallback */ }

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7,
            Padding = new Padding(20, 16, 20, 16),
            Margin = Padding.Empty, BackColor = Palette.Canvas
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 99));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 99));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 49));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 61));
        Controls.Add(body);

        var banner = new GradientHeader(logo) { Dock = DockStyle.Fill, Margin = new Padding(0,0,0,9) };
        body.Controls.Add(banner, 0, 0);

        body.Controls.Add(MakePathCard("SOURCE  ·  Select package root, _Src or feeds", source, false), 0, 1);
        body.Controls.Add(MakePathCard("OUTPUT  ·  nipkg.ini destination", output, true), 0, 2);

        var commands = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false, AutoScroll = true, BackColor = Palette.Canvas,
            Margin = new Padding(0,8,0,6)
        };
        var inspect = NewButton("Preview", ButtonGlyph.Eye, false, Palette.Blue, 142);
        var create = NewButton("Create nipkg.ini", ButtonGlyph.Create, true, Palette.Green, 198);
        var open = NewButton("Open output folder", ButtonGlyph.Open, true, Palette.Blue, 214);
        inspect.Click += (_, _) => GeneratePreview();
        create.Click += (_, _) => Save();
        open.Click += (_, _) => OpenFolder();
        commands.Controls.AddRange(new Control[] { inspect, create, open });
        body.Controls.Add(commands, 0, 3);

        var previewHead = new SurfaceCard
        {
            Dock = DockStyle.Fill, Fill = Palette.SoftBlue, Outline = Palette.Stroke,
            BackColor = Palette.SoftBlue, Padding = new Padding(15,5,10,5),
            Margin = new Padding(0,0,0,6)
        };
        var previewRow = new TableLayoutPanel { Dock=DockStyle.Fill, BackColor=Palette.SoftBlue,
            ColumnCount=2, RowCount=1, Margin=Padding.Empty };
        previewRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        previewRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,105));
        previewLabel.Dock = DockStyle.Fill;
        previewLabel.Text = "Configuration preview  ·  nipkg.ini";
        previewLabel.Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold);
        previewLabel.TextAlign = ContentAlignment.MiddleLeft;
        previewLabel.ForeColor = Palette.Navy;
        copyButton.Dock = DockStyle.Fill;
        copyButton.Margin = Padding.Empty;
        copyButton.Enabled = false;
        copyButton.Click += (_, _) => CopyPreview();
        previewRow.Controls.Add(previewLabel,0,0);
        previewRow.Controls.Add(copyButton,1,0);
        previewHead.Controls.Add(previewRow);
        body.Controls.Add(previewHead,0,4);

        var previewCard = new SurfaceCard
        {
            Dock=DockStyle.Fill, Padding=new Padding(13,12,13,12),
            Margin=Padding.Empty, Fill=Color.White, Outline=Palette.Stroke
        };
        preview.Dock = DockStyle.Fill;
        preview.BorderStyle = BorderStyle.None;
        preview.ReadOnly = true;
        preview.WordWrap = false;
        preview.ScrollBars = RichTextBoxScrollBars.Both;
        preview.BackColor = Color.White;
        preview.ForeColor = Palette.Ink;
        preview.Font = new Font("Consolas", 10.5f);
        preview.DetectUrls = false;
        preview.HideSelection = false;
        previewCard.Controls.Add(preview);
        body.Controls.Add(previewCard,0,5);

        statusCard.Dock = DockStyle.Fill;
        statusCard.Padding = new Padding(16,7,16,7);
        statusCard.Margin = new Padding(0,9,0,0);
        statusCard.Fill = Palette.SoftBlue;
        statusCard.Outline = Palette.Stroke;
        statusCard.BackColor = Palette.SoftBlue;
        status.Dock = DockStyle.Fill;
        status.Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold);
        status.ForeColor = Palette.Navy;
        status.TextAlign = ContentAlignment.MiddleLeft;
        status.AutoEllipsis = true;
        status.Text = "●  Ready — choose a package folder";
        statusCard.Controls.Add(status);
        body.Controls.Add(statusCard,0,6);

        resolver.Tick += (_, _) => { resolver.Stop(); ResolvePath(); };
        source.TextChanged += (_, _) =>
        {
            resolver.Stop();
            resolver.Start();
            preview.Clear();
            copyButton.Enabled = false;
            previewLabel.Text = "Configuration preview  ·  nipkg.ini";
        };
        output.TextChanged += (_, _) => { if (output.Focused) customOutput = true; };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;
        try
        {
            int darkMode = 1;
            DwmSetWindowAttribute(Handle, 20, ref darkMode, sizeof(int));
            int caption = ColorTranslator.ToWin32(Palette.Navy);
            int captionText = ColorTranslator.ToWin32(Color.White);
            DwmSetWindowAttribute(Handle, 35, ref caption, sizeof(int));
            DwmSetWindowAttribute(Handle, 36, ref captionText, sizeof(int));
        }
        catch { /* older Windows uses system caption */ }
    }

    private static TextBox NewPathBox() => new()
    {
        Dock=DockStyle.Fill, Margin=new Padding(0,3,11,4),
        BorderStyle=BorderStyle.FixedSingle, BackColor=Color.FromArgb(250,252,255),
        ForeColor=Palette.Ink, Font=new Font("Segoe UI",10)
    };
    private static ModernButton NewButton(string text, ButtonGlyph glyph, bool filled, Color accent, int width)
        => new(text, glyph, filled, accent) { Width=width, Height=42, Margin=new Padding(0,0,12,0) };

    private SurfaceCard MakePathCard(string title, TextBox field, bool isOutput)
    {
        var panel = new SurfaceCard { Dock=DockStyle.Fill, Margin=new Padding(0,0,0,9),
            Fill=Color.White, Outline=Palette.Stroke, Padding=new Padding(16,9,16,8) };
        var grid = new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=isOutput ? 3:2,
            RowCount=2, Margin=Padding.Empty, BackColor=Color.White };
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute,29));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,126));
        if (isOutput) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,99));
        var label = new Label { Dock=DockStyle.Fill, Text=title,
            Font=new Font("Segoe UI Semibold",10,FontStyle.Bold), ForeColor=Palette.Ink,
            TextAlign=ContentAlignment.MiddleLeft, AutoEllipsis=true };
        grid.Controls.Add(label,0,0);
        grid.SetColumnSpan(label, isOutput ? 3:2);
        field.Dock = DockStyle.Fill;
        field.Margin = new Padding(0,2,11,4);
        field.BorderStyle = BorderStyle.FixedSingle;
        field.BackColor = Color.FromArgb(249,251,255);
        field.ForeColor = Palette.Ink;
        field.Font = new Font("Segoe UI", 10);
        grid.Controls.Add(field,0,1);
        var browse = NewButton("Browse...", ButtonGlyph.Folder,false,Palette.Blue,120);
        browse.Dock = DockStyle.Fill;
        browse.Margin = new Padding(0,0,isOutput ? 9 : 0,1);
        browse.Click += (_, _) => { if (isOutput) BrowseOutput(); else BrowseSource(); };
        grid.Controls.Add(browse,1,1);
        if (isOutput)
        {
            var automatic = NewButton("Auto",ButtonGlyph.Refresh,false,Palette.Blue,90);
            automatic.Dock = DockStyle.Fill;
            automatic.Margin = new Padding(0,0,0,1);
            automatic.Click += (_, _) => { customOutput = false; ResolvePath(); };
            grid.Controls.Add(automatic,2,1);
        }
        panel.Controls.Add(grid);
        return panel;
    }

    private void SetStatus(string message, bool success=false, bool error=false)
    {
        status.Text = (error ? "✕  " : success ? "✓  " : "●  ") + message;
        status.ForeColor = error ? Color.FromArgb(136,39,48) :
                           success ? Color.FromArgb(16,113,60) : Palette.Navy;
        statusCard.Fill = error ? Color.FromArgb(255,236,237) :
                          success ? Palette.SoftGreen : Palette.SoftBlue;
        statusCard.Outline = error ? Color.FromArgb(241,179,179) :
                             success ? Color.FromArgb(159,222,182) : Palette.Stroke;
        statusCard.BackColor = statusCard.Fill;
        statusCard.Invalidate();
    }

    private void ResolvePath()
    {
        try
        {
            var resolved = RepositoryGenerator.Resolve(source.Text);
            if (!customOutput) output.Text = resolved.OutputPath;
            SetStatus("Feeds found: " + resolved.FeedsPath);
        }
        catch (Exception ex)
        {
            if (!customOutput) output.Clear();
            SetStatus(ex.Message,error:true);
        }
    }

    private void BrowseSource()
    {
        using var dialog = new FolderBrowserDialog { Description="Select package root, _Src, or feeds",
            UseDescriptionForTitle=true };
        if (Directory.Exists(source.Text)) dialog.SelectedPath = source.Text;
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        source.Text = dialog.SelectedPath;
        resolver.Stop();
        ResolvePath();
    }

    private void BrowseOutput()
    {
        using var dialog = new FolderBrowserDialog { Description="Select output folder for nipkg.ini",
            UseDescriptionForTitle=true };
        var folder = Path.GetDirectoryName(output.Text);
        if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder)) dialog.SelectedPath = folder;
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        customOutput = true;
        output.Text = Path.Combine(dialog.SelectedPath,"nipkg.ini");
        SetStatus("Custom output folder selected.");
    }

    private (string Content, int Count)? GeneratePreview()
    {
        resolver.Stop();
        try
        {
            var result = RepositoryGenerator.Generate(source.Text.Trim());
            preview.Text = result.Content.Replace("\r\n","\n");
            HighlightPreview();
            previewLabel.Text = "Configuration preview  ·  " + result.Count + " repositories";
            copyButton.Enabled = true;
            SetStatus("Found " + result.Count + " repositories  |  " + output.Text, success:true);
            return result;
        }
        catch(Exception ex)
        {
            SetStatus(ex.Message,error:true);
            MessageBox.Show(this, ex.Message, "Unable to generate preview",
                MessageBoxButtons.OK,MessageBoxIcon.Error);
            return null;
        }
    }

    private void HighlightPreview()
    {
        if (preview.TextLength > 200000) return;
        preview.SuspendLayout();
        try
        {
            int offset=0;
            foreach(var line in preview.Lines)
            {
                if (line.StartsWith("[",StringComparison.Ordinal))
                {
                    preview.Select(offset,line.Length);
                    preview.SelectionColor=Color.FromArgb(87,48,196);
                }
                else
                {
                    int equals=line.IndexOf('=');
                    if (equals>0)
                    {
                        preview.Select(offset,equals+1);
                        preview.SelectionColor=Palette.BlueDark;
                        if (line.AsSpan(equals+1).Trim().SequenceEqual("true".AsSpan()))
                        {
                            preview.Select(offset+equals+1,line.Length-equals-1);
                            preview.SelectionColor=Palette.GreenDark;
                        }
                    }
                }
                offset+=line.Length+1;
            }
            preview.Select(0,0);
        }
        finally { preview.ResumeLayout(); }
    }

    private void CopyPreview()
    {
        if (preview.TextLength == 0) return;
        try { Clipboard.SetText(preview.Text); SetStatus("Preview copied to clipboard.",success:true); }
        catch(Exception ex) { SetStatus(ex.Message,error:true); }
    }

    private void Save()
    {
        var generated=GeneratePreview();
        if (generated == null) return;
        var path=output.Text.Trim();
        if (string.IsNullOrWhiteSpace(path) ||
            !Path.GetFileName(path).Equals("nipkg.ini",StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "Output filename must be nipkg.ini.",
                "Invalid output path", MessageBoxButtons.OK,MessageBoxIcon.Warning);
            return;
        }
        if (File.Exists(path) &&
            MessageBox.Show(this,"Replace existing nipkg.ini?\n\n"+path,
                "Confirm overwrite", MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes) return;
        try
        {
            RepositoryGenerator.Save(path,generated.Value.Content);
            SetStatus("Saved "+generated.Value.Count+" repositories to "+path,success:true);
            MessageBox.Show(this,"File created successfully:\n\n"+path,
                "Nipkg Creator",MessageBoxButtons.OK,MessageBoxIcon.Information);
        }
        catch(Exception ex)
        {
            SetStatus(ex.Message,error:true);
            MessageBox.Show(this,ex.Message,"Save failed",MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
    }

    private void OpenFolder()
    {
        try
        {
            var directory=Path.GetDirectoryName(output.Text);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                throw new DirectoryNotFoundException("Select a valid output folder first.");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe")
                { Arguments="\""+directory+"\"", UseShellExecute=true });
        }
        catch(Exception ex)
        {
            SetStatus(ex.Message,error:true);
            MessageBox.Show(this,ex.Message,"Cannot open folder",MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
    }
}
