using System.Windows;
using Application = System.Windows.Application;

namespace Bledob.App;

public partial class App : Application
{
    private const string MutexName = "Local\\Bledob.Controller";
    private const string ShowEventName = "Local\\Bledob.Controller.Show";

    private Mutex? _instance;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _showWait;
    private bool _ownsInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            _instance = new Mutex(false, MutexName);
            _ownsInstance = _instance.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            // The previous instance exited without releasing the mutex. This process owns it now.
            _ownsInstance = true;
        }

        if (!_ownsInstance)
        {
            SignalShow();
            Shutdown();
            return;
        }

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var window = new MainWindow();
        MainWindow = window;
        window.EnableTray();
        _showWait = ThreadPool.RegisterWaitForSingleObject(
            _showEvent,
            (_, _) =>
            {
                try
                {
                    window.Dispatcher.BeginInvoke(window.BringToFront);
                }
                catch (InvalidOperationException)
                {
                    // The window is already closing.
                }
            },
            null,
            Timeout.Infinite,
            false);

        var startInTray = e.Args.Any(arg => arg.Equals("--tray", StringComparison.OrdinalIgnoreCase));
        if (!startInTray)
            window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showWait?.Unregister(null);
        _showEvent?.Dispose();
        if (_ownsInstance)
        {
            try
            {
                _instance?.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // The mutex was already released during shutdown.
            }
        }

        _instance?.Dispose();
        base.OnExit(e);
    }

    private static void SignalShow()
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                using var show = EventWaitHandle.OpenExisting(ShowEventName);
                show.Set();
                return;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                Thread.Sleep(50);
            }
        }
    }
}
