using Microsoft.UI.Xaml;
using OpenQCY_Desktop.Services;
using OpenQCY_Desktop.ViewModels;

namespace OpenQCY_Desktop;

public partial class App : Application
{
    public static Window Window { get; private set; } = null!;
    public static MainPageViewModel ViewModel { get; } = new();
    public static Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; private set; } = null!;
    public static bool IsExiting { get; private set; }

    public static nint WindowHandle => WinRT.Interop.WindowNative.GetWindowHandle(Window);

    private static System.Threading.Mutex? _singleInstanceMutex;
    private static System.Threading.EventWaitHandle? _showEvent;

    public App()
    {
        UnhandledException += (s, e) =>
        {
            try
            {
                var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenQCY");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "crash.log"),
                    $"\n[{DateTime.Now}] Unhandled XAML Exception: {e.Message}\n{e.Exception}\n{e.Exception?.StackTrace}\n");
            }
            catch { }
        };
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            try
            {
                var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenQCY");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "crash.log"),
                    $"\n[{DateTime.Now}] AppDomain Exception: {e.ExceptionObject}\n");
            }
            catch { }
        };
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _singleInstanceMutex = new System.Threading.Mutex(true, "OpenQCY.Desktop.SingleInstanceMutex", out bool createdNew);
        if (!createdNew)
        {
            try
            {
                using var showSignal = System.Threading.EventWaitHandle.OpenExisting("OpenQCY.Desktop.ShowEvent");
                showSignal.Set();
            }
            catch { }
            Environment.Exit(0);
            return;
        }

        try
        {
            _showEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, "OpenQCY.Desktop.ShowEvent");
            var waitThread = new System.Threading.Thread(() =>
            {
                while (_showEvent.WaitOne())
                {
                    if (IsExiting) break;
                    DispatcherQueue?.TryEnqueue(ShowMainWindow);
                }
            })
            {
                IsBackground = true
            };
            waitThread.Start();
        }
        catch { }

        var isStartupLaunch = WindowsStartupService.IsStartupLaunch(Environment.GetCommandLineArgs());
        Window = new MainWindow();
        DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        if (isStartupLaunch && Window is MainWindow mainWindow)
        {
            mainWindow.StartHidden();
            _ = ViewModel.StartAsync();
            return;
        }

        Window.Activate();
    }

    public static void ShowMainWindow()
    {
        if (Window is MainWindow mainWindow)
        {
            mainWindow.HideTrayFlyout();
            mainWindow.ShowFromTray();
        }
    }

    public static void HideTrayFlyout()
    {
        if (Window is MainWindow mainWindow)
        {
            mainWindow.HideTrayFlyout();
        }
    }

    public static void ExitApplication()
    {
        IsExiting = true;
        try
        {
            _showEvent?.Set();
            _singleInstanceMutex?.ReleaseMutex();
        }
        catch { }
        ViewModel.Shutdown();
        if (Window is MainWindow mainWindow)
        {
            mainWindow.DisposeTrayIcon();
        }

        Window.Close();
    }
}
