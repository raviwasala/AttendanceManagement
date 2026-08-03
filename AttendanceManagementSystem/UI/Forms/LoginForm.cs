using AttendanceManagementSystem.UI.Controls;
using AttendanceManagementSystem.UI.Theme;
using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace AttendanceManagementSystem.UI.Forms;

/// <summary>Login form — authenticates user and launches MainForm.</summary>
public class LoginForm : Form
{
    private LabeledTextBox _txtUsername = null!;
    private LabeledTextBox _txtPassword = null!;
    private CheckBox _chkRemember = null!;
    private AppButton _btnLogin = null!;
    private Label _lblError = null!;
    private AppButton _btnForgot = null!;
    private ProgressBar _progressBar = null!;

    private readonly IAuthService _authService;
    private readonly IServiceProvider _services;

    public LoginForm(IAuthService authService, IServiceProvider services)
    {
        _authService = authService;
        _services = services;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "Login — Attendance Management System";
        Size = new Size(440, 560);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = AppTheme.FormBg;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        // ── Card container ────────────────────────────────────────────────────
        var card = new Panel
        {
            Size = new Size(380, 460),
            BackColor = AppTheme.CardBg,
            Location = new Point(30, 40)
        };

        // Logo / title
        var logo = new Label
        {
            Text = "📋",
            Font = new Font("Segoe UI", 36),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.None,
            Size = new Size(380, 70),
            Location = new Point(0, 20)
        };

        var lblTitle = new Label
        {
            Text = "Attendance Management",
            Font = AppTheme.TitleFont,
            ForeColor = AppTheme.PrimaryColor,
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(380, 30),
            Location = new Point(0, 94)
        };

        var lblSubtitle = new Label
        {
            Text = "Sign in to your account",
            Font = AppTheme.SmallFont,
            ForeColor = AppTheme.SubText,
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(380, 22),
            Location = new Point(0, 126)
        };

        // Divider
        var div = new Panel { BackColor = AppTheme.BorderColor, Size = new Size(320, 1), Location = new Point(30, 158) };

        // Username
        _txtUsername = new LabeledTextBox { LabelText = "Username", Size = new Size(320, 72), Location = new Point(30, 172) };

        // Password
        _txtPassword = new LabeledTextBox { LabelText = "Password", Size = new Size(320, 72), Location = new Point(30, 252) };
        _txtPassword.IsPassword = true;

        // Remember me
        _chkRemember = new CheckBox
        {
            Text = "Remember me",
            Font = AppTheme.SmallFont,
            ForeColor = AppTheme.SubText,
            Location = new Point(30, 330),
            AutoSize = true
        };

        // Error label
        _lblError = new Label
        {
            Text = string.Empty,
            Font = AppTheme.SmallFont,
            ForeColor = AppTheme.DangerColor,
            Size = new Size(320, 36),
            Location = new Point(30, 352),
            Visible = false
        };

        // Login button
        _btnLogin = new AppButton
        {
            Text = "Sign In",
            Size = new Size(320, 40),
            Location = new Point(30, 395)
        };
        _btnLogin.Click += async (s, e) => await LoginAsync();

        // Forgot password
        _btnForgot = new AppButton
        {
            Text = "Forgot Password?",
            Size = new Size(320, 26),
            Location = new Point(30, 442)
        };
        _btnForgot.SetSecondary();
        _btnForgot.BackColor = Color.Transparent;
        _btnForgot.ForeColor = AppTheme.PrimaryColor;
        _btnForgot.FlatAppearance.BorderSize = 0;
        _btnForgot.Click += (s, e) => ShowForgotPassword();

        card.Controls.AddRange([logo, lblTitle, lblSubtitle, div,
            _txtUsername, _txtPassword, _chkRemember, _lblError, _btnLogin, _btnForgot]);

        // Progress bar
        _progressBar = new ProgressBar
        {
            Style = ProgressBarStyle.Marquee,
            Size = new Size(440, 4),
            Location = new Point(0, 0),
            Visible = false
        };

        Controls.Add(card);
        Controls.Add(_progressBar);

        // Enter key shortcut
        KeyPreview = true;
        KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) _ = LoginAsync(); };
    }

    private async Task LoginAsync()
    {
        _txtUsername.ClearError();
        _txtPassword.ClearError();
        _lblError.Visible = false;

        var username = _txtUsername.Value.Trim();
        var password = _txtPassword.Value;

        if (string.IsNullOrWhiteSpace(username)) { _txtUsername.ShowError("Username is required."); return; }
        if (string.IsNullOrWhiteSpace(password))  { _txtPassword.ShowError("Password is required."); return; }

        SetLoading(true);
        try
        {
            var result = await _authService.LoginAsync(new LoginDto(username, password, _chkRemember.Checked));
            if (result.IsSuccess)
            {
                var main = _services.GetRequiredService<MainForm>();
                main.Show();
                Hide();
            }
            else
            {
                _lblError.Text = result.ErrorMessage;
                _lblError.Visible = true;
                _txtPassword.Value = string.Empty;
            }
        }
        catch (Exception ex)
        {
            GlobalExceptionHandler.HandleUI(ex, nameof(LoginForm));
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void SetLoading(bool loading)
    {
        _btnLogin.Enabled = !loading;
        _btnLogin.Text = loading ? "Signing in..." : "Sign In";
        _progressBar.Visible = loading;
    }

    private void ShowForgotPassword()
    {
        using var prompt = new Form
        {
            Width = 360, Height = 220, FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = "Reset Password", StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false, MinimizeBox = false, BackColor = AppTheme.FormBg
        };
        var lbl = new Label { Left = 20, Top = 15, Width = 300, Height = 35, Text = "Enter your registered Email address or Username:" };
        var txt = new TextBox { Left = 20, Top = 55, Width = 300 };
        var btnSubmit = new AppButton { Text = "Reset", Left = 120, Top = 110, Width = 90 };
        var btnCancel = new Button { Text = "Cancel", Left = 225, Top = 110, Width = 95, DialogResult = DialogResult.Cancel };
        btnSubmit.Click += async (s, e) =>
        {
            var val = txt.Text.Trim();
            if (string.IsNullOrEmpty(val)) { MessageBox.Show("Please enter an email or username.", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            btnSubmit.Enabled = false;
            var res = await _authService.RequestPasswordResetAsync(new ForgotPasswordDto(val), "https://localhost:7196");
            if (res.IsSuccess)
            {
                MessageBox.Show("If an account matching that email address exists, a password reset link has been sent to your email inbox.", "Reset Requested", MessageBoxButtons.OK, MessageBoxIcon.Information);
                prompt.Close();
            }
            else
            {
                MessageBox.Show(res.ErrorMessage, "Reset Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnSubmit.Enabled = true;
            }
        };
        prompt.Controls.AddRange([lbl, txt, btnSubmit, btnCancel]);
        prompt.AcceptButton = btnSubmit;
        prompt.CancelButton = btnCancel;
        prompt.ShowDialog(this);
    }
}
