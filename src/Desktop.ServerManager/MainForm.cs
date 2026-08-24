using System.Diagnostics;
using ResellerSystem.Desktop.Services;
using ResellerSystem.Desktop.Services.Api;
using ResellerSystem.Domain.Shared.Dto;

namespace ResellerSystem.Desktop.ServerManager;

public sealed class MainForm : Form
{
    private const string ServiceName = "ResellerSystemServer";
    private const string InstallDir = @"C:\Program Files\ResellerSystem";

    private readonly ServerStatusReader _statusReader = new(InstallDir);
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 5000 };
    private readonly NotifyIcon _trayIcon;
    private readonly IServerApiClient _apiClient = new ServerApiClient(new HttpClient());
    private bool _isSignedIn;

    private Label _serviceStatusValue = null!;
    private Label _pgStatusValue = null!;
    private Label _versionValue = null!;
    private Label _addressValue = null!;
    private Label _storageValue = null!;
    private Label _diskSpaceValue = null!;
    private Button _startButton = null!;
    private Button _stopButton = null!;
    private Button _restartButton = null!;
    private Button _signInButton = null!;
    private Button _backupNowButton = null!;
    private Button _viewBackupsButton = null!;
    private Button _checkUpdatesButton = null!;
    private Button _installUpdateButton = null!;
    private Label _updateStatusLabel = null!;

    public MainForm()
    {
        Text = "Reseller System Server Manager";
        Width = 560;
        Height = 620;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        _apiClient.Configure("http://localhost:5000");

        BuildLayout();

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "Reseller System Server"
        };
        _trayIcon.DoubleClick += (_, _) => ShowFromTray();
        _trayIcon.ContextMenuStrip = BuildTrayMenu();

        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized) Hide();
        };

        _refreshTimer.Tick += async (_, _) => await RefreshStatusAsync();
        _refreshTimer.Start();

        Load += async (_, _) => await RefreshStatusAsync();
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => ShowFromTray());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => { _trayIcon.Visible = false; Application.Exit(); });
        return menu;
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(16),
            AutoSize = true
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));

        (Label label, Label value) Row(string caption)
        {
            var l = new Label { Text = caption, AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 6, 0, 6) };
            var v = new Label { Text = "-", AutoSize = true, Margin = new Padding(0, 6, 0, 6) };
            root.Controls.Add(l);
            root.Controls.Add(v);
            return (l, v);
        }

        (_, _serviceStatusValue) = Row("Server status:");
        (_, _pgStatusValue) = Row("PostgreSQL status:");
        (_, _versionValue) = Row("Server version:");
        (_, _addressValue) = Row("Address:");
        (_, _storageValue) = Row("Storage location:");
        (_, _diskSpaceValue) = Row("Free disk space:");

        var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 100, Padding = new Padding(16) };

        _startButton = new Button { Text = "Start Server", Width = 150, Height = 32 };
        _stopButton = new Button { Text = "Stop Server", Width = 150, Height = 32 };
        _restartButton = new Button { Text = "Restart Server", Width = 150, Height = 32 };
        var openClientButton = new Button { Text = "Open Client", Width = 150, Height = 32 };
        var settingsButton = new Button { Text = "Settings", Width = 150, Height = 32 };
        var logsButton = new Button { Text = "Logs", Width = 150, Height = 32 };

        _startButton.Click += async (_, _) => await RunActionAsync(_startButton, () => ServiceControlHelper.StartAsync(ServiceName));
        _stopButton.Click += async (_, _) => await RunActionAsync(_stopButton, () => ServiceControlHelper.StopAsync(ServiceName));
        _restartButton.Click += async (_, _) => await RunActionAsync(_restartButton, () => ServiceControlHelper.RestartAsync(ServiceName));
        openClientButton.Click += (_, _) => OpenClient();
        settingsButton.Click += (_, _) => OpenSettingsFolder();
        logsButton.Click += (_, _) => OpenLogsFolder();

        buttonPanel.Controls.AddRange(new Control[]
        {
            _startButton, _stopButton, _restartButton, openClientButton, settingsButton, logsButton
        });

        var authAndUpdatesPanel = BuildAuthAndUpdatesPanel();

        Controls.Add(root);
        Controls.Add(authAndUpdatesPanel);
        Controls.Add(buttonPanel);
    }

    private Panel BuildAuthAndUpdatesPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 140,
            Padding = new Padding(16, 8, 16, 8),
            BorderStyle = BorderStyle.FixedSingle,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };

        var authRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        _signInButton = new Button { Text = "Sign In", Width = 150, Height = 30 };
        _signInButton.Click += async (_, _) => await SignInAsync();
        authRow.Controls.Add(_signInButton);
        authRow.Controls.Add(new Label { Text = "Backups and updates require signing in.", AutoSize = true, Margin = new Padding(8, 8, 0, 0) });

        var backupRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        _backupNowButton = new Button { Text = "Backup Now (Full)", Width = 150, Height = 30, Enabled = false };
        _viewBackupsButton = new Button { Text = "View / Restore Backups", Width = 180, Height = 30, Enabled = false };
        _backupNowButton.Click += async (_, _) => await BackupNowAsync();
        _viewBackupsButton.Click += async (_, _) => await ViewBackupsAsync();
        backupRow.Controls.Add(_backupNowButton);
        backupRow.Controls.Add(_viewBackupsButton);

        var updateRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        _checkUpdatesButton = new Button { Text = "Check Updates", Width = 150, Height = 30, Enabled = false };
        _installUpdateButton = new Button { Text = "Install Update", Width = 150, Height = 30, Enabled = false };
        _checkUpdatesButton.Click += async (_, _) => await CheckUpdatesAsync();
        _installUpdateButton.Click += async (_, _) => await InstallUpdateAsync();
        updateRow.Controls.Add(_checkUpdatesButton);
        updateRow.Controls.Add(_installUpdateButton);

        _updateStatusLabel = new Label { Text = "", AutoSize = true, MaximumSize = new Size(480, 0) };

        panel.Controls.Add(authRow);
        panel.Controls.Add(backupRow);
        panel.Controls.Add(updateRow);
        panel.Controls.Add(_updateStatusLabel);

        return panel;
    }

    private async Task SignInAsync()
    {
        using var dialog = new LoginDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            await _apiClient.LoginAsync(dialog.Username, dialog.Password);
            _isSignedIn = true;
            _signInButton.Text = "Signed In";
            _signInButton.Enabled = false;
            _backupNowButton.Enabled = true;
            _viewBackupsButton.Enabled = true;
            _checkUpdatesButton.Enabled = true;
        }
        catch (ServerApiException ex)
        {
            MessageBox.Show(this, ex.Error.Message, "Sign In Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Sign In Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task BackupNowAsync()
    {
        _backupNowButton.Enabled = false;
        try
        {
            var manifest = await _apiClient.CreateBackupAsync(BackupTypeDto.Full);
            MessageBox.Show(this,
                $"Backup '{manifest.Id}' completed ({manifest.TotalSizeBytes / 1024 / 1024} MB, {manifest.Databases.Count} databases).",
                "Backup Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Backup Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _backupNowButton.Enabled = true;
        }
    }

    private async Task ViewBackupsAsync()
    {
        try
        {
            var backups = await _apiClient.ListBackupsAsync();
            if (backups.Count == 0)
            {
                MessageBox.Show(this, "No backups yet.", "Backups", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var picker = new BackupPickerDialog(backups);
            if (picker.ShowDialog(this) != DialogResult.OK || picker.SelectedBackupId is null) return;

            var confirm = MessageBox.Show(this,
                $"This will OVERWRITE current data with backup '{picker.SelectedBackupId}'. This cannot be undone. Continue?",
                "Confirm Restore", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            await _apiClient.RestoreBackupAsync(picker.SelectedBackupId);
            MessageBox.Show(this, "Restore completed.", "Restore", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Restore Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task CheckUpdatesAsync()
    {
        _checkUpdatesButton.Enabled = false;
        try
        {
            var result = await _apiClient.CheckForUpdateAsync();
            _updateStatusLabel.Text = result.UpdateAvailable
                ? $"Update available: {result.AvailableVersion} (current: {result.CurrentVersion})"
                : $"Up to date (current: {result.CurrentVersion})";
            _installUpdateButton.Enabled = result.UpdateAvailable;
        }
        catch (Exception ex)
        {
            _updateStatusLabel.Text = $"Check failed: {ex.Message}";
        }
        finally
        {
            _checkUpdatesButton.Enabled = true;
        }
    }

    private async Task InstallUpdateAsync()
    {
        var confirm = MessageBox.Show(this,
            "This will back up your data, stop the server briefly, and install the update. Continue?",
            "Install Update", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes) return;

        _installUpdateButton.Enabled = false;
        try
        {
            var result = await _apiClient.InstallUpdateAsync();
            _updateStatusLabel.Text = $"{result.Status}: {result.Message}";
        }
        catch (Exception ex)
        {
            _updateStatusLabel.Text = $"Install failed: {ex.Message}";
        }
    }

    private async Task RunActionAsync(Button trigger, Func<Task<bool>> action)
    {
        trigger.Enabled = false;
        try
        {
            var ok = await action();
            if (!ok)
            {
                MessageBox.Show(this,
                    "The action was cancelled or failed. Administrator approval is required to start/stop/restart the server.",
                    "Reseller System", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        finally
        {
            trigger.Enabled = true;
            await RefreshStatusAsync();
        }
    }

    private static void OpenClient()
    {
        var clientPath = @"C:\Program Files\ResellerSystem Client\Desktop.App.exe";
        if (File.Exists(clientPath))
        {
            Process.Start(new ProcessStartInfo(clientPath) { UseShellExecute = true });
        }
        else
        {
            MessageBox.Show(
                "Reseller System Client is not installed on this computer.\n\n" +
                "Install it separately using ResellerSystem-Client-Setup.exe, or connect from another computer on your network.",
                "Reseller System", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private static void OpenSettingsFolder() =>
        Process.Start(new ProcessStartInfo(Path.Combine(InstallDir, "config")) { UseShellExecute = true });

    private static void OpenLogsFolder() =>
        Process.Start(new ProcessStartInfo(Path.Combine(InstallDir, "logs")) { UseShellExecute = true });

    private async Task RefreshStatusAsync()
    {
        try
        {
            var snapshot = await _statusReader.ReadAsync();

            _serviceStatusValue.Text = snapshot.ServiceStatus;
            _pgStatusValue.Text = snapshot.PostgresServiceStatus;
            _versionValue.Text = snapshot.ServerVersion ?? "(unavailable)";
            _addressValue.Text = $"http://{snapshot.LocalIpAddress}:{snapshot.Port}";
            _storageValue.Text = snapshot.StorageLocation;
            _diskSpaceValue.Text = FormatBytes(snapshot.FreeDiskSpaceBytes);

            var running = snapshot.ServiceStatus == "Running";
            _startButton.Enabled = !running;
            _stopButton.Enabled = running;
            _restartButton.Enabled = running;

            _trayIcon.Text = running
                ? $"Reseller System Server — running ({snapshot.HealthStatus ?? "unknown"})"
                : "Reseller System Server — stopped";
        }
        catch
        {
            // Best-effort UI — a transient failure here shouldn't crash the tray app.
        }
    }

    private static string FormatBytes(long bytes)
    {
        double gb = bytes / 1024.0 / 1024.0 / 1024.0;
        return $"{gb:0.0} GB";
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Closing the window minimizes to tray instead of exiting, so the
        // status icon (and the server, which is a separate Windows Service
        // regardless) keeps running. Real exit is via the tray menu.
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnFormClosing(e);
    }
}
