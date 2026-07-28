using AttendanceManagementSystem.UI.Controls;
using AttendanceManagementSystem.UI.Theme;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Exceptions;
using AttendanceSystem.Common.Session;
using Microsoft.Extensions.DependencyInjection;

namespace AttendanceManagementSystem.UI.Forms;

/// <summary>Main application shell with sidebar navigation and content area.</summary>
public class MainForm : Form
{
    private Panel _sidebar = null!;
    private Panel _contentArea = null!;
    private Panel _topBar = null!;
    private Panel _statusBar = null!;
    private Label _lblPageTitle = null!;
    private Label _lblUser = null!;
    private Label _lblClock = null!;
    private Label _lblStatus = null!;
    private System.Windows.Forms.Timer _clockTimer = null!;
    private Form? _currentChild;
    private Button? _activeNavBtn;

    private readonly IServiceProvider _services;

    public MainForm(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();
        BuildSidebar();
        BuildTopBar();
        BuildStatusBar();
        StartClock();
        Navigate<DashboardForm>("Dashboard");
    }

    private void InitializeComponent()
    {
        Text = "Attendance Management System";
        Size = new Size(1280, 780);
        MinimumSize = new Size(1024, 600);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = AppTheme.FormBg;
        WindowState = FormWindowState.Maximized;

        _topBar = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Color.White };
        _sidebar = new Panel { Dock = DockStyle.Left, Width = 220, BackColor = AppTheme.SidebarBg };
        _contentArea = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.FormBg, Padding = new Padding(16) };
        _statusBar = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = AppTheme.StatusBarBg };

        Controls.Add(_contentArea);
        Controls.Add(_sidebar);
        Controls.Add(_topBar);
        Controls.Add(_statusBar);
    }

    private void BuildTopBar()
    {
        _topBar.BackColor = Color.White;

        // App logo / name
        var logo = new Label
        {
            Text = "📋  Attendance System",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = AppTheme.PrimaryColor,
            AutoSize = true,
            Location = new Point(14, 14)
        };

        _lblPageTitle = new Label
        {
            Text = "Dashboard",
            Font = AppTheme.HeaderFont,
            ForeColor = AppTheme.BodyText,
            AutoSize = true,
            Location = new Point(240, 18)
        };

        _lblUser = new Label
        {
            Text = $"👤  {AppSession.FullName}  ({AppSession.RoleName})",
            Font = AppTheme.SmallFont,
            ForeColor = AppTheme.SubText,
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _lblUser.Location = new Point(_topBar.Width - 320, 8);

        _lblClock = new Label
        {
            Font = AppTheme.SmallFont,
            ForeColor = AppTheme.SubText,
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _lblClock.Location = new Point(_topBar.Width - 200, 26);

        // Theme toggle
        var btnTheme = new AppButton { Text = "🌙 Theme", Width = 90, Height = 30, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        btnTheme.SetSecondary();
        btnTheme.Location = new Point(_topBar.Width - 110, 12);
        btnTheme.Click += (s, e) => { AppTheme.ToggleTheme(); ApplyTheme(); };

        // Logout
        var btnLogout = new AppButton { Text = "⏻ Logout", Width = 85, Height = 30, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        btnLogout.SetDanger();
        btnLogout.Location = new Point(_topBar.Width - 210, 12);
        btnLogout.Click += BtnLogout_Click;

        // Separator line bottom
        var sep = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = AppTheme.BorderColor };

        _topBar.Controls.AddRange([logo, _lblPageTitle, _lblUser, _lblClock, btnTheme, btnLogout, sep]);
        _topBar.Resize += (s, e) =>
        {
            _lblUser.Location = new Point(_topBar.Width - 420, 8);
            _lblClock.Location = new Point(_topBar.Width - 420, 26);
            btnLogout.Location = new Point(_topBar.Width - 210, 12);
            btnTheme.Location = new Point(_topBar.Width - 110, 12);
        };
    }

    private void BuildStatusBar()
    {
        _lblStatus = new Label
        {
            Text = "Ready",
            Font = AppTheme.SmallFont,
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(8, 6)
        };
        _statusBar.Controls.Add(_lblStatus);
    }

    private void BuildSidebar()
    {
        _sidebar.Controls.Clear();
        var y = 0;

        // Branding header
        var header = new Panel { Width = 220, Height = 64, BackColor = Color.FromArgb(0, 0, 0, 40) };
        var branding = new Label
        {
            Text = "AMS",
            Font = new Font("Segoe UI", 22f, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };
        header.Controls.Add(branding);
        header.Top = y; y += 64;
        _sidebar.Controls.Add(header);

        // Nav items: icon, label, form type, permission
        var navItems = new (string Icon, string Label, string Tag)[]
        {
            ("🏠", "Dashboard",         "Dashboard"),
            ("👥", "Employees",         "Employees"),
            ("🏢", "Departments",       "Departments"),
            ("🎖", "Designations",      "Designations"),
            ("🏦", "Branches",          "Branches"),
            ("⏱", "Shifts",            "Shifts"),
            ("📅", "Attendance",        "Attendance"),
            ("📄", "Leave Management",  "Leave"),
            ("🗓", "Holidays",          "Holidays"),
            ("📊", "Reports",           "Reports"),
            ("👤", "Users",             "Users"),
            ("⚙", "Settings",          "Settings"),
            ("📋", "Audit Log",         "AuditLog"),
            ("🖐", "Biometric Import",   "BiometricImport"),
        };

        foreach (var (icon, label, tag) in navItems)
        {
            var btn = CreateNavButton(icon, label, tag);
            btn.Top = y; y += 44;
            _sidebar.Controls.Add(btn);
        }
    }

    private Button CreateNavButton(string icon, string label, string tag)
    {
        var btn = new Button
        {
            Text = $"  {icon}  {label}",
            Width = 220,
            Height = 44,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(200, 200, 200),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = AppTheme.SidebarFont,
            Tag = tag,
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = AppTheme.SidebarHover;

        btn.Click += (s, e) =>
        {
            SetActiveNav(btn);
            switch (tag)
            {
                case "Dashboard":   Navigate<DashboardForm>(label); break;
                case "Employees":   Navigate<EmployeeListForm>(label); break;
                case "Departments": Navigate<DepartmentForm>(label); break;
                case "Designations":Navigate<DesignationForm>(label); break;
                case "Branches":    Navigate<BranchForm>(label); break;
                case "Shifts":      Navigate<ShiftForm>(label); break;
                case "Attendance":  Navigate<AttendanceForm>(label); break;
                case "Leave":       Navigate<LeaveTypeForm>(label); break;
                case "Holidays":    Navigate<HolidayForm>(label); break;
                case "Reports":     Navigate<ReportForm>(label); break;
                case "Users":       Navigate<UserForm>(label); break;
                case "Settings":    Navigate<SettingsForm>(label); break;
                case "AuditLog":       Navigate<AuditLogForm>(label); break;
                case "BiometricImport": OpenBiometricImport(); break;
            }
        };
        return btn;
    }

    private void SetActiveNav(Button btn)
    {
        if (_activeNavBtn != null)
        {
            _activeNavBtn.BackColor = Color.Transparent;
            _activeNavBtn.ForeColor = Color.FromArgb(200, 200, 200);
        }
        btn.BackColor = AppTheme.SidebarActive;
        btn.ForeColor = Color.White;
        _activeNavBtn = btn;
    }

    private void Navigate<T>(string title) where T : Form
    {
        try
        {
            _currentChild?.Hide();
            _currentChild?.Dispose();

            var form = _services.GetRequiredService<T>();
            form.TopLevel = false;
            form.FormBorderStyle = FormBorderStyle.None;
            form.Dock = DockStyle.Fill;
            form.BackColor = AppTheme.FormBg;

            _contentArea.Controls.Clear();
            _contentArea.Controls.Add(form);
            form.Show();
            _currentChild = form;
            _lblPageTitle.Text = title;
            SetStatus($"Viewing: {title}");
        }
        catch (Exception ex)
        {
            GlobalExceptionHandler.HandleUI(ex, nameof(MainForm));
        }
    }

    public void SetStatus(string message) =>
        _lblStatus.Text = $"  {DateTime.Now:HH:mm:ss}  |  {message}";

    private void StartClock()
    {
        _clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _clockTimer.Tick += (s, e) =>
        {
            _lblClock.Text = DateTime.Now.ToString("ddd, dd MMM yyyy  HH:mm:ss");
        };
        _clockTimer.Start();
    }

    private void OpenBiometricImport()
    {
        try
        {
            var svc = _services.GetRequiredService<IBiometricImportService>();
            var form = new BiometricImportForm(svc);
            form.ShowDialog(this);
            SetStatus("Biometric Import closed.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnLogout_Click(object? s, EventArgs e)
    {
        if (MessageBox.Show("Are you sure you want to logout?", "Confirm Logout",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            _clockTimer.Stop();
            AppSession.Clear();
            var login = _services.GetRequiredService<LoginForm>();
            login.Show();
            Close();
        }
    }

    private void ApplyTheme()
    {
        BackColor = AppTheme.FormBg;
        _sidebar.BackColor = AppTheme.SidebarBg;
        _statusBar.BackColor = AppTheme.StatusBarBg;
        _contentArea.BackColor = AppTheme.FormBg;
        if (_currentChild is Form child) child.BackColor = AppTheme.FormBg;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _clockTimer.Stop();
        base.OnFormClosed(e);
    }
}
