using System.Diagnostics;

namespace AudioDeviceGuardian;

public sealed class TrayContext : ApplicationContext
{
    private readonly NotifyIcon _icon;
    private readonly AppConfig _config;
    private readonly AudioDeviceManager _manager;
    private readonly ToolStripMenuItem _suspendItem;

    public TrayContext()
    {
        _config = AppConfig.Load();
        _manager = new AudioDeviceManager(_config);
        _manager.StatusChanged += OnStatus;

        var menu = new ContextMenuStrip();

        _suspendItem = new ToolStripMenuItem("Suspend enforcement", null, (_, _) => ToggleSuspend());
        menu.Items.Add(_suspendItem);

        var suspendFor = new ToolStripMenuItem("Suspend for...");
        suspendFor.DropDownItems.Add(new ToolStripMenuItem("15 minutes", null, (_, _) => SuspendFor(TimeSpan.FromMinutes(15))));
        suspendFor.DropDownItems.Add(new ToolStripMenuItem("1 hour", null, (_, _) => SuspendFor(TimeSpan.FromHours(1))));
        suspendFor.DropDownItems.Add(new ToolStripMenuItem("Until resumed", null, (_, _) => SuspendFor(null)));
        menu.Items.Add(suspendFor);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Capture current devices as preferred", null,
            (_, _) => _manager.CaptureCurrentAsPreferred()));
        menu.Items.Add(new ToolStripMenuItem("Edit config file...", null,
            (_, _) => AppConfig.OpenInEditor()));
        menu.Items.Add(new ToolStripMenuItem("Open config folder", null,
            (_, _) => Process.Start(new ProcessStartInfo(AppConfig.ConfigFolder) { UseShellExecute = true })));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => ExitApp()));

        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = menu
        };

        UpdateSuspendLabel();
        _manager.Start();
    }

    private void ToggleSuspend()
    {
        _config.Suspended = !_config.Suspended;
        _config.SuspendedUntilUtc = null;
        _config.Save();
        UpdateSuspendLabel();
        if (!_config.Suspended) _manager.ApplyPreferred("resumed");
    }

    private void SuspendFor(TimeSpan? duration)
    {
        _config.Suspended = true;
        _config.SuspendedUntilUtc = duration.HasValue ? DateTime.UtcNow.Add(duration.Value) : null;
        _config.Save();
        UpdateSuspendLabel();
    }

    private void UpdateSuspendLabel()
    {
        _suspendItem.Text = _config.Suspended ? "Resume enforcement" : "Suspend enforcement";
        _icon.Text = _config.Suspended
            ? "Audio Device Guardian (suspended)"
            : "Audio Device Guardian (active)";
    }

    private void OnStatus(string message)
    {
        // Kept quiet by default so it doesn't nag you every time Windows twitches.
        // Uncomment to get a balloon tip whenever it acts:
        // _icon.BalloonTipTitle = "Audio Device Guardian";
        // _icon.BalloonTipText = message;
        // _icon.ShowBalloonTip(2000);
    }

    private void ExitApp()
    {
        _icon.Visible = false;
        _manager.Dispose();
        Application.Exit();
    }
}
