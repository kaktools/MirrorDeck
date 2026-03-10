using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MirrorDeck.WinUI.Infrastructure;
using MirrorDeck.WinUI.Models;
using MirrorDeck.WinUI.Services.Interfaces;
using Windows.ApplicationModel.DataTransfer;

namespace MirrorDeck.WinUI.ViewModels;

public class LogsViewModel : ObservableObject
{
    private readonly ILoggingService _loggingService;
    private string _selectedSource = "All";

    public ObservableCollection<LogEntry> Entries { get; } = [];
    public ObservableCollection<LogEntry> FilteredEntries { get; } = [];
    public ObservableCollection<string> Sources { get; } = ["All", "MirrorDeck", "UxPlay", "scrcpy", "Setup"];

    public string SelectedSource
    {
        get => _selectedSource;
        set
        {
            if (SetProperty(ref _selectedSource, value))
            {
                ApplyFilter();
            }
        }
    }

    public IRelayCommand ClearCommand { get; }
    public IRelayCommand CopyCommand { get; }
    public IRelayCommand OpenLogFileCommand { get; }

    public LogsViewModel(ILoggingService loggingService)
    {
        _loggingService = loggingService;
        ClearCommand = new RelayCommand(Clear);
        CopyCommand = new RelayCommand(CopyToClipboard);
        OpenLogFileCommand = new RelayCommand(OpenLogFile);

        foreach (var entry in _loggingService.Snapshot())
        {
            Entries.Add(entry);
        }

        ApplyFilter();

        _loggingService.LogReceived += (_, entry) =>
        {
            if (App.UiDispatcherQueue is null)
            {
                AddEntry(entry);
                return;
            }

            App.UiDispatcherQueue.TryEnqueue(() => AddEntry(entry));
        };
    }

    public void Clear()
    {
        Entries.Clear();
        FilteredEntries.Clear();
    }

    private void AddEntry(LogEntry entry)
    {
        Entries.Add(entry);
        if (MatchesFilter(entry))
        {
            FilteredEntries.Add(entry);
        }
    }

    private void ApplyFilter()
    {
        FilteredEntries.Clear();
        foreach (var entry in Entries)
        {
            if (MatchesFilter(entry))
            {
                FilteredEntries.Add(entry);
            }
        }
    }

    private bool MatchesFilter(LogEntry entry)
    {
        if (string.Equals(SelectedSource, "All", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return entry.Source.Contains(SelectedSource, StringComparison.OrdinalIgnoreCase);
    }

    private void CopyToClipboard()
    {
        var builder = new StringBuilder();
        foreach (var entry in FilteredEntries)
        {
            builder.Append(entry.Timestamp.ToString("u"));
            builder.Append(" [");
            builder.Append(entry.Level);
            builder.Append("] [");
            builder.Append(entry.Source);
            builder.Append("] ");
            builder.AppendLine(entry.Message);
        }

        var package = new DataPackage();
        package.SetText(builder.ToString());
        Clipboard.SetContent(package);
    }

    private void OpenLogFile()
    {
        var path = AppPaths.LogFile;
        AppPaths.EnsureDirectories();

        if (!File.Exists(path))
        {
            File.WriteAllText(path, string.Empty);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{path}\"",
            UseShellExecute = true
        });
    }
}
