using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;

namespace AudioDeviceGuardian;

/// <summary>
/// Watches for default audio device changes and re-asserts the configured
/// preferred devices, unless enforcement is suspended.
/// </summary>
public sealed class AudioDeviceManager : IDisposable
{
    private readonly CoreAudioController _controller = new();
    private readonly AppConfig _config;
    private IDisposable? _subscription;

    // Guards against reacting to the change events we cause ourselves.
    private bool _applying;

    public event Action<string>? StatusChanged;

    public AudioDeviceManager(AppConfig config)
    {
        _config = config;
    }

    public void Start()
    {
        // AudioDeviceChanged is a bare IObservable<T>; the Subscribe(Action<T>)
        // convenience overload lives in System.Reactive, which we don't
        // reference. A tiny observer adapter keeps us dependency-free.
        _subscription = _controller.AudioDeviceChanged.Subscribe(new ActionObserver<DeviceChangedArgs>(OnDeviceChanged));
        ApplyPreferred("startup");
    }

    /// <summary>
    /// Minimal IObserver that forwards OnNext to a delegate and ignores
    /// completion/errors — enough to bridge AudioSwitcher's Rx stream without
    /// pulling in the full System.Reactive dependency.
    /// </summary>
    private sealed class ActionObserver<T> : IObserver<T>
    {
        private readonly Action<T> _onNext;
        public ActionObserver(Action<T> onNext) => _onNext = onNext;
        public void OnNext(T value) => _onNext(value);
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }

    private void OnDeviceChanged(DeviceChangedArgs args)
    {
        if (_applying) return;
        if (args.ChangedType != DeviceChangedType.DefaultChanged) return;

        ApplyPreferred($"device change ({args.Device?.FullName})");
    }

    public void ApplyPreferred(string reason)
    {
        if (IsEffectivelySuspended())
        {
            StatusChanged?.Invoke($"Suspended \u2014 left change alone ({reason})");
            return;
        }

        try
        {
            _applying = true;

            TrySetDefault(_controller.GetPlaybackDevices(DeviceState.Active),
                _config.PreferredPlaybackDevice, comms: false);
            TrySetDefault(_controller.GetCaptureDevices(DeviceState.Active),
                _config.PreferredRecordingDevice, comms: false);

            if (!string.IsNullOrWhiteSpace(_config.PreferredPlaybackCommsDevice))
                TrySetDefault(_controller.GetPlaybackDevices(DeviceState.Active),
                    _config.PreferredPlaybackCommsDevice, comms: true);

            if (!string.IsNullOrWhiteSpace(_config.PreferredRecordingCommsDevice))
                TrySetDefault(_controller.GetCaptureDevices(DeviceState.Active),
                    _config.PreferredRecordingCommsDevice, comms: true);

            StatusChanged?.Invoke($"Applied preferred devices ({reason})");
        }
        finally
        {
            _applying = false;
        }
    }

    private static void TrySetDefault(IEnumerable<CoreAudioDevice> devices, string? nameContains, bool comms)
    {
        if (string.IsNullOrWhiteSpace(nameContains)) return;

        var match = devices.FirstOrDefault(d =>
            d.FullName.Contains(nameContains, StringComparison.OrdinalIgnoreCase));

        if (match == null) return;

        if (comms) match.SetAsDefaultCommunications();
        else match.SetAsDefault();
    }

    public bool IsEffectivelySuspended()
    {
        if (_config.SuspendedUntilUtc is { } until)
        {
            if (DateTime.UtcNow >= until)
            {
                // Timed suspension has expired: clear it and resume enforcing.
                _config.Suspended = false;
                _config.SuspendedUntilUtc = null;
                _config.Save();
                return false;
            }
            return true;
        }

        return _config.Suspended;
    }

    /// <summary>
    /// Adopts whatever is currently set as the new preferred devices.
    /// Use this after manually switching devices, instead of fighting the watcher.
    /// </summary>
    public void CaptureCurrentAsPreferred()
    {
        _config.PreferredPlaybackDevice = _controller.DefaultPlaybackDevice?.FullName;
        _config.PreferredRecordingDevice = _controller.DefaultCaptureDevice?.FullName;
        _config.PreferredPlaybackCommsDevice = _controller.DefaultPlaybackCommunicationsDevice?.FullName;
        _config.PreferredRecordingCommsDevice = _controller.DefaultCaptureCommunicationsDevice?.FullName;
        _config.Save();
        StatusChanged?.Invoke("Captured current devices as preferred");
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        _controller.Dispose();
    }
}
