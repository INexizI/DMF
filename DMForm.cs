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
    private ComboBox cmbTrimMode = null!;
    private TextBox startTime = null!;
    private TextBox durationTime = null!;
    private TextBox endTime = null!;
    private ComboBox cmbAudioCodec = null!;
    private ComboBox cmbVideoCodec = null!;
    private Button btnProcess = null!;
    private Label status = null!;
    private ProgressBar progressBar = null!;
    private TableLayoutPanel timeTable = null!;
    private CheckBox chkOpenOnSuccess = null!;

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
      chkOpenOnSuccess.Checked = settings.OpenOnSuccess;
      UpdateProcessButton();
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
        settings.OpenOnSuccess = chkOpenOnSuccess.Checked;

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
        Height = 300,
        Padding = new Padding(10),
        ForeColor = Color.White
      };
      mainContainer.Controls.Add(groupFFmpeg);

      var table = new TableLayoutPanel
      {
        Dock = DockStyle.Fill,
        ColumnCount = 3,
        RowCount = 9,
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
      table.Controls.Add(inputFile, 1, 0);
      btnInput = new Button { Text = "Browse...", Dock = DockStyle.Fill };
      table.Controls.Add(btnInput, 2, 0);

      // Row 1: Output
      table.Controls.Add(new Label { Text = "Output:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 1);
      outputFile = new TextBox { Dock = DockStyle.Fill };
      outputFile.TextChanged += (s, e) => UpdateProcessButton();
      table.Controls.Add(outputFile, 1, 1);
      btnOutput = new Button { Text = "Browse...", Dock = DockStyle.Fill };
      table.Controls.Add(btnOutput, 2, 1);

      // Row 2: Trim mode
      table.Controls.Add(new Label { Text = "Trim mode:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 2);
      cmbTrimMode = new ComboBox
      {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Items = { "Source", "Duration", "End time" },
        SelectedIndex = 0
      };
      table.Controls.Add(cmbTrimMode, 1, 2);
      table.Controls.Add(new Label { Dock = DockStyle.Fill }, 2, 2);

      // Row 3: Start time
      table.Controls.Add(new Label { Text = "Start time:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 3);
      startTime = new TextBox { Dock = DockStyle.Fill, Text = "00:00:00" };
      table.Controls.Add(startTime, 1, 3);
      table.Controls.Add(new Label { Text = "(HH:MM:SS)", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 2, 3);

      // Row 4: Duration
      table.Controls.Add(new Label { Text = "Duration:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 4);
      durationTime = new TextBox { Dock = DockStyle.Fill, Text = "00:00:10" };
      table.Controls.Add(durationTime, 1, 4);
      table.Controls.Add(new Label { Text = "(HH:MM:SS)", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 2, 4);

      // Row 5: End time
      table.Controls.Add(new Label { Text = "End time:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 5);
      endTime = new TextBox { Dock = DockStyle.Fill, Text = "00:00:30" };
      table.Controls.Add(endTime, 1, 5);
      table.Controls.Add(new Label { Text = "(HH:MM:SS)", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 2, 5);

      // Row 6: Audio codec
      table.Controls.Add(new Label { Text = "Audio codec:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 6);
      cmbAudioCodec = new ComboBox
      {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Items = { "copy", "aac", "mp3", "libmp3lame", "ac3", "flac" },
        SelectedIndex = 0
      };
      table.Controls.Add(cmbAudioCodec, 1, 6);
      table.Controls.Add(new Label { Dock = DockStyle.Fill }, 2, 6);

      // Row 7: Video codec
      table.Controls.Add(new Label { Text = "Video codec:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 7);
      cmbVideoCodec = new ComboBox
      {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Items = { "copy", "libx264", "libx265", "libvpx-vp9", "mpeg4" },
        SelectedIndex = 0
      };
      table.Controls.Add(cmbVideoCodec, 1, 7);
      table.Controls.Add(new Label { Dock = DockStyle.Fill }, 2, 7);

      // Row 8: Checkbox
      table.Controls.Add(new Label { Dock = DockStyle.Fill }, 0, 8);
      chkOpenOnSuccess = new CheckBox
      {
        Text = "Open folder on success",
        AutoSize = false,
        Size = new Size(180, 20),
        Anchor = AnchorStyles.Left,
        Checked = true
      };
      table.Controls.Add(chkOpenOnSuccess, 1, 8);
      table.Controls.Add(new Label { Dock = DockStyle.Fill }, 2, 8);

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
      cmbTrimMode.SelectedIndexChanged += CmbTrimMode_SelectedIndexChanged;

      UpdateTime();
    }

    private void UpdateProcessButton()
    {
      btnProcess.Enabled = !string.IsNullOrWhiteSpace(inputFile.Text) &&
                           !string.IsNullOrWhiteSpace(outputFile.Text);
    }

    private void CmbTrimMode_SelectedIndexChanged(object? sender, EventArgs e) => UpdateTime();

    private void UpdateTime()
    {
      if (timeTable == null) return;

      string mode = cmbTrimMode.SelectedItem?.ToString() ?? "Duration";
      bool showDuration = mode == "Duration";
      bool showEnd = mode == "End time";

      foreach (Control ctrl in timeTable.Controls)
      {
        int row = timeTable.GetRow(ctrl);
        if (row == 4)
          ctrl.Visible = showDuration;
        else if (row == 5)
          ctrl.Visible = showEnd;
      }
    }

    private void BtnInput_Click(object? sender, EventArgs e)
    {
      using var file = new OpenFileDialog();
      file.Title = "Select input file";
      file.Filter = "Media files|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.flv;*.webm|All files|*.*";
      if (file.ShowDialog() == DialogResult.OK)
        inputFile.Text = file.FileName;
    }

    private void BtnOutput_Click(object? sender, EventArgs e)
    {
      using var file = new SaveFileDialog();
      file.Title = "Select output file";
      file.Filter = "MP4 files|*.mp4|AVI files|*.avi|MKV files|*.mkv|All files|*.*";
      if (file.ShowDialog() == DialogResult.OK)
        outputFile.Text = file.FileName;
    }

    private async void BtnProcess_Click(object? sender, EventArgs e)
    {
      if (string.IsNullOrWhiteSpace(inputFile.Text) || !File.Exists(inputFile.Text))
      {
        MessageBox.Show("Please select a valid input file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        return;
      }
      if (string.IsNullOrWhiteSpace(outputFile.Text))
      {
        MessageBox.Show("Please select an output file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        return;
      }
      if (!TimeSpan.TryParse(startTime.Text, out TimeSpan start))
      {
        MessageBox.Show("Invalid start time. Use HH:MM:SS format.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        return;
      }

      string trimMode = cmbTrimMode.SelectedItem?.ToString() ?? "Duration";
      TimeSpan? duration = null;
      TimeSpan? end = null;

      if (trimMode == "Duration")
      {
        if (!TimeSpan.TryParse(durationTime.Text, out TimeSpan dur) || dur.TotalSeconds <= 0)
        {
          MessageBox.Show("Invalid duration. Use HH:MM:SS format and must be > 0.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
          return;
        }
        duration = dur;
      }
      else if (trimMode == "End time")
      {
        if (!TimeSpan.TryParse(endTime.Text, out TimeSpan endTs) || endTs.TotalSeconds <= 0)
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

      string audioCodec = cmbAudioCodec.SelectedItem?.ToString() ?? "copy";
      string videoCodec = cmbVideoCodec.SelectedItem?.ToString() ?? "copy";

      btnProcess.Enabled = false;
      progressBar.Visible = true;
      progressBar.Style = ProgressBarStyle.Marquee;
      status.Text = "Processing...";

      try
      {
        string ffmpegPath = "ffmpeg";
        var argsList = new List<string>();

        if (start.TotalSeconds > 0)
          argsList.Add($"-ss {start:hh\\:mm\\:ss}");

        if (trimMode == "Duration" && duration.HasValue)
          argsList.Add($"-t {duration.Value:hh\\:mm\\:ss}");
        else if (trimMode == "End time" && end.HasValue)
          argsList.Add($"-to {end.Value:hh\\:mm\\:ss}");

        argsList.Add($"-i \"{inputFile.Text}\"");

        if (audioCodec != "copy")
          argsList.Add($"-c:a {audioCodec}");
        if (videoCodec != "copy")
          argsList.Add($"-c:v {videoCodec}");

        argsList.Add($"\"{outputFile.Text}\"");

        string args = string.Join(" ", argsList);

        await Task.Run(() => RunFFmpeg(ffmpegPath, args));

        status.Text = "Done!";
        MessageBox.Show("Processing completed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

        if (chkOpenOnSuccess.Checked)
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