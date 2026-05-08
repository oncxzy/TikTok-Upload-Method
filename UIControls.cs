using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TikTokUploadMethod;

internal static class ColorTheme
{
    public static readonly Color Bg          = Color.FromArgb(6, 6, 8);
    public static readonly Color Sidebar     = Color.FromArgb(11, 11, 14);
    public static readonly Color Surface     = Color.FromArgb(15, 15, 19);
    public static readonly Color SurfaceHi   = Color.FromArgb(21, 21, 26);
    public static readonly Color Border      = Color.FromArgb(28, 28, 34);
    public static readonly Color Text        = Color.FromArgb(250, 250, 250);
    public static readonly Color Muted       = Color.FromArgb(139, 139, 147);
    public static readonly Color MutedSubtle = Color.FromArgb(82, 82, 91);
    public static readonly Color Accent      = Color.FromArgb(167, 139, 250);
    public static readonly Color AccentDeep  = Color.FromArgb(139, 92, 246);
    public static readonly Color Success     = Color.FromArgb(52, 211, 153);
    public static readonly Color Danger      = Color.FromArgb(248, 113, 113);
}

internal sealed class SidebarButton : Control
{
    private bool _hover;
    private bool _isActive;

    public bool IsActive
    {
        get => _isActive;
        set { _isActive = value; Invalidate(); }
    }

    public SidebarButton(string text)
    {
        Text = text;
        Height = 38;
        SetStyle(ControlStyles.AllPaintingInWmPaint
               | ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.ResizeRedraw
               | ControlStyles.UserPaint, true);
        BackColor = ColorTheme.Sidebar;
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 2, Width - 1, Height - 4);
        var radius = 8;

        using var path = RoundedPath(rect, radius);

        if (_isActive)
        {
            using var b = new SolidBrush(Color.FromArgb(28, 167, 139, 250));
            g.FillPath(b, path);
            using var border = new Pen(Color.FromArgb(60, 167, 139, 250), 1);
            g.DrawPath(border, path);
        }
        else if (_hover)
        {
            using var b = new SolidBrush(ColorTheme.SurfaceHi);
            g.FillPath(b, path);
        }

        var textColor = _isActive ? ColorTheme.Text : (_hover ? ColorTheme.Text : ColorTheme.Muted);
        var textRect = new Rectangle(14, 0, Width - 14, Height);
        TextRenderer.DrawText(g, Text, Font, textRect, textColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
    }

    private static GraphicsPath RoundedPath(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class ModernButton : Button
{
    public ModernButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = ColorTheme.AccentDeep;
        ForeColor = ColorTheme.Text;
        Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint
               | ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.ResizeRedraw
               | ControlStyles.UserPaint, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width, Height);
        using var path = RoundedPath(rect, 8);

        Color fill = !Enabled ? ColorTheme.SurfaceHi
            : (ClientRectangle.Contains(PointToClient(MousePosition)) ? ColorTheme.Accent : ColorTheme.AccentDeep);

        using var b = new SolidBrush(fill);
        g.FillPath(b, path);

        var fg = Enabled ? ColorTheme.Text : ColorTheme.Muted;
        TextRenderer.DrawText(g, Text, Font, rect, fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    protected override void OnMouseMove(MouseEventArgs e) { Invalidate(); base.OnMouseMove(e); }
    protected override void OnMouseLeave(EventArgs e) { Invalidate(); base.OnMouseLeave(e); }

    private static GraphicsPath RoundedPath(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d - 1, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d - 1, r.Bottom - d - 1, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d - 1, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class FlatProgressBar : Control
{
    private int _value;
    public int Value
    {
        get => _value;
        set { _value = Math.Max(0, Math.Min(100, value)); Invalidate(); }
    }

    public FlatProgressBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint
               | ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.ResizeRedraw
               | ControlStyles.UserPaint, true);
        Height = 6;
        BackColor = ColorTheme.Bg;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var trackRect = new Rectangle(0, 0, Width, Height);
        using var trackPath = RoundedPath(trackRect, Height / 2);
        using var trackBrush = new SolidBrush(ColorTheme.SurfaceHi);
        g.FillPath(trackBrush, trackPath);

        if (_value > 0)
        {
            int fillWidth = (int)Math.Round(Width * (_value / 100.0));
            if (fillWidth > 0)
            {
                var fillRect = new Rectangle(0, 0, fillWidth, Height);
                using var fillPath = RoundedPath(fillRect, Height / 2);
                using var fillBrush = new LinearGradientBrush(
                    fillRect,
                    ColorTheme.AccentDeep,
                    ColorTheme.Accent,
                    LinearGradientMode.Horizontal);
                g.FillPath(fillBrush, fillPath);
            }
        }
    }

    private static GraphicsPath RoundedPath(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        if (radius < 1 || r.Width < radius * 2 || r.Height < radius * 2)
        {
            path.AddRectangle(r);
            path.CloseFigure();
            return path;
        }
        int d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class DropZonePanel : Panel
{
    private bool _hover;
    private bool _isBusy;

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; Cursor = value ? Cursors.Default : Cursors.Hand; Invalidate(); }
    }

    public event EventHandler<string>? FileDropped;

    public DropZonePanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint
               | ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.ResizeRedraw
               | ControlStyles.UserPaint, true);
        BackColor = ColorTheme.Bg;
        AllowDrop = true;
        Cursor = Cursors.Hand;

        DragEnter += OnDragEnter;
        DragLeave += (_, _) => { _hover = false; Invalidate(); };
        DragDrop += OnDragDrop;
        MouseEnter += (_, _) => { _hover = true; Invalidate(); };
        MouseLeave += (_, _) => { _hover = false; Invalidate(); };
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (_isBusy)
        {
            e.Effect = DragDropEffects.None;
            return;
        }
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            e.Effect = DragDropEffects.Copy;
            _hover = true;
            Invalidate();
        }
        else
        {
            e.Effect = DragDropEffects.None;
        }
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        _hover = false;
        Invalidate();
        if (_isBusy) return;
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            FileDropped?.Invoke(this, files[0]);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = RoundedPath(rect, 12);

        using var bg = new SolidBrush(_hover ? ColorTheme.SurfaceHi : ColorTheme.Surface);
        g.FillPath(bg, path);

        var borderColor = _hover ? ColorTheme.Accent : ColorTheme.Border;
        using var borderPen = new Pen(borderColor, _hover ? 1.6f : 1f);
        if (_hover) borderPen.DashStyle = DashStyle.Solid;
        else borderPen.DashStyle = DashStyle.Dash;
        g.DrawPath(borderPen, path);

        var iconSize = 32;
        var iconRect = new Rectangle(
            (Width - iconSize) / 2,
            (Height / 2) - 38,
            iconSize, iconSize);

        using (var iconPen = new Pen(_hover ? ColorTheme.Accent : ColorTheme.Muted, 2f))
        {
            iconPen.LineJoin = LineJoin.Round;
            iconPen.StartCap = LineCap.Round;
            iconPen.EndCap = LineCap.Round;

            int cx = iconRect.X + iconRect.Width / 2;
            int top = iconRect.Y + 4;
            int bottom = iconRect.Bottom - 8;
            g.DrawLine(iconPen, cx, top, cx, bottom);
            g.DrawLine(iconPen, cx - 7, top + 7, cx, top);
            g.DrawLine(iconPen, cx + 7, top + 7, cx, top);

            int lineY = iconRect.Bottom - 2;
            g.DrawLine(iconPen, iconRect.X + 4, lineY, iconRect.Right - 4, lineY);
        }

        var titleText = _isBusy ? "Working…" : "Drop video here";
        var titleColor = _isBusy ? ColorTheme.Muted : ColorTheme.Text;
        var titleFont = new Font("Segoe UI Semibold", 11.5f, FontStyle.Bold);
        var titleSize = TextRenderer.MeasureText(g, titleText, titleFont);
        TextRenderer.DrawText(g, titleText, titleFont,
            new Point((Width - titleSize.Width) / 2, Height / 2 + 6),
            titleColor);

        if (!_isBusy)
        {
            var subText = "or click to browse";
            var subFont = new Font("Segoe UI", 9f);
            var subSize = TextRenderer.MeasureText(g, subText, subFont);
            TextRenderer.DrawText(g, subText, subFont,
                new Point((Width - subSize.Width) / 2, Height / 2 + 32),
                ColorTheme.Muted);
            subFont.Dispose();
        }

        titleFont.Dispose();
    }

    private static GraphicsPath RoundedPath(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class InfoCard : Control
{
    private readonly string _title;
    private readonly string _value;
    private readonly string _sub;
    private bool _hover;

    public InfoCard(string title, string value, string sub)
    {
        _title = title;
        _value = value;
        _sub = sub;
        SetStyle(ControlStyles.AllPaintingInWmPaint
               | ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.ResizeRedraw
               | ControlStyles.UserPaint, true);
        BackColor = ColorTheme.Bg;
        Height = 72;
        MouseEnter += (_, _) => { _hover = true; Invalidate(); };
        MouseLeave += (_, _) => { _hover = false; Invalidate(); };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = RoundedPath(rect, 10);

        var bgColor = _hover ? ColorTheme.SurfaceHi : ColorTheme.Surface;
        using var bg = new SolidBrush(bgColor);
        g.FillPath(bg, path);

        var borderColor = _hover ? Color.FromArgb(50, 167, 139, 250) : ColorTheme.Border;
        using var border = new Pen(borderColor, 1f);
        g.DrawPath(border, path);

        var dotRect = new Rectangle(14, 14, 6, 6);
        using var dotPath = RoundedPath(dotRect, 3);
        using var dot = new SolidBrush(ColorTheme.AccentDeep);
        g.FillPath(dot, dotPath);

        var titleFont = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        TextRenderer.DrawText(g, _title.ToUpperInvariant(), titleFont,
            new Point(28, 12), ColorTheme.MutedSubtle);
        titleFont.Dispose();

        var valueFont = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold);
        TextRenderer.DrawText(g, _value, valueFont,
            new Point(14, 30), ColorTheme.Text);
        valueFont.Dispose();

        var subFont = new Font("Segoe UI", 8.5f);
        TextRenderer.DrawText(g, _sub, subFont,
            new Point(14, 51), ColorTheme.Muted);
        subFont.Dispose();
    }

    private static GraphicsPath RoundedPath(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        if (r.Width < d || r.Height < d) { path.AddRectangle(r); return path; }
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
