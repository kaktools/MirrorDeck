using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MirrorDeck.WinUI.Infrastructure;
using MirrorDeck.WinUI.Services.Interfaces;

namespace MirrorDeck.WinUI.ViewModels;

public class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IAutoStartService _autoStartService;

    private string _theme = "Default";
    private bool _startMinimized;
    private bool _minimizeToTray = true;
    private bool _autoDependencyCheck = true;
    private string _airPlayName = string.Empty;
    private string _settingsPath = string.Empty;
    private bool _enableAirPlayService = true;
    private bool _enableAndroidService = true;
    private bool _runBackgroundOnly;
    private bool _autoStartUxPlay;
    private bool _autoStartAndroidService;
    private string _androidTcpHost = "192.168.0.10";
    private int _androidTcpPort = 5555;
    private bool _startWithWindows;
    private bool _enableWindowsNotifications = true;
    private bool _isLoading;

    public string Theme
    {
        get => _theme;
        set
        {
            if (SetProperty(ref _theme, value))
            {
                PersistSettingsImmediate();
            }
        }
    }

    public bool StartMinimized
    {
        get => _startMinimized;
        set
        {
            if (SetProperty(ref _startMinimized, value))
            {
                PersistSettingsImmediate();
            }
        }
    }

    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set
        {
            if (SetProperty(ref _minimizeToTray, value))
            {
                PersistSettingsImmediate();
            }
        }
    }

    public bool AutoDependencyCheck
    {
        get => _autoDependencyCheck;
        set
        {
            if (SetProperty(ref _autoDependencyCheck, value))
            {
                PersistSettingsImmediate();
            }
        }
    }

    public string AirPlayName
    {
        get => _airPlayName;
        set
        {
            if (SetProperty(ref _airPlayName, value))
            {
                PersistSettingsImmediate();
            }
        }
    }

    public string SettingsPath
    {
        get => _settingsPath;
        set => SetProperty(ref _settingsPath, value);
    }

    public bool EnableAirPlayService
    {
        get => _enableAirPlayService;
        set
        {
            if (SetProperty(ref _enableAirPlayService, value))
            {
                PersistSettingsImmediate();
            }
        }
    }

    public bool EnableAndroidService
    {
        get => _enableAndroidService;
        set
        {
            if (SetProperty(ref _enableAndroidService, value))
            {
                PersistSettingsImmediate();
            }
        }
    }

    public bool RunBackgroundOnly
    {
        get => _runBackgroundOnly;
        set
        {
            if (SetProperty(ref _runBackgroundOnly, value))
            {
                PersistSettingsImmediate();
            }
        }
    }

    public bool AutoStartUxPlay
    {
        get => _autoStartUxPlay;
        set
        {
            if (SetProperty(ref _autoStartUxPlay, value))
            {
                PersistSettingsImmediate();
            }
        }
    }

    public bool AutoStartAndroidService
    {
        get => _autoStartAndroidService;
        set
        {
            if (SetProperty(ref _autoStartAndroidService, value))
            {
                PersistSettingsImmediate();
            }
        }
    }

    public string AndroidTcpHost
    {
        get => _androidTcpHost;
        set
        {
            if (SetProperty(ref _androidTcpHost, value))
            {
                PersistSettingsImmediate();
            }
        }
    }

    public int AndroidTcpPort
    {
        get => _androidTcpPort;
        set
        {
            if (SetProperty(ref _androidTcpPort, value))
            {
                PersistSettingsImmediate();
            }
        }
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (SetProperty(ref _startWithWindows, value))
            {
                if (value)
                {
                    _autoStartService.Enable();
                }
                else
                {
                    _autoStartService.Disable();
                }

                PersistSettingsImmediate();
            }
        }
    }

    public bool EnableWindowsNotifications
    {
        get => _enableWindowsNotifications;
        set
        {
            if (SetProperty(ref _enableWindowsNotifications, value))
            {
                PersistSettingsImmediate();
            }
        }
    }

    public IAsyncRelayCommand SaveCommand { get; }

    public SettingsViewModel(ISettingsService settingsService, IAutoStartService autoStartService)
    {
        _settingsService = settingsService;
        _autoStartService = autoStartService;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        LoadFromModel();
    }

    public Task SaveAsync()
    {
        var settings = _settingsService.Current;
        settings.Theme = Theme;
        settings.StartMinimized = StartMinimized;
        settings.MinimizeToTrayOnClose = MinimizeToTray;
        settings.AutoUpdateDependencyCheck = AutoDependencyCheck;
        settings.AirPlayName = AirPlayName;
        settings.EnableAirPlayService = EnableAirPlayService;
        settings.EnableAndroidService = EnableAndroidService;
        settings.RunMirroringInBackgroundOnly = RunBackgroundOnly;
        settings.AutoStartUxPlay = AutoStartUxPlay;
        settings.AutoStartAndroidService = AutoStartAndroidService;
        settings.AndroidTcpHost = AndroidTcpHost;
        settings.AndroidTcpPort = AndroidTcpPort;
        settings.StartWithWindows = StartWithWindows;
        settings.EnableWindowsNotifications = EnableWindowsNotifications;

        if (StartWithWindows)
        {
            _autoStartService.Enable();
        }
        else
        {
            _autoStartService.Disable();
        }

        return _settingsService.SaveAsync();
    }

    private void LoadFromModel()
    {
        _isLoading = true;
        var settings = _settingsService.Current;
        Theme = settings.Theme;
        StartMinimized = settings.StartMinimized;
        MinimizeToTray = settings.MinimizeToTrayOnClose;
        AutoDependencyCheck = settings.AutoUpdateDependencyCheck;
        AirPlayName = settings.AirPlayName;
        EnableAirPlayService = settings.EnableAirPlayService;
        EnableAndroidService = settings.EnableAndroidService;
        RunBackgroundOnly = settings.RunMirroringInBackgroundOnly;
        AutoStartUxPlay = settings.AutoStartUxPlay;
        AutoStartAndroidService = settings.AutoStartAndroidService;
        AndroidTcpHost = settings.AndroidTcpHost;
        AndroidTcpPort = settings.AndroidTcpPort;
        StartWithWindows = _autoStartService.IsEnabled() || settings.StartWithWindows;
        EnableWindowsNotifications = settings.EnableWindowsNotifications;
        SettingsPath = _settingsService.GetSettingsPath();
        _isLoading = false;
    }

    private void PersistSettingsImmediate()
    {
        if (_isLoading)
        {
            return;
        }

        var settings = _settingsService.Current;
        settings.Theme = Theme;
        settings.StartMinimized = StartMinimized;
        settings.MinimizeToTrayOnClose = MinimizeToTray;
        settings.AutoUpdateDependencyCheck = AutoDependencyCheck;
        settings.AirPlayName = AirPlayName;
        settings.EnableAirPlayService = EnableAirPlayService;
        settings.EnableAndroidService = EnableAndroidService;
        settings.RunMirroringInBackgroundOnly = RunBackgroundOnly;
        settings.AutoStartUxPlay = AutoStartUxPlay;
        settings.AutoStartAndroidService = AutoStartAndroidService;
        settings.AndroidTcpHost = AndroidTcpHost;
        settings.AndroidTcpPort = AndroidTcpPort;
        settings.StartWithWindows = StartWithWindows;
        settings.EnableWindowsNotifications = EnableWindowsNotifications;

        ThemeCoordinator.ApplyTheme(settings.Theme);

        _ = _settingsService.SaveAsync();
    }
}
