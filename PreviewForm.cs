using System;
using System.Drawing;
using System.Windows.Forms;

namespace DMF
{
  public class PreviewForm : Form
  {
    private readonly PictureBox pictureBox;

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
      Controls.Add(pictureBox);
    }

    public void UpdateImage(Image image)
    {
      pictureBox.Image?.Dispose();
      pictureBox.Image = image;
    }
  }
}