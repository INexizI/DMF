using System;
using System.Windows.Forms;

namespace DMF
{
  internal static class App
  {
    [STAThread]
    static void Main()
    {
      ApplicationConfiguration.Initialize();
      Application.Run(new DMForm());
    }
  }
}