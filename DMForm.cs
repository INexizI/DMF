using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DMF
{
  public partial class DMForm : Form
  {
    /* Application Settings */
    private Settings settings = new();
    private readonly string settingsFile = "settings.json";
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
    // Audio
    private TextBox audioBitrate = null!;
    private NumericUpDown audioQuality = null!;
    // Filters
    private TextBox videoFilter = null!;
    private TextBox audioFilter = null!;
    // Advanced
    private TextBox mapStreams = null!;
    private ComboBox hwAccel = null!;
    private ComboBox hwAccelOutput = null!;
    // Common
    private Button btnProcess = null!;
    private Label status = null!;
    private ProgressBar progressBar = null!;

    private readonly List<string> audioFormats = ["mp3", "m4a", "aac", "flac", "wav", "ogg", "opus", "ac3"];
    private readonly List<string> videoFormats = ["mp4", "avi", "mkv", "mov", "webm", "flv", "wmv", "m4v", "ts"];
    private bool _autoOutput = false;
    private const string InputPlaceholder = "Select input file...";
    private const string OutputPlaceholder = "Select output file...";
    private const string TimePlaceholder = "HH:MM:SS";
    private ToolTip toolTip = new ToolTip();

    private readonly Dictionary<string, string> audioCodecDescriptions = new()
    {
      { "copy", "Stream copy (no re-encode)" },
      { "aac", "AAC (Advanced Audio Coding)" },
      { "libfdk_aac", "Fraunhofer FDK AAC (high quality)" },
      { "mp3", "MPEG-1 Audio Layer III" },
      { "libmp3lame", "LAME MP3 encoder" },
      { "ac3", "Dolby Digital (AC-3)" },
      { "flac", "Free Lossless Audio Codec" },
      { "opus", "Opus (low-latency, high quality)" },
      { "libvorbis", "Vorbis (open, patent-free)" },
      { "pcm_s16le", "Uncompressed PCM (WAV-like)" },
      { "wav", "WAV (PCM 16-bit)" }
    };

    private readonly Dictionary<string, string> videoCodecDescriptions = new()
    {
      { "copy", "Stream copy (no re-encode)" },
      { "libx264", "H.264 / AVC (software, widely compatible)" },
      { "libx265", "H.265 / HEVC (software, higher compression)" },
      { "libvpx-vp9", "VP9 (open, good compression)" },
      { "libvpx", "VP8 (older open format)" },
      { "mpeg4", "MPEG-4 part 2 (Xvid/DivX compatible)" },
      { "libxvid", "Xvid (MPEG-4 ASP)" },
      { "mpeg2video", "MPEG-2 (DVD, broadcast)" },
      { "wmv2", "Windows Media Video 2" },
      { "h264_nvenc", "NVIDIA hardware H.264" },
      { "hevc_nvenc", "NVIDIA hardware HEVC" },
      { "h264_amf", "AMD hardware H.264" },
      { "hevc_amf", "AMD hardware HEVC" },
      { "h264_qsv", "Intel QuickSync H.264" },
      { "hevc_qsv", "Intel QuickSync HEVC" },
      { "libaom-av1", "AV1 (software, very slow)" }
    };

    [Serializable]
    public class Settings
    {
      public int WinWidth { get; set; } = 640;
      public int WinHeight { get; set; } = 480;
      public int WinX { get; set; } = -1;
      public int WinY { get; set; } = -1;
      public bool WinMax { get; set; } = false;
      public bool OpenOnSuccess { get; set; } = true;
    }

    public DMForm()
    {
      LoadSettings();
      InitializeForm();
      InitializeLayout();
      openOnSuccess.Checked = settings.OpenOnSuccess;
      UpdateProcessButton();
      SetPlaceholders();
      UpdateTimeFields();
      UpdateCodecHints();
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
              settings.WinX < workingArea.Width - 50 &&
              settings.WinY < workingArea.Height - 50)
            Location = new Point(settings.WinX, settings.WinY);
          else
            StartPosition = FormStartPosition.CenterScreen;
        }
        else
          StartPosition = FormStartPosition.CenterScreen;
      }

      FormBorderStyle = FormBorderStyle.FixedSingle;
      MaximizeBox = false;
      MinimumSize = new Size(640, 480);
      MaximumSize = new Size(640, 480);
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
      Size = new Size(640, 480);
      MinimumSize = new Size(640, 480);
      MaximumSize = new Size(640, 480);
      DoubleBuffered = true;
      FormBorderStyle = FormBorderStyle.FixedSingle;
      MaximizeBox = false;
    }

    private void InitializeLayout()
    {
      var mainContainer = new Panel { Dock = DockStyle.Fill };
      Controls.Add(mainContainer);

      var tabControl = new TabControl
      {
        Dock = DockStyle.Top,
        Height = 330,
        Padding = new Point(10, 5)
      };
      mainContainer.Controls.Add(tabControl);


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
      tableBasic.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
      tabBasic.Controls.Add(tableBasic);

      // Row 0: Input
      tableBasic.Controls.Add(new Label { Text = "Input:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 0);
      inputFile = new TextBox { Dock = DockStyle.Fill };
      inputFile.TextChanged += (s, e) => UpdateProcessButton();
      inputFile.GotFocus += (s, e) => RemovePlaceholder(inputFile, InputPlaceholder);
      inputFile.LostFocus += (s, e) => RestorePlaceholder(inputFile, InputPlaceholder);
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
      tableBasic.Controls.Add(new Label { Dock = DockStyle.Fill }, 2, 2);

      // Row 3: Trim mode
      tableBasic.Controls.Add(new Label { Text = "Trim mode:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 3);
      trimMode = new ComboBox
      {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Items = { "Source", "Range" },
        SelectedIndex = 0
      };
      tableBasic.Controls.Add(trimMode, 1, 3);
      tableBasic.Controls.Add(new Label { Dock = DockStyle.Fill }, 2, 3);

      // Row 4: Start time
      tableBasic.Controls.Add(new Label { Text = "Start time:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 4);
      startTime = new TextBox { Dock = DockStyle.Fill };
      startTime.GotFocus += (s, e) => RemovePlaceholder(startTime, TimePlaceholder);
      startTime.LostFocus += (s, e) => RestorePlaceholder(startTime, TimePlaceholder);
      tableBasic.Controls.Add(startTime, 1, 4);
      tableBasic.Controls.Add(new Label { Dock = DockStyle.Fill }, 2, 4);

      // Row 5: End time
      tableBasic.Controls.Add(new Label { Text = "End time:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 5);
      endTime = new TextBox { Dock = DockStyle.Fill };
      endTime.GotFocus += (s, e) => RemovePlaceholder(endTime, TimePlaceholder);
      endTime.LostFocus += (s, e) => RestorePlaceholder(endTime, TimePlaceholder);
      tableBasic.Controls.Add(endTime, 1, 5);
      tableBasic.Controls.Add(new Label { Dock = DockStyle.Fill }, 2, 5);

      // Row 6: Audio codec
      tableBasic.Controls.Add(new Label { Text = "Audio codec:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 6);
      audioCodec = new ComboBox
      {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Items = { "copy", "aac", "libfdk_aac", "mp3", "libmp3lame", "ac3", "flac", "opus", "libvorbis", "pcm_s16le", "wav" },
        SelectedIndex = 0
      };
      audioCodec.SelectedIndexChanged += (s, e) => UpdateCodecHints();
      tableBasic.Controls.Add(audioCodec, 1, 6);
      audioCodecHint = new Label
      {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Color.LightYellow,
        Font = new Font("Segoe UI", 8, FontStyle.Italic),
        AutoSize = false
      };
      tableBasic.Controls.Add(audioCodecHint, 2, 6);

      // Row 7: Video codec
      tableBasic.Controls.Add(new Label { Text = "Video codec:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 7);
      videoCodec = new ComboBox
      {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Items = { "copy", "libx264", "libx265", "libvpx-vp9", "libvpx", "mpeg4", "libxvid", "mpeg2video", "wmv2",
                  "h264_nvenc", "hevc_nvenc", "h264_amf", "hevc_amf", "h264_qsv", "hevc_qsv", "libaom-av1" },
        SelectedIndex = 0
      };
      videoCodec.SelectedIndexChanged += (s, e) => UpdateCodecHints();
      tableBasic.Controls.Add(videoCodec, 1, 7);
      videoCodecHint = new Label
      {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Color.LightYellow,
        Font = new Font("Segoe UI", 8, FontStyle.Italic),
        AutoSize = false
      };
      tableBasic.Controls.Add(videoCodecHint, 2, 7);

      // Row 8: Checkboxes
      tableBasic.Controls.Add(new Label { Dock = DockStyle.Fill }, 0, 8);
      var checkPanel = new FlowLayoutPanel
      {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = false
      };
      audioOnly = new CheckBox { Text = "Audio only", AutoSize = true, Checked = false };
      audioOnly.CheckedChanged += ChkAudioOnly_CheckedChanged;
      overwrite = new CheckBox { Text = "Overwrite", AutoSize = true, Checked = true };
      openOnSuccess = new CheckBox { Text = "Open folder on success", AutoSize = true, Checked = true };
      checkPanel.Controls.Add(audioOnly);
      checkPanel.Controls.Add(overwrite);
      checkPanel.Controls.Add(openOnSuccess);
      tableBasic.Controls.Add(checkPanel, 1, 8);
      tableBasic.Controls.Add(new Label { Dock = DockStyle.Fill }, 2, 8);

      var tabVideo = new TabPage("Video");
      tabControl.TabPages.Add(tabVideo);
      var tableVideo = new TableLayoutPanel
      {
        Dock = DockStyle.Fill,
        ColumnCount = 3,
        RowCount = 8,
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
      tableVideo.Controls.Add(new Label { Text = "0–51 (lower = better)", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, ForeColor = Color.Gray }, 2, 0);

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
        Items = { "yuv420p", "yuv422p", "yuv444p", "yuvj420p", "yuvj422p", "yuvj444p" },
        SelectedIndex = 0
      };
      tableVideo.Controls.Add(pixelFormat, 1, 2);
      tableVideo.Controls.Add(new Label { Text = "compatibility", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, ForeColor = Color.Gray }, 2, 2);

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
        Items = { "baseline", "main", "high" },
        SelectedIndex = 2
      };
      tableVideo.Controls.Add(profile, 1, 6);
      tableVideo.Controls.Add(new Label { Text = "H.264/H.265 only", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, ForeColor = Color.Gray }, 2, 6);

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
      tableVideo.Controls.Add(new Label { Text = "0 = default, max - 1000", TextAlign = ContentAlignment.BottomLeft, Dock = DockStyle.Top, ForeColor = Color.Gray }, 2, 7);

      var tabAudio = new TabPage("Audio");
      tabControl.TabPages.Add(tabAudio);
      var tableAudio = new TableLayoutPanel
      {
        Dock = DockStyle.Fill,
        ColumnCount = 3,
        RowCount = 2,
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
      tableAudio.Controls.Add(new Label { Text = "VBR (0–10, lower = better)", TextAlign = ContentAlignment.BottomLeft, Dock = DockStyle.Top, ForeColor = Color.Gray }, 2, 1);

      var tabFilters = new TabPage("Filters");
      tabControl.TabPages.Add(tabFilters);
      var tableFilters = new TableLayoutPanel
      {
        Dock = DockStyle.Fill,
        ColumnCount = 3,
        RowCount = 2,
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
      Label videoHint = new Label { Text = "e.g. fade=in:0:5", TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill, ForeColor = Color.Gray };
      tableFilters.Controls.Add(videoHint, 2, 0);
      toolTip.SetToolTip(videoHint,
        "Common video filters:\n" +
        "\n" +
        "• scale=W:H – resize (use -2 for auto even height)\n" +
        "• crop=W:H:X:Y – crop video\n" +
        "• hflip / vflip – flip horizontally/vertically\n" +
        "• rotate=A – rotate by angle (degrees)\n" +
        "• fade=in:0:30 – fade in/out\n" +
        "• overlay=X:Y – overlay another video\n" +
        "• unsharp – sharpen/soften (see docs)\n" +
        "• eq – brightness, contrast, saturation");

      // Row 1: Video filter hint
      tableFilters.Controls.Add(new Label { Text = "Video examples:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill, ForeColor = Color.Gray }, 0, 1);
      var hintVideo = new Label
      {
        Text = "scale=1280:-2, crop=1920:1080:0:0, hflip, vflip, rotate=45, fade=out:0:30, overlay=10:10, unsharp=5:5:1.0, eq=contrast=1.2:brightness=0.1:saturation=1.0\n",
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
      Label audioHint = new Label { Text = "e.g. volume=2", TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill, ForeColor = Color.Gray };
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
      tableFilters.Controls.Add(new Label { Text = "Audio examples:", TextAlign = ContentAlignment.BottomRight, Padding = new Padding(0, 5, 0, 0), Dock = DockStyle.Top, ForeColor = Color.Gray }, 0, 3);
      var hintAudio = new Label
      {
        Text = "volume=1.5, afadet=in:ss=0:d=5, equalizerf=100:t=h:w=1:g=-10, pan=mono|c0=0.5*c0+0.5*c1, aecho=0.8:0.9:1000:0.3, chorus=0.7:0.9:55:0.4:0.25:2, areverse, asetrate=44100, aresample=44100\n",
        TextAlign = ContentAlignment.BottomLeft,
        Dock = DockStyle.Top,
        ForeColor = Color.Gray,
        Font = new Font("Segoe UI", 8, FontStyle.Regular),
        AutoSize = true,
        UseCompatibleTextRendering = true
      };
      tableFilters.SetColumnSpan(hintAudio, 2);
      tableFilters.Controls.Add(hintAudio, 1, 3);

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
        ColumnCount = 2,
        RowCount = 1,
        Padding = new Padding(0)
      };
      actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
      actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
      bottomLayout.Controls.Add(actionPanel, 0, 1);

      btnProcess = new Button
      {
        Text = "Run FFmpeg",
        Dock = DockStyle.Fill,
        BackColor = Color.LightGreen,
        Enabled = false
      };
      actionPanel.Controls.Add(btnProcess, 0, 0);

      status = new Label
      {
        Text = "Ready",
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(10, 0, 0, 0)
      };
      actionPanel.Controls.Add(status, 1, 0);

      btnInput.Click += BtnInput_Click;
      btnOutput.Click += BtnOutput_Click;
      btnProcess.Click += BtnProcess_Click;
      format.SelectedIndexChanged += Format_SelectedIndexChanged;
      trimMode.SelectedIndexChanged += TrimMode_SelectedIndexChanged;
      audioOnly.CheckedChanged += ChkAudioOnly_CheckedChanged;

      UpdateTimeFields();
    }

    private void SetPlaceholders()
    {
      SetPlaceholder(inputFile, InputPlaceholder);
      SetPlaceholder(outputFile, OutputPlaceholder);
      SetPlaceholder(startTime, TimePlaceholder);
      SetPlaceholder(endTime, TimePlaceholder);
    }

    private void SetPlaceholder(TextBox tb, string placeholder)
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

    private void RestorePlaceholder(TextBox tb, string placeholder)
    {
      if (string.IsNullOrWhiteSpace(tb.Text))
      {
        tb.Text = placeholder;
        tb.ForeColor = Color.Gray;
      }
    }

    private static bool IsPlaceholder(TextBox tb, string placeholder) => tb.Text == placeholder;

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

    private void TrimMode_SelectedIndexChanged(object? sender, EventArgs e) => UpdateTimeFields();

    private void UpdateTimeFields()
    {
      if (startTime == null || endTime == null || trimMode == null) return;

      string mode = trimMode.SelectedItem?.ToString() ?? "Source";
      bool isRange = mode == "Range";

      startTime.Enabled = isRange;
      endTime.Enabled = isRange;
    }

    private void BtnInput_Click(object? sender, EventArgs e)
    {
      using var file = new OpenFileDialog();
      file.Title = "Select input file";
      file.Filter = "Media files|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.flv;*.webm|All files|*.*";
      if (file.ShowDialog() == DialogResult.OK)
      {
        inputFile.Text = file.FileName;
        inputFile.ForeColor = SystemColors.WindowText;
        SetDefaultOutputIfEmpty();
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

    private void Format_SelectedIndexChanged(object? sender, EventArgs e)
    {
      if (_autoOutput && !string.IsNullOrWhiteSpace(outputFile.Text) && !IsPlaceholder(outputFile, OutputPlaceholder))
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
    }

    private void ChkAudioOnly_CheckedChanged(object? sender, EventArgs e)
    {
      bool audioOnlyChecked = audioOnly.Checked;
      videoCodec.Enabled = !audioOnlyChecked;

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
    }

    private async void BtnProcess_Click(object? sender, EventArgs e)
    {
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

      string trimModeStr = trimMode.SelectedItem?.ToString() ?? "Source";
      TimeSpan start = TimeSpan.Zero;
      TimeSpan? end = null;
      if (trimModeStr == "Range")
      {
        if (IsPlaceholder(startTime, TimePlaceholder) || string.IsNullOrWhiteSpace(startTime.Text))
          start = TimeSpan.Zero;
        else if (!TimeSpan.TryParse(startTime.Text, out start))
        {
          MessageBox.Show("Invalid start time. Use HH:MM:SS format.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
          return;
        }

        if (IsPlaceholder(endTime, TimePlaceholder) || !TimeSpan.TryParse(endTime.Text, out TimeSpan endTs) || endTs.TotalSeconds <= 0)
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

      string audioCodecSelected = audioCodec.SelectedItem?.ToString() ?? "copy";
      string videoCodecSelected = videoCodec.SelectedItem?.ToString() ?? "copy";
      bool audioOnlyChecked = audioOnly.Checked;

      btnProcess.Enabled = false;
      progressBar.Visible = true;
      progressBar.Style = ProgressBarStyle.Marquee;
      status.Text = "Processing...";

      try
      {
        string ffmpegPath = "ffmpeg";
        var argsList = new List<string>();

        if (overwrite.Checked)
          argsList.Add("-y");

        if (trimModeStr == "Range")
        {
          if (start.TotalSeconds > 0)
            argsList.Add($"-ss {start:hh\\:mm\\:ss}");
          if (end.HasValue)
            argsList.Add($"-to {end.Value:hh\\:mm\\:ss}");
        }

        argsList.Add($"-i \"{inputFile.Text}\"");

        bool reencodeVideo = !audioOnlyChecked && videoCodecSelected != "copy";
        if (reencodeVideo)
        {
          argsList.Add($"-c:v {videoCodecSelected}");

          if (crf.Value > 0)
            argsList.Add($"-crf {crf.Value}");

          string presetVal = preset.SelectedItem?.ToString() ?? "medium";
          argsList.Add($"-preset {presetVal}");

          string pixFmt = pixelFormat.SelectedItem?.ToString() ?? "yuv420p";
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
        }
        else if (!audioOnlyChecked && videoCodecSelected == "copy")
        {
          argsList.Add($"-c:v {videoCodecSelected}");
        }

        if (audioCodecSelected != "copy")
        {
          argsList.Add($"-c:a {audioCodecSelected}");

          if (!string.IsNullOrWhiteSpace(audioBitrate.Text))
            argsList.Add($"-b:a {audioBitrate.Text.Trim()}");

          if (audioQuality.Value > 0)
            argsList.Add($"-aq {audioQuality.Value}");
        }
        else
        {
          argsList.Add($"-c:a {audioCodecSelected}");
        }

        if (audioOnlyChecked)
          argsList.Add("-vn");

        if (!string.IsNullOrWhiteSpace(videoFilter.Text))
          argsList.Add($"-vf \"{videoFilter.Text.Trim()}\"");

        if (!string.IsNullOrWhiteSpace(audioFilter.Text))
          argsList.Add($"-af \"{audioFilter.Text.Trim()}\"");

        if (!string.IsNullOrWhiteSpace(mapStreams.Text))
        {
          argsList.Add($"-map {mapStreams.Text.Trim()}");
        }

        string hw = hwAccel.SelectedItem?.ToString() ?? "none";
        if (hw != "none")
        {
          argsList.Add($"-hwaccel {hw}");
          string hwOut = hwAccelOutput.SelectedItem?.ToString() ?? "";
          if (!string.IsNullOrEmpty(hwOut))
            argsList.Add($"-hwaccel_output_format {hwOut}");
        }

        argsList.Add($"\"{outputFile.Text}\"");

        string args = string.Join(" ", argsList);

        // Debug: you can show the command if needed
        // MessageBox.Show(args, "Command");

        await Task.Run(() => RunFFmpeg(ffmpegPath, args));

        status.Text = "Done!";
        MessageBox.Show("Processing completed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

        if (openOnSuccess.Checked)
          OpenFolder(outputFile.Text);
      }
      catch (Exception ex)
      {
        status.Text = "Error";
        MessageBox.Show($"Error: {ex.Message}", "FFmpeg Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
      finally
      {
        btnProcess.Enabled = true;
        progressBar.Visible = false;
        UpdateProcessButton();
      }
    }

    private static void RunFFmpeg(string path, string args)
    {
      using var process = new Process();
      process.StartInfo.FileName = path;
      process.StartInfo.Arguments = args;
      process.StartInfo.UseShellExecute = false;
      process.StartInfo.RedirectStandardError = true;
      process.StartInfo.RedirectStandardOutput = true;
      process.StartInfo.CreateNoWindow = true;

      process.Start();
      string error = process.StandardError.ReadToEnd();
      process.WaitForExit();

      if (process.ExitCode != 0)
        throw new Exception($"FFmpeg exited with code {process.ExitCode}. Error: {error}");
    }

    private void OpenFolder(string path)
    {
      if (string.IsNullOrWhiteSpace(path))
        return;

      try
      {
        if (File.Exists(path))
          Process.Start("explorer.exe", $"/select, \"{path}\"");
        else
        {
          string? directory = Path.GetDirectoryName(path);
          if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            Process.Start("explorer.exe", directory);
          else
            MessageBox.Show("Could not open folder.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
      }
      catch (Exception ex) { MessageBox.Show($"Could not open folder: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
      SaveSettings();
      base.OnFormClosing(e);
    }
  }
}