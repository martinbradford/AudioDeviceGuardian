using System.Threading;

namespace AudioDeviceGuardian;

internal static class Program
{
    // Named mutex so a second launch (e.g. Task Scheduler at logon *and* a
    // manual start) exits quietly instead of running a duplicate watcher that
    // fights the first over the default device. The "Global\" prefix scopes it
    // across sessions; the GUID keeps the name from colliding with anything else.
    private const string InstanceMutexName = @"Global\AudioDeviceGuardian-{8F3A2B1C-4D5E-4F6A-9B0C-1D2E3F4A5B6C}";

    [STAThread]
    private static void Main()
    {
        using var singleInstance = new Mutex(initiallyOwned: true, InstanceMutexName, out bool isFirstInstance);
        if (!isFirstInstance)
            return; // Another instance already owns the mutex; leave it be.

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayContext());

        GC.KeepAlive(singleInstance); // Keep the mutex alive for the app's lifetime.
    }
}
