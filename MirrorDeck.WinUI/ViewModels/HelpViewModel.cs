using CommunityToolkit.Mvvm.ComponentModel;

namespace MirrorDeck.WinUI.ViewModels;

public sealed class HelpViewModel : ObservableObject
{
    private sealed record LocalizedHelp(
        string PageTitle,
        string LanguageLabel,
        string HowItWorksTitle,
        string HowStep1,
        string HowStep2,
        string HowStep3,
        string HowStep4,
        string SourcesTitle,
        string SourceUxPlay,
        string SourceScrcpy,
        string SourceBonjour,
        string SourceApi,
        string TrayTitle,
        string TrayItem1,
        string TrayItem2,
        string TrayItem3,
        string TrayItem4,
        string TrayItem5,
        string TroubleTitle,
        string TroubleAirPlayTitle,
        string TroubleAirPlay1,
        string TroubleAirPlay2,
        string TroubleAirPlay3,
        string TroubleAdbTitle,
        string TroubleAdb1,
        string TroubleAdb2,
        string TroubleTcpTitle,
        string TroubleTcp1,
        string TroubleTcp2,
        string TroubleTcp3,
        string AndroidQuickGuideTitle,
        string AndroidQuickGuideSteps);

    public sealed record LanguageOption(string Code, string DisplayName);

    private static readonly LocalizedHelp German = new(
        PageTitle: "Hilfe",
        LanguageLabel: "Sprache",
        HowItWorksTitle: "So funktioniert MirrorDeck",
        HowStep1: "1. Im Setup-Assistenten auswählen, ob UxPlay, scrcpy oder beide installiert werden sollen.",
        HowStep2: "2. Das Dashboard zeigt jederzeit, welche Dienste verfügbar sind und ob sie laufen.",
        HowStep3: "3. Start/Stop/Restart/Pause/Snapshot direkt über Dashboard oder Tasktray.",
        HowStep4: "4. Wenn nichts installiert wurde, startet die App trotzdem und Nachinstallation ist jederzeit möglich.",
        SourcesTitle: "Externe Quellen / Komponenten",
        SourceUxPlay: "- UxPlay (AirPlay Receiver): https://github.com/FDH2/UxPlay",
        SourceScrcpy: "- scrcpy (Android Mirroring): https://github.com/Genymobile/scrcpy",
        SourceBonjour: "- Bonjour Service (AirPlay Discovery): Apple Bonjour Print Services",
        SourceApi: "- Releases werden dynamisch per GitHub API ermittelt, nicht statisch hinterlegt.",
        TrayTitle: "Tasktray-Menü",
        TrayItem1: "- App öffnen",
        TrayItem2: "- AirPlay Start/Stop/Restart/Pause",
        TrayItem3: "- Android USB/TCP Aktionen",
        TrayItem4: "- Setup-Assistent und Hilfe",
        TrayItem5: "- Alle Sessions stoppen / Beenden",
        TroubleTitle: "Troubleshooting",
        TroubleAirPlayTitle: "AirPlay findet kein Gerät:",
        TroubleAirPlay1: "- Bonjour-Status im Setup-Assistenten prüfen.",
        TroubleAirPlay2: "- Bonjour-Dienst starten oder neu starten.",
        TroubleAirPlay3: "- AirPlay-Modul installiert und aktiviert?",
        TroubleAdbTitle: "adb fehlt / Android startet nicht:",
        TroubleAdb1: "- Setup-Assistent: scrcpy-Modul installieren oder aktualisieren.",
        TroubleAdb2: "- Verfügbarkeit im Dashboard prüfen (adb).",
        TroubleTcpTitle: "TCP-Connect schlägt fehl:",
        TroubleTcp1: "- IP/Port in Settings oder Android-Ansicht kontrollieren.",
        TroubleTcp2: "- adb TCP connect erneut aus Tasktray oder Android-Seite ausführen.",
        TroubleTcp3: "- USB einmal verbinden und 'adb tcpip 5555' auf dem Gerät aktivieren.",
        AndroidQuickGuideTitle: "Mini-Kurzfassung: Android für MirrorDeck aktivieren",
        AndroidQuickGuideSteps:
            "1. Entwickleroptionen aktivieren\n" +
            "\t- Meist über Einstellungen -> Geräteinfo / Tabletinfo -> Softwareinformationen -> mehrfach auf 'Buildnummer' tippen\n" +
            "\t- Hinweis: Der genaue Pfad kann je nach Hersteller und Android-Version abweichen.\n\n" +
            "2. USB-Debugging einschalten\n\n" +
            "3. Tablet und PC ins gleiche WLAN bringen\n\n" +
            "4. Tablet einmal per USB anschließen\n\n" +
            "5. Debugging auf dem Tablet erlauben\n\n" +
            "6. USB starten\n\n" +
            "7. Geräte aktualisieren\n\n" +
            "8. IP-Adresse / Hostname vom Gerät + Port (5555) ausfüllen\n\n" +
            "9. USB-Kabel abziehen\n\n" +
            "10. Verbinden + Starten, Tablet erscheint auf dem PC");

    private static readonly LocalizedHelp English = new(
        PageTitle: "Help",
        LanguageLabel: "Language",
        HowItWorksTitle: "How MirrorDeck Works",
        HowStep1: "1. In Setup Assistant, choose whether to install UxPlay, scrcpy, or both.",
        HowStep2: "2. The dashboard always shows which services are available and running.",
        HowStep3: "3. Start/Stop/Restart/Pause/Snapshot directly from dashboard or tray.",
        HowStep4: "4. Even with no modules installed, the app still starts and supports later install.",
        SourcesTitle: "External Sources / Components",
        SourceUxPlay: "- UxPlay (AirPlay receiver): https://github.com/FDH2/UxPlay",
        SourceScrcpy: "- scrcpy (Android mirroring): https://github.com/Genymobile/scrcpy",
        SourceBonjour: "- Bonjour Service (AirPlay discovery): Apple Bonjour Print Services",
        SourceApi: "- Releases are resolved dynamically via GitHub API, not hardcoded.",
        TrayTitle: "Tray Menu",
        TrayItem1: "- Open app",
        TrayItem2: "- AirPlay start/stop/restart/pause",
        TrayItem3: "- Android USB/TCP actions",
        TrayItem4: "- Setup Assistant and Help",
        TrayItem5: "- Stop all sessions / Exit",
        TroubleTitle: "Troubleshooting",
        TroubleAirPlayTitle: "AirPlay device not found:",
        TroubleAirPlay1: "- Check Bonjour status in Setup Assistant.",
        TroubleAirPlay2: "- Start or restart the Bonjour service.",
        TroubleAirPlay3: "- Is the AirPlay module installed and enabled?",
        TroubleAdbTitle: "adb missing / Android not starting:",
        TroubleAdb1: "- Setup Assistant: install or update the scrcpy module.",
        TroubleAdb2: "- Check availability in dashboard (adb).",
        TroubleTcpTitle: "TCP connect fails:",
        TroubleTcp1: "- Verify IP/port in Settings or Android page.",
        TroubleTcp2: "- Run adb TCP connect again from tray or Android page.",
        TroubleTcp3: "- Connect once by USB and enable 'adb tcpip 5555' on device.",
        AndroidQuickGuideTitle: "Mini Quick Guide: Enable Android for MirrorDeck",
        AndroidQuickGuideSteps: "1. Enable Developer Options\n2. Enable USB debugging\n3. Put tablet and PC on the same Wi-Fi\n4. Connect tablet once via USB\n5. Allow debugging prompt on device\n6. Verify with 'adb devices'\n7. Run 'scrcpy --tcpip'\n8. Unplug USB\n9. Tablet appears on your PC");

    private string _selectedLanguageCode = "de";

    public IReadOnlyList<LanguageOption> LanguageOptions { get; } =
    [
        new("de", "Deutsch"),
        new("en", "English")
    ];

    public string SelectedLanguageCode
    {
        get => _selectedLanguageCode;
        set
        {
            var normalized = value?.ToLowerInvariant() == "en" ? "en" : "de";
            if (SetProperty(ref _selectedLanguageCode, normalized))
            {
                ApplyLocalization();
            }
        }
    }

    private string _pageTitle = string.Empty;
    public string PageTitle
    {
        get => _pageTitle;
        private set => SetProperty(ref _pageTitle, value);
    }

    private string _languageLabel = string.Empty;
    public string LanguageLabel
    {
        get => _languageLabel;
        private set => SetProperty(ref _languageLabel, value);
    }

    private string _howItWorksTitle = string.Empty;
    public string HowItWorksTitle
    {
        get => _howItWorksTitle;
        private set => SetProperty(ref _howItWorksTitle, value);
    }

    private string _howStep1 = string.Empty;
    public string HowStep1
    {
        get => _howStep1;
        private set => SetProperty(ref _howStep1, value);
    }

    private string _howStep2 = string.Empty;
    public string HowStep2
    {
        get => _howStep2;
        private set => SetProperty(ref _howStep2, value);
    }

    private string _howStep3 = string.Empty;
    public string HowStep3
    {
        get => _howStep3;
        private set => SetProperty(ref _howStep3, value);
    }

    private string _howStep4 = string.Empty;
    public string HowStep4
    {
        get => _howStep4;
        private set => SetProperty(ref _howStep4, value);
    }

    private string _sourcesTitle = string.Empty;
    public string SourcesTitle
    {
        get => _sourcesTitle;
        private set => SetProperty(ref _sourcesTitle, value);
    }

    private string _sourceUxPlay = string.Empty;
    public string SourceUxPlay
    {
        get => _sourceUxPlay;
        private set => SetProperty(ref _sourceUxPlay, value);
    }

    private string _sourceScrcpy = string.Empty;
    public string SourceScrcpy
    {
        get => _sourceScrcpy;
        private set => SetProperty(ref _sourceScrcpy, value);
    }

    private string _sourceBonjour = string.Empty;
    public string SourceBonjour
    {
        get => _sourceBonjour;
        private set => SetProperty(ref _sourceBonjour, value);
    }

    private string _sourceApi = string.Empty;
    public string SourceApi
    {
        get => _sourceApi;
        private set => SetProperty(ref _sourceApi, value);
    }

    private string _trayTitle = string.Empty;
    public string TrayTitle
    {
        get => _trayTitle;
        private set => SetProperty(ref _trayTitle, value);
    }

    private string _trayItem1 = string.Empty;
    public string TrayItem1
    {
        get => _trayItem1;
        private set => SetProperty(ref _trayItem1, value);
    }

    private string _trayItem2 = string.Empty;
    public string TrayItem2
    {
        get => _trayItem2;
        private set => SetProperty(ref _trayItem2, value);
    }

    private string _trayItem3 = string.Empty;
    public string TrayItem3
    {
        get => _trayItem3;
        private set => SetProperty(ref _trayItem3, value);
    }

    private string _trayItem4 = string.Empty;
    public string TrayItem4
    {
        get => _trayItem4;
        private set => SetProperty(ref _trayItem4, value);
    }

    private string _trayItem5 = string.Empty;
    public string TrayItem5
    {
        get => _trayItem5;
        private set => SetProperty(ref _trayItem5, value);
    }

    private string _troubleTitle = string.Empty;
    public string TroubleTitle
    {
        get => _troubleTitle;
        private set => SetProperty(ref _troubleTitle, value);
    }

    private string _troubleAirPlayTitle = string.Empty;
    public string TroubleAirPlayTitle
    {
        get => _troubleAirPlayTitle;
        private set => SetProperty(ref _troubleAirPlayTitle, value);
    }

    private string _troubleAirPlay1 = string.Empty;
    public string TroubleAirPlay1
    {
        get => _troubleAirPlay1;
        private set => SetProperty(ref _troubleAirPlay1, value);
    }

    private string _troubleAirPlay2 = string.Empty;
    public string TroubleAirPlay2
    {
        get => _troubleAirPlay2;
        private set => SetProperty(ref _troubleAirPlay2, value);
    }

    private string _troubleAirPlay3 = string.Empty;
    public string TroubleAirPlay3
    {
        get => _troubleAirPlay3;
        private set => SetProperty(ref _troubleAirPlay3, value);
    }

    private string _troubleAdbTitle = string.Empty;
    public string TroubleAdbTitle
    {
        get => _troubleAdbTitle;
        private set => SetProperty(ref _troubleAdbTitle, value);
    }

    private string _troubleAdb1 = string.Empty;
    public string TroubleAdb1
    {
        get => _troubleAdb1;
        private set => SetProperty(ref _troubleAdb1, value);
    }

    private string _troubleAdb2 = string.Empty;
    public string TroubleAdb2
    {
        get => _troubleAdb2;
        private set => SetProperty(ref _troubleAdb2, value);
    }

    private string _troubleTcpTitle = string.Empty;
    public string TroubleTcpTitle
    {
        get => _troubleTcpTitle;
        private set => SetProperty(ref _troubleTcpTitle, value);
    }

    private string _troubleTcp1 = string.Empty;
    public string TroubleTcp1
    {
        get => _troubleTcp1;
        private set => SetProperty(ref _troubleTcp1, value);
    }

    private string _troubleTcp2 = string.Empty;
    public string TroubleTcp2
    {
        get => _troubleTcp2;
        private set => SetProperty(ref _troubleTcp2, value);
    }

    private string _troubleTcp3 = string.Empty;
    public string TroubleTcp3
    {
        get => _troubleTcp3;
        private set => SetProperty(ref _troubleTcp3, value);
    }

    private string _androidQuickGuideTitle = string.Empty;
    public string AndroidQuickGuideTitle
    {
        get => _androidQuickGuideTitle;
        private set => SetProperty(ref _androidQuickGuideTitle, value);
    }

    private string _androidQuickGuideSteps = string.Empty;
    public string AndroidQuickGuideSteps
    {
        get => _androidQuickGuideSteps;
        private set => SetProperty(ref _androidQuickGuideSteps, value);
    }

    public HelpViewModel()
    {
        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        var localized = SelectedLanguageCode == "en" ? English : German;

        PageTitle = localized.PageTitle;
        LanguageLabel = localized.LanguageLabel;
        HowItWorksTitle = localized.HowItWorksTitle;
        HowStep1 = localized.HowStep1;
        HowStep2 = localized.HowStep2;
        HowStep3 = localized.HowStep3;
        HowStep4 = localized.HowStep4;
        SourcesTitle = localized.SourcesTitle;
        SourceUxPlay = localized.SourceUxPlay;
        SourceScrcpy = localized.SourceScrcpy;
        SourceBonjour = localized.SourceBonjour;
        SourceApi = localized.SourceApi;
        TrayTitle = localized.TrayTitle;
        TrayItem1 = localized.TrayItem1;
        TrayItem2 = localized.TrayItem2;
        TrayItem3 = localized.TrayItem3;
        TrayItem4 = localized.TrayItem4;
        TrayItem5 = localized.TrayItem5;
        TroubleTitle = localized.TroubleTitle;
        TroubleAirPlayTitle = localized.TroubleAirPlayTitle;
        TroubleAirPlay1 = localized.TroubleAirPlay1;
        TroubleAirPlay2 = localized.TroubleAirPlay2;
        TroubleAirPlay3 = localized.TroubleAirPlay3;
        TroubleAdbTitle = localized.TroubleAdbTitle;
        TroubleAdb1 = localized.TroubleAdb1;
        TroubleAdb2 = localized.TroubleAdb2;
        TroubleTcpTitle = localized.TroubleTcpTitle;
        TroubleTcp1 = localized.TroubleTcp1;
        TroubleTcp2 = localized.TroubleTcp2;
        TroubleTcp3 = localized.TroubleTcp3;
        AndroidQuickGuideTitle = localized.AndroidQuickGuideTitle;
        AndroidQuickGuideSteps = localized.AndroidQuickGuideSteps;
    }
}
