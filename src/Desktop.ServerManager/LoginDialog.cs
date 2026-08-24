namespace ResellerSystem.Desktop.ServerManager;

public sealed class LoginDialog : Form
{
    private readonly TextBox _usernameBox = new() { Width = 220 };
    private readonly TextBox _passwordBox = new() { Width = 220, PasswordChar = '*' };

    public string Username => _usernameBox.Text;
    public string Password => _passwordBox.Text;

    public LoginDialog()
    {
        Text = "Sign In — Reseller System";
        Width = 320;
        Height = 220;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;

        var layout = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, Padding = new Padding(16), AutoSize = true };
        layout.Controls.Add(new Label { Text = "Username:", AutoSize = true, Margin = new Padding(0, 8, 8, 0) });
        layout.Controls.Add(_usernameBox);
        layout.Controls.Add(new Label { Text = "Password:", AutoSize = true, Margin = new Padding(0, 8, 8, 0) });
        layout.Controls.Add(_passwordBox);

        var okButton = new Button { Text = "Sign In", DialogResult = DialogResult.OK, Left = 130, Top = 130, Width = 80 };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 220, Top = 130, Width = 80 };

        AcceptButton = okButton;
        CancelButton = cancelButton;

        Controls.Add(layout);
        Controls.Add(okButton);
        Controls.Add(cancelButton);
    }
}
