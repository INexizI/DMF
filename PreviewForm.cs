using System;
using System.Drawing;
using System.Windows.Forms;

namespace DMF
{
  public class PreviewForm : Form
  {
    private readonly PictureBox pictureBox;
    private string _cropText = "";

    public PreviewForm()
    {
      Text = "Preview";
      Size = new Size(640, 480);
      MinimumSize = new Size(320, 240);
      StartPosition = FormStartPosition.CenterParent;

      try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
      catch { /* ... */ }

      pictureBox = new PictureBox
      {
        Dock = DockStyle.Fill,
        SizeMode = PictureBoxSizeMode.Zoom,
        BackColor = Color.Black
      };
      pictureBox.Paint += PictureBox_Paint;
      Controls.Add(pictureBox);
    }

    public void UpdateImage(Image image, string cropText)
    {
      var old = pictureBox.Image;
      pictureBox.Image = image;
      old?.Dispose();

      _cropText = cropText ?? "";
      pictureBox.Invalidate();
    }

    private void PictureBox_Paint(object? sender, PaintEventArgs e)
    {
      if (string.IsNullOrEmpty(_cropText) || pictureBox.Image == null) return;

      var g = e.Graphics;
      using var font = new Font("Segoe UI", 12, FontStyle.Bold);
      using var brush = new SolidBrush(Color.Yellow);
      using var shadowBrush = new SolidBrush(Color.Black);

      g.DrawString(_cropText, font, shadowBrush, 11, 11);
      g.DrawString(_cropText, font, shadowBrush, 9, 9);
      g.DrawString(_cropText, font, brush, 10, 10);
    }
  }
}