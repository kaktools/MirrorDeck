using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

ApplicationConfiguration.Initialize();
Application.Run(new BootstrapperForm());

internal sealed class BootstrapperForm : Form
{
    private readonly CheckBox _uxPlayCheck = new() { Text = "AirPlay module (UxPlay)", Checked = true, AutoSize = true };
    private readonly CheckBox _scrcpyCheck = new() { Text = "Android module (scrcpy)", Checked = true, AutoSize = true };
    private readonly CheckBox _bonjourCheck = new() { Text = "Bonjour helper", Checked = true, AutoSize = true };
    private readonly ProgressBar _progress = new() { Minimum = 0, Maximum = 100, Value = 0, Style = ProgressBarStyle.Continuous, Width = 420 };
    private readonly Label _statusLabel = new() { Text = "Select components and launch installer.", AutoSize = true };
    private readonly Button _launchButton = new() { Text = "Start Installer", Width = 140, Height = 34 };
    private readonly Button _closeButton = new() { Text = "Close", Width = 96, Height = 34 };

    public BootstrapperForm()
    {
        Text = "MirrorDeck Bootstrapper";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(470, 290);

        var title = new Label
        {
            Text = "MirrorDeck Installer Setup",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 16)
        };

        var subtitle = new Label
        {
            Text = "Choose modules to preselect in the installer.",
            AutoSize = true,
            Location = new Point(20, 44)
        };

        var group = new GroupBox
        {
            Text = "Components",
            Location = new Point(20, 72),
            Size = new Size(430, 98)
        };

        _uxPlayCheck.Location = new Point(14, 24);
        _scrcpyCheck.Location = new Point(14, 47);
        _bonjourCheck.Location = new Point(220, 24);

        _uxPlayCheck.CheckedChanged += (_, _) =>
        {
            if (_uxPlayCheck.Checked && !_bonjourCheck.Checked)
            {
                _bonjourCheck.Checked = true;
            }
        };

        group.Controls.Add(_uxPlayCheck);
        group.Controls.Add(_scrcpyCheck);
        group.Controls.Add(_bonjourCheck);

        _progress.Location = new Point(20, 184);
        _statusLabel.Location = new Point(20, 214);

        _launchButton.Location = new Point(214, 244);
        _closeButton.Location = new Point(354, 244);

        _launchButton.Click += async (_, _) => await LaunchInstallerAsync();
        _closeButton.Click += (_, _) => Close();

        Controls.Add(title);
        Controls.Add(subtitle);
        Controls.Add(group);
        Controls.Add(_progress);
        Controls.Add(_statusLabel);
        Controls.Add(_launchButton);
        Controls.Add(_closeButton);
    }

    private async Task LaunchInstallerAsync()
    {
        try
        {
            SetBusy(true);
            UpdateProgress(10, "Resolving installer path...");
            await Task.Delay(120);

            var setupPath = FindLatestInstaller();
            if (string.IsNullOrWhiteSpace(setupPath) || !File.Exists(setupPath))
            {
                MessageBox.Show(this, "Installer not found. Build the installer first (dist/MirrorDeck-Setup-v*.exe).", "MirrorDeck", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                UpdateProgress(0, "Installer not found.");
                return;
            }

            UpdateProgress(45, "Preparing component selection...");
            await Task.Delay(120);

            var components = new List<string> { "core" };
            if (_uxPlayCheck.Checked)
            {
                components.Add("uxplay");
            }

            if (_scrcpyCheck.Checked)
            {
                components.Add("scrcpy");
            }

            if (_bonjourCheck.Checked)
            {
                components.Add("bonjour");
            }

            var componentArg = string.Join(",", components);
            var installerArgs = $"/COMPONENTS=\"{componentArg}\"";

            UpdateProgress(70, "Launching installer...");
            await Task.Delay(120);

            Process.Start(new ProcessStartInfo
            {
                FileName = setupPath,
                Arguments = installerArgs,
                UseShellExecute = true
            });

            UpdateProgress(100, "Installer launched successfully.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Failed to launch installer:\n" + ex.Message, "MirrorDeck", MessageBoxButtons.OK, MessageBoxIcon.Error);
            UpdateProgress(0, "Failed to launch installer.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private string? FindLatestInstaller()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "dist"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "dist")),
            Path.Combine(Environment.CurrentDirectory, "dist")
        };

        foreach (var dir in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(dir))
            {
                continue;
            }

            var latest = Directory
                .GetFiles(dir, "MirrorDeck-Setup-v*.exe")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(latest))
            {
                return latest;
            }
        }

        return null;
    }

    private void UpdateProgress(int value, string status)
    {
        _progress.Value = Math.Clamp(value, _progress.Minimum, _progress.Maximum);
        _statusLabel.Text = status;
    }

    private void SetBusy(bool isBusy)
    {
        _launchButton.Enabled = !isBusy;
        _uxPlayCheck.Enabled = !isBusy;
        _scrcpyCheck.Enabled = !isBusy;
        _bonjourCheck.Enabled = !isBusy;
    }
}
