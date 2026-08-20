using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DMF
{
  public partial class DMForm : Form
  {
    /* Application Settings */
    private Settings settings = new();
    private readonly string settingsFile;
    /* Application paths and folders */
    private readonly string UpdateCacheFile = Path.Combine(GetAppDataFolder(), "update_cache.json");
    /* UI placeholders and defaults */
    private const string InputPlaceholder = "Select input file...";
    private const string OutputPlaceholder = "Select output file...";
    private const string TimePlaceholder = "HH:MM:SS";
    /* Supported formats */
    private readonly List<string> audioFormats = ["mp3", "m4a", "aac", "flac", "wav", "ogg", "opus", "ac3"];
    private readonly List<string> videoFormats = ["mp4", "avi", "mkv", "mov", "webm", "flv", "wmv", "m4v", "ts", "gif"];
    /* GitHub update settings */
    private const string GithubOwner = "INexizI";
    private const string GithubRepo = "DMF";
    private const string GithubReleasesUrl = $"https://api.github.com/repos/{GithubOwner}/{GithubRepo}/releases/latest";
    private const int UpdateCheckIntervalHours = 1;
    /* Application constants */
    private const string FfmpegExecutable = "ffmpeg";
    private const string FfprobeExecutable = "ffprobe";
    private const string ExplorerExecutable = "explorer.exe";
    private const string FfmpegDownloadUrl = "https://ffmpeg.org/download.html";
    private const int ProcessCheckTimeoutMs = 3000;
    private const int WindowEdgeOffset = 50;
    /* Presets */
    private const string PresetWeb = "Web";
    private const string PresetHD = "HD";
    private const string PresetMobile = "Mobile";
    private const string PresetLossless = "Lossless";
    private const string Preset2K = "2K";
    private const string Preset4K = "4K";
    private const string PresetGif = "GIF";
    /* UI Controls */
    // Basic
    private TextBox inputFile = null!;
    private Button btnInput = null!;
    private TextBox outputFile = null!;
    private Button btnOutput = null!;
    private ComboBox format = null!;
    private ComboBox trimMode = null!;
    private TextBox startTime = null!;
    private TextBox endTime = null!;
    private ComboBox audioCodec = null!;
    private ComboBox videoCodec = null!;
    private Label audioCodecHint = null!;
    private Label videoCodecHint = null!;
    private CheckBox audioOnly = null!;
    private CheckBox overwrite = null!;
    private CheckBox openOnSuccess = null!;
    // Video
    private NumericUpDown crf = null!;
    private ComboBox preset = null!;
    private ComboBox pixelFormat = null!;
    private TextBox videoBitrate = null!;
    private TextBox maxrate = null!;
    private TextBox bufsize = null!;
    private ComboBox profile = null!;
    private NumericUpDown gop = null!;
    private NumericUpDown videoFps = null!;
    private ComboBox colorMatrix = null!;
    private ComboBox colorRange = null!;
    // Audio
    private TextBox audioBitrate = null!;
    private NumericUpDown audioQuality = null!;
    // Filters
    private TextBox videoFilter = null!;
    private TextBox audioFilter = null!;
    // Subtitles
    private CheckBox chkSubtitles = null!;
    private RadioButton rbSubFromInput = null!;
    private RadioButton rbSubExternal = null!;
    private NumericUpDown subTrackNumber = null!;
    private TextBox subExternalFile = null!;
    private Button btnSubBrowse = null!;
    private CheckBox chkSubCopy = null!;
    // Advanced
    private TextBox mapStreams = null!;
    private ComboBox hwAccel = null!;
    private ComboBox hwAccelOutput = null!;
    // Common
    private Button btnProcess = null!;
    private Button btnCancel = null!;
    private Label status = null!;
    private ProgressBar progressBar = null!;
    private bool _autoOutput = false;
    private ToolTip toolTip = new ToolTip();
    private double inputDuration = 0;
    private NumericUpDown gifFps = null!;
    private NumericUpDown gifScaleW = null!;
    private NumericUpDown gifScaleH = null!;
    private TextBox gifCrop = null!;
    private CheckBox chkPalette = null!;
    private ComboBox gifDither = null!;
    private NumericUpDown gifBayerScale = null!;
    private Button btnUpdatePreview = null!;
    private string? previewTempFile = null;
    private PreviewForm? _previewForm = null;
    private bool _updatingFormatFromPath = false;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _ffmpegAvailable = false;
    private bool _ffmpegChecked = false;
    private string? _lastFfmpegPath = null;
    private string _ffmpegVersion = "Checking...";
    private string _ffprobeVersion = "Checking...";
    // Info
    private Label dmfVersion = null!;
    private Label ffmpegVersion = null!;
    private Label ffprobeVersion = null!;
    private Label ffmpegPath = null!;
    private Label ffprobePath = null!;
    private Label settingsPathLabel = null!;
    private Label logPathLabel = null!;
    private Label dotNetVersion = null!;
    private Label osVersion = null!;

    private readonly Dictionary<string, string> videoCodecDescriptions = new()
    {
      { "copy", "Stream copy\n(no re-encode)" },
      { "libx264", "H.264 / AVC\n(software, widely compatible)" },
      { "libx265", "H.265 / HEVC\n(software, high compression)" },
      { "libvpx-vp9", "VP9\n(open, good compression)" },
      { "libvpx", "VP8\n(older open format)" },
      { "mpeg4", "MPEG-4 part 2\n(Xvid/DivX compatible)" },
      { "libxvid", "Xvid\n(MPEG-4 ASP)" },
      { "mpeg2video", "MPEG-2\n(DVD, broadcast)" },
      { "wmv2", "Windows Media Video 2" },
      { "h264_nvenc", "NVIDIA hardware H.264" },
      { "hevc_nvenc", "NVIDIA hardware HEVC" },
      { "h264_amf", "AMD hardware H.264" },
      { "hevc_amf", "AMD hardware HEVC" },
      { "h264_qsv", "Intel QuickSync H.264" },
      { "hevc_qsv", "Intel QuickSync HEVC" },
      { "libaom-av1", "AV1\n(software, very slow)" }
    };
    private readonly Dictionary<string, string> audioCodecDescriptions = new()
    {
      { "copy", "Stream copy\n(no re-encode)" },
      { "aac", "AAC\n(Advanced Audio Coding)" },
      { "libfdk_aac", "Fraunhofer FDK AAC\n(high quality)" },
      { "mp3", "MPEG-1 Audio Layer III" },
      { "libmp3lame", "LAME MP3 encoder" },
      { "ac3", "Dolby Digital\n(AC-3)" },
      { "flac", "Free Lossless Audio Codec" },
      { "opus", "Opus\n(low-latency, high quality)" },
      { "libvorbis", "Vorbis\n(open, patent-free)" },
      { "pcm_s16le", "Uncompressed PCM\n(WAV-like)" },
      { "wav", "WAV\n(PCM 16-bit)" }
    };
    private readonly Dictionary<string, string> colorMatrixDescriptions = new()
    {
      { "bt709", "BT.709 (HD, SDR)" },
      { "bt470bg", "BT.470 BG (SD PAL)" },
      { "smpte170m", "SMPTE 170M (SD NTSC)" },
      { "bt2020nc", "BT.2020 NC (UHD, HDR)" },
      { "bt2020c", "BT.2020 C (UHD, constant luminance)" },
      { "ycgco", "Y'CgCo (color space)" }
    };
    private readonly Dictionary<string, string> colorRangeDescriptions = new()
    {
      { "limited", "Limited (TV range 16-235)" },
      { "full", "Full (PC range 0-255)" }
    };

    [Serializable]
    public class Settings
    {
      public int WinWidth { get; set; } = 800;
      public int WinHeight { get; set; } = 500;
      public int WinX { get; set; } = -1;
      public int WinY { get; set; } = -1;
      public bool WinMax { get; set; } = false;
      public bool OpenOnSuccess { get; set; } = true;
      public string? FfmpegPath { get; set; } = null;
      public string? FfprobePath { get; set; } = null;
    }

    private class UpdateCache
    {
      public DateTime LastCheckTime { get; set; } = DateTime.MinValue;
      public string? ETag { get; set; }
      public string? LatestVersion { get; set; }
    }

    public static class Logger
    {
      private static readonly Lock LockObj = new();
      private static string LogPath => Path.Combine(GetAppDataFolder(), "log.txt");
      private static readonly int MaxLogSizeBytes = 5 * 1024 * 1024;

      static Logger() => RotateLogIfNeeded();

      private static void RotateLogIfNeeded()
      {
        try
        {
          if (File.Exists(LogPath) && new FileInfo(LogPath).Length > MaxLogSizeBytes)
          {
            string oldPath = Path.Combine(Path.GetDirectoryName(LogPath)!, "log.txt");
            if (File.Exists(oldPath)) File.Delete(oldPath);
            File.Move(LogPath, oldPath);
          }
        }
        catch { /* ... */ }
      }

      private static void WriteLog(string level, string message)
      {
        try
        {
          string line = $"{DateTime.Now:yyyy-MM-dd HH-mm-ss.fff} [{level}] {message}";
          lock (LockObj) { File.AppendAllText(LogPath, line + Environment.NewLine); }
        }
        catch { /* ... */ }
      }

      public static void Info(string message) => WriteLog("INFO", message);
      public static void Warning(string message) => WriteLog("WARN", message);
      public static void Error(string message) => WriteLog("ERROR", message);
      public static void Debug(string message) => WriteLog("DEBUG", message);
    }

    public DMForm()
    {
      settingsFile = Path.Combine(GetAppDataFolder(), "settings.json");
      LoadSettings();
      InitializeForm();
      InitializeLayout();
      _ = UpdateInfoTabAsync();
      openOnSuccess.Checked = settings.OpenOnSuccess;
      UpdateProcessButton();
      SetPlaceholders();
      UpdateTimeFields();
      UpdateCodecHints();

      this.Shown += async (s, e) => await CheckForUpdatesAsync();

      if (!File.Exists(UpdateCacheFile))
      {
        SaveUpdateCache(new UpdateCache());
        Logger.Debug("Update cache file created");
      }

      if (!CheckFFmpeg())
      {
        status.Text = "FFmpeg not found";
        btnProcess.Enabled = false;
      }

      Logger.Info($"DMF started, version {GetCurrentVersion()}");
      Logger.Info($"Settings file: {settingsFile}");
    }

    private async Task CheckForUpdatesAsync()
    {
      Logger.Debug("Checking for updates...");
      try
      {
        var cache = LoadUpdateCache();

        if ((DateTime.Now - cache.LastCheckTime).TotalHours < UpdateCheckIntervalHours)
        {
          Logger.Debug($"Update check skipped: last check at {cache.LastCheckTime}");
          return;
        }

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "DMF-UpdateChecker");
        if (!string.IsNullOrEmpty(cache.ETag))
          client.DefaultRequestHeaders.Add("If-None-Match", cache.ETag);

        var response = await client.GetAsync(GithubReleasesUrl);

        if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
        {
          if (response.Headers.ETag != null)
            cache.ETag = response.Headers.ETag.Tag?.Trim('"');
          cache.LastCheckTime = DateTime.Now;
          SaveUpdateCache(cache);
          Logger.Debug("Update check: no new version (304)");
          return;
        }

        if (!response.IsSuccessStatusCode)
        {
          Logger.Warning($"Update check failed: {response.StatusCode}");
          return;
        }

        var jsonString = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(jsonString);
        var tagName = json.RootElement.GetProperty("tag_name").GetString();
        if (string.IsNullOrEmpty(tagName)) return;

        if (tagName.StartsWith('v')) tagName = tagName[1..];
        var latestVersion = new Version(tagName);
        var currentVersion = GetCurrentVersion();

        if (response.Headers.ETag != null)
          cache.ETag = response.Headers.ETag.Tag?.Trim('"');
        cache.LastCheckTime = DateTime.Now;
        cache.LatestVersion = tagName;
        SaveUpdateCache(cache);

        if (latestVersion > currentVersion)
        {
          Logger.Info($"New version {latestVersion} available (current {currentVersion})");
          var result = MessageBox.Show(
            $"New version {latestVersion} available (current: {currentVersion})\n\nDo you want to download it?",
            "Update Available",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);
          if (result == DialogResult.Yes)
            Process.Start(new ProcessStartInfo($"https://github.com/{GithubOwner}/{GithubRepo}/releases/latest") { UseShellExecute = true });
        }
      }
      catch (Exception ex) { Logger.Error($"Update check failed: {ex.Message}"); }
    }

    private UpdateCache LoadUpdateCache()
    {
      try
      {
        if (File.Exists(UpdateCacheFile))
        {
          string json = File.ReadAllText(UpdateCacheFile);
          return JsonSerializer.Deserialize<UpdateCache>(json) ?? new UpdateCache();
        }
      }
      catch { /* ... */ }
      return new UpdateCache();
    }

    private void SaveUpdateCache(UpdateCache cache)
    {
      try
      {
        string json = JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(UpdateCacheFile, json);
      }
      catch (Exception ex) { Debug.WriteLine($"Failed to save update cache: {ex.Message}"); }
    }

    private static bool IsExecutableAvailable(string path, string arguments, int timeoutMs = ProcessCheckTimeoutMs)
    {
      try
      {
        var procInfo = new ProcessStartInfo
        {
          FileName = path,
          Arguments = arguments,
          UseShellExecute = false,
          RedirectStandardOutput = true,
          CreateNoWindow = true
        };
        using var p = Process.Start(procInfo);
        if (p == null) return false;
        p.WaitForExit(ProcessCheckTimeoutMs);
        return p.HasExited && p.ExitCode == 0;
      }
      catch (Exception ex)
      {
        Logger.Error($"IsExecutableAvailable failed: {ex.Message}");
        return false;
      }
    }

    private bool CheckFFmpeg()
    {
      string ffmpegPath = settings.FfmpegPath ?? FfmpegExecutable;

      if (_ffmpegChecked && _lastFfmpegPath == ffmpegPath)
      {
        Logger.Debug($"Using cached FFmpeg check result: {_ffmpegAvailable} (path: {ffmpegPath})");
        return _ffmpegAvailable;
      }

      Logger.Debug($"Checking FFmpeg at '{ffmpegPath}'");
      bool available = IsExecutableAvailable(ffmpegPath, "-version", 3000);

      if (available)
      {
        Logger.Info($"FFmpeg found at '{ffmpegPath}'");
        _ffmpegAvailable = true;
        _ffmpegChecked = true;
        _lastFfmpegPath = ffmpegPath;
        return true;
      }

      string appDir = AppDomain.CurrentDomain.BaseDirectory;
      string localFfmpeg = Path.Combine(appDir, "ffmpeg.exe");
      if (File.Exists(localFfmpeg))
      {
        Logger.Info($"FFmpeg found in app directory: '{localFfmpeg}'");
        settings.FfmpegPath = localFfmpeg;
        SaveSettings();
        _ffmpegAvailable = true;
        _ffmpegChecked = true;
        _lastFfmpegPath = localFfmpeg;
        return true;
      }

      Logger.Warning($"FFmpeg not found at '{ffmpegPath}'");
      _ffmpegAvailable = false;
      _ffmpegChecked = true;
      _lastFfmpegPath = ffmpegPath;

      var result = MessageBox.Show(
        "FFmpeg not found.\n\nWould you like to specify the path to ffmpeg.exe?",
        "Missing FFmpeg",
        MessageBoxButtons.YesNo,
        MessageBoxIcon.Warning);

      if (result == DialogResult.Yes)
      {
        using var dialog = new OpenFileDialog
        {
          Title = "Select ffmpeg.exe",
          Filter = "Executable|ffmpeg.exe|All files|*.*"
        };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
          settings.FfmpegPath = dialog.FileName;
          SaveSettings();

          _ffmpegChecked = false;
          return CheckFFmpeg();
        }
        else
        {
          var download = MessageBox.Show(
            "FFmpeg is required to run this application.\nDo you want to open the download page?",
            "Download FFmpeg",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);
          if (download == DialogResult.Yes)
            Process.Start(new ProcessStartInfo(FfmpegDownloadUrl) { UseShellExecute = true });
          return false;
        }
      }
      else
      {
        var download = MessageBox.Show(
          "FFmpeg is required to run this application.\nDo you want to open the download page?",
          "Download FFmpeg",
          MessageBoxButtons.YesNo,
          MessageBoxIcon.Information);
        if (download == DialogResult.Yes)
          Process.Start(new ProcessStartInfo(FfmpegDownloadUrl) { UseShellExecute = true });
        return false;
      }
    }

    private static string GetAppDataFolder()
    {
      string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
      string appFolder = Path.Combine(localAppData, "DMF");
      if (!Directory.Exists(appFolder)) Directory.CreateDirectory(appFolder);
      return appFolder;
    }

    private void LoadSettings()
    {
      try
      {
        if (File.Exists(settingsFile))
        {
          string json = File.ReadAllText(settingsFile);
          settings = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
        }
        else
          settings = new Settings();
      }
      catch { settings = new Settings(); }

      _ffmpegChecked = false;
      _lastFfmpegPath = null;

      ApplySettings();
    }

    private void ApplySettings()
    {
      if (settings.WinMax)
        WindowState = FormWindowState.Maximized;
      else
      {
        Width = settings.WinWidth;
        Height = settings.WinHeight;

        var screen = Screen.PrimaryScreen;
        if (screen != null)
        {
          var workingArea = screen.WorkingArea;
          if (settings.WinX >= 0 && settings.WinY >= 0 &&
            settings.WinX < workingArea.Width - WindowEdgeOffset &&
            settings.WinY < workingArea.Height - WindowEdgeOffset)
            Location = new Point(settings.WinX, settings.WinY);
          else
            StartPosition = FormStartPosition.CenterScreen;
        }
        else
          StartPosition = FormStartPosition.CenterScreen;
      }

      FormBorderStyle = FormBorderStyle.Sizable;
      MaximizeBox = true;
      MinimumSize = new Size(800, 500);
    }

    private void SaveSettings()
    {
      try
      {
        if (WindowState == FormWindowState.Normal)
        {
          settings.WinWidth = Width;
          settings.WinHeight = Height;
          settings.WinX = Location.X;
          settings.WinY = Location.Y;
        }
        settings.WinMax = WindowState == FormWindowState.Maximized;
        settings.OpenOnSuccess = openOnSuccess.Checked;

        string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(settingsFile, json);
      }
      catch (Exception ex) { Console.WriteLine($"Error saving settings: {ex.Message}"); }
    }

    private void InitializeForm()
    {
      Text = "DMF";
      DoubleBuffered = true;

      this.AllowDrop = true;
      this.DragEnter += DMForm_DragEnter;
      this.DragDrop += DMForm_DragDrop;

      try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
      catch { /* ... */ }
    }

    private void InitializeLayout()
    {
      var mainContainer = new Panel { Dock = DockStyle.Fill };
      Controls.Add(mainContainer);

      var tabControl = new TabControl
      {
        Dock = DockStyle.Fill,
        Padding = new Point(10, 5)
      };
      mainContainer.Controls.Add(tabControl);

      /* Basic */
      var tabBasic = new TabPage("Basic");
      tabControl.TabPages.Add(tabBasic);
      var tableBasic = new TableLayoutPanel
      {
        Dock = DockStyle.Fill,
        ColumnCount = 3,
        RowCount = 9,
        Padding = new Padding(10),
        AutoSize = false
      };
      tableBasic.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
      tableBasic.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
      tableBasic.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
      tabBasic.Controls.Add(tableBasic);

      // Row 0: Input
      tableBasic.Controls.Add(new Label { Text = "Input:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 0);
      inputFile = new TextBox { Dock = DockStyle.Fill };
      inputFile.TextChanged += (s, e) =>
      {
        UpdateProcessButton();
        UpdateControlStates();
      };
      inputFile.GotFocus += (s, e) => RemovePlaceholder(inputFile, InputPlaceholder);
      inputFile.LostFocus += (s, e) => RestorePlaceholder(inputFile, InputPlaceholder);
      inputFile.Leave += async (s, e) => await UpdateDurationAsync();
      tableBasic.Controls.Add(inputFile, 1, 0);
      btnInput = new Button { Text = "Browse...", Dock = DockStyle.Fill };
      tableBasic.Controls.Add(btnInput, 2, 0);

      // Row 1: Output
      tableBasic.Controls.Add(new Label { Text = "Output:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 1);
      outputFile = new TextBox { Dock = DockStyle.Fill };
      outputFile.TextChanged += (s, e) => UpdateProcessButton();
      outputFile.GotFocus += (s, e) => RemovePlaceholder(outputFile, OutputPlaceholder);
      outputFile.LostFocus += (s, e) => RestorePlaceholder(outputFile, OutputPlaceholder);
      tableBasic.Controls.Add(outputFile, 1, 1);
      outputFile.TextChanged += OutputFile_TextChanged;
      btnOutput = new Button { Text = "Browse...", Dock = DockStyle.Fill };
      tableBasic.Controls.Add(btnOutput, 2, 1);

      // Row 2: Format
      tableBasic.Controls.Add(new Label { Text = "Format:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 2);
      format = new ComboBox
      {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
      };
      format.Items.AddRange(videoFormats.Cast<object>().ToArray());
      format.SelectedIndex = 0;
      tableBasic.Controls.Add(format, 1, 2);

      // Row 3: Trim mode
      tableBasic.Controls.Add(new Label { Text = "Trim mode:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 3);
      trimMode = new ComboBox
      {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Items = { "Source", "Range" },
        SelectedIndex = 0
      };
      trimMode.SelectedIndexChanged += TrimMode_SelectedIndexChanged;
      tableBasic.Controls.Add(trimMode, 1, 3);

      // Row 4: Start time
      tableBasic.Controls.Add(new Label { Text = "Start time:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 4);
      startTime = new TextBox { Dock = DockStyle.Fill };
      startTime.GotFocus += (s, e) => RemovePlaceholder(startTime, TimePlaceholder);
      startTime.LostFocus += (s, e) => RestorePlaceholder(startTime, TimePlaceholder);
      tableBasic.Controls.Add(startTime, 1, 4);

      // Row 5: End time
      tableBasic.Controls.Add(new Label { Text = "End time:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 5);
      endTime = new TextBox { Dock = DockStyle.Fill };
      endTime.GotFocus += (s, e) => RemovePlaceholder(endTime, TimePlaceholder);
      endTime.LostFocus += (s, e) => RestorePlaceholder(endTime, TimePlaceholder);
      tableBasic.Controls.Add(endTime, 1, 5);

      // Row 6: Video codec
      tableBasic.Controls.Add(new Label { Text = "Video codec:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 6);
      videoCodec = new ComboBox
      {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Items = { "copy", "libx264", "libx265", "libvpx-vp9", "libvpx", "mpeg4", "libxvid", "mpeg2video", "wmv2",
                  "h264_nvenc", "hevc_nvenc", "h264_amf", "hevc_amf", "h264_qsv", "hevc_qsv", "libaom-av1" },
        SelectedIndex = 0
      };
      videoCodec.SelectedIndexChanged += (s, e) => UpdateCodecHints();
      tableBasic.Controls.Add(videoCodec, 1, 6);
      videoCodecHint = new Label
      {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        ForeColor = Color.Gray,
        Font = new Font("Segoe UI", 8, FontStyle.Italic),
        AutoSize = false
      };
      tableBasic.Controls.Add(videoCodecHint, 2, 6);

      // Row 7: Audio codec
      tableBasic.Controls.Add(new Label { Text = "Audio codec:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 7);
      audioCodec = new ComboBox
      {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Items = { "copy", "aac", "libfdk_aac", "mp3", "libmp3lame", "ac3", "flac", "opus", "libvorbis", "pcm_s16le", "wav" },
        SelectedIndex = 0
      };
      audioCodec.SelectedIndexChanged += (s, e) => UpdateCodecHints();
      tableBasic.Controls.Add(audioCodec, 1, 7);
      audioCodecHint = new Label
      {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        ForeColor = Color.Gray,
        Font = new Font("Segoe UI", 8, FontStyle.Italic),
        AutoSize = false
      };
      tableBasic.Controls.Add(audioCodecHint, 2, 7);

      // Row 8: Checkboxes
      tableBasic.Controls.Add(new Label { Dock = DockStyle.Fill }, 0, 8);
      var checkPanel = new FlowLayoutPanel
      {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = false,
        AutoSize = true
      };
      audioOnly = new CheckBox { Text = "Audio only", AutoSize = true, Checked = false };
      audioOnly.CheckedChanged += ChkAudioOnly_CheckedChanged;
      overwrite = new CheckBox { Text = "Overwrite", AutoSize = true, Checked = true };
      openOnSuccess = new CheckBox { Text = "Open folder on success", AutoSize = true, Checked = true };
      checkPanel.Controls.Add(audioOnly);
      checkPanel.Controls.Add(overwrite);
      checkPanel.Controls.Add(openOnSuccess);
      tableBasic.Controls.Add(checkPanel, 1, 8);

      /* Presets */
      var tabPresets = new TabPage("Presets");
      tabControl.TabPages.Add(tabPresets);
      var tablePresets = new TableLayoutPanel
      {
        Dock = DockStyle.Fill,
        ColumnCount = 2,
        RowCount = 4,
        Padding = new Padding(10),
        AutoSize = false
      };
      tablePresets.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
      tablePresets.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
      tablePresets.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      tablePresets.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
      tablePresets.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      tabPresets.Controls.Add(tablePresets);

      // Row 0: Presets
      tablePresets.Controls.Add(new Label { Text = "Preset:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 0);
      var presetCombo = new ComboBox
      {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Items = { PresetWeb, PresetHD, PresetMobile, PresetLossless, Preset2K, Preset4K, PresetGif },
        SelectedIndex = 0
      };
      tablePresets.Controls.Add(presetCombo, 1, 0);

      // Row 1: Description
      tablePresets.Controls.Add(new Label { Text = "Description:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 1);
      var presetDescription = new Label
      {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Color.Gray,
        Font = new Font("Segoe UI", 9, FontStyle.Regular),
        AutoSize = false
      };
      tablePresets.Controls.Add(presetDescription, 1, 1);

      // Row 2: Apply
      var btnApplyPreset = new Button
      {
        Text = "Apply Preset",
        Dock = DockStyle.Fill,
        BackColor = Color.LightSteelBlue,
        AutoSize = false,
        Height = 30
      };
      tablePresets.Controls.Add(btnApplyPreset, 1, 2);
      tablePresets.Controls.Add(new Label { Dock = DockStyle.Fill }, 0, 2);

      presetCombo.SelectedIndexChanged += (s, e) =>
      {
        string selected = presetCombo.SelectedItem?.ToString() ?? "";
        presetDescription.Text = GetPresetDescription(selected);
      };

      btnApplyPreset.Click += (s, e) =>
      {
        string selected = presetCombo.SelectedItem?.ToString() ?? "";
        ApplyPreset(selected);
      };
      presetDescription.Text = GetPresetDescription(PresetWeb);

      /* Video */
      var tabVideo = new TabPage("Video");
      tabControl.TabPages.Add(tabVideo);
      var tableVideo = new TableLayoutPanel
      {
        Dock = DockStyle.Fill,
        ColumnCount = 3,
        RowCount = 12,
        Padding = new Padding(10),
        AutoSize = false
      };
      tableVideo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
      tableVideo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
      tableVideo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
      tabVideo.Controls.Add(tableVideo);

      // Row 0: CRF
      tableVideo.Controls.Add(new Label { Text = "CRF:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 0);
      crf = new NumericUpDown
      {
        Dock = DockStyle.Fill,
        Minimum = 0,
        Maximum = 51,
        Value = 23,
        Increment = 1
      };
      tableVideo.Controls.Add(crf, 1, 0);
      var crfHint = new Label { Text = "0–51 (lower = better)", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, ForeColor = Color.Gray };
      tableVideo.Controls.Add(crfHint, 2, 0);
      toolTip.SetToolTip(crfHint,
        "Quality control parameter.\n" +
        "For software codecs (libx264, libx265): CRF (0–51, lower = better).\n" +
        "For hardware codecs (NVENC, AMF, QSV): CQ/NVENC or QP/AMF-QSV (0–51, lower = better).");

      // Row 1: Preset
      tableVideo.Controls.Add(new Label { Text = "Preset:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 1);
      preset = new ComboBox
      {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Items = { "ultrafast", "superfast", "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow" },
        SelectedIndex = 5
      };
      tableVideo.Controls.Add(preset, 1, 1);
      tableVideo.Controls.Add(new Label { Text = "speed vs compression", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, ForeColor = Color.Gray }, 2, 1);

      // Row 2: Pixel Format
      tableVideo.Controls.Add(new Label { Text = "Pixel format:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 2);
      pixelFormat = new ComboBox
      {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Items =
        {
          "yuv420p",      // 8-bit, default
          "yuv422p",      // 8-bit
          "yuv444p",      // 8-bit
          "yuvj420p",     // 8-bit, JPEG range
          "yuvj422p",
          "yuvj444p",
          "yuv420p10le",  // 10-bit, little-endian
          "yuv422p10le",
          "yuv444p10le",
          "yuv420p12le",  // 12-bit (optional!?)
          "yuv422p12le",
          "yuv444p12le"
        },
        SelectedIndex = 0
      };
      tableVideo.Controls.Add(pixelFormat, 1, 2);
      var pixelHint = new Label
      {
        Text = "8-bit / 10-bit / 12-bit",
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill,
        ForeColor = Color.Gray
      };
      tableVideo.Controls.Add(pixelHint, 2, 2);
      toolTip.SetToolTip(pixelHint,
        "Pixel format determines bit depth and chroma subsampling.\n" +
        "• yuv420p  – 8-bit, 4:2:0 (most compatible)\n" +
        "• yuv422p  – 8-bit, 4:2:2\n" +
        "• yuv444p  – 8-bit, 4:4:4\n" +
        "• yuv420p10le – 10-bit, 4:2:0 (required for BT.2020 HDR)\n" +
        "• yuv422p10le – 10-bit, 4:2:2\n" +
        "• yuv444p10le – 10-bit, 4:4:4\n" +
        "• 12-bit formats – for high-end HDR/archival");

      // Row 3: Video bitrate
      tableVideo.Controls.Add(new Label { Text = "Bitrate (v):", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 3);
      videoBitrate = new TextBox { Dock = DockStyle.Fill, Text = "" };
      tableVideo.Controls.Add(videoBitrate, 1, 3);
      tableVideo.Controls.Add(new Label { Text = "e.g. 1500k, 2M", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, ForeColor = Color.Gray }, 2, 3);

      // Row 4: Maxrate
      tableVideo.Controls.Add(new Label { Text = "Maxrate:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 4);
      maxrate = new TextBox { Dock = DockStyle.Fill, Text = "" };
      tableVideo.Controls.Add(maxrate, 1, 4);
      tableVideo.Controls.Add(new Label { Text = "e.g. 2000k", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, ForeColor = Color.Gray }, 2, 4);

      // Row 5: Buffer size
      tableVideo.Controls.Add(new Label { Text = "Buffer size:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 5);
      bufsize = new TextBox { Dock = DockStyle.Fill, Text = "" };
      tableVideo.Controls.Add(bufsize, 1, 5);
      tableVideo.Controls.Add(new Label { Text = "e.g. 2000k", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, ForeColor = Color.Gray }, 2, 5);

      // Row 6: Profile
      tableVideo.Controls.Add(new Label { Text = "Profile:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 6);
      profile = new ComboBox
      {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Items =
        {
          "baseline",   // H.264 only
          "main",       // H.264 / H.265 (8-bit)
          "high",       // H.264 only
          "high10",     // H.264 10-bit
          "high422",    // H.264 4:2:2
          "high444",    // H.264 4:4:4
          "main10",     // H.265 10-bit
          "main422-10", // H.265 4:2:2 10-bit
          "main444-10"  // H.265 4:4:4 10-bit
        },
        SelectedIndex = 1
      };
      tableVideo.Controls.Add(profile, 1, 6);
      tableVideo.Controls.Add(new Label
      {
        Text = "check compatibility\nwith codec/bit depth",
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill,
        ForeColor = Color.Gray
      }, 2, 6);

      // Row 7: GOP size
      tableVideo.Controls.Add(new Label { Text = "GOP size:", Padding = new Padding(0, 4, 0, 0), TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Top }, 0, 7);
      gop = new NumericUpDown
      {
        Dock = DockStyle.Fill,
        Minimum = 0,
        Maximum = 1000,
        Value = 0,
        Increment = 10
      };
      tableVideo.Controls.Add(gop, 1, 7);
      tableVideo.Controls.Add(new Label { Text = "0 = default, max - 1000", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, ForeColor = Color.Gray }, 2, 7);

      // Row 8: Output FPS
      tableVideo.Controls.Add(new Label { Text = "Output FPS:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 8);
      videoFps = new NumericUpDown
      {
        Dock = DockStyle.Fill,
        Minimum = 0,
        Maximum = 120,
        Value = 0,
        Increment = 1
      };
      tableVideo.Controls.Add(videoFps, 1, 8);
      tableVideo.Controls.Add(new Label { Text = "0 = source FPS", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, ForeColor = Color.Gray }, 2, 8);

      // Row 9: Color Matrix
      tableVideo.Controls.Add(new Label { Text = "Color matrix:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 9);
      colorMatrix = new ComboBox
      {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList
      };
      colorMatrix.Items.AddRange(colorMatrixDescriptions.Keys.Cast<object>().ToArray());
      colorMatrix.SelectedIndex = 0;
      tableVideo.Controls.Add(colorMatrix, 1, 9);
      var matrixHint = new Label
      {
        Text = "color space metadata",
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill,
        ForeColor = Color.Gray
      };
      tableVideo.Controls.Add(matrixHint, 2, 9);
      toolTip.SetToolTip(matrixHint,
        "Defines the color matrix (color space) for the output video.\n" +
        "• bt709 – HD/SDR (default)\n" +
        "• bt470bg – SD PAL\n" +
        "• smpte170m – SD NTSC\n" +
        "• bt2020nc – UHD HDR (BT.2020 non-constant)\n" +
        "• bt2020c – UHD HDR (constant luminance)\n" +
        "• ycgco – Y'CgCo\n\n" +
        "Not all codecs support all matrices. Use with appropriate codecs.");

      // Row 10: Color Range
      tableVideo.Controls.Add(new Label { Text = "Color range:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 10);
      colorRange = new ComboBox
      {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList
      };
      colorRange.Items.AddRange(colorRangeDescriptions.Keys.Cast<object>().ToArray());
      colorRange.SelectedIndex = 0;
      tableVideo.Controls.Add(colorRange, 1, 10);
      var rangeHint = new Label
      {
        Text = "luma range metadata",
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill,
        ForeColor = Color.Gray
      };
      tableVideo.Controls.Add(rangeHint, 2, 10);
      toolTip.SetToolTip(rangeHint,
        "Defines the luma range (limited vs full).\n" +
        "• limited – TV range (16-235) – typical for broadcast\n" +
        "• full – PC range (0-255) – typical for computer monitors\n\n" +
        "Set according to your target display.");

      /* Audio */
      var tabAudio = new TabPage("Audio");
      tabControl.TabPages.Add(tabAudio);
      var tableAudio = new TableLayoutPanel
      {
        Dock = DockStyle.Fill,
        ColumnCount = 3,
        RowCount = 3,
        Padding = new Padding(10),
        AutoSize = false
      };
      tableAudio.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
      tableAudio.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
      tableAudio.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
      tabAudio.Controls.Add(tableAudio);

      // Row 0: Audio bitrate
      tableAudio.Controls.Add(new Label { Text = "Bitrate:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 0);
      audioBitrate = new TextBox { Dock = DockStyle.Fill, Text = "" };
      tableAudio.Controls.Add(audioBitrate, 1, 0);
      tableAudio.Controls.Add(new Label { Text = "e.g. 128k", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, ForeColor = Color.Gray }, 2, 0);

      // Row 1: Audio quality
      tableAudio.Controls.Add(new Label { Text = "Audio quality:", TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 5, 0, 0), Dock = DockStyle.Top }, 0, 1);
      audioQuality = new NumericUpDown
      {
        Dock = DockStyle.Fill,
        Minimum = 0,
        Maximum = 10,
        Value = 2,
        Increment = 1
      };
      tableAudio.Controls.Add(audioQuality, 1, 1);
      tableAudio.Controls.Add(new Label { Text = "VBR (0–10, lower = better)", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, ForeColor = Color.Gray }, 2, 1);

      /* Filters */
      var tabFilters = new TabPage("Filters");
      tabControl.TabPages.Add(tabFilters);
      var tableFilters = new TableLayoutPanel
      {
        Dock = DockStyle.Fill,
        ColumnCount = 3,
        RowCount = 5,
        Padding = new Padding(10),
        AutoSize = false
      };
      tableFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
      tableFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
      tableFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
      tabFilters.Controls.Add(tableFilters);

      // Row 0: Video filter
      tableFilters.Controls.Add(new Label { Text = "Video filter:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 0);
      videoFilter = new TextBox { Dock = DockStyle.Fill, Text = "" };
      tableFilters.Controls.Add(videoFilter, 1, 0);
      Label videoHint = new Label { Text = "e.g. fade=in:0:5", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, ForeColor = Color.Gray };
      tableFilters.Controls.Add(videoHint, 2, 0);
      toolTip.SetToolTip(videoHint,
        "Common video filters:\n" +
        "\n" +
        "• scale=W:H – resize (use -2 for auto even height)\n" +
        "• crop=W:H:X:Y – crop video\n" +
        "• hflip / vflip – flip horizontally/vertically\n" +
        "• rotate=A – rotate by angle (degrees)\n" +
        "• transpose=dir – rotate/reflect (1=90°CW, 2=180°, 3=270°CW)\n" +
        "• fade=in:0:30 – fade in/out\n" +
        "• overlay=X:Y – overlay another video\n" +
        "• unsharp – sharpen/soften (see docs)\n" +
        "• eq – brightness, contrast, saturation");

      // Row 1: Video filter hint
      tableFilters.Controls.Add(new Label { Text = "Video examples:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill, ForeColor = Color.Gray }, 0, 1);
      var hintVideo = new Label
      {
        Text = "scale=1280:-2, crop=1920:1080:0:0, hflip, vflip, rotate=45, transpose=1, fade=out:0:30, overlay=10:10, unsharp=5:5:1.0, eq=contrast=1.2:brightness=0.1:saturation=1.0\n",
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill,
        ForeColor = Color.Gray,
        Font = new Font("Segoe UI", 8, FontStyle.Regular),
        AutoSize = true,
        UseCompatibleTextRendering = true
      };
      tableFilters.SetColumnSpan(hintVideo, 2);
      tableFilters.Controls.Add(hintVideo, 1, 1);

      // Row 2: Audio filter
      tableFilters.Controls.Add(new Label { Text = "Audio filter:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 2);
      audioFilter = new TextBox { Dock = DockStyle.Fill, Text = "" };
      tableFilters.Controls.Add(audioFilter, 1, 2);
      Label audioHint = new Label { Text = "e.g. volume=2", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, ForeColor = Color.Gray };
      tableFilters.Controls.Add(audioHint, 2, 2);
      toolTip.SetToolTip(audioHint,
        "Common audio filters:\n" +
        "\n" +
        "• volume – (e.g. volume=1.5, volume=0.5)\n" +
        "• afade – fade in/out (type, start seconds, duration)\n" +
        "• equalizer – equalizer (frequency, type, width, gain)\n" +
        "• pan – pan audio (e.g. mix to mono)\n" +
        "• aecho – echo (in_gain, out_gain, delay, decay)\n" +
        "• chorus – chorus effect\n" +
        "• areverse – reverse audio\n" +
        "• asetrate – change sample rate (Hz)\n" +
        "• aresample – resample audio");

      // Row 3: Audio filter hint
      tableFilters.Controls.Add(new Label { Text = "Audio examples:", TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 5, 0, 0), Dock = DockStyle.Fill, ForeColor = Color.Gray }, 0, 3);
      var hintAudio = new Label
      {
        Text = "volume=1.5, afadet=in:ss=0:d=5, equalizerf=100:t=h:w=1:g=-10, pan=mono|c0=0.5*c0+0.5*c1, aecho=0.8:0.9:1000:0.3, chorus=0.7:0.9:55:0.4:0.25:2, areverse, asetrate=44100, aresample=44100\n",
        TextAlign = ContentAlignment.BottomLeft,
        Dock = DockStyle.Fill,
        ForeColor = Color.Gray,
        Font = new Font("Segoe UI", 8, FontStyle.Regular),
        AutoSize = true,
        UseCompatibleTextRendering = true
      };
      tableFilters.SetColumnSpan(hintAudio, 2);
      tableFilters.Controls.Add(hintAudio, 1, 3);

      /* Subtitles */
      var tabSubtitles = new TabPage("Subtitles");
      tabControl.TabPages.Add(tabSubtitles);
      var tableSubtitles = new TableLayoutPanel
      {
        Dock = DockStyle.Fill,
        ColumnCount = 3,
        RowCount = 5,
        Padding = new Padding(10),
        AutoSize = false
      };
      tableSubtitles.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
      tableSubtitles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
      tableSubtitles.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
      tabSubtitles.Controls.Add(tableSubtitles);

      // Row 0: Enable subtitles
      chkSubtitles = new CheckBox { Text = "Add subtitles", Dock = DockStyle.Fill, Checked = false };
      chkSubtitles.CheckedChanged += (s, e) => UpdateControlStates();
      tableSubtitles.Controls.Add(chkSubtitles, 1, 0);

      // Row 1: Source selection
      tableSubtitles.Controls.Add(new Label { Text = "Source:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 1);
      var sourcePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, MaximumSize = new Size(0, 25), FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
      rbSubFromInput = new RadioButton { Text = "From input file", AutoSize = true, Checked = true };
      rbSubExternal = new RadioButton { Text = "External file", AutoSize = true };
      sourcePanel.Controls.Add(rbSubFromInput);
      sourcePanel.Controls.Add(rbSubExternal);
      tableSubtitles.Controls.Add(sourcePanel, 1, 1);

      // Row 2: Track number (for input)
      tableSubtitles.Controls.Add(new Label { Text = "Track number:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 2);
      subTrackNumber = new NumericUpDown
      {
        Minimum = 0,
        Maximum = 10,
        Value = 0,
        Increment = 1,
        Dock = DockStyle.Fill
      };
      tableSubtitles.Controls.Add(subTrackNumber, 1, 2);
      tableSubtitles.Controls.Add(new Label { Text = "0 = first track", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, ForeColor = Color.Gray }, 2, 2);

      // Row 3: External file
      tableSubtitles.Controls.Add(new Label { Text = "File:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 3);
      subExternalFile = new TextBox { Dock = DockStyle.Fill, Enabled = false };
      btnSubBrowse = new Button { Text = "Browse...", Dock = DockStyle.Fill, AutoSize = false, Enabled = false };
      btnSubBrowse.Click += (s, e) =>
      {
        using var dialog = new OpenFileDialog
        {
          Title = "Select subtitle file",
          Filter = "Subtitle files|*.srt;*.ass;*.ssa;*.vtt|All files|*.*"
        };
        if (dialog.ShowDialog() == DialogResult.OK) subExternalFile.Text = dialog.FileName;
      };
      var browsePanel = new TableLayoutPanel { Dock = DockStyle.Fill, MaximumSize = new Size(0, 25), ColumnCount = 2, RowCount = 1 };
      browsePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
      browsePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
      browsePanel.Controls.Add(subExternalFile, 0, 0);
      browsePanel.Controls.Add(btnSubBrowse, 1, 0);
      tableSubtitles.Controls.Add(browsePanel, 1, 3);

      // Row 4: Copy subtitles
      chkSubCopy = new CheckBox { Text = "Copy subtitles (no re-encode)", Dock = DockStyle.Fill, MaximumSize = new Size(0, 25), Checked = true };
      tableSubtitles.Controls.Add(chkSubCopy, 1, 4);
      tableSubtitles.Controls.Add(new Label
      {
        Text = "Recommended",
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill,
        MaximumSize = new Size(0, 25),
        ForeColor = Color.Gray
      }, 2, 4);

      rbSubFromInput.CheckedChanged += (s, e) =>
      {
        subTrackNumber.Enabled = rbSubFromInput.Checked;
        subExternalFile.Enabled = rbSubExternal.Checked;
        btnSubBrowse.Enabled = rbSubExternal.Checked;
      };

      /* Advanced */
      var tabAdvanced = new TabPage("Advanced");
      tabControl.TabPages.Add(tabAdvanced);
      var tableAdvanced = new TableLayoutPanel
      {
        Dock = DockStyle.Fill,
        ColumnCount = 3,
        RowCount = 2,
        Padding = new Padding(10),
        AutoSize = false
      };
      tableAdvanced.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
      tableAdvanced.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
      tableAdvanced.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
      tabAdvanced.Controls.Add(tableAdvanced);

      // Row 0: Map
      tableAdvanced.Controls.Add(new Label { Text = "Map streams:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 0);
      mapStreams = new TextBox { Dock = DockStyle.Fill, Text = "" };
      tableAdvanced.Controls.Add(mapStreams, 1, 0);
      tableAdvanced.Controls.Add(new Label { Text = "e.g. 0:v:0 0:a:1", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, ForeColor = Color.Gray }, 2, 0);

      // Row 1: Hardware acceleration
      tableAdvanced.Controls.Add(new Label { Text = "HW Accel:", TextAlign = ContentAlignment.BottomRight, Padding = new Padding(0, 10, 0, 0), Dock = DockStyle.Top }, 0, 1);
      var hwPanel = new TableLayoutPanel
      {
        Dock = DockStyle.Fill,
        ColumnCount = 2,
        RowCount = 1,
        Padding = new Padding(0)
      };
      hwPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
      hwPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
      hwAccel = new ComboBox
      {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Items = { "none", "cuda", "vaapi", "qsv", "d3d11va", "vulkan" },
        SelectedIndex = 0
      };
      hwAccel.SelectedIndexChanged += (s, e) => hwAccelOutput.Enabled = hwAccel.SelectedIndex != 0;
      hwAccelOutput = new ComboBox
      {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Items = { "cuda", "vaapi", "qsv", "d3d11va" },
        SelectedIndex = 0,
        Enabled = false
      };
      hwPanel.Controls.Add(hwAccel, 0, 0);
      hwPanel.Controls.Add(hwAccelOutput, 1, 0);
      tableAdvanced.Controls.Add(hwPanel, 1, 1);
      tableAdvanced.Controls.Add(new Label { Text = "decoder/output", TextAlign = ContentAlignment.BottomLeft, Dock = DockStyle.Top, ForeColor = Color.Gray }, 2, 1);

      /* GIF */
      var tabGif = new TabPage("GIF");
      tabControl.TabPages.Add(tabGif);
      var tableGif = new TableLayoutPanel
      {
        Dock = DockStyle.Fill,
        ColumnCount = 3,
        RowCount = 7,
        Padding = new Padding(10),
        AutoSize = false
      };
      for (int i = 0; i < tableGif.RowCount; i++)
        tableGif.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      tableGif.RowStyles.Add(new RowStyle(SizeType.AutoSize));

      tableGif.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
      tableGif.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
      tableGif.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
      tabGif.Controls.Add(tableGif);

      // Row 0: Output FPS
      tableGif.Controls.Add(new Label { Text = "Output FPS:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 0);
      gifFps = new NumericUpDown
      {
        Dock = DockStyle.Fill,
        Minimum = 0,
        Maximum = 60,
        Value = 0,
        Increment = 1
      };
      tableGif.Controls.Add(gifFps, 1, 0);
      tableGif.Controls.Add(new Label { Text = "0 = source FPS", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, ForeColor = Color.Gray }, 2, 0);

      // Row 1: Scale
      tableGif.Controls.Add(new Label { Text = "Scale:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 1);
      var scalePanel = new TableLayoutPanel
      {
        Dock = DockStyle.Fill,
        ColumnCount = 4,
        RowCount = 1,
        Padding = new Padding(0),
        MaximumSize = new Size(0, 30)
      };
      scalePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
      scalePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
      scalePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
      scalePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));

      gifScaleW = new NumericUpDown { Minimum = 0, Maximum = 4096, Value = 0, Increment = 10, Dock = DockStyle.Fill };
      gifScaleH = new NumericUpDown { Minimum = 0, Maximum = 4096, Value = 0, Increment = 10, Dock = DockStyle.Fill };

      scalePanel.Controls.Add(new Label { Text = "W:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 0);
      scalePanel.Controls.Add(gifScaleW, 1, 0);
      scalePanel.Controls.Add(new Label { Text = "H:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 2, 0);
      scalePanel.Controls.Add(gifScaleH, 3, 0);

      tableGif.Controls.Add(scalePanel, 1, 1);
      tableGif.Controls.Add(new Label { Text = "0 = auto (keep aspect if set)", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, ForeColor = Color.Gray }, 2, 1);

      // Row 2: Crop
      tableGif.Controls.Add(new Label { Text = "Crop:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 2);
      gifCrop = new TextBox
      {
        Dock = DockStyle.Fill,
        ForeColor = Color.Gray,
        Text = "w:h:x:y (0 for auto)"
      };

      gifCrop.GotFocus += (s, e) => RemovePlaceholder(gifCrop, "w:h:x:y (0 for auto)");
      gifCrop.LostFocus += (s, e) => RestorePlaceholder(gifCrop, "w:h:x:y (0 for auto)");
      tableGif.Controls.Add(gifCrop, 1, 2);

      var cropHint = new Label
      {
        Text = "e.g. 640:480:0:0",
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill,
        ForeColor = Color.Gray
      };
      toolTip.SetToolTip(cropHint,
        "Crop filter syntax:\n" +
        "• w:h:x:y – width, height, x offset, y offset\n" +
        "• Use 0 for auto (e.g. 0:480:0:0 – auto width)\n" +
        "• iw/ih variables: e.g. iw-100:ih-100:50:50\n" +
        "• Example: crop=640:480:0:0");
      tableGif.Controls.Add(cropHint, 2, 2);

      // Row 3: Palette
      tableGif.Controls.Add(new Label { Text = "Palette:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 3);
      chkPalette = new CheckBox { Text = "Use palette generation (better quality)", AutoSize = true, Dock = DockStyle.Fill, Checked = true };
      tableGif.Controls.Add(chkPalette, 1, 3);
      tableGif.Controls.Add(new Label { Text = "recommended", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, ForeColor = Color.Gray }, 2, 3);

      // Row 4: Dithering
      tableGif.Controls.Add(new Label { Text = "Dithering:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 4);
      gifDither = new ComboBox
      {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Items = { "bayer", "heckbert", "floyd_steinberg", "sierra2", "sierra2_4a", "none" },
        SelectedIndex = 0
      };
      gifDither.SelectedIndexChanged += (s, e) =>
      {
        string dither = gifDither.SelectedItem?.ToString() ?? "";
        gifBayerScale.Enabled = dither == "bayer";
      };
      tableGif.Controls.Add(gifDither, 1, 4);
      tableGif.Controls.Add(new Label { Text = "dithering algorithm", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, ForeColor = Color.Gray }, 2, 4);

      // Row 5: Bayer scale
      tableGif.Controls.Add(new Label { Text = "Bayer scale:", TextAlign = ContentAlignment.BottomRight, Dock = DockStyle.Top }, 0, 5);
      gifBayerScale = new NumericUpDown
      {
        Dock = DockStyle.Fill,
        Minimum = 0,
        Maximum = 5,
        Value = 5,
        Increment = 1,
        Enabled = false
      };
      tableGif.Controls.Add(gifBayerScale, 1, 5);
      tableGif.Controls.Add(new Label { Text = "0–5 (for Bayer dither)", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, ForeColor = Color.Gray }, 2, 5);

      /* Info */
      var tabInfo = new TabPage("Info");
      tabControl.TabPages.Add(tabInfo);

      var tableInfo = new TableLayoutPanel
      {
        Dock = DockStyle.Fill,
        ColumnCount = 3,
        RowCount = 11,
        Padding = new Padding(10),
        AutoSize = false
      };
      tableInfo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
      tableInfo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
      tableInfo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
      for (int i = 0; i < 11; i++) tableInfo.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      tabInfo.Controls.Add(tableInfo);

      // Row 0: DMF version
      tableInfo.Controls.Add(new Label { Text = "DMF version:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 0);
      dmfVersion = new Label { Text = "...", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, ForeColor = Color.Gray };
      tableInfo.Controls.Add(dmfVersion, 1, 0);
      tableInfo.Controls.Add(new Label { Dock = DockStyle.Fill }, 2, 0);

      // Row 1: FFmpeg version
      tableInfo.Controls.Add(new Label { Text = "FFmpeg version:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 1);
      ffmpegVersion = new Label
      {
        Text = "...",
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill,
        ForeColor = Color.Gray,
        AutoSize = false
      };
      tableInfo.SetColumnSpan(ffmpegVersion, 2);
      tableInfo.Controls.Add(ffmpegVersion, 1, 1);

      // Row 2: FFprobe version
      tableInfo.Controls.Add(new Label { Text = "FFprobe version:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 2);
      ffprobeVersion = new Label
      {
        Text = "...",
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill,
        ForeColor = Color.Gray,
        AutoSize = false
      };
      tableInfo.SetColumnSpan(ffprobeVersion, 2);
      tableInfo.Controls.Add(ffprobeVersion, 1, 2);

      // Row 3: FFmpeg path
      tableInfo.Controls.Add(new Label { Text = "FFmpeg path:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 3);
      ffmpegPath = new Label { Text = "...", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, ForeColor = Color.Gray };
      tableInfo.Controls.Add(ffmpegPath, 1, 3);
      var btnChangeFfmpeg = new Button { Text = "Change", Dock = DockStyle.Fill, AutoSize = false };
      btnChangeFfmpeg.Click += (s, e) => ChangeFFmpegPath();
      tableInfo.Controls.Add(btnChangeFfmpeg, 2, 3);

      // Row 4: FFprobe path
      tableInfo.Controls.Add(new Label { Text = "FFprobe path:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 4);
      ffprobePath = new Label { Text = "...", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, ForeColor = Color.Gray };
      tableInfo.Controls.Add(ffprobePath, 1, 4);
      var btnChangeFfprobe = new Button { Text = "Change", Dock = DockStyle.Fill, AutoSize = false };
      btnChangeFfprobe.Click += (s, e) => ChangeFFprobePath();
      tableInfo.Controls.Add(btnChangeFfprobe, 2, 4);

      // Row 5: Settings path
      tableInfo.Controls.Add(new Label { Text = "Settings path:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 5);
      settingsPathLabel = new Label
      {
        Text = "...",
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill,
        ForeColor = Color.Gray,
        AutoSize = false
      };
      tableInfo.SetColumnSpan(settingsPathLabel, 2);
      tableInfo.Controls.Add(settingsPathLabel, 1, 5);

      // Row 6: Log path
      tableInfo.Controls.Add(new Label { Text = "Log path:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 6);
      logPathLabel = new Label
      {
        Text = "...",
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill,
        ForeColor = Color.Gray,
        AutoSize = false
      };
      tableInfo.SetColumnSpan(logPathLabel, 2);
      tableInfo.Controls.Add(logPathLabel, 1, 6);

      // Row 7: .NET version
      tableInfo.Controls.Add(new Label { Text = ".NET version:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 7);
      dotNetVersion = new Label { Text = "...", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, ForeColor = Color.Gray };
      tableInfo.Controls.Add(dotNetVersion, 1, 7);
      tableInfo.Controls.Add(new Label { Dock = DockStyle.Fill }, 2, 7);

      // Row 8: OS version
      tableInfo.Controls.Add(new Label { Text = "OS version:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 8);
      osVersion = new Label { Text = "...", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, ForeColor = Color.Gray };
      tableInfo.Controls.Add(osVersion, 1, 8);
      tableInfo.Controls.Add(new Label { Dock = DockStyle.Fill }, 2, 8);

      // Row 9: Button Panel
      var buttonPanel = new TableLayoutPanel
      {
        Dock = DockStyle.Fill,
        ColumnCount = 2,
        RowCount = 1,
        AutoSize = true,
        Margin = new Padding(0, 10, 0, 0)
      };
      buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
      buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

      var copyButton = new Button
      {
        Text = "Copy info",
        Dock = DockStyle.Fill,
        BackColor = Color.LightSteelBlue,
        AutoSize = false,
        Height = 30
      };
      copyButton.Click += (s, e) =>
      {
        string info = $"DMF: {dmfVersion.Text}\n" +
                      $"FFmpeg: {ffmpegVersion.Text}\n" +
                      $"FFprobe: {ffprobeVersion.Text}\n" +
                      $"FFmpeg path: {ffmpegPath.Text}\n" +
                      $"FFprobe path: {ffprobePath.Text}\n" +
                      $"Settings: {settingsPathLabel.Text}\n" +
                      $"Log: {logPathLabel.Text}\n" +
                      $".NET: {dotNetVersion.Text}\n" +
                      $"OS: {osVersion.Text}";
        Clipboard.SetText(info);
        status.Text = "Info copied to clipboard";
      };
      buttonPanel.Controls.Add(copyButton, 0, 0);

      var openFolderButton = new Button
      {
        Text = "Open data folder",
        Dock = DockStyle.Fill,
        BackColor = Color.LightSteelBlue,
        AutoSize = false,
        Height = 30
      };
      openFolderButton.Click += (s, e) => OpenFolder(Path.Combine(GetAppDataFolder(), "DMF"));
      buttonPanel.Controls.Add(openFolderButton, 1, 0);

      tableInfo.Controls.Add(new Label { Dock = DockStyle.Fill }, 0, 9);
      tableInfo.Controls.Add(buttonPanel, 1, 9);
      tableInfo.SetColumnSpan(buttonPanel, 2);

      /* Bottom Panel/Buttons */
      var bottomPanel = new Panel
      {
        Dock = DockStyle.Bottom,
        Height = 80,
        Padding = new Padding(10)
      };
      mainContainer.Controls.Add(bottomPanel);

      var bottomLayout = new TableLayoutPanel
      {
        Dock = DockStyle.Fill,
        ColumnCount = 1,
        RowCount = 2,
        Padding = new Padding(0)
      };
      bottomLayout.RowStyles.Clear();
      bottomLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
      bottomLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
      bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
      bottomPanel.Controls.Add(bottomLayout);

      progressBar = new ProgressBar
      {
        Dock = DockStyle.Fill,
        Style = ProgressBarStyle.Marquee,
        Visible = false
      };
      bottomLayout.Controls.Add(progressBar, 0, 0);

      var actionPanel = new TableLayoutPanel
      {
        Dock = DockStyle.Fill,
        ColumnCount = 3,
        RowCount = 1,
        Padding = new Padding(0)
      };
      actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
      actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
      actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
      bottomLayout.Controls.Add(actionPanel, 0, 1);

      btnProcess = new Button
      {
        Text = "Run FFmpeg",
        Dock = DockStyle.Fill,
        BackColor = Color.LightGreen,
        Enabled = false
      };
      actionPanel.Controls.Add(btnProcess, 0, 0);

      btnCancel = new Button
      {
        Text = "Cancel",
        Dock = DockStyle.Fill,
        BackColor = Color.LightCoral,
        Enabled = false,
        Visible = false
      };
      btnCancel.Click += BtnCancel_Click;
      actionPanel.Controls.Add(btnCancel, 0, 0);

      status = new Label
      {
        Text = "Ready",
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(10, 0, 0, 0)
      };
      actionPanel.Controls.Add(status, 1, 0);

      btnUpdatePreview = new Button
      {
        Text = "Open Preview",
        AutoSize = true,
        Dock = DockStyle.Fill,
        BackColor = Color.LightSkyBlue,
        Enabled = false
      };
      actionPanel.Controls.Add(btnUpdatePreview, 2, 0);

      btnInput.Click += BtnInput_Click;
      btnOutput.Click += BtnOutput_Click;
      btnProcess.Click += BtnProcess_Click;
      btnUpdatePreview.Click += BtnUpdatePreview_Click;
      format.SelectedIndexChanged += Format_SelectedIndexChanged;
      audioOnly.CheckedChanged += ChkAudioOnly_CheckedChanged;

      audioCodec.SelectedIndexChanged += (s, e) => { UpdateCodecHints(); UpdateControlStates(); };
      videoCodec.SelectedIndexChanged += (s, e) => { UpdateCodecHints(); UpdateControlStates(); };
      videoBitrate.TextChanged += (s, e) =>
      {
        if (!string.IsNullOrWhiteSpace(videoBitrate.Text))
          crf.Value = 0;
        UpdateControlStates();
      };
      crf.ValueChanged += (s, e) => UpdateControlStates();
      tabControl.SelectedIndexChanged += async (s, e) =>
      {
        if (tabControl.SelectedTab == tabInfo)
          await UpdateInfoTabAsync();
      };

      UpdateTimeFields();
      UpdateControlStates();
    }

    private void DMForm_DragEnter(object? sender, DragEventArgs e)
    {
      if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        e.Effect = DragDropEffects.Copy;
      else
        e.Effect = DragDropEffects.None;
    }

    private void DMForm_DragDrop(object? sender, DragEventArgs e)
    {
      if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
        return;

      string file = files[0];
      if (!File.Exists(file)) return;
      Logger.Info($"Input file selected via drag&drop: '{file}'");

      inputFile.Text = file;
      inputFile.ForeColor = SystemColors.WindowText;

      if (_autoOutput || string.IsNullOrWhiteSpace(outputFile.Text) || IsPlaceholder(outputFile, OutputPlaceholder))
        SetAutoOutput();

      _ = UpdateDurationAsync();
      UpdateControlStates();
    }

    private void SetPlaceholders()
    {
      SetPlaceholder(inputFile, InputPlaceholder);
      SetPlaceholder(outputFile, OutputPlaceholder);
      SetPlaceholder(startTime, TimePlaceholder);
      SetPlaceholder(endTime, TimePlaceholder);
    }

    private static void SetPlaceholder(TextBox tb, string placeholder)
    {
      if (string.IsNullOrWhiteSpace(tb.Text))
      {
        tb.Text = placeholder;
        tb.ForeColor = Color.Gray;
      }
    }

    private static void RemovePlaceholder(TextBox tb, string placeholder)
    {
      if (tb.Text == placeholder)
      {
        tb.Text = "";
        tb.ForeColor = SystemColors.WindowText;
      }
    }

    private static void RestorePlaceholder(TextBox tb, string placeholder)
    {
      if (string.IsNullOrWhiteSpace(tb.Text))
      {
        tb.Text = placeholder;
        tb.ForeColor = Color.Gray;
      }
    }

    private static bool IsPlaceholder(TextBox tb, string placeholder) => tb.Text == placeholder;

    private void OutputFile_TextChanged(object? sender, EventArgs e)
    {
      if (format == null) return;
      if (_updatingFormatFromPath) return;
      string path = outputFile.Text;
      if (string.IsNullOrWhiteSpace(path) || IsPlaceholder(outputFile, OutputPlaceholder))
        return;
      string extension = Path.GetExtension(path);
      if (string.IsNullOrEmpty(extension))
        return;
      extension = extension.TrimStart('.').ToLowerInvariant();
      if (string.IsNullOrEmpty(extension))
        return;

      List<string> formatList = audioOnly.Checked ? audioFormats : videoFormats;
      int index = formatList.FindIndex(f => f.Equals(extension, StringComparison.OrdinalIgnoreCase));
      if (index >= 0)
      {
        _updatingFormatFromPath = true;
        try
        {
          for (int i = 0; i < format.Items.Count; i++)
          {
            if (format.Items[i].ToString()?.Equals(extension, StringComparison.OrdinalIgnoreCase) == true)
            {
              format.SelectedIndex = i;
              break;
            }
          }
        }
        finally { _updatingFormatFromPath = false; }
      }
    }

    private void UpdateCodecHints()
    {
      if (audioCodecHint != null && audioCodec != null)
      {
        string selected = audioCodec.SelectedItem?.ToString() ?? "";
        audioCodecHint.Text = audioCodecDescriptions.TryGetValue(selected, out string? desc) ? desc : "";
      }
      if (videoCodecHint != null && videoCodec != null)
      {
        string selected = videoCodec.SelectedItem?.ToString() ?? "";
        videoCodecHint.Text = videoCodecDescriptions.TryGetValue(selected, out string? desc) ? desc : "";
      }
    }

    private string GetDefaultOutputPath()
    {
      string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
      if (!Directory.Exists(downloads))
        downloads = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

      string baseName = Path.GetFileNameWithoutExtension(inputFile.Text);
      if (string.IsNullOrEmpty(baseName) || IsPlaceholder(inputFile, InputPlaceholder))
        baseName = "output";
      else
        baseName = "output-" + baseName;

      string fmt = format.SelectedItem?.ToString() ?? "mp4";
      string fileName = $"{baseName}.{fmt}";
      return Path.Combine(downloads, fileName);
    }

    private void SetDefaultOutputIfEmpty()
    {
      if (string.IsNullOrWhiteSpace(outputFile.Text) || IsPlaceholder(outputFile, OutputPlaceholder))
      {
        string defaultPath = GetDefaultOutputPath();
        outputFile.Text = defaultPath;
        outputFile.ForeColor = SystemColors.WindowText;
        _autoOutput = true;
      }
    }

    private void UpdateProcessButton()
    {
      bool inputValid = !string.IsNullOrWhiteSpace(inputFile.Text) && !IsPlaceholder(inputFile, InputPlaceholder);
      bool outputValid = !string.IsNullOrWhiteSpace(outputFile.Text) && !IsPlaceholder(outputFile, OutputPlaceholder);
      btnProcess.Enabled = inputValid && outputValid;
    }

    private void TrimMode_SelectedIndexChanged(object? sender, EventArgs e)
    {
      UpdateTimeFields();
      FillTimeFieldsIfEmpty();
    }

    private void UpdateControlStates()
    {
      if (inputFile == null || format == null) return;

      bool isGif = format.SelectedItem?.ToString() == "gif";

      if (isGif)
      {
        // ----- GIF mode: disable everything except GIF-specific controls -----
        videoCodec.Enabled = false;
        crf.Enabled = false;
        preset.Enabled = false;
        pixelFormat.Enabled = false;
        videoBitrate.Enabled = false;
        maxrate.Enabled = false;
        bufsize.Enabled = false;
        profile.Enabled = false;
        gop.Enabled = false;
        videoFps.Enabled = false;
        colorMatrix.Enabled = false;
        colorRange.Enabled = false;
        audioCodec.Enabled = false;
        audioBitrate.Enabled = false;
        audioQuality.Enabled = false;
        videoFilter.Enabled = false;
        audioFilter.Enabled = false;
        mapStreams.Enabled = false;
        hwAccel.Enabled = false;
        hwAccelOutput.Enabled = false;
        audioOnly.Enabled = false;
        audioOnly.Checked = false;
        gifFps.Enabled = true;
        gifScaleW.Enabled = true;
        gifScaleH.Enabled = true;
        gifCrop.Enabled = true;
        chkPalette.Enabled = true;
        gifDither.Enabled = true;
        gifBayerScale.Enabled = gifDither.SelectedItem?.ToString() == "bayer";

        return;
      }

      // ----- Normal video/audio mode -----
      gifFps.Enabled = false;
      gifScaleW.Enabled = false;
      gifScaleH.Enabled = false;
      gifCrop.Enabled = false;
      chkPalette.Enabled = false;
      gifDither.Enabled = false;
      gifBayerScale.Enabled = false;

      audioOnly.Enabled = true;
      audioCodec.Enabled = true;

      bool audioOnlyChecked = audioOnly.Checked;
      string videoCodecSelected = videoCodec.SelectedItem?.ToString() ?? "copy";
      string audioCodecSelected = audioCodec.SelectedItem?.ToString() ?? "copy";
      bool videoBitrateSet = !string.IsNullOrWhiteSpace(videoBitrate.Text);

      // ------ Video controls ------
      bool videoEnabled = !audioOnlyChecked;
      videoCodec.Enabled = videoEnabled;

      bool encodingEnabled = videoEnabled && videoCodecSelected != "copy";
      crf.Enabled = encodingEnabled && !videoBitrateSet;
      preset.Enabled = encodingEnabled;
      pixelFormat.Enabled = encodingEnabled;
      videoBitrate.Enabled = encodingEnabled;
      maxrate.Enabled = encodingEnabled && videoBitrateSet;
      bufsize.Enabled = encodingEnabled && videoBitrateSet;
      profile.Enabled = encodingEnabled;
      gop.Enabled = encodingEnabled;
      videoFps.Enabled = encodingEnabled;
      colorMatrix.Enabled = encodingEnabled;
      colorRange.Enabled = encodingEnabled;
      videoFilter.Enabled = videoEnabled;

      // ------ Audio controls ------
      bool audioEncoding = audioCodecSelected != "copy";
      audioBitrate.Enabled = audioEncoding;
      audioQuality.Enabled = audioEncoding;

      // ------ Advanced controls ------
      mapStreams.Enabled = true;
      hwAccel.Enabled = true;
      hwAccelOutput.Enabled = hwAccel.SelectedIndex != 0;

      // ------ Subtitles controls ------
      bool subtitlesEnabled = chkSubtitles.Checked && !audioOnlyChecked && videoEnabled;
      chkSubtitles.Enabled = !audioOnlyChecked;
      rbSubFromInput.Enabled = subtitlesEnabled;
      rbSubExternal.Enabled = subtitlesEnabled;
      subTrackNumber.Enabled = subtitlesEnabled && rbSubFromInput.Checked;
      subExternalFile.Enabled = subtitlesEnabled && rbSubExternal.Checked;
      btnSubBrowse.Enabled = subtitlesEnabled && rbSubExternal.Checked;
      chkSubCopy.Enabled = subtitlesEnabled;

      // ------ Preview controls ------
      bool previewEnabled = !audioOnly.Checked &&
                            !string.IsNullOrWhiteSpace(inputFile.Text) &&
                            !IsPlaceholder(inputFile, InputPlaceholder) &&
                            File.Exists(inputFile.Text);
      btnUpdatePreview.Enabled = previewEnabled;
    }

    private void ForceUpdateTimeFields()
    {
      if (trimMode.SelectedItem?.ToString() != "Range") return;

      startTime.Text = "00:00:00";
      startTime.ForeColor = SystemColors.WindowText;

      if (inputDuration > 0)
      {
        endTime.Text = TimeSpan.FromSeconds(inputDuration).ToString(@"hh\:mm\:ss");
        endTime.ForeColor = SystemColors.WindowText;
      }
    }

    private void FillTimeFieldsIfEmpty()
    {
      if (trimMode.SelectedItem?.ToString() != "Range") return;

      if (IsPlaceholder(startTime, TimePlaceholder) || string.IsNullOrWhiteSpace(startTime.Text))
      {
        startTime.Text = "00:00:00";
        startTime.ForeColor = SystemColors.WindowText;
      }

      if (inputDuration > 0 && (IsPlaceholder(endTime, TimePlaceholder) || string.IsNullOrWhiteSpace(endTime.Text)))
      {
        endTime.Text = TimeSpan.FromSeconds(inputDuration).ToString(@"hh\:mm\:ss");
        endTime.ForeColor = SystemColors.WindowText;
      }
    }

    private async Task UpdateDurationAsync()
    {
      if (File.Exists(inputFile.Text))
      {
        inputDuration = await GetInputDurationAsync(inputFile.Text);
        ForceUpdateTimeFields();
      }
    }

    private static async Task<double> GetInputDurationAsync(string filePath)
    {
      try
      {
        var psi = new ProcessStartInfo
        {
          FileName = FfprobeExecutable,
          Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{filePath}\"",
          UseShellExecute = false,
          RedirectStandardOutput = true,
          CreateNoWindow = true
        };
        using var process = Process.Start(psi);
        if (process == null) return 0;
        string output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode == 0 && double.TryParse(output.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double duration))
          return duration;
        return 0;
      }
      catch { return 0; }
    }

    private void UpdateTimeFields()
    {
      if (startTime == null || endTime == null || trimMode == null) return;

      string mode = trimMode.SelectedItem?.ToString() ?? "Source";
      bool isRange = mode == "Range";

      startTime.Enabled = isRange;
      endTime.Enabled = isRange;

      if (!isRange)
      {
        if (!IsPlaceholder(startTime, TimePlaceholder))
        {
          startTime.Text = TimePlaceholder;
          startTime.ForeColor = Color.Gray;
        }
        if (!IsPlaceholder(endTime, TimePlaceholder))
        {
          endTime.Text = TimePlaceholder;
          endTime.ForeColor = Color.Gray;
        }
      }
    }

    private void UpdateTimeFieldsForRange()
    {
      if (trimMode.SelectedItem?.ToString() != "Range") return;

      if (IsPlaceholder(startTime, TimePlaceholder) || string.IsNullOrWhiteSpace(startTime.Text))
      {
        startTime.Text = "00:00:00";
        startTime.ForeColor = SystemColors.WindowText;
      }

      if (inputDuration > 0 && (IsPlaceholder(endTime, TimePlaceholder) || string.IsNullOrWhiteSpace(endTime.Text)))
      {
        endTime.Text = TimeSpan.FromSeconds(inputDuration).ToString(@"hh\:mm\:ss");
        endTime.ForeColor = SystemColors.WindowText;
      }
    }

    private void SetAutoOutput()
    {
      outputFile.Text = GetDefaultOutputPath();
      outputFile.ForeColor = SystemColors.WindowText;
      _autoOutput = true;
    }

    private static string GetPresetDescription(string presetName)
    {
      return presetName switch
      {
        PresetWeb => "H.264 / AAC, MP4, CRF 23, medium preset.\nGood quality for web streaming.",
        PresetHD => "H.264 High Profile, MP4, CRF 18, slow preset.\nHigh quality for HD content.",
        PresetMobile => "H.264 Baseline, MP4, CRF 25, veryfast preset, low bitrate.\nOptimized for mobile devices.",
        PresetLossless => "H.264 lossless (CRF 0), FLAC audio, MKV.\n" +
                          "4:4:4 color, high444 profile for maximum quality.\n" +
                          "Note: Windows thumbnails may not be generated for this format.",
        Preset2K => "H.264 High Profile, MP4, CRF 18, 12 Mbps, 30 fps, 2560x1440.\nHigh quality for 2K (QHD) content.",
        Preset4K => "H.265 (HEVC), MP4, CRF 20, 25 Mbps, 30 fps, 3840x2160.\n" +
                    "BT.2020, 10-bit (main10 profile), limited range.",
        PresetGif => "GIF with 30 fps, scaled to 640px width, palette generation with Bayer dithering.",
        _ => ""
      };
    }

    private void ApplyPreset(string presetName)
    {
      switch (presetName)
      {
        case PresetWeb:
          format.SelectedItem = "mp4";
          videoCodec.SelectedItem = "libx264";
          audioCodec.SelectedItem = "aac";
          crf.Value = 23;
          preset.SelectedItem = "medium";
          pixelFormat.SelectedItem = "yuv420p";
          profile.SelectedItem = "high";
          videoBitrate.Text = "";
          audioBitrate.Text = "128k";
          maxrate.Text = "";
          bufsize.Text = "";
          gop.Value = 0;
          colorMatrix.SelectedItem = "bt709";
          colorRange.SelectedItem = "limited";
          pixelFormat.SelectedItem = "yuv420p";
          audioOnly.Checked = false;
          break;

        case PresetHD:
          format.SelectedItem = "mp4";
          videoCodec.SelectedItem = "libx264";
          audioCodec.SelectedItem = "aac";
          crf.Value = 18;
          preset.SelectedItem = "slow";
          pixelFormat.SelectedItem = "yuv420p";
          profile.SelectedItem = "high";
          videoBitrate.Text = "";
          audioBitrate.Text = "192k";
          maxrate.Text = "";
          bufsize.Text = "";
          gop.Value = 0;
          colorMatrix.SelectedItem = "bt709";
          colorRange.SelectedItem = "limited";
          pixelFormat.SelectedItem = "yuv420p";
          audioOnly.Checked = false;
          break;

        case PresetMobile:
          format.SelectedItem = "mp4";
          videoCodec.SelectedItem = "libx264";
          audioCodec.SelectedItem = "aac";
          crf.Value = 25;
          preset.SelectedItem = "veryfast";
          pixelFormat.SelectedItem = "yuv420p";
          profile.SelectedItem = "baseline";
          videoBitrate.Text = "500k";
          audioBitrate.Text = "64k";
          maxrate.Text = "";
          bufsize.Text = "";
          gop.Value = 0;
          colorMatrix.SelectedItem = "bt709";
          colorRange.SelectedItem = "limited";
          pixelFormat.SelectedItem = "yuv420p";
          audioOnly.Checked = false;
          break;

        case PresetLossless:
          format.SelectedItem = "mkv";
          videoCodec.SelectedItem = "libx264";
          audioCodec.SelectedItem = "flac";
          crf.Value = 0;
          preset.SelectedItem = "ultrafast";
          pixelFormat.SelectedItem = "yuv444p";
          profile.SelectedItem = "high444";
          videoBitrate.Text = "";
          audioBitrate.Text = "";
          maxrate.Text = "";
          bufsize.Text = "";
          gop.Value = 0;
          colorMatrix.SelectedItem = "bt709";
          colorRange.SelectedItem = "limited";
          pixelFormat.SelectedItem = "yuv444p";
          audioOnly.Checked = false;
          break;

        case Preset2K:
          format.SelectedItem = "mp4";
          videoCodec.SelectedItem = "libx264";
          audioCodec.SelectedItem = "aac";
          crf.Value = 18;
          preset.SelectedItem = "slow";
          pixelFormat.SelectedItem = "yuv420p";
          profile.SelectedItem = "high";
          videoBitrate.Text = "12M";
          audioBitrate.Text = "192k";
          maxrate.Text = "18M";
          bufsize.Text = "24M";
          gop.Value = 0;
          videoFps.Value = 30;
          colorMatrix.SelectedItem = "bt709";
          colorRange.SelectedItem = "limited";
          pixelFormat.SelectedItem = "yuv420p";
          videoFilter.Text = "scale=2560:-2";
          audioOnly.Checked = false;
          break;

        case Preset4K:
          format.SelectedItem = "mp4";
          videoCodec.SelectedItem = "libx265";
          audioCodec.SelectedItem = "aac";
          crf.Value = 20;
          preset.SelectedItem = "medium";
          pixelFormat.SelectedItem = "yuv420p10le";
          profile.SelectedItem = "main10";
          videoBitrate.Text = "25M";
          audioBitrate.Text = "256k";
          maxrate.Text = "35M";
          bufsize.Text = "50M";
          gop.Value = 0;
          videoFps.Value = 30;
          colorMatrix.SelectedItem = "bt2020nc";
          colorRange.SelectedItem = "limited";
          videoFilter.Text = "scale=3840:-2";
          audioOnly.Checked = false;
          break;

        case PresetGif:
          format.SelectedItem = "gif";
          gifFps.Value = 30;
          gifScaleW.Value = 640;
          gifScaleH.Value = 0;
          gifCrop.Text = "w:h:x:y (0 for auto)";
          gifCrop.ForeColor = Color.Gray;
          chkPalette.Checked = true;
          gifDither.SelectedItem = "bayer";
          gifBayerScale.Value = 5;
          audioOnly.Checked = false;
          break;
      }

      UpdateControlStates();

      if (_previewForm != null && !_previewForm.IsDisposed && _previewForm.Visible)
        BtnUpdatePreview_Click(this, EventArgs.Empty);

      UpdateCodecHints();
    }

    private List<string> BuildGifArgs()
    {
      var args = new List<string>();

      int fps = (int)gifFps.Value;
      int w = (int)gifScaleW.Value;
      int h = (int)gifScaleH.Value;
      bool usePalette = chkPalette.Checked;
      string dither = gifDither.SelectedItem?.ToString() ?? "floyd_steinberg";
      string crop = gifCrop.Text.Trim();

      var filterParts = new List<string> { $"fps={fps}" };

      if (!string.IsNullOrEmpty(crop) && crop != "w:h:x:y (0 for auto)")
        filterParts.Add($"crop={crop}");

      string scaleFilter;
      if (w > 0 && h > 0)
        scaleFilter = $"scale={w}:{h}";
      else if (w > 0 && h == 0)
        scaleFilter = $"scale={w}:-2";
      else if (h > 0 && w == 0)
        scaleFilter = $"scale=-2:{h}";
      else
        scaleFilter = "scale=-2:-2";
      filterParts.Add($"{scaleFilter}:flags=lanczos");

      string filters = string.Join(",", filterParts);

      if (usePalette)
      {
        string paletteUseOptions = "";
        if (dither != "none")
        {
          paletteUseOptions = $"dither={dither}";
          if (dither == "bayer")
          {
            int bayerScale = (int)gifBayerScale.Value;
            if (bayerScale > 0)
              paletteUseOptions += $":bayer_scale={bayerScale}";
          }
        }

        string filterComplex =
          $"[0:v]{filters},split [a][b];" +
          $"[a]palettegen=stats_mode=full [p];" +
          $"[b][p]paletteuse{(string.IsNullOrEmpty(paletteUseOptions) ? "" : "=" + paletteUseOptions)}";

        args.Add($"-filter_complex \"{filterComplex}\"");
        args.Add("-c:v gif");
      }
      else
      {
        args.Add($"-vf \"{filters}\"");
        args.Add("-c:v gif");
      }

      args.Add("-an");
      return args;
    }

    private string BuildPreviewArgs(string inputFile, string tempFile)
    {
      var args = new List<string>();

      if (trimMode.SelectedItem?.ToString() == "Range" && !IsPlaceholder(startTime, TimePlaceholder))
      {
        if (TimeSpan.TryParseExact(startTime.Text, @"h\:mm\:ss", CultureInfo.InvariantCulture, out var startTs) && startTs.TotalSeconds > 0)
          args.Add($"-ss {startTs:hh\\:mm\\:ss}");
      }

      args.Add($"-i \"{inputFile}\"");

      var filterParts = new List<string>();
      bool isGif = format.SelectedItem?.ToString() == "gif";

      if (isGif)
      {
        int fps = (int)gifFps.Value;
        int w = (int)gifScaleW.Value;
        int h = (int)gifScaleH.Value;

        filterParts.Add($"fps={fps}");

        string scaleFilter;
        if (w > 0 && h > 0)
          scaleFilter = $"scale={w}:{h}";
        else if (w > 0 && h == 0)
          scaleFilter = $"scale={w}:-2";
        else if (h > 0 && w == 0)
          scaleFilter = $"scale=-2:{h}";
        else
          scaleFilter = "scale=-2:-2";
        filterParts.Add($"{scaleFilter}:flags=lanczos");
      }
      else
      {
        string vf = videoFilter.Text.Trim();
        if (!string.IsNullOrEmpty(vf))
        {
          var parts = vf.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
          var filtered = new List<string>();
          foreach (var part in parts)
          {
            if (!part.StartsWith("crop", StringComparison.OrdinalIgnoreCase))
              filtered.Add(part);
          }
          if (filtered.Count > 0)
            filterParts.Add(string.Join(",", filtered));
        }
      }

      if (filterParts.Count > 0)
      {
        string filters = string.Join(",", filterParts);
        args.Add($"-vf \"{filters}\"");
      }

      args.Add("-vframes 1");
      args.Add("-f image2");
      args.Add("-vcodec png");
      args.Add($"\"{tempFile}\"");

      return string.Join(" ", args);
    }

    private static (int w, int h, int? x, int? y)? ParseCropFromFilter(string filter)
    {
      if (string.IsNullOrWhiteSpace(filter)) return null;

      var match = CropFilter().Match(filter);
      if (match.Success)
      {
        int w = int.Parse(match.Groups["w"].Value);
        int h = int.Parse(match.Groups["h"].Value);
        int? x = match.Groups["x"].Success ? int.Parse(match.Groups["x"].Value) : null;
        int? y = match.Groups["y"].Success ? int.Parse(match.Groups["y"].Value) : null;
        return (w, h, x, y);
      }
      return null;
    }

    private void DrawCropRectangle(Bitmap bitmap)
    {
      if (bitmap == null) return;

      int? cropW = null, cropH = null;
      int? cropX = null, cropY = null;
      bool isGif = format.SelectedItem?.ToString() == "gif";

      if (isGif)
      {
        string cropText = gifCrop.Text.Trim();
        if (!string.IsNullOrEmpty(cropText) && cropText != "w:h:x:y (0 for auto)")
        {
          var parts = cropText.Split(':', StringSplitOptions.RemoveEmptyEntries);
          if (parts.Length >= 2 && parts.Length <= 4 &&
            int.TryParse(parts[0], out int parsedW) &&
            int.TryParse(parts[1], out int parsedH))
          {
            cropW = parsedW;
            cropH = parsedH;
            if (parts.Length >= 3 && int.TryParse(parts[2], out int parsedX))
              cropX = parsedX;
            if (parts.Length >= 4 && int.TryParse(parts[3], out int parsedY))
              cropY = parsedY;
          }
        }
      }
      else
      {
        var parsed = ParseCropFromFilter(videoFilter.Text);
        if (parsed.HasValue)
        {
          cropW = parsed.Value.w;
          cropH = parsed.Value.h;
          cropX = parsed.Value.x;
          cropY = parsed.Value.y;
        }
      }

      if (!cropW.HasValue) return;

      int w = cropW.Value;
      int h = cropH.Value;
      int x, y;

      if (!cropX.HasValue || !cropY.HasValue)
      {
        x = (bitmap.Width - w) / 2;
        y = (bitmap.Height - h) / 2;
      }
      else
      {
        x = cropX.Value;
        y = cropY.Value;
      }

      if (w == 0) w = bitmap.Width - x;
      if (h == 0) h = bitmap.Height - y;
      if (x < 0) x = 0;
      if (y < 0) y = 0;

      w = Math.Min(w, bitmap.Width - x);
      h = Math.Min(h, bitmap.Height - y);
      if (w <= 0 || h <= 0) return;

      using var g = Graphics.FromImage(bitmap);
      g.DrawRectangle(new Pen(Color.Red, 3), x, y, w, h);
    }

    private string GetCropInfo(Bitmap bitmap)
    {
      if (bitmap == null) return "";

      int? cropW = null, cropH = null;
      int? cropX = null, cropY = null;
      bool isGif = format.SelectedItem?.ToString() == "gif";

      if (isGif)
      {
        string cropText = gifCrop.Text.Trim();
        if (!string.IsNullOrEmpty(cropText) && cropText != "w:h:x:y (0 for auto)")
        {
          var parts = cropText.Split(':', StringSplitOptions.RemoveEmptyEntries);
          if (parts.Length >= 2 && parts.Length <= 4 &&
            int.TryParse(parts[0], out int parsedW) &&
            int.TryParse(parts[1], out int parsedH))
          {
            cropW = parsedW;
            cropH = parsedH;
            if (parts.Length >= 3 && int.TryParse(parts[2], out int parsedX))
              cropX = parsedX;
            if (parts.Length >= 4 && int.TryParse(parts[3], out int parsedY))
              cropY = parsedY;
          }
        }
      }
      else
      {
        var parsed = ParseCropFromFilter(videoFilter.Text);
        if (parsed.HasValue)
        {
          cropW = parsed.Value.w;
          cropH = parsed.Value.h;
          cropX = parsed.Value.x;
          cropY = parsed.Value.y;
        }
      }

      if (!cropW.HasValue) return "";

      int w = cropW.Value;
      int h = cropH.Value;
      int x, y;

      if (!cropX.HasValue || !cropY.HasValue)
      {
        x = (bitmap.Width - w) / 2;
        y = (bitmap.Height - h) / 2;
      }
      else
      {
        x = cropX.Value;
        y = cropY.Value;
      }

      if (w == 0) w = bitmap.Width - x;
      if (h == 0) h = bitmap.Height - y;
      if (x < 0) x = 0;
      if (y < 0) y = 0;

      w = Math.Min(w, bitmap.Width - x);
      h = Math.Min(h, bitmap.Height - y);
      if (w <= 0 || h <= 0) return "";

      return $"crop: {w}x{h} (X={x}, Y={y})";
    }

    private void BtnInput_Click(object? sender, EventArgs e)
    {
      using var file = new OpenFileDialog();
      file.Title = "Select input file";
      file.Filter = "Media files|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.flv;*.webm|All files|*.*";
      if (file.ShowDialog() == DialogResult.OK)
      {
        Logger.Info($"Input file selected via dialog: '{file.FileName}'");
        inputFile.Text = file.FileName;
        inputFile.ForeColor = SystemColors.WindowText;

        if (_autoOutput || string.IsNullOrWhiteSpace(outputFile.Text) || IsPlaceholder(outputFile, OutputPlaceholder))
          SetAutoOutput();

        _ = UpdateDurationAsync();
        UpdateControlStates();
      }
    }

    private void BtnOutput_Click(object? sender, EventArgs e)
    {
      using var file = new SaveFileDialog();
      file.Title = "Select output file";

      string fmt = format.SelectedItem?.ToString() ?? "mp4";
      file.Filter = $"{fmt.ToUpper()} files|*.{fmt}|All files|*.*";
      file.DefaultExt = fmt;

      if (file.ShowDialog() == DialogResult.OK)
      {
        outputFile.Text = file.FileName;
        outputFile.ForeColor = SystemColors.WindowText;
        _autoOutput = false;
        UpdateProcessButton();
      }
    }

    private async void BtnUpdatePreview_Click(object? sender, EventArgs e)
    {
      if (string.IsNullOrWhiteSpace(inputFile.Text) || IsPlaceholder(inputFile, InputPlaceholder) || !File.Exists(inputFile.Text))
      {
        MessageBox.Show("Browse input file first.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return;
      }

      btnUpdatePreview.Enabled = false;
      status.Text = "Load frame...";
      progressBar.Visible = true;
      progressBar.Style = ProgressBarStyle.Marquee;

      try
      {
        string tempFile = Path.GetTempFileName() + ".png";
        previewTempFile = tempFile;

        string ffmpegArgs = BuildPreviewArgs(inputFile.Text, tempFile);
        await RunFFmpeg(FfmpegExecutable, ffmpegArgs, CancellationToken.None, 0, null);

        using var img = Image.FromFile(tempFile);
        var bitmap = new Bitmap(img);
        DrawCropRectangle(bitmap);
        string cropInfo = GetCropInfo(bitmap);

        if (_previewForm == null || _previewForm.IsDisposed)
        {
          _previewForm = new PreviewForm { Owner = this };
          _previewForm.FormClosed += (s, args) =>
           {
             btnUpdatePreview.Text = "Open Preview";
             _previewForm = null;
           };
          btnUpdatePreview.Text = "Update Preview";
        }
        _previewForm.UpdateImage(bitmap, cropInfo);
        _previewForm.Show();
        _previewForm.BringToFront();

        status.Text = "Preview updated";
      }
      catch (Exception ex)
      {
        status.Text = "Loading Error";
        MessageBox.Show($"Frame doesn't load: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
      finally
      {
        btnUpdatePreview.Enabled = true;
        progressBar.Visible = false;
      }
    }

    private void Format_SelectedIndexChanged(object? sender, EventArgs e)
    {
      if (format == null) return;

      bool isGif = format.SelectedItem?.ToString() == "gif";

      if (isGif)
      {
        if (!string.IsNullOrWhiteSpace(videoFilter.Text))
          videoFilter.Text = "";
        if (!string.IsNullOrWhiteSpace(audioFilter.Text))
          audioFilter.Text = "";
      }
      else
      {
        gifCrop.Text = "w:h:x:y (0 for auto)";
        gifCrop.ForeColor = Color.Gray;
        gifFps.Value = 30;
        gifScaleW.Value = 200;
        gifScaleH.Value = 0;
        chkPalette.Checked = true;
        gifDither.SelectedIndex = 0;
        gifBayerScale.Value = 5;
        gifBayerScale.Enabled = false;
      }

      UpdateControlStates();

      if (_autoOutput && !_updatingFormatFromPath && !string.IsNullOrWhiteSpace(outputFile.Text) && !IsPlaceholder(outputFile, OutputPlaceholder))
      {
        string current = outputFile.Text;
        string? dir = Path.GetDirectoryName(current);
        string fileName = Path.GetFileNameWithoutExtension(current);
        string newExt = format.SelectedItem?.ToString() ?? "mp4";
        string newPath = Path.Combine(dir ?? "", fileName + "." + newExt);
        if (!string.Equals(current, newPath, StringComparison.OrdinalIgnoreCase))
        {
          outputFile.Text = newPath;
          outputFile.ForeColor = SystemColors.WindowText;
        }
      }

      if (isGif)
      {
        audioOnly.Enabled = false;
        audioOnly.Checked = false;
      }
      else
        audioOnly.Enabled = true;

      if (_previewForm != null && !_previewForm.IsDisposed && _previewForm.Visible)
        BtnUpdatePreview_Click(sender, e);
    }

    private void TryAutoDetectFormat() => OutputFile_TextChanged(this, EventArgs.Empty);

    private void ChkAudioOnly_CheckedChanged(object? sender, EventArgs e)
    {
      if (format == null) return;
      bool audioOnlyChecked = audioOnly.Checked;
      string currentFormat = format.SelectedItem?.ToString() ?? "";

      format.Items.Clear();
      if (audioOnlyChecked)
        format.Items.AddRange(audioFormats.Cast<object>().ToArray());
      else
        format.Items.AddRange(videoFormats.Cast<object>().ToArray());

      int index = format.Items.IndexOf(currentFormat);
      if (index >= 0)
        format.SelectedIndex = index;
      else
        format.SelectedIndex = 0;

      if (_autoOutput && !string.IsNullOrWhiteSpace(inputFile.Text) && !IsPlaceholder(inputFile, InputPlaceholder))
        SetDefaultOutputIfEmpty();

      UpdateControlStates();
    }

    private bool ValidateParameters(out string errorMessage, out string warningMessage)
    {
      errorMessage = string.Empty;
      warningMessage = string.Empty;

      string videoCodecSelected = videoCodec.SelectedItem?.ToString() ?? "copy";
      string audioCodecSelected = audioCodec.SelectedItem?.ToString() ?? "copy";
      string formatSelected = format.SelectedItem?.ToString() ?? "mp4";
      bool audioOnlyChecked = audioOnly.Checked;
      bool hasVideoFilter = !string.IsNullOrWhiteSpace(videoFilter.Text);
      bool isHardwareCodec = videoCodecSelected.Contains("nvenc") ||
                             videoCodecSelected.Contains("amf") ||
                             videoCodecSelected.Contains("qsv");

      // 1. Conflict CRF / Video Bitrate
      bool hasCrf = crf.Value > 0;
      bool hasVideoBitrate = !string.IsNullOrWhiteSpace(videoBitrate.Text);
      if (hasCrf && hasVideoBitrate)
      {
        errorMessage = "Both CRF and Video Bitrate are set.\n"
                     + "FFmpeg will ignore CRF when bitrate is specified.\n"
                     + "Clear the bitrate field to use CRF, or clear CRF to use bitrate.";
        return false;
      }

      // 2. Warning: Filter + copy
      if (hasVideoFilter && videoCodecSelected == "copy")
      {
        warningMessage = "Video filter is active, but codec is set to 'copy'.\n"
                       + "The codec will be automatically switched to libx264 to apply the filter.";
      }

      // 3. Check format container and codec
      if (formatSelected != "gif" && videoCodecSelected != "copy")
      {
        if (formatSelected == "mp4" && videoCodecSelected.Contains("vp9", StringComparison.OrdinalIgnoreCase))
        {
          errorMessage = "MP4 does not support VP9. Use WebM container for VP9, or switch to H.264/H.265.";
          return false;
        }
        if (formatSelected == "webm" && (videoCodecSelected.Contains("h264", StringComparison.OrdinalIgnoreCase) ||
                                         videoCodecSelected.Contains("h265", StringComparison.OrdinalIgnoreCase) ||
                                         videoCodecSelected.Contains("hevc", StringComparison.OrdinalIgnoreCase)))
        {
          errorMessage = "WebM does not support H.264/H.265. Use VP9 or VP8 for WebM.";
          return false;
        }
        if (formatSelected == "avi" && (videoCodecSelected.Contains("h265", StringComparison.OrdinalIgnoreCase) ||
                                videoCodecSelected.Contains("hevc", StringComparison.OrdinalIgnoreCase) ||
                                videoCodecSelected.Contains("x265", StringComparison.OrdinalIgnoreCase)))
        {
          errorMessage = "AVI container does not support HEVC (H.265). Use MKV or MP4 for H.265.";
          return false;
        }
        if (formatSelected == "mov" && videoCodecSelected.Contains("libx265", StringComparison.OrdinalIgnoreCase))
        {
          errorMessage = "MOV container does not officially support H.265. Use MP4 or MKV for H.265.";
          return false;
        }
      }

      // 4. Check Audio
      if (formatSelected == "mp3" && audioCodecSelected != "mp3" && audioCodecSelected != "libmp3lame" && audioCodecSelected != "copy")
      {
        warningMessage += (string.IsNullOrEmpty(warningMessage) ? "" : "\n")
                        + "MP3 container usually contains MP3 audio. Consider selecting MP3 codec.";
      }
      if (formatSelected == "aac" && audioCodecSelected != "aac" && audioCodecSelected != "libfdk_aac" && audioCodecSelected != "copy")
      {
        warningMessage += (string.IsNullOrEmpty(warningMessage) ? "" : "\n")
                        + "AAC container typically contains AAC audio. Consider selecting AAC codec.";
      }
      if (formatSelected == "flac" && audioCodecSelected != "flac" && audioCodecSelected != "copy")
      {
        warningMessage += (string.IsNullOrEmpty(warningMessage) ? "" : "\n")
                        + "FLAC container expected FLAC audio. Consider selecting FLAC codec for lossless.";
      }

      // 5. GOP size
      if (gop.Value > 0 && gop.Value < 10)
      {
        warningMessage += (string.IsNullOrEmpty(warningMessage) ? "" : "\n")
                        + "Very small GOP size (<10) may reduce compression efficiency.";
      }

      // 6. Audio only + video codec
      if (audioOnlyChecked && videoCodecSelected != "copy")
      {
        warningMessage += (string.IsNullOrEmpty(warningMessage) ? "" : "\n")
                        + "Audio only mode is enabled, but video codec is not 'copy'. Video will be disabled (-vn).";
      }

      // 7. HW acceleration
      string hwAccelSelected = hwAccel.SelectedItem?.ToString() ?? "none";
      if (hwAccelSelected != "none" && videoCodecSelected != "copy")
      {
        bool isH264 = videoCodecSelected.Contains("h264", StringComparison.OrdinalIgnoreCase) ||
                      videoCodecSelected.Contains("264", StringComparison.OrdinalIgnoreCase);
        bool isH265 = videoCodecSelected.Contains("h265", StringComparison.OrdinalIgnoreCase) ||
                      videoCodecSelected.Contains("hevc", StringComparison.OrdinalIgnoreCase);
        if (!isH264 && !isH265)
        {
          warningMessage += (string.IsNullOrEmpty(warningMessage) ? "" : "\n")
                          + "Hardware acceleration enabled, but selected codec may not support it.\n"
                          + "H.264/H.265 are recommended for HW acceleration.";
        }
      }

      // 8. Check color params
      if (videoCodecSelected != "copy")
      {
        string matrix = colorMatrix.SelectedItem?.ToString() ?? "bt709";
        string range = colorRange.SelectedItem?.ToString() ?? "limited";

        if (videoCodecSelected.Contains("mpeg4", StringComparison.OrdinalIgnoreCase) ||
            videoCodecSelected.Contains("libxvid", StringComparison.OrdinalIgnoreCase) ||
            videoCodecSelected.Contains("mpeg2video", StringComparison.OrdinalIgnoreCase))
        {
          if (matrix.Contains("bt2020", StringComparison.OrdinalIgnoreCase))
          {
            string pixFmt = pixelFormat.SelectedItem?.ToString() ?? "yuv420p";
            if (!pixFmt.Contains("10le", StringComparison.OrdinalIgnoreCase) &&
                !pixFmt.Contains("p10", StringComparison.OrdinalIgnoreCase))
            {
              errorMessage = "BT.2020 color matrix requires a 10-bit pixel format (e.g., yuv420p10le, yuv422p10le, yuv444p10le).\n"
                           + "Please select a 10-bit pixel format in the Video tab.";
              return false;
            }
          }
        }

        if (videoCodecSelected.Contains("libvpx", StringComparison.OrdinalIgnoreCase) ||
            videoCodecSelected.Contains("libaom-av1", StringComparison.OrdinalIgnoreCase))
        {
          if (matrix.Contains("bt2020", StringComparison.OrdinalIgnoreCase) &&
              !videoCodecSelected.Contains("libaom-av1", StringComparison.OrdinalIgnoreCase))
            warningMessage += (string.IsNullOrEmpty(warningMessage) ? "" : "\n")
                            + "VP9 may not fully support BT.2020 with all FFmpeg builds. Consider using AV1 or H.265 for HDR.";
        }

        if (range == "full" && matrix.Contains("bt709", StringComparison.OrdinalIgnoreCase))
          warningMessage += (string.IsNullOrEmpty(warningMessage) ? "" : "\n")
                          + "Full range with BT.709 is unusual. Ensure your playback chain supports full range.";
      }
      else
      {
        if (colorMatrix.SelectedIndex != 0 || colorRange.SelectedIndex != 0)
          warningMessage += (string.IsNullOrEmpty(warningMessage) ? "" : "\n")
                          + "Color matrix and range are ignored when video codec is 'copy'. They will not be applied.";
      }

      // 9. Check BT.2020 and pixel format
      if (videoCodecSelected != "copy")
      {
        string matrix = colorMatrix.SelectedItem?.ToString() ?? "bt709";
        if (matrix.Contains("bt2020", StringComparison.OrdinalIgnoreCase))
        {
          string pixFmt = pixelFormat.SelectedItem?.ToString() ?? "yuv420p";
          if (!pixFmt.Contains("10le", StringComparison.OrdinalIgnoreCase) &&
              !pixFmt.Contains("p10", StringComparison.OrdinalIgnoreCase))
          {
            errorMessage = "BT.2020 color matrix requires a 10-bit pixel format (e.g., yuv420p10le, yuv422p10le, yuv444p10le).\n"
                         + "Please select a 10-bit pixel format in the Video tab.";
            return false;
          }
        }
      }

      // 10. Check profile / bit
      if (videoCodecSelected != "copy")
      {
        string selectedProfile = profile.SelectedItem?.ToString() ?? "";
        string selectedPixFmt = pixelFormat.SelectedItem?.ToString() ?? "yuv420p";
        bool is10Bit = selectedPixFmt.Contains("10le") || selectedPixFmt.Contains("p10");
        bool is12Bit = selectedPixFmt.Contains("12le") || selectedPixFmt.Contains("p12");

        if (videoCodecSelected.Contains("libx264", StringComparison.OrdinalIgnoreCase) ||
            videoCodecSelected.Contains("h264", StringComparison.OrdinalIgnoreCase))
        {
          if (is10Bit || is12Bit)
          {
            if (selectedProfile != "high10" && selectedProfile != "high422" && selectedProfile != "high444")
            {
              errorMessage = $"10-bit pixel format requires a 10-bit H.264 profile (high10, high422, or high444).\nCurrent profile: {selectedProfile}";
              return false;
            }
          }
          else
          {
            if (!new[] { "baseline", "main", "high" }.Contains(selectedProfile))
              warningMessage += (string.IsNullOrEmpty(warningMessage) ? "" : "\n")
                              + $"Profile '{selectedProfile}' may not be supported for 8-bit H.264. Consider using 'high'.";
          }
        }
        else if (videoCodecSelected.Contains("libx265", StringComparison.OrdinalIgnoreCase) ||
                 videoCodecSelected.Contains("hevc", StringComparison.OrdinalIgnoreCase))
        {
          if (is10Bit || is12Bit)
          {
            if (selectedProfile != "main10" && selectedProfile != "main422-10" && selectedProfile != "main444-10")
            {
              errorMessage = $"10-bit pixel format requires a 10-bit H.265 profile (main10, main422-10, or main444-10).\nCurrent profile: {selectedProfile}";
              return false;
            }
          }
          else
          {
            if (selectedProfile != "main" && selectedProfile != "main10" && selectedProfile != "main422-10" && selectedProfile != "main444-10")
              warningMessage += (string.IsNullOrEmpty(warningMessage) ? "" : "\n")
                              + $"Profile '{selectedProfile}' is not typical for 8-bit H.265. Consider using 'main'.";
          }
        }
      }

      // 11. Check lossless + profile
      if (videoCodecSelected == "libx264" && crf.Value == 0)
      {
        string selectedPixFmt = pixelFormat.SelectedItem?.ToString() ?? "yuv420p";
        string selectedProfile = profile.SelectedItem?.ToString() ?? "";

        if (selectedPixFmt.Contains("444", StringComparison.OrdinalIgnoreCase) ||
            selectedPixFmt.Contains("422", StringComparison.OrdinalIgnoreCase))
        {
          if (selectedProfile != "high444" && selectedProfile != "high422" && selectedProfile != "high10")
          {
            errorMessage = "Lossless (CRF 0) with 4:2:2 or 4:4:4 requires a suitable profile.\n" +
                           "For 4:4:4 use 'high444', for 4:2:2 use 'high422' or 'high10'.\n" +
                           "Current profile: " + selectedProfile;
            return false;
          }
        }
      }

      // 12. Block 10-bit for libx264
      if (videoCodecSelected.Contains("libx264", StringComparison.OrdinalIgnoreCase) ||
          videoCodecSelected.Contains("h264", StringComparison.OrdinalIgnoreCase))
      {
        string pixFmt = pixelFormat.SelectedItem?.ToString() ?? "yuv420p";
        if (pixFmt.Contains("10le", StringComparison.OrdinalIgnoreCase) ||
            pixFmt.Contains("12le", StringComparison.OrdinalIgnoreCase))
        {
          errorMessage = "libx264 (software H.264) does not support 10-bit or 12-bit pixel formats.\n"
                       + "Use 8-bit formats (e.g., yuv420p, yuv422p, yuv444p).\n"
                       + "For 10-bit H.264, consider using hardware encoder (h264_nvenc) or libx265.";
          return false;
        }
      }

      // 13. Warning: HW Accel + software codec
      if (hwAccel.SelectedItem?.ToString() != "none")
      {
        if (!isHardwareCodec && videoCodecSelected != "copy")
        {
          warningMessage += (string.IsNullOrEmpty(warningMessage) ? "" : "\n")
                          + "Hardware acceleration (decoding) is enabled, but the selected video codec is software-based.\n"
                          + "This is allowed, but -hwaccel_output_format will be ignored to avoid conversion errors.";
        }
      }

      // 14. Warning: CRF for hardware codecs
      if (isHardwareCodec && crf.Value > 0 && videoCodecSelected != "copy")
      {
        warningMessage += (string.IsNullOrEmpty(warningMessage) ? "" : "\n")
                        + $"The selected codec ({videoCodecSelected}) uses {(videoCodecSelected.Contains("nvenc") ? "CQ" : "QP")} instead of CRF.\n"
                        + "The value from CRF field will be used as quality parameter.";
      }

      // 15. Check pixel format for hardware codecs
      if (isHardwareCodec)
      {
        string pixFmt = pixelFormat.SelectedItem?.ToString() ?? "yuv420p";
        if (pixFmt.Contains("12le", StringComparison.OrdinalIgnoreCase))
        {
          errorMessage = "Hardware encoders (NVENC, AMF, QSV) do not support 12-bit pixel formats.\n"
                       + "Please select an 8-bit or 10-bit format.";
          return false;
        }
        if (pixFmt.Contains("10le", StringComparison.OrdinalIgnoreCase))
        {
          if (videoCodecSelected.Contains("nvenc") || videoCodecSelected.Contains("amf"))
            warningMessage += (string.IsNullOrEmpty(warningMessage) ? "" : "\n")
                            + $"Pixel format '{pixFmt}' will be automatically converted to 'p010le' for hardware encoding.";
        }
      }

      // 16. Warning: experemental audiocodecs
      if (audioCodecSelected == "opus" || audioCodecSelected == "libfdk_aac")
      {
        warningMessage += (string.IsNullOrEmpty(warningMessage) ? "" : "\n")
                        + $"Codec '{audioCodecSelected}' may be experimental in your FFmpeg build.\n"
                        + "The '-strict -2' flag will be added automatically.";
      }

      // 17. Warning: HWAccel + color matrix
      if (hwAccelSelected != "none" && videoCodecSelected != "copy")
      {
        bool hasColorParams = !string.IsNullOrWhiteSpace(colorMatrix.SelectedItem?.ToString()) &&
                              colorMatrix.SelectedItem?.ToString() != "bt709";
        bool hasColorRange = !string.IsNullOrWhiteSpace(colorRange.SelectedItem?.ToString()) &&
                             colorRange.SelectedItem?.ToString() != "limited";

        if (hasColorParams || hasColorRange || hasVideoFilter)
        {
          warningMessage += (string.IsNullOrEmpty(warningMessage) ? "" : "\n")
                          + "Hardware acceleration (decoding) is enabled, but color parameters or filters are used.\n"
                          + "The output format of hardware decoder will be switched to CPU format to allow processing.\n"
                          + "This may reduce performance but ensures correct color/filter application.";
        }
      }

      // 18. Warning: preset doesnt support AMF and QSV
      if (videoCodecSelected.Contains("amf") || videoCodecSelected.Contains("qsv"))
      {
        warningMessage += (string.IsNullOrEmpty(warningMessage) ? "" : "\n")
                        + $"Codec '{videoCodecSelected}' does not support preset parameter. It will be ignored.";
      }

      // Subtitles validation
      if (chkSubtitles.Checked)
      {
        if (rbSubExternal.Checked && !string.IsNullOrWhiteSpace(subExternalFile.Text) && !File.Exists(subExternalFile.Text))
        {
          errorMessage = "External subtitle file not found.";
          return false;
        }
        if (audioOnlyChecked)
          warningMessage += (string.IsNullOrEmpty(warningMessage) ? "" : "\n") + "Subtitles are ignored when Audio only mode is enabled.";
        string fmt = format.SelectedItem?.ToString() ?? "mp4";
        if (fmt == "avi" || fmt == "ts" || fmt == "flv")
          warningMessage += (string.IsNullOrEmpty(warningMessage) ? "" : "\n") + $"Subtitles in '{fmt}' container may not be supported or may not play correctly.";
      }
      // Check compatibility SRT with MP4
      if (chkSubtitles.Checked && rbSubExternal.Checked && !string.IsNullOrWhiteSpace(subExternalFile.Text))
      {
        string fmt = format.SelectedItem?.ToString() ?? "mp4";
        string ext = Path.GetExtension(subExternalFile.Text).ToLowerInvariant();
        if ((fmt == "mp4" || fmt == "mov" || fmt == "m4v") && ext == ".srt")
        {
          warningMessage += (string.IsNullOrEmpty(warningMessage) ? "" : "\n")
                          + "SRT subtitles in MP4/MOV will be converted to mov_text (required by container).\n"
                          + "This is normal and does not affect quality.";
        }
      }


      if (!string.IsNullOrEmpty(errorMessage))
        Logger.Warning($"Parameter validation error: {errorMessage}");
      if (!string.IsNullOrEmpty(warningMessage))
        Logger.Warning($"Parameter validation warning: {warningMessage}");

      return true;
    }

    private async void BtnProcess_Click(object? sender, EventArgs e)
    {
      if (format == null) return;
      if (string.IsNullOrWhiteSpace(inputFile.Text) || IsPlaceholder(inputFile, InputPlaceholder) || !File.Exists(inputFile.Text))
      {
        MessageBox.Show("Please select a valid input file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        return;
      }

      if (string.IsNullOrWhiteSpace(outputFile.Text) || IsPlaceholder(outputFile, OutputPlaceholder))
        SetDefaultOutputIfEmpty();

      if (string.IsNullOrWhiteSpace(outputFile.Text) || IsPlaceholder(outputFile, OutputPlaceholder))
      {
        MessageBox.Show("Please select an output file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        return;
      }

      if (!CheckFFmpeg())
      {
        MessageBox.Show("FFmpeg is not available. Please install it or specify the path.", "FFmpeg Missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
        return;
      }

      if (!ValidateParameters(out string errorMessage, out string warningMessage))
      {
        Logger.Warning($"Parameter validation error: {errorMessage}");
        MessageBox.Show(errorMessage, "Parameter conflict", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        status.Text = "Parameter conflict";
        return;
      }

      if (!string.IsNullOrEmpty(warningMessage))
      {
        Logger.Warning($"Parameter validation warning: {warningMessage}");
        status.Text = "Warning: " + warningMessage;
      }

      string trimModeStr = trimMode.SelectedItem?.ToString() ?? "Source";
      TimeSpan start = TimeSpan.Zero;
      TimeSpan? end = null;
      if (trimModeStr == "Range")
      {
        if (IsPlaceholder(startTime, TimePlaceholder) || string.IsNullOrWhiteSpace(startTime.Text))
          start = TimeSpan.Zero;
        else if (!TimeSpan.TryParseExact(startTime.Text, @"h\:mm\:ss", CultureInfo.InvariantCulture, out start))
        {
          MessageBox.Show("Invalid start time. Use HH:MM:SS format.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
          return;
        }

        if (IsPlaceholder(endTime, TimePlaceholder) ||
          !TimeSpan.TryParseExact(endTime.Text, @"h\:mm\:ss", CultureInfo.InvariantCulture, out TimeSpan endTs) ||
          endTs.TotalSeconds <= 0)
        {
          MessageBox.Show("Invalid end time. Use HH:MM:SS format and must be > 0.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
          return;
        }
        end = endTs;
        if (end <= start)
        {
          MessageBox.Show("End time must be after start time.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
          return;
        }
      }

      string formatStr = format.SelectedItem?.ToString() ?? "";
      bool isGif = formatStr.Equals("gif", StringComparison.OrdinalIgnoreCase);

      string videoCodecSelected = videoCodec.SelectedItem?.ToString() ?? "copy";
      string audioCodecSelected = audioCodec.SelectedItem?.ToString() ?? "copy";

      _cancellationTokenSource = new CancellationTokenSource();
      var token = _cancellationTokenSource.Token;

      btnProcess.Visible = false;
      btnCancel.Visible = true;
      btnCancel.Enabled = true;
      btnProcess.Enabled = false;
      progressBar.Visible = true;
      progressBar.Style = ProgressBarStyle.Marquee;
      progressBar.Value = 0;
      status.Text = "Processing...";

      string fullCommand = "";
      Logger.Info($"Encoding started: input '{inputFile.Text}', output '{outputFile.Text}'");

      try
      {
        string ffmpegPath = FfmpegExecutable;
        var argsList = new List<string>();

        if (overwrite.Checked)
          argsList.Add("-y");

        string hw = hwAccel.SelectedItem?.ToString() ?? "none";
        string hwOut = hwAccelOutput.SelectedItem?.ToString() ?? "";

        bool isHardwareCodec = videoCodecSelected.Contains("nvenc") ||
                               videoCodecSelected.Contains("amf") ||
                               videoCodecSelected.Contains("qsv");

        bool hasColorParams = !string.IsNullOrWhiteSpace(colorMatrix.SelectedItem?.ToString()) &&
                              colorMatrix.SelectedItem?.ToString() != "bt709";
        bool hasColorRange = !string.IsNullOrWhiteSpace(colorRange.SelectedItem?.ToString()) &&
                             colorRange.SelectedItem?.ToString() != "limited";
        bool hasVideoFilter = !string.IsNullOrWhiteSpace(videoFilter.Text);

        bool useHwOutputFormat = isHardwareCodec && !hasColorParams && !hasColorRange && !hasVideoFilter;

        if (hw != "none")
        {
          argsList.Add($"-hwaccel {hw}");
          if (!string.IsNullOrEmpty(hwOut) && hwOut != "none" && useHwOutputFormat)
            argsList.Add($"-hwaccel_output_format {hwOut}");
        }

        if (trimModeStr == "Range")
        {
          if (start.TotalSeconds > 0)
            argsList.Add($"-ss {start:hh\\:mm\\:ss}");
          if (end.HasValue)
            argsList.Add($"-to {end.Value:hh\\:mm\\:ss}");
        }

        argsList.Add($"-i \"{inputFile.Text}\"");

        if (isGif)
        {
          var gifArgs = BuildGifArgs();
          argsList.AddRange(gifArgs);
        }
        else
        {
          bool audioOnlyChecked = audioOnly.Checked;

          if (hasVideoFilter && videoCodecSelected == "copy")
            videoCodecSelected = "libx264";
          bool reencodeVideo = !audioOnlyChecked && videoCodecSelected != "copy";
          if (reencodeVideo)
          {
            argsList.Add($"-c:v {videoCodecSelected}");

            if (isHardwareCodec)
            {
              if (videoCodecSelected.Contains("nvenc") && crf.Value > 0)
                argsList.Add($"-cq {crf.Value}");
              else if ((videoCodecSelected.Contains("amf") || videoCodecSelected.Contains("qsv")) && crf.Value > 0)
                argsList.Add($"-qp {crf.Value}");
            }
            else
            {
              if (crf.Value > 0)
                argsList.Add($"-crf {crf.Value}");
            }

            bool supportsPreset = !videoCodecSelected.Contains("amf") && !videoCodecSelected.Contains("qsv");
            if (supportsPreset && videoCodecSelected != "copy")
            {
              string presetVal = preset.SelectedItem?.ToString() ?? "medium";
              argsList.Add($"-preset {presetVal}");
            }
            else if (videoCodecSelected.Contains("amf"))
            {
              string presetVal = preset.SelectedItem?.ToString() ?? "medium";
              string amfQuality = presetVal switch
              {
                "ultrafast" or "superfast" or "veryfast" or "faster" => "speed",
                "fast" or "medium" or "slow" => "balanced",
                "slower" or "veryslow" => "quality",
                _ => "balanced"
              };
              argsList.Add($"-quality {amfQuality}");
            }

            string pixFmt = pixelFormat.SelectedItem?.ToString() ?? "yuv420p";
            if (isHardwareCodec)
            {
              if (pixFmt.Contains("10le", StringComparison.OrdinalIgnoreCase))
              {
                if (videoCodecSelected.Contains("nvenc") || videoCodecSelected.Contains("amf"))
                {
                  pixFmt = "p010le";
                  Logger.Debug($"Pixel format automatically changed to '{pixFmt}' for hardware encoder");
                }
              }
              else if (pixFmt.Contains("12le", StringComparison.OrdinalIgnoreCase))
              {
                pixFmt = "p010le";
                Logger.Warning($"12-bit format not supported by hardware encoder, falling back to 'p010le'");
              }
            }
            argsList.Add($"-pix_fmt {pixFmt}");

            if (!string.IsNullOrWhiteSpace(videoBitrate.Text))
              argsList.Add($"-b:v {videoBitrate.Text.Trim()}");

            if (!string.IsNullOrWhiteSpace(maxrate.Text))
              argsList.Add($"-maxrate {maxrate.Text.Trim()}");

            if (!string.IsNullOrWhiteSpace(bufsize.Text))
              argsList.Add($"-bufsize {bufsize.Text.Trim()}");

            string profileVal = profile.SelectedItem?.ToString() ?? "";
            if (!string.IsNullOrEmpty(profileVal) && profileVal != "high")
              argsList.Add($"-profile:v {profileVal}");

            if (gop.Value > 0)
              argsList.Add($"-g {gop.Value}");

            if (videoFps.Value > 0)
              argsList.Add($"-r {videoFps.Value}");

            string matrix = colorMatrix.SelectedItem?.ToString() ?? "";
            if (!string.IsNullOrEmpty(matrix) && matrix != "bt709")
              argsList.Add($"-colorspace {matrix}");

            string range = colorRange.SelectedItem?.ToString() ?? "";
            if (!string.IsNullOrEmpty(range) && range != "limited")
              argsList.Add($"-color_range {range}");
          }
          else if (!audioOnlyChecked && videoCodecSelected == "copy")
            argsList.Add($"-c:v {videoCodecSelected}");

          if (audioCodecSelected != "copy")
          {
            argsList.Add($"-c:a {audioCodecSelected}");
            if (!string.IsNullOrWhiteSpace(audioBitrate.Text))
              argsList.Add($"-b:a {audioBitrate.Text.Trim()}");
            if (audioQuality.Value > 0)
              argsList.Add($"-aq {audioQuality.Value}");
            if (audioCodecSelected == "opus" || audioCodecSelected == "libfdk_aac")
              argsList.Add("-strict -2");
          }
          else
            argsList.Add($"-c:a {audioCodecSelected}");

          if (audioOnlyChecked)
            argsList.Add("-vn");

          if (!string.IsNullOrWhiteSpace(videoFilter.Text))
            argsList.Add($"-vf \"{videoFilter.Text.Trim()}\"");

          if (!string.IsNullOrWhiteSpace(audioFilter.Text))
            argsList.Add($"-af \"{audioFilter.Text.Trim()}\"");

          if (!string.IsNullOrWhiteSpace(mapStreams.Text))
            argsList.Add($"-map {mapStreams.Text.Trim()}");

          if (chkSubtitles.Checked && !audioOnlyChecked)
          {
            string containerFormat = format.SelectedItem?.ToString() ?? "mp4";
            string subtitleCodec = "copy";

            if (containerFormat == "mp4" || containerFormat == "mov" || containerFormat == "m4v")
              subtitleCodec = "mov_text";
            else if (containerFormat == "mkv")
              subtitleCodec = "copy";
            else
              subtitleCodec = "copy";

            if (rbSubFromInput.Checked)
            {
              int track = (int)subTrackNumber.Value;
              argsList.Add($"-map 0:s:{track}");
              argsList.Add($"-c:s {subtitleCodec}");
            }
            else if (rbSubExternal.Checked && !string.IsNullOrWhiteSpace(subExternalFile.Text) && File.Exists(subExternalFile.Text))
            {
              if (string.IsNullOrWhiteSpace(mapStreams.Text))
              {
                argsList.Add("-map 0:v -map 0:a");
              }

              string inputArg = $"-i \"{inputFile.Text}\"";
              int mainInputIndex = -1;
              for (int i = 0; i < argsList.Count; i++)
              {
                if (argsList[i] == inputArg)
                {
                  mainInputIndex = i;
                  break;
                }
              }
              if (mainInputIndex != -1)
              {
                argsList.Insert(mainInputIndex + 1, $"\"{subExternalFile.Text}\"");
                argsList.Insert(mainInputIndex + 1, "-i");
                argsList.Add($"-map 1:s");
                argsList.Add($"-c:s {subtitleCodec}");
              }
            }
          }
        }

        argsList.Add($"\"{outputFile.Text}\"");

        string args = string.Join(" ", argsList);
        fullCommand = $"\"{ffmpegPath}\" {args}";
        Logger.Debug($"FFmpeg command: {fullCommand}");

        await RunFFmpeg(ffmpegPath, args, token, inputDuration, (percent, _) =>
        {
          Invoke(() =>
          {
            if (percent > 0)
            {
              progressBar.Style = ProgressBarStyle.Continuous;
              progressBar.Value = Math.Min(percent, 100);
              status.Text = $"Processing... {percent}%";
            }
          });
        });

        status.Text = "Done!";
        Logger.Info($"Encoding finished successfully: '{outputFile.Text}'");
        MessageBox.Show("Processing completed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

        if (openOnSuccess.Checked)
          OpenFolder(outputFile.Text);
      }
      catch (OperationCanceledException)
      {
        Logger.Warning("Encoding cancelled by user");
        status.Text = "Cancelled";
        MessageBox.Show("Encoding cancelled.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        DeleteIncompleteOutput();
      }
      catch (Exception ex)
      {
        Logger.Error($"Encoding failed: {ex.Message}");
        Logger.Error($"Full command: {fullCommand}");

        string userMessage = ex.Message;

        if (userMessage.Contains("Invalid data found", StringComparison.OrdinalIgnoreCase))
          userMessage = "The input file appears to be corrupted or unsupported.\n\n" + userMessage;
        else if (userMessage.Contains("codec not found", StringComparison.OrdinalIgnoreCase))
          userMessage = "The selected codec is not available in your FFmpeg build.\n\n" + userMessage;
        else if (userMessage.Contains("Permission denied", StringComparison.OrdinalIgnoreCase))
          userMessage = "Access denied to the output folder or file.\n\n" + userMessage;
        else if (userMessage.Contains("Filter not found", StringComparison.OrdinalIgnoreCase) ||
                 userMessage.Contains("No such filter", StringComparison.OrdinalIgnoreCase))
          userMessage = "The specified filter does not exist.\nCheck the filter name (see ffmpeg -filters).\n\n" + userMessage;
        else if (userMessage.Contains("Unsupported codec", StringComparison.OrdinalIgnoreCase) ||
                 userMessage.Contains("Encoder not found", StringComparison.OrdinalIgnoreCase))
          userMessage = "The selected codec is not supported for this format.\nTry changing the format or codec.\n\n" + userMessage;
        else if (userMessage.Contains("Unable to find a suitable output format", StringComparison.OrdinalIgnoreCase))
          userMessage = "The output format is not compatible with your settings.\nCheck container and codec combination.\n\n" + userMessage;
        else if (userMessage.Contains("Invalid argument", StringComparison.OrdinalIgnoreCase))
          userMessage = "One of the FFmpeg arguments is incorrect.\nCheck values (bitrate, filter parameters, etc.).\n\n" + userMessage;
        else if (userMessage.Contains("No such file", StringComparison.OrdinalIgnoreCase))
          userMessage = "The input file was not found.\nPlease check the path and try again.\n\n" + userMessage;
        else if (userMessage.Contains("out of range", StringComparison.OrdinalIgnoreCase))
          userMessage = "A numeric parameter is out of allowed range.\nCheck CRF, bitrate, or FPS values.\n\n" + userMessage;

        userMessage += "\n\nCommand:\n" + fullCommand;

        status.Text = "Error";
        MessageBox.Show(userMessage, "FFmpeg Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        DeleteIncompleteOutput();
      }
      finally
      {
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        btnProcess.Visible = true;
        btnCancel.Visible = false;
        btnCancel.Enabled = false;
        btnProcess.Enabled = true;
        progressBar.Style = ProgressBarStyle.Marquee;
        progressBar.Value = 0;
        progressBar.Visible = false;
        status.Text = "Ready";
        Application.DoEvents();
        UpdateProcessButton();
      }
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
      _cancellationTokenSource?.Cancel();
      btnCancel.Enabled = false;
    }

    private static string ExtractRelevantError(string fullError)
    {
      if (string.IsNullOrWhiteSpace(fullError))
        return "Unknown error (no output from FFmpeg).";

      var lines = fullError.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
      var errorLines = new List<string>();

      var skipPhrases = new[]
      {
        "version", "built with", "configuration:", "Copyright", "libav", "gcc",
        "x264 - core", "options:", "cabac", "ref=", "deblock", "analyse", "me=",
        "psy", "mixed_ref", "trellis", "8x8dct", "fast_pskip", "chroma_qp_offset",
        "threads=", "lookahead_threads", "sliced_threads", "nr=", "decimate",
        "interlaced", "bluray_compat", "constrained_intra", "bframes",
        "b_pyramid", "b_adapt", "b_bias", "direct", "weightb", "open_gop",
        "weightp", "keyint", "scenecut", "intra_refresh", "rc_lookahead",
        "rc=", "mbtree", "crf=", "qcomp", "qpmin", "qpmax", "qpstep",
        "ip_ratio", "aq="
      };

      foreach (var line in lines)
      {
        bool skip = false;
        foreach (var phrase in skipPhrases)
        {
          if (line.Contains(phrase, StringComparison.OrdinalIgnoreCase))
          {
            skip = true;
            break;
          }
        }
        if (skip) continue;

        if (line.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Invalid", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("No such", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Cannot", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Unable", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Permission denied", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("codec", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("filter", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("unsupported", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("invalid", StringComparison.OrdinalIgnoreCase))
        {
          string cleaned = CleanErrorMessage().Replace(line, "");
          cleaned = ReplaceErrorMessage().Replace(cleaned, " ").Trim();
          if (!string.IsNullOrEmpty(cleaned))
            errorLines.Add(cleaned);
        }
      }

      if (errorLines.Count > 0)
      {
        string first = errorLines.First();
        string last = errorLines.Last();
        return errorLines.Count == 1 ? first : first + "\n" + last;
      }

      var lastLines = lines.TakeLast(2).ToList();
      return lastLines.Count > 0 ? string.Join("\n", lastLines) : "No error details available.";
    }

    private static async Task RunFFmpeg(string path, string args, CancellationToken cancellationToken, double totalDuration, Action<int, TimeSpan>? progressCallback = null)
    {
      using var process = new Process();
      process.StartInfo.FileName = path;
      process.StartInfo.Arguments = args;
      process.StartInfo.UseShellExecute = false;
      process.StartInfo.RedirectStandardError = true;
      process.StartInfo.RedirectStandardOutput = true;
      process.StartInfo.CreateNoWindow = true;

      process.Start();

      _ = process.StandardOutput.ReadToEndAsync();

      var errorBuilder = new StringBuilder();
      var errorTask = Task.Run(async () =>
      {
        try
        {
          while (true)
          {
            string? line = await process.StandardError.ReadLineAsync(cancellationToken);
            if (line == null) break;

            lock (errorBuilder) { errorBuilder.AppendLine(line); }

            if (line.Contains("time="))
            {
              var match = ProcessTime().Match(line);
              if (match.Success && TimeSpan.TryParse(match.Groups[1].Value, out var currentTime))
              {
                int progressPercent = 0;
                TimeSpan remaining = TimeSpan.Zero;
                if (totalDuration > 0)
                {
                  double percent = currentTime.TotalSeconds / totalDuration * 100;
                  progressPercent = (int)Math.Min(percent, 100);
                  remaining = TimeSpan.FromSeconds(Math.Max(totalDuration - currentTime.TotalSeconds, 0));
                }
                progressCallback?.Invoke(progressPercent, remaining);
              }
            }
          }
        }
        catch (OperationCanceledException) { /* ... */ }
      });

      try { await process.WaitForExitAsync(cancellationToken); }
      catch (OperationCanceledException)
      {
        if (!process.HasExited)
        {
          try
          {
            Logger.Warning("Killing FFmpeg process due to cancellation");
            process.Kill();
            await process.WaitForExitAsync(cancellationToken);
          }
          catch (Exception ex) { Logger.Error($"Error killing process: {ex.Message}"); }
        }
        throw new OperationCanceledException("Encoding cancelled.");
      }

      await errorTask;

      if (process.ExitCode != 0)
      {
        string fullError = errorBuilder.ToString();
        Logger.Error($"FFmpeg exited with code {process.ExitCode}. Full error:\n{fullError}");
        string relevant = ExtractRelevantError(fullError);
        throw new Exception(relevant);
      }
    }

    private void DeleteIncompleteOutput()
    {
      if (!string.IsNullOrWhiteSpace(outputFile.Text) && File.Exists(outputFile.Text))
      {
        try
        {
          File.Delete(outputFile.Text);
          Logger.Debug($"Deleted incomplete output file: '{outputFile.Text}'");
        }
        catch (Exception ex)
        {
          Logger.Error($"Failed to delete incomplete file: {ex.Message}");
        }
      }
    }

    private void OpenFolder(string path)
    {
      if (string.IsNullOrWhiteSpace(path)) return;

      try
      {
        if (File.Exists(path))
          Process.Start(ExplorerExecutable, $"/select, \"{path}\"");
        else
        {
          string? directory = Path.GetDirectoryName(path);
          if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            Process.Start(ExplorerExecutable, directory);
          else
            MessageBox.Show("Could not open folder.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
      }
      catch (Exception ex) { MessageBox.Show($"Could not open folder: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private static async Task<string> GetToolVersion(string toolPath, string toolName)
    {
      try
      {
        var psi = new ProcessStartInfo
        {
          FileName = toolPath,
          Arguments = "-version",
          UseShellExecute = false,
          RedirectStandardOutput = true,
          CreateNoWindow = true
        };
        using var p = Process.Start(psi);
        if (p == null) return "Not found";
        string output = await p.StandardOutput.ReadLineAsync() ?? "";
        await p.WaitForExitAsync();
        if (p.ExitCode == 0 && !string.IsNullOrEmpty(output)) return output.Trim();
        return "Unknown";
      }
      catch { return "Error"; }
    }

    private async Task UpdateInfoTabAsync()
    {
      try
      {
        string ffmpegPathStr = settings.FfmpegPath ?? FfmpegExecutable;
        string ffprobePathStr = settings.FfprobePath ?? FfprobeExecutable;

        if (!Path.IsPathRooted(ffmpegPathStr))
        {
          string? fullPath = FindExecutableInPath(ffmpegPathStr);
          if (fullPath != null) ffmpegPathStr = fullPath;
        }
        if (!Path.IsPathRooted(ffprobePathStr))
        {
          string? fullPath = FindExecutableInPath(ffprobePathStr);
          if (fullPath != null) ffprobePathStr = fullPath;
        }

        _ffmpegVersion = await GetToolVersion(ffmpegPathStr, "ffmpeg");
        _ffprobeVersion = await GetToolVersion(ffprobePathStr, "ffprobe");

        Invoke(() =>
        {
          ffmpegVersion.Text = _ffmpegVersion;
          ffprobeVersion.Text = _ffprobeVersion;
          dmfVersion.Text = GetCurrentVersion().ToString();
          ffmpegPath.Text = ffmpegPathStr;
          ffprobePath.Text = ffprobePathStr;
          settingsPathLabel.Text = settingsFile;
          logPathLabel.Text = Path.Combine(GetAppDataFolder(), "log.txt");
          dotNetVersion.Text = Environment.Version.ToString();
          osVersion.Text = Environment.OSVersion.ToString();
        });
      }
      catch (Exception ex) { /* ... */ }
    }

    private static string? FindExecutableInPath(string fileName)
    {
      try
      {
        var psi = new ProcessStartInfo
        {
          FileName = "where",
          Arguments = fileName,
          UseShellExecute = false,
          RedirectStandardOutput = true,
          CreateNoWindow = true
        };
        using var p = Process.Start(psi);
        if (p == null) return null;
        string output = p.StandardOutput.ReadLine();
        p.WaitForExit(1000);
        if (p.ExitCode == 0 && !string.IsNullOrEmpty(output))
          return output.Trim();
      }
      catch { /* ignore */ }
      return null;
    }

    private void ChangeFFmpegPath()
    {
      using var dialog = new OpenFileDialog
      {
        Title = "Select ffmpeg.exe",
        Filter = "Executable|ffmpeg.exe|All files|*.*"
      };
      if (dialog.ShowDialog() == DialogResult.OK)
      {
        settings.FfmpegPath = dialog.FileName;
        SaveSettings();
        _ffmpegChecked = false;
        _ = UpdateInfoTabAsync();
        CheckFFmpeg();
      }
    }

    private void ChangeFFprobePath()
    {
      using var dialog = new OpenFileDialog
      {
        Title = "Select ffprobe.exe",
        Filter = "Executable|ffprobe.exe|All files|*.*"
      };
      if (dialog.ShowDialog() == DialogResult.OK)
      {
        settings.FfprobePath = dialog.FileName;
        SaveSettings();
        _ = UpdateInfoTabAsync();
      }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
      if (_cancellationTokenSource != null)
      {
        var result = MessageBox.Show(
          "A conversion process is currently running.\n\nDo you want to cancel it and exit?",
          "Process Running",
          MessageBoxButtons.YesNo,
          MessageBoxIcon.Warning,
          MessageBoxDefaultButton.Button2);

        if (result == DialogResult.No)
        {
          e.Cancel = true;
          return;
        }
        else
        {
          _cancellationTokenSource.Cancel();
          Application.DoEvents();
          Thread.Sleep(300);
        }
      }

      if (!string.IsNullOrEmpty(previewTempFile) && File.Exists(previewTempFile))
      {
        try { File.Delete(previewTempFile); }
        catch { /* ... */ }
      }

      if (_previewForm != null && !_previewForm.IsDisposed)
        _previewForm.Close();

      SaveSettings();
      base.OnFormClosing(e);
    }

    [System.Text.RegularExpressions.GeneratedRegex(
      @"crop\s*=\s*(?<w>\d+)\s*:\s*(?<h>\d+)(?:\s*:\s*(?<x>\d+)\s*:\s*(?<y>\d+))?",
      System.Text.RegularExpressions.RegexOptions.IgnoreCase, "en-US")]
    private static partial System.Text.RegularExpressions.Regex CropFilter();
    [System.Text.RegularExpressions.GeneratedRegex(@"time=(\d{2}:\d{2}:\d{2}\.\d+)")]
    private static partial System.Text.RegularExpressions.Regex ProcessTime();

    private static Version GetCurrentVersion()
    {
      var assembly = Assembly.GetEntryAssembly();
      var versionAttr = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
      var versionString = versionAttr?.InformationalVersion ?? "0.0.0";
      var idx = versionString.IndexOf('+');
      if (idx > 0) versionString = versionString[..idx];
      if (Version.TryParse(versionString, out var version))
        return version;
      return new Version(0, 0, 0);
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\[[^\]]*@[^\]]*\]\s*")]
    private static partial System.Text.RegularExpressions.Regex CleanErrorMessage();
    [System.Text.RegularExpressions.GeneratedRegex(@"\s+")]
    private static partial System.Text.RegularExpressions.Regex ReplaceErrorMessage();
  }
}