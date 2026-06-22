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
    private Button btnProcess = null!;
    private Label status = null!;
    private ProgressBar progressBar = null!;
    private TableLayoutPanel timeTable = null!;
    private CheckBox openOnSuccess = null!;
    private CheckBox audioOnly = null!;
    private readonly List<string> audioFormats = ["mp3", "m4a", "aac", "flac", "wav", "ogg", "opus", "ac3"];
    private readonly List<string> videoFormats = ["mp4", "avi", "mkv", "mov", "webm", "flv", "wmv", "m4v", "ts"];
    private bool _autoOutput = false;
    private const string InputPlaceholder = "Select input file...";
    private const string OutputPlaceholder = "Select output file...";
    private const string TimePlaceholder = "HH:MM:SS";

    [Serializable]
    public class Settings
    {
      public int WinWidth { get; set; } = 800;
      public int WinHeight { get; set; } = 600;
      public int WinX { get; set; } = -1;
      public int WinY { get; set; } = -1;
      public bool WinMax { get; set; } = false;
      public bool Resizing { get; set; } = false;
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

      FormBorderStyle = settings.Resizing ?
        FormBorderStyle.FixedSingle : FormBorderStyle.Sizable;
      MaximizeBox = !settings.Resizing;
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

        var settingsToSave = new Settings
        {
          WinWidth = settings.WinWidth,
          WinHeight = settings.WinHeight,
          WinX = settings.WinX,
          WinY = settings.WinY,
          WinMax = settings.WinMax,
          Resizing = settings.Resizing,
          OpenOnSuccess = settings.OpenOnSuccess
        };

        string json = JsonSerializer.Serialize(settingsToSave, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(settingsFile, json);
      }
      catch (Exception ex) { Console.WriteLine($"Error saving settings: {ex.Message}"); }
    }

    private void InitializeForm()
    {
      Text = "DMF";
      Size = new Size(800, 600);
      BackColor = Color.FromArgb(150, 150, 160);
      MinimumSize = new Size(800, 600);
      DoubleBuffered = true;
    }

    private void InitializeLayout()
    {
      var mainContainer = new Panel
      {
        Dock = DockStyle.Fill,
        Padding = new Padding(10),
        BackColor = Color.FromArgb(120, 120, 125)
      };
      Controls.Add(mainContainer);

      var groupFFmpeg = new GroupBox
      {
        Text = "FFmpeg Processing",
        Dock = DockStyle.Top,
        Height = 330,
        Padding = new Padding(10),
        ForeColor = Color.White
      };
      mainContainer.Controls.Add(groupFFmpeg);

      var table = new TableLayoutPanel
      {
        Dock = DockStyle.Fill,
        ColumnCount = 3,
        RowCount = 10,
        Padding = new Padding(10),
        AutoSize = false
      };
      table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
      table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
      table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
      groupFFmpeg.Controls.Add(table);
      timeTable = table;

      // Row 0: Input
      table.Controls.Add(new Label { Text = "Input:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 0);
      inputFile = new TextBox { Dock = DockStyle.Fill };
      inputFile.TextChanged += (s, e) => UpdateProcessButton();
      inputFile.GotFocus += (s, e) => RemovePlaceholder(inputFile, InputPlaceholder);
      inputFile.LostFocus += (s, e) => RestorePlaceholder(inputFile, InputPlaceholder);
      table.Controls.Add(inputFile, 1, 0);
      btnInput = new Button { Text = "Browse...", Dock = DockStyle.Fill };
      table.Controls.Add(btnInput, 2, 0);

      // Row 1: Output
      table.Controls.Add(new Label { Text = "Output:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 1);
      outputFile = new TextBox { Dock = DockStyle.Fill };
      outputFile.TextChanged += (s, e) => UpdateProcessButton();
      outputFile.GotFocus += (s, e) => RemovePlaceholder(outputFile, OutputPlaceholder);
      outputFile.LostFocus += (s, e) => RestorePlaceholder(outputFile, OutputPlaceholder);
      table.Controls.Add(outputFile, 1, 1);
      btnOutput = new Button { Text = "Browse...", Dock = DockStyle.Fill };
      table.Controls.Add(btnOutput, 2, 1);

      // Row 2: Format
      table.Controls.Add(new Label { Text = "Format:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 2);
      format = new ComboBox
      {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
      };
      format.Items.AddRange(videoFormats.Cast<object>().ToArray());
      format.SelectedIndex = 0;
      table.Controls.Add(format, 1, 2);
      table.Controls.Add(new Label { Dock = DockStyle.Fill }, 2, 2);

      // Row 3: Trim mode
      table.Controls.Add(new Label { Text = "Trim mode:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 3);
      trimMode = new ComboBox
      {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Items = { "Source", "Range" },
        SelectedIndex = 0
      };
      table.Controls.Add(trimMode, 1, 3);
      table.Controls.Add(new Label { Dock = DockStyle.Fill }, 2, 3);

      // Row 4: Start time
      table.Controls.Add(new Label { Text = "Start time:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 4);
      startTime = new TextBox { Dock = DockStyle.Fill };
      startTime.GotFocus += (s, e) => RemovePlaceholder(startTime, TimePlaceholder);
      startTime.LostFocus += (s, e) => RestorePlaceholder(startTime, TimePlaceholder);
      table.Controls.Add(startTime, 1, 4);

      // Row 5: End time
      table.Controls.Add(new Label { Text = "End time:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 5);
      endTime = new TextBox { Dock = DockStyle.Fill };
      endTime.GotFocus += (s, e) => RemovePlaceholder(endTime, TimePlaceholder);
      endTime.LostFocus += (s, e) => RestorePlaceholder(endTime, TimePlaceholder);
      table.Controls.Add(endTime, 1, 5);

      // Row 6: Audio codec
      table.Controls.Add(new Label { Text = "Audio codec:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 6);
      audioCodec = new ComboBox
      {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Items =
        {
          "copy",
          "aac",
          "libfdk_aac",
          "mp3",
          "libmp3lame",
          "ac3",
          "flac",
          "opus",
          "libvorbis",
          "pcm_s16le",
          "wav"
        },
        SelectedIndex = 0
      };
      table.Controls.Add(audioCodec, 1, 6);
      table.Controls.Add(new Label { Dock = DockStyle.Fill }, 2, 6);

      // Row 7: Video codec
      table.Controls.Add(new Label { Text = "Video codec:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 7);
      videoCodec = new ComboBox
      {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Items =
        {
          "copy",
          "libx264",
          "libx265",
          "libvpx-vp9",
          "libvpx",
          "mpeg4",
          "libxvid",
          "mpeg2video",
          "wmv2",
          "h264_nvenc",
          "hevc_nvenc",
          "h264_amf",
          "hevc_amf",
          "h264_qsv",
          "hevc_qsv",
          "libaom-av1"
        },
        SelectedIndex = 0
      };
      table.Controls.Add(videoCodec, 1, 7);
      table.Controls.Add(new Label { Dock = DockStyle.Fill }, 2, 7);

      // Row 8: Audio only checkbox
      table.Controls.Add(new Label { Dock = DockStyle.Fill }, 0, 8);
      audioOnly = new CheckBox
      {
        Text = "Audio only",
        AutoSize = false,
        Size = new Size(100, 20),
        Anchor = AnchorStyles.Left,
        Checked = false
      };
      table.Controls.Add(audioOnly, 1, 8);
      table.Controls.Add(new Label { Dock = DockStyle.Fill }, 2, 8);

      // Row 9: Open folder on success
      table.Controls.Add(new Label { Dock = DockStyle.Fill }, 0, 9);
      openOnSuccess = new CheckBox
      {
        Text = "Open folder on success",
        AutoSize = false,
        Size = new Size(180, 20),
        Anchor = AnchorStyles.Left,
        Checked = true
      };
      table.Controls.Add(openOnSuccess, 1, 9);
      table.Controls.Add(new Label { Dock = DockStyle.Fill }, 2, 9);

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

      if (trimModeStr == "Source")
      {
        // No trimming
      }
      else
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

        if (trimModeStr != "Source")
        {
          if (start.TotalSeconds > 0)
            argsList.Add($"-ss {start:hh\\:mm\\:ss}");
          if (end.HasValue)
            argsList.Add($"-to {end.Value:hh\\:mm\\:ss}");
        }

        argsList.Add($"-i \"{inputFile.Text}\"");

        if (audioCodecSelected != "copy")
          argsList.Add($"-c:a {audioCodecSelected}");

        if (!audioOnlyChecked && videoCodecSelected != "copy")
          argsList.Add($"-c:v {videoCodecSelected}");

        if (audioOnlyChecked)
          argsList.Add("-vn");

        argsList.Add($"\"{outputFile.Text}\"");

        string args = string.Join(" ", argsList);

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