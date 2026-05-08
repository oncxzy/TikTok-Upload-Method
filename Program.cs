using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace TikTokUploadMethod;

internal static class Program
{
    public const string AppName = "TikTok Upload Method";

    
    
    
    
    
    
    public static string AppDirectory
    {
        get
        {
            try
            {
                var p = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(p))
                {
                    var dir = Path.GetDirectoryName(p);
                    if (!string.IsNullOrEmpty(dir)) return dir;
                }
            }
            catch { }
            return AppContext.BaseDirectory;
        }
    }

    [STAThread]
    static void Main()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            try { LogCrash("UnhandledException", e.ExceptionObject as Exception); } catch { }
        };

        Application.ThreadException += (s, e) =>
        {
            try { LogCrash("ThreadException", e.Exception); } catch { }
            try
            {
                MessageBox.Show(
                    "An error occurred:\n\n" + e.Exception.Message + "\n\nA crash log was saved next to the .exe.",
                    AppName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch { }
        };

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        try
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        }
        catch { }

        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
        }
        catch (Exception ex)
        {
            LogCrash("Init", ex);
            ShowError("Init failure: " + ex.Message);
            return;
        }

        try
        {
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            LogCrash("MainForm", ex);
            ShowError("Startup error: " + ex.Message + "\n\n" + ex.GetType().Name);
        }
    }

    private static void LogCrash(string source, Exception? ex)
    {
        try
        {
            var path = Path.Combine(AppDirectory, "crash.log");
            var stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var sep = new string('-', 60);
            var msg = $"{sep}\n[{stamp}] {source}\n{ex}\n";
            File.AppendAllText(path, msg);
        }
        catch { }
    }

    private static void ShowError(string text)
    {
        try
        {
            MessageBox.Show(text, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch { }
    }
}
