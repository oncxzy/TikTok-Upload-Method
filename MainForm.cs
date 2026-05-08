using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace TikTokUploadMethod;

public sealed class MainForm : Form
{
    private const string TikTokHomeUrl = "https://www.tiktok.com/tiktokstudio/upload";
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/131.0.0.0 Safari/537.36";

    private const int ContentMaxWidth = 980;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".mkv", ".webm", ".m4v",
        ".avi", ".wmv", ".flv",
        ".mpg", ".mpeg", ".ts", ".m2ts", ".3gp"
    };

    private static readonly string[] AllowedHostSuffixes = new[]
    {
        "tiktok.com", "byteoversea.com", "tiktokcdn.com",
        "tiktokcdn-us.com", "ttwstatic.com", "musical.ly"
    };

    private readonly string _baseDir;
    private readonly string _userDataDir;
    private readonly string _historyPath;

    private SidebarButton _navEncodeBtn = null!;
    private SidebarButton _navUploadBtn = null!;
    private Panel _content = null!;
    private Panel _sidebar = null!;

    private Panel _encodePanel = null!;
    private Panel _uploadPanel = null!;
    private Panel _encodeInner = null!;
    private DropZonePanel _dropZone = null!;
    private Label _stageLabel = null!;
    private Label _percentLabel = null!;
    private FlatProgressBar _progressBar = null!;
    private Label _resultLabel = null!;
    private Button _selectButton = null!;
    private Button _cancelButton = null!;

    private Panel _queuePanel = null!;
    private Label _queueHeaderLabel = null!;
    private FlowLayoutPanel _queueList = null!;

    private Panel _historyPanel = null!;
    private Label _historyHeaderLabel = null!;
    private FlowLayoutPanel _historyList = null!;

    private WebView2? _webView;
    private bool _webViewReady;
    private CancellationTokenSource? _encodeCts;
    private bool _isEncoding;

    private readonly Queue<string> _encodeQueue = new();
    private readonly List<HistoryEntry> _history = new();

    private sealed record HistoryEntry(string FileName, string OutputPath, DateTime When, bool Success, string Note);

    public MainForm()
    {
        _baseDir = Program.AppDirectory;
        _userDataDir = Path.Combine(_baseDir, "userdata");
        _historyPath = Path.Combine(_baseDir, "history.log");

        try { Directory.CreateDirectory(_userDataDir); }
        catch (Exception ex) { LogStartup("Could not create userdata folder", ex); }

        try
        {
            Text = Program.AppName;
            BackColor = ColorTheme.Bg;
            ForeColor = ColorTheme.Text;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
            AllowDrop = true;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimumSize = new Size(960, 600);
            WindowState = FormWindowState.Maximized;
            Resize += OnFormResizeEnforceFullscreen;
        }
        catch (Exception ex) { LogStartup("BasicSetup failed", ex); }

        TryLoadIcon();

        try { BuildLayout(); }
        catch (Exception ex)
        {
            LogStartup("BuildLayout failed", ex);
            MessageBox.Show("UI failed to build:\n\n" + ex.Message, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            throw;
        }

        LoadHistory();

        try { ShowEncodePanel(); }
        catch (Exception ex) { LogStartup("ShowEncodePanel failed", ex); }

        Load += (_, _) => { RefreshHistoryUI(); RefreshQueueUI(); };

        try { LogToolDiagnostics(); }
        catch (Exception ex) { LogStartup("LogToolDiagnostics failed", ex); }
    }

    private void OnFormResizeEnforceFullscreen(object? sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Normal)
            WindowState = FormWindowState.Maximized;
    }

    private void TryLoadIcon()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var s = asm.GetManifestResourceStream("app.ico");
            if (s != null) { Icon = new Icon(s); return; }
        }
        catch (Exception ex) { LogStartup("Icon load (embedded) failed", ex); }

        try
        {
            var iconPath = Path.Combine(_baseDir, "imgs", "icon.ico");
            if (File.Exists(iconPath)) Icon = new Icon(iconPath);
            else LogStartupInfo($"Icon file not found at: {iconPath}");
        }
        catch (Exception ex) { LogStartup("Icon load (file) failed", ex); }
    }

    private void LogStartup(string label, Exception ex)
    {
        try
        {
            var path = Path.Combine(_baseDir, "crash.log");
            var stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            File.AppendAllText(path, $"{new string('-', 60)}\n[{stamp}] MainForm.{label}\n{ex}\n");
        }
        catch { }
    }

    private void LogStartupInfo(string text)
    {
        try
        {
            var path = Path.Combine(_baseDir, "crash.log");
            var stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            File.AppendAllText(path, $"[{stamp}] INFO: {text}\n");
        }
        catch { }
    }

    private void LogToolDiagnostics()
    {
        var ffmpegPath = Path.Combine(_baseDir, "ffmpeg", "ffmpeg.exe");
        var ffprobePath = Path.Combine(_baseDir, "ffmpeg", "ffprobe.exe");
        var lines = new List<string>
        {
            $"ProcessPath          : {Environment.ProcessPath}",
            $"AppContext.BaseDir   : {AppContext.BaseDirectory}",
            $"Resolved AppDir      : {_baseDir}",
            $"ffmpeg path          : {ffmpegPath}  exists={File.Exists(ffmpegPath)}",
            $"ffprobe path         : {ffprobePath}  exists={File.Exists(ffprobePath)}",
        };
        if (File.Exists(ffmpegPath))
        {
            try { lines.Add($"ffmpeg size          : {new FileInfo(ffmpegPath).Length:N0} bytes"); }
            catch (Exception ex) { lines.Add("ffmpeg size          : ERROR " + ex.Message); }
        }
        try
        {
            var path = Path.Combine(_baseDir, "crash.log");
            var stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            File.AppendAllText(path, $"[{stamp}] DIAG: tool detection\n  " + string.Join("\n  ", lines) + "\n");
        }
        catch { }
    }

    private void BuildLayout()
    {
        _sidebar = new Panel
        {
            Dock = DockStyle.Left,
            Width = 220,
            BackColor = ColorTheme.Sidebar,
            Padding = new Padding(16, 22, 8, 16)
        };

        var titleLabel = new Label
        {
            Text = Program.AppName,
            Font = new Font("Segoe UI Semibold", 11.5f, FontStyle.Bold),
            ForeColor = ColorTheme.Text,
            AutoSize = false, Height = 28,
            Dock = DockStyle.Top,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var subtitleLabel = new Label
        {
            Text = "by Oncxzy",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = ColorTheme.MutedSubtle,
            AutoSize = false, Height = 18,
            Dock = DockStyle.Top,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var spacer = new Panel { Height = 28, Dock = DockStyle.Top };

        _navEncodeBtn = new SidebarButton("Encode") { IsActive = true };
        _navEncodeBtn.Click += (_, _) => ShowEncodePanel();

        _navUploadBtn = new SidebarButton("Upload");
        _navUploadBtn.Click += (_, _) => ShowUploadPanel();

        var navHolder = new Panel { Dock = DockStyle.Top, Height = 100 };
        navHolder.Controls.Add(_navUploadBtn);
        navHolder.Controls.Add(_navEncodeBtn);
        _navEncodeBtn.Dock = DockStyle.Top;
        _navUploadBtn.Dock = DockStyle.Top;

        var versionLabel = new Label
        {
            Text = "v1.0",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = ColorTheme.MutedSubtle,
            AutoSize = false, Height = 22,
            Dock = DockStyle.Bottom,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var creatorLink = new LinkLabel
        {
            Text = "Creator: @oncxzy",
            Font = new Font("Segoe UI", 8.5f),
            LinkColor = ColorTheme.MutedSubtle,
            ActiveLinkColor = ColorTheme.Accent,
            VisitedLinkColor = ColorTheme.MutedSubtle,
            AutoSize = false,
            Height = 22,
            Dock = DockStyle.Bottom,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = ColorTheme.Sidebar
        };
        creatorLink.LinkClicked += (_, _) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://tiktok.com/@oncxzy",
                    UseShellExecute = true
                });
            }
            catch { }
        };

        _sidebar.Controls.Add(navHolder);
        _sidebar.Controls.Add(spacer);
        _sidebar.Controls.Add(subtitleLabel);
        _sidebar.Controls.Add(titleLabel);
        _sidebar.Controls.Add(versionLabel);
        _sidebar.Controls.Add(creatorLink);

        _content = new Panel { Dock = DockStyle.Fill, BackColor = ColorTheme.Bg, Padding = new Padding(0) };

        BuildEncodePanel();
        BuildUploadPanel();

        _content.Controls.Add(_uploadPanel);
        _content.Controls.Add(_encodePanel);

        Controls.Add(_content);
        Controls.Add(_sidebar);
    }

    private void BuildEncodePanel()
    {
        _encodePanel = new Panel { Dock = DockStyle.Fill, BackColor = ColorTheme.Bg, AutoScroll = true };

        _encodeInner = new Panel { BackColor = ColorTheme.Bg, Width = ContentMaxWidth, Location = new Point(40, 36) };

        var headingLabel = new Label
        {
            Text = "Encode Video",
            Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold),
            ForeColor = ColorTheme.Text,
            AutoSize = true,
            Location = new Point(0, 0)
        };

        var subLabel = new Label
        {
            Text = "Drop a video below or click to browse. Output saves next to the original.",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = ColorTheme.Muted,
            AutoSize = true,
            Location = new Point(0, 34)
        };

        _dropZone = new DropZonePanel
        {
            Location = new Point(0, 72),
            Size = new Size(ContentMaxWidth - 40, 200)
        };
        _dropZone.FileDropped += (_, path) => EnqueueFile(path);
        _dropZone.Click += async (_, _) => await BrowseAndEnqueueAsync();

        _selectButton = new ModernButton
        {
            Text = "Select Video",
            Size = new Size(148, 36),
            Location = new Point(0, 290)
        };
        _selectButton.Click += async (_, _) => await BrowseAndEnqueueAsync();

        _cancelButton = new ModernButton
        {
            Text = "Cancel",
            Size = new Size(90, 36),
            Location = new Point(158, 290),
            Visible = false
        };
        _cancelButton.Click += (_, _) =>
        {
            try { _encodeCts?.Cancel(); } catch { }
            _encodeQueue.Clear();
            RefreshQueueUI();
        };

        _stageLabel = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 10f),
            ForeColor = ColorTheme.Text,
            AutoSize = true,
            Location = new Point(0, 346),
            Visible = false
        };

        _percentLabel = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = ColorTheme.Accent,
            AutoSize = false,
            Size = new Size(56, 20),
            TextAlign = ContentAlignment.MiddleRight,
            Location = new Point(ContentMaxWidth - 96, 346),
            Visible = false
        };

        _progressBar = new FlatProgressBar
        {
            Location = new Point(0, 374),
            Size = new Size(ContentMaxWidth - 40, 5),
            Visible = false
        };

        _resultLabel = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = ColorTheme.Muted,
            AutoSize = false,
            Size = new Size(ContentMaxWidth - 40, 44),
            Location = new Point(0, 390),
            TextAlign = ContentAlignment.TopLeft
        };

        _queuePanel = new Panel
        {
            Location = new Point(0, 444),
            Size = new Size(ContentMaxWidth - 40, 0),
            BackColor = ColorTheme.Bg
        };

        _queueHeaderLabel = new Label
        {
            Text = "QUEUE",
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = ColorTheme.MutedSubtle,
            AutoSize = true,
            Location = new Point(0, 0)
        };

        _queueList = new FlowLayoutPanel
        {
            Location = new Point(0, 20),
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = ColorTheme.Bg
        };

        _queuePanel.Controls.Add(_queueHeaderLabel);
        _queuePanel.Controls.Add(_queueList);

        _historyPanel = new Panel
        {
            Location = new Point(0, 444),
            Size = new Size(ContentMaxWidth - 40, 0),
            BackColor = ColorTheme.Bg
        };

        _historyHeaderLabel = new Label
        {
            Text = "HISTORY",
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = ColorTheme.MutedSubtle,
            AutoSize = true,
            Location = new Point(0, 0)
        };

        _historyList = new FlowLayoutPanel
        {
            Location = new Point(0, 20),
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = ColorTheme.Bg
        };

        _historyPanel.Controls.Add(_historyHeaderLabel);
        _historyPanel.Controls.Add(_historyList);

        _encodeInner.Controls.Add(headingLabel);
        _encodeInner.Controls.Add(subLabel);
        _encodeInner.Controls.Add(_dropZone);
        _encodeInner.Controls.Add(_selectButton);
        _encodeInner.Controls.Add(_cancelButton);
        _encodeInner.Controls.Add(_stageLabel);
        _encodeInner.Controls.Add(_percentLabel);
        _encodeInner.Controls.Add(_progressBar);
        _encodeInner.Controls.Add(_resultLabel);
        _encodeInner.Controls.Add(_queuePanel);
        _encodeInner.Controls.Add(_historyPanel);
        _encodeInner.Height = 900;

        _encodePanel.Controls.Add(_encodeInner);
        _encodePanel.Resize += (_, _) => RecenterEncodeInner();
        RecenterEncodeInner();
    }

    private void RecenterEncodeInner()
    {
        if (_encodeInner == null) return;

        int availW = _encodePanel.ClientSize.Width - 80;
        int innerW = Math.Min(ContentMaxWidth, Math.Max(400, availW));
        _encodeInner.Width = innerW;

        _dropZone.Width = innerW - 40;
        _progressBar.Width = innerW - 40;
        _resultLabel.Width = innerW - 40;
        _queuePanel.Width = innerW - 40;
        _historyPanel.Width = innerW - 40;
        _queueList.Width = innerW - 40;
        _historyList.Width = innerW - 40;
        _percentLabel.Left = innerW - 40 - _percentLabel.Width;

        int leftPad = Math.Max(40, (_encodePanel.ClientSize.Width - innerW) / 2);
        _encodeInner.Location = new Point(leftPad, 36);
    }

    private void RefreshQueueUI()
    {
        if (InvokeRequired) { BeginInvoke(new Action(RefreshQueueUI)); return; }

        _queueList.Controls.Clear();
        var items = _encodeQueue.ToArray();

        if (items.Length == 0)
        {
            _queuePanel.Height = 0;
            _queuePanel.Visible = false;
        }
        else
        {
            foreach (var path in items)
            {
                var lbl = new Label
                {
                    Text = $"  ⏳  {Path.GetFileName(path)}",
                    Font = new Font("Segoe UI", 9f),
                    ForeColor = ColorTheme.Muted,
                    AutoSize = false,
                    Size = new Size(_queueList.Width > 0 ? _queueList.Width : 600, 26),
                    TextAlign = ContentAlignment.MiddleLeft
                };
                _queueList.Controls.Add(lbl);
            }
            _queuePanel.Height = 20 + items.Length * 28 + 10;
            _queuePanel.Visible = true;
        }

        RelayoutBottomPanels();
    }

    private void RefreshHistoryUI()
    {
        if (InvokeRequired) { BeginInvoke(new Action(RefreshHistoryUI)); return; }

        _historyList.Controls.Clear();

        if (_history.Count == 0)
        {
            _historyPanel.Height = 0;
            _historyPanel.Visible = false;
        }
        else
        {
            foreach (var entry in Enumerable.Reverse(_history).Take(20))
            {
                var icon = entry.Success ? "✓" : "✗";
                var iconColor = entry.Success ? ColorTheme.Success : ColorTheme.Danger;
                var timeStr = entry.When.ToString("MMM d  h:mm tt");

                var row = new Panel
                {
                    Size = new Size(_historyList.Width > 0 ? _historyList.Width : 600, 40),
                    BackColor = ColorTheme.Bg
                };

                var iconLbl = new Label
                {
                    Text = icon,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    ForeColor = iconColor,
                    Size = new Size(20, 40),
                    Location = new Point(0, 0),
                    TextAlign = ContentAlignment.MiddleCenter
                };

                var nameLbl = new Label
                {
                    Text = Path.GetFileName(entry.FileName),
                    Font = new Font("Segoe UI", 9f),
                    ForeColor = ColorTheme.Text,
                    Size = new Size(340, 22),
                    Location = new Point(24, 4),
                    TextAlign = ContentAlignment.MiddleLeft
                };

                var timeLbl = new Label
                {
                    Text = timeStr,
                    Font = new Font("Segoe UI", 8f),
                    ForeColor = ColorTheme.MutedSubtle,
                    Size = new Size(200, 16),
                    Location = new Point(24, 22),
                    TextAlign = ContentAlignment.MiddleLeft
                };

                string noteTip = entry.Note;
                var noteLbl = new Label
                {
                    Text = noteTip,
                    Font = new Font("Segoe UI", 8f),
                    ForeColor = entry.Success ? ColorTheme.Muted : ColorTheme.Danger,
                    Size = new Size(160, 40),
                    Location = new Point(row.Width - 165, 0),
                    TextAlign = ContentAlignment.MiddleRight
                };

                row.Controls.Add(iconLbl);
                row.Controls.Add(nameLbl);
                row.Controls.Add(timeLbl);
                row.Controls.Add(noteLbl);

                var sep = new Panel
                {
                    Size = new Size(_historyList.Width > 0 ? _historyList.Width : 600, 1),
                    BackColor = ColorTheme.Border
                };

                _historyList.Controls.Add(row);
                _historyList.Controls.Add(sep);
            }

            _historyPanel.Height = 20 + Math.Min(_history.Count, 20) * 42 + 10;
            _historyPanel.Visible = true;
        }

        RelayoutBottomPanels();
    }

    private void RelayoutBottomPanels()
    {
        int top = 444;

        if (_queuePanel.Visible)
        {
            _queuePanel.Location = new Point(0, top);
            top += _queuePanel.Height + 16;
        }

        if (_historyPanel.Visible)
        {
            _historyPanel.Location = new Point(0, top);
            top += _historyPanel.Height + 16;
        }

        _encodeInner.Height = Math.Max(900, top + 40);
    }

    private void LoadHistory()
    {
        try
        {
            if (!File.Exists(_historyPath)) return;
            var lines = File.ReadAllLines(_historyPath);
            foreach (var line in lines)
            {
                var parts = line.Split('\t');
                if (parts.Length < 5) continue;
                if (!DateTime.TryParse(parts[0], out var when)) continue;
                var success = parts[1] == "1";
                _history.Add(new HistoryEntry(parts[2], parts[3], when, success, parts[4]));
            }
            RefreshHistoryUI();
        }
        catch { }
    }

    private void SaveHistoryEntry(HistoryEntry entry)
    {
        try
        {
            var line = $"{entry.When:o}\t{(entry.Success ? "1" : "0")}\t{entry.FileName}\t{entry.OutputPath}\t{entry.Note}";
            File.AppendAllText(_historyPath, line + "\n");
        }
        catch { }
    }

    private void AddHistory(string inputPath, string outputPath, bool success, string note)
    {
        var entry = new HistoryEntry(inputPath, outputPath, DateTime.Now, success, note);
        _history.Add(entry);
        SaveHistoryEntry(entry);
        RefreshHistoryUI();
    }

    private void BuildUploadPanel()
    {
        _uploadPanel = new Panel { Dock = DockStyle.Fill, BackColor = ColorTheme.Bg, Visible = false };
    }

    private async Task EnsureWebViewAsync()
    {
        if (_webViewReady) return;

        var loading = new Label
        {
            Text = "Loading…",
            ForeColor = ColorTheme.Muted,
            Font = new Font("Segoe UI", 10f),
            AutoSize = true,
            Location = new Point(24, 20)
        };
        _uploadPanel.Controls.Add(loading);

        _webView = new WebView2 { Dock = DockStyle.Fill };
        _uploadPanel.Controls.Add(_webView);

        try
        {
            var env = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: _userDataDir,
                options: new CoreWebView2EnvironmentOptions
                {
                    AdditionalBrowserArguments = "--disable-features=msEdgeShoppingAssistant"
                });

            await _webView.EnsureCoreWebView2Async(env);

            var settings = _webView.CoreWebView2.Settings;
            settings.UserAgent = DesktopUserAgent;
            settings.AreDevToolsEnabled = false;
            settings.AreDefaultContextMenusEnabled = false;
            settings.IsStatusBarEnabled = false;
            settings.IsZoomControlEnabled = true;
            settings.AreBrowserAcceleratorKeysEnabled = false;
            settings.IsPasswordAutosaveEnabled = true;
            settings.IsGeneralAutofillEnabled = true;

            await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(BypassScript.Code);
            _webView.CoreWebView2.NavigationStarting += OnNavigationStarting;
            _webView.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
            _webView.CoreWebView2.Navigate(TikTokHomeUrl);

            loading.Dispose();
            _webViewReady = true;
        }
        catch (Exception ex)
        {
            loading.Text = "Failed to load browser: " + ex.Message;
            loading.ForeColor = ColorTheme.Danger;
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!IsAllowedUrl(e.Uri)) { e.Cancel = true; try { OpenInDefaultBrowser(e.Uri); } catch { } }
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        try { OpenInDefaultBrowser(e.Uri); } catch { }
    }

    private static bool IsAllowedUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        var scheme = uri.Scheme.ToLowerInvariant();
        if (scheme == "about" || scheme == "data" || scheme == "blob") return true;
        if (scheme != "http" && scheme != "https") return false;
        var host = uri.Host.ToLowerInvariant();
        foreach (var suffix in AllowedHostSuffixes)
            if (host == suffix || host.EndsWith("." + suffix, StringComparison.Ordinal)) return true;
        return false;
    }

    private static void OpenInDefaultBrowser(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true }); }
        catch { }
    }

    private void ShowEncodePanel()
    {
        _navEncodeBtn.IsActive = true;
        _navUploadBtn.IsActive = false;
        _encodePanel.Visible = true;
        _uploadPanel.Visible = false;
    }

    private async void ShowUploadPanel()
    {
        _navEncodeBtn.IsActive = false;
        _navUploadBtn.IsActive = true;
        _encodePanel.Visible = false;
        _uploadPanel.Visible = true;
        await EnsureWebViewAsync();
    }

    private async Task BrowseAndEnqueueAsync()
    {
        var pattern = string.Join(";", SupportedExtensions.Select(x => "*" + x));
        using var dlg = new OpenFileDialog
        {
            Filter = $"Video files|{pattern}|All files|*.*",
            Title = "Select video(s)",
            Multiselect = true
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            foreach (var f in dlg.FileNames) EnqueueFile(f);
            await DrainQueueAsync();
        }
    }

    private static string FormatSupportedListShort() =>
        string.Join(", ", SupportedExtensions.Select(x => x.TrimStart('.').ToUpperInvariant()));

    private void EnqueueFile(string path)
    {
        var ext = Path.GetExtension(path);
        if (string.IsNullOrEmpty(ext) || !SupportedExtensions.Contains(ext))
        {
            ShowResult($"Unsupported: {(string.IsNullOrEmpty(ext) ? "(none)" : ext)} — Supported: {FormatSupportedListShort()}", isError: true);
            return;
        }
        _encodeQueue.Enqueue(path);
        RefreshQueueUI();

        if (!_isEncoding)
            _ = DrainQueueAsync();
    }

    private async Task DrainQueueAsync()
    {
        if (_isEncoding) return;

        while (_encodeQueue.Count > 0)
        {
            var next = _encodeQueue.Dequeue();
            RefreshQueueUI();
            await RunSingleEncodeAsync(next);
        }

        _dropZone.IsBusy = false;
        _selectButton.Enabled = true;
        _cancelButton.Visible = false;
    }

    private async Task RunSingleEncodeAsync(string inputPath)
    {
        _isEncoding = true;
        _selectButton.Enabled = false;
        _dropZone.IsBusy = true;
        _cancelButton.Visible = true;
        _progressBar.Visible = true;
        _stageLabel.Visible = true;
        _percentLabel.Visible = true;
        _resultLabel.Text = $"Encoding: {Path.GetFileName(inputPath)}";
        _resultLabel.ForeColor = ColorTheme.Muted;
        _progressBar.Value = 0;
        _stageLabel.Text = "Preparing…";
        _percentLabel.Text = "0%";

        _encodeCts?.Dispose();
        _encodeCts = new CancellationTokenSource();

        var dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var nameNoExt = Path.GetFileNameWithoutExtension(inputPath);
        var expectedOutput = Path.Combine(dir, $"{nameNoExt} Upload Method - Oncxzy.mp4");

        string outputPath = "";

        try
        {
            var encoder = new VideoEncoder(_baseDir);

            if (!encoder.ToolsExist())
            {
                ShowResult(
                    "Encoder tools not found.\n" +
                    $"ffmpeg: {encoder.FfmpegPath}\n" +
                    $"ffprobe: {encoder.FfprobePath}",
                    isError: true);
                AddHistory(inputPath, "", false, "tools not found");
                return;
            }

            var progress = new Progress<EncoderProgress>(OnEncodeProgress);
            outputPath = await encoder.RunPipelineAsync(inputPath, progress, _encodeCts.Token);

            _stageLabel.Text = "Done";
            _percentLabel.Text = "100%";
            _progressBar.Value = 100;
            ShowResult($"Saved: {outputPath}", isError: false);
            AddHistory(inputPath, outputPath, true, "ok");
        }
        catch (OperationCanceledException)
        {
            try { if (File.Exists(expectedOutput)) File.Delete(expectedOutput); } catch { }
            ShowResult("Cancelled.", isError: true);
            AddHistory(inputPath, expectedOutput, false, "cancelled");
        }
        catch (Exception ex)
        {
            ShowResult("Error: " + ex.Message, isError: true);
            AddHistory(inputPath, outputPath, false, ex.Message.Length > 40 ? ex.Message[..40] + "…" : ex.Message);
        }
        finally
        {
            _isEncoding = false;
        }
    }

    private void OnEncodeProgress(EncoderProgress p)
    {
        if (InvokeRequired) { try { BeginInvoke(new Action<EncoderProgress>(OnEncodeProgress), p); } catch { } return; }
        _stageLabel.Text = p.Stage;
        _percentLabel.Text = $"{(int)Math.Round(p.Percent)}%";
        _progressBar.Value = (int)Math.Round(p.Percent);
    }

    private void ShowResult(string message, bool isError)
    {
        _resultLabel.Text = message;
        _resultLabel.ForeColor = isError ? ColorTheme.Danger : ColorTheme.Success;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        try { _encodeCts?.Cancel(); } catch { }
        try { _webView?.Dispose(); } catch { }
        base.OnFormClosed(e);
    }
}
