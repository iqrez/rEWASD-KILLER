using System.Windows.Forms;

namespace Mapper;

internal static class ApplicationConfiguration
{
    // Mimics the WinForms template initialization to set default font and high DPI mode.
    public static void Initialize()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
    }
}
