using ResellerSystem.Domain.Shared.Dto;

namespace ResellerSystem.Desktop.ServerManager;

public sealed class BackupPickerDialog : Form
{
    private readonly ListBox _list = new() { Dock = DockStyle.Fill };
    private readonly List<BackupManifestDto> _backups;

    public string? SelectedBackupId { get; private set; }

    public BackupPickerDialog(IReadOnlyList<BackupManifestDto> backups)
    {
        _backups = backups.ToList();

        Text = "Select a Backup to Restore";
        Width = 480;
        Height = 360;
        StartPosition = FormStartPosition.CenterParent;

        foreach (var b in _backups)
        {
            _list.Items.Add($"{b.CreatedAt:yyyy-MM-dd HH:mm}  [{b.Type}]  v{b.ServerVersionAtBackup}  ({b.TotalSizeBytes / 1024 / 1024} MB)  — {b.Id}");
        }

        var okButton = new Button { Text = "Restore Selected", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom, Height = 36 };
        okButton.Click += (_, _) =>
        {
            if (_list.SelectedIndex >= 0) SelectedBackupId = _backups[_list.SelectedIndex].Id;
        };

        AcceptButton = okButton;
        Controls.Add(_list);
        Controls.Add(okButton);
    }
}
