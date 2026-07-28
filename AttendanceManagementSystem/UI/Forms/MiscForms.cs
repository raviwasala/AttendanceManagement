using AttendanceManagementSystem.UI.Controls;
using AttendanceManagementSystem.UI.Theme;
using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Models;
using AttendanceSystem.Common.Session;

namespace AttendanceManagementSystem.UI.Forms;

/// <summary>Company settings form.</summary>
public class SettingsForm : Form
{
    private LabeledTextBox _txtCompany = null!;
    private LabeledTextBox _txtAddress = null!;
    private LabeledTextBox _txtPhone = null!;
    private LabeledTextBox _txtEmail = null!;
    private LabeledTextBox _txtWebsite = null!;
    private DateTimePicker _dtpWorkStart = null!;
    private DateTimePicker _dtpWorkEnd = null!;
    private LabeledTextBox _txtWeekend = null!;
    private NumericUpDown _nudLate = null!;

    private readonly ISettingsService _settingsService;
    public SettingsForm(ISettingsService settingsService) { _settingsService = settingsService; Build(); _ = LoadAsync(); }

    private void Build()
    {
        BackColor = AppTheme.FormBg;
        var card = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.CardBg, Padding = new Padding(24) };

        var lblTitle = new Label { Text = "⚙  Company & System Settings", Font = AppTheme.TitleFont, ForeColor = AppTheme.PrimaryColor, AutoSize = true, Location = new Point(24, 16) };

        int x1 = 24, x2 = 380, y = 56, gap = 80;

        _txtCompany = new LabeledTextBox { LabelText = "Company Name *", Location = new Point(x1, y), Width = 320 };
        _txtPhone   = new LabeledTextBox { LabelText = "Phone",          Location = new Point(x2, y), Width = 260 };
        y += gap;
        _txtEmail   = new LabeledTextBox { LabelText = "Email",          Location = new Point(x1, y), Width = 320 };
        _txtWebsite = new LabeledTextBox { LabelText = "Website",        Location = new Point(x2, y), Width = 260 };
        y += gap;
        _txtAddress = new LabeledTextBox { LabelText = "Address",        Location = new Point(x1, y), Width = 616 };
        y += gap;

        var lblWS = new Label { Text = "Work Start Time", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(x1, y), AutoSize = true };
        _dtpWorkStart = new DateTimePicker { Location = new Point(x1, y + 18), Width = 160, Format = DateTimePickerFormat.Time, ShowUpDown = true, Font = AppTheme.BodyFont };
        var lblWE = new Label { Text = "Work End Time", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(x2, y), AutoSize = true };
        _dtpWorkEnd = new DateTimePicker { Location = new Point(x2, y + 18), Width = 160, Format = DateTimePickerFormat.Time, ShowUpDown = true, Font = AppTheme.BodyFont };
        y += gap;

        _txtWeekend = new LabeledTextBox { LabelText = "Weekend Days (comma separated)", Location = new Point(x1, y), Width = 320 };
        var lblLate = new Label { Text = "Max Late Minutes", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(x2, y), AutoSize = true };
        _nudLate = new NumericUpDown { Location = new Point(x2, y + 18), Width = 80, Minimum = 0, Maximum = 120, Font = AppTheme.BodyFont };
        y += gap;

        var btnSave = new AppButton { Text = "💾 Save Settings", Width = 140, Location = new Point(x1, y) };
        btnSave.Click += async (s, e) => await SaveAsync();

        card.Controls.AddRange([lblTitle, _txtCompany, _txtPhone, _txtEmail, _txtWebsite,
            _txtAddress, lblWS, _dtpWorkStart, lblWE, _dtpWorkEnd, _txtWeekend, lblLate, _nudLate, btnSave]);
        Controls.Add(card);
    }

    private async Task LoadAsync()
    {
        var r = await _settingsService.GetAsync();
        if (!r.IsSuccess) return;
        var s = r.Data!;
        _txtCompany.Value = s.CompanyName;
        _txtPhone.Value   = s.Phone ?? "";
        _txtEmail.Value   = s.Email ?? "";
        _txtWebsite.Value = s.Website ?? "";
        _txtAddress.Value = s.Address ?? "";
        _dtpWorkStart.Value = DateTime.Today.Add(s.WorkStartTime);
        _dtpWorkEnd.Value   = DateTime.Today.Add(s.WorkEndTime);
        _txtWeekend.Value = s.WeekendDays;
        _nudLate.Value = s.MaxLateMinutes;
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_txtCompany.Value)) { _txtCompany.ShowError("Required"); return; }
        var dto = new CompanySettingsDto
        {
            CompanyName  = _txtCompany.Value.Trim(),
            Phone        = _txtPhone.Value.Trim(),
            Email        = _txtEmail.Value.Trim(),
            Website      = _txtWebsite.Value.Trim(),
            Address      = _txtAddress.Value.Trim(),
            WorkStartTime = _dtpWorkStart.Value.TimeOfDay,
            WorkEndTime   = _dtpWorkEnd.Value.TimeOfDay,
            WeekendDays   = _txtWeekend.Value.Trim(),
            MaxLateMinutes = (int)_nudLate.Value
        };
        var r = await _settingsService.SaveAsync(dto, AppSession.UserId);
        if (r.IsSuccess) MessageBox.Show("Settings saved successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

/// <summary>Audit log viewer.</summary>
public class AuditLogForm : Form
{
    private AppDataGrid _grid = null!;
    private ComboBox _cmbModule = null!;

    private readonly IAuditService _auditService;
    public AuditLogForm(IAuditService auditService) { _auditService = auditService; Build(); _ = LoadAsync(); }

    private void Build()
    {
        BackColor = AppTheme.FormBg;
        var toolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = AppTheme.CardBg, Padding = new Padding(8, 8, 8, 0) };

        var lblMod = new Label { Text = "Module:", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(8, 14), AutoSize = true };
        _cmbModule = new ComboBox { Location = new Point(58, 8), Width = 160, Font = AppTheme.BodyFont, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbModule.Items.AddRange(["All", "Dashboard", "Employees", "Attendance", "Leave", "Users", "Settings"]);
        _cmbModule.SelectedIndex = 0;
        var btnLoad = new AppButton { Text = "🔍 Load", Width = 80, Location = new Point(228, 8) };
        btnLoad.Click += async (s, e) => await LoadAsync();
        toolbar.Controls.AddRange([lblMod, _cmbModule, btnLoad]);

        _grid = new AppDataGrid { Dock = DockStyle.Fill };
        _grid.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "Date/Time",  DataPropertyName = "CreatedAtDisplay", Width = 140 },
            new DataGridViewTextBoxColumn { HeaderText = "User",       DataPropertyName = "Username",         Width = 110 },
            new DataGridViewTextBoxColumn { HeaderText = "Module",     DataPropertyName = "Module",           Width = 110 },
            new DataGridViewTextBoxColumn { HeaderText = "Action",     DataPropertyName = "Action",           Width = 100 },
            new DataGridViewTextBoxColumn { HeaderText = "Entity",     DataPropertyName = "EntityName",       Width = 100 },
            new DataGridViewTextBoxColumn { HeaderText = "Entity ID",  DataPropertyName = "EntityId",         Width = 80  },
            new DataGridViewTextBoxColumn { HeaderText = "Old Values", DataPropertyName = "OldValues" },
            new DataGridViewTextBoxColumn { HeaderText = "New Values", DataPropertyName = "NewValues" }
        );
        Controls.Add(_grid); Controls.Add(toolbar);
    }

    private async Task LoadAsync()
    {
        var module = _cmbModule.SelectedItem?.ToString();
        Result<IEnumerable<AuditLogDto>> r;
        if (module == "All" || string.IsNullOrWhiteSpace(module))
            r = await _auditService.GetRecentAsync(500);
        else
            r = await _auditService.GetByModuleAsync(module);

        if (r.IsSuccess) { _grid.DataSource = null; _grid.DataSource = r.Data!.ToList(); }
    }
}

/// <summary>Change password form.</summary>
public class ChangePasswordForm : Form
{
    private LabeledTextBox _txtCurrent = null!;
    private LabeledTextBox _txtNew = null!;
    private LabeledTextBox _txtConfirm = null!;
    private readonly IAuthService _authService;

    public ChangePasswordForm(IAuthService authService) { _authService = authService; Build(); }

    private void Build()
    {
        Text = "Change Password"; Size = new Size(400, 340);
        StartPosition = FormStartPosition.CenterParent; BackColor = AppTheme.CardBg;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;

        var lblTitle = new Label { Text = "Change Password", Font = AppTheme.HeaderFont, ForeColor = AppTheme.PrimaryColor, AutoSize = true, Location = new Point(20, 16) };
        _txtCurrent = new LabeledTextBox { LabelText = "Current Password", Location = new Point(20, 52), Width = 340 };
        _txtNew     = new LabeledTextBox { LabelText = "New Password",     Location = new Point(20, 132), Width = 340 };
        _txtConfirm = new LabeledTextBox { LabelText = "Confirm New",      Location = new Point(20, 212), Width = 340 };
        _txtCurrent.IsPassword = true; _txtNew.IsPassword = true; _txtConfirm.IsPassword = true;

        var btnSave   = new AppButton { Text = "💾 Change", Width = 110, Location = new Point(110, 288) };
        var btnCancel = new AppButton { Text = "Cancel",    Width = 80,  Location = new Point(230, 288) };
        btnCancel.SetSecondary();
        btnSave.Click   += async (s, e) => await SaveAsync();
        btnCancel.Click += (s, e) => Close();
        Controls.AddRange([lblTitle, _txtCurrent, _txtNew, _txtConfirm, btnSave, btnCancel]);
    }

    private async Task SaveAsync()
    {
        _txtCurrent.ClearError(); _txtNew.ClearError(); _txtConfirm.ClearError();
        if (string.IsNullOrWhiteSpace(_txtCurrent.Value)) { _txtCurrent.ShowError("Required"); return; }
        if (_txtNew.Value != _txtConfirm.Value) { _txtConfirm.ShowError("Passwords do not match."); return; }
        var r = await _authService.ChangePasswordAsync(AppSession.UserId, new(
            _txtCurrent.Value, _txtNew.Value, _txtConfirm.Value));
        if (r.IsSuccess)
        {
            MessageBox.Show("Password changed successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
