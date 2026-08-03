using AttendanceManagementSystem.UI.Controls;
using AttendanceManagementSystem.UI.Theme;
using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceManagementSystem.Session;
using AttendanceSystem.Domain.Enums;

namespace AttendanceManagementSystem.UI.Forms;

/// <summary>Manual attendance check-in/out, edit and history.</summary>
public class AttendanceForm : Form
{
    private AppDataGrid _grid = null!;
    private DateTimePicker _dtpDate = null!;
    private TabControl _tabs = null!;
    private AppDataGrid _gridHistory = null!;

    private readonly IAttendanceService _attendanceService;
    private readonly IEmployeeService _empService;

    public AttendanceForm(IAttendanceService attendanceService, IEmployeeService empService)
    {
        _attendanceService = attendanceService; _empService = empService;
        Build(); _ = LoadTodayAsync();
    }

    private void Build()
    {
        BackColor = AppTheme.FormBg;
        _tabs = new TabControl { Dock = DockStyle.Fill, Font = AppTheme.BodyFont };

        // ── Today ─────────────────────────────────────────────────────────────
        var tabToday = new TabPage("Today's Attendance") { BackColor = AppTheme.FormBg };
        var toolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = AppTheme.CardBg, Padding = new Padding(8, 8, 8, 0) };
        var btnCheckIn  = new AppButton { Text = "✅ Check In",  Width = 110, Location = new Point(8,   8) };
        var btnCheckOut = new AppButton { Text = "🔴 Check Out", Width = 110, Location = new Point(124, 8) };
        var btnEdit     = new AppButton { Text = "✏ Edit",       Width = 90,  Location = new Point(240, 8) };
        var btnDelete   = new AppButton { Text = "🗑 Delete",     Width = 90,  Location = new Point(336, 8) };
        var btnRefresh  = new AppButton { Text = "↻ Refresh",    Width = 90,  Location = new Point(432, 8) };
        btnCheckOut.SetSecondary(); btnEdit.SetSecondary(); btnDelete.SetDanger(); btnRefresh.SetSecondary();
        btnCheckIn.Click  += async (s, e) => await DoCheckIn();
        btnCheckOut.Click += async (s, e) => await DoCheckOut();
        btnEdit.Click     += async (s, e) => await EditAttendance();
        btnDelete.Click   += async (s, e) => await DeleteAttendance();
        btnRefresh.Click  += async (s, e) => await LoadTodayAsync();
        toolbar.Controls.AddRange([btnCheckIn, btnCheckOut, btnEdit, btnDelete, btnRefresh]);

        _grid = new AppDataGrid { Dock = DockStyle.Fill };
        BuildTodayColumns(_grid);
        tabToday.Controls.Add(_grid); tabToday.Controls.Add(toolbar);

        // ── History ───────────────────────────────────────────────────────────
        var tabHistory = new TabPage("Attendance History") { BackColor = AppTheme.FormBg };
        var hToolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = AppTheme.CardBg, Padding = new Padding(8, 8, 8, 0) };

        var lblFrom = new Label { Text = "From:", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(8, 12), AutoSize = true };
        var dtpFrom = new DateTimePicker { Location = new Point(44, 8), Width = 130, Format = DateTimePickerFormat.Short, Font = AppTheme.BodyFont, Value = DateTime.Today.AddDays(-30) };
        var lblTo   = new Label { Text = "To:", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(184, 12), AutoSize = true };
        var dtpTo   = new DateTimePicker { Location = new Point(206, 8), Width = 130, Format = DateTimePickerFormat.Short, Font = AppTheme.BodyFont, Value = DateTime.Today };
        var btnLoad = new AppButton { Text = "🔍 Load", Width = 80, Location = new Point(346, 8) };
        btnLoad.Click += async (s, e) => await LoadHistoryAsync(dtpFrom.Value, dtpTo.Value);
        hToolbar.Controls.AddRange([lblFrom, dtpFrom, lblTo, dtpTo, btnLoad]);

        _gridHistory = new AppDataGrid { Dock = DockStyle.Fill };
        BuildTodayColumns(_gridHistory);
        tabHistory.Controls.Add(_gridHistory); tabHistory.Controls.Add(hToolbar);

        _tabs.TabPages.AddRange([tabToday, tabHistory]);
        Controls.Add(_tabs);
    }

    private void BuildTodayColumns(AppDataGrid grid)
    {
        grid.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "Date",         DataPropertyName = "AttendanceDate",  Width = 100 },
            new DataGridViewTextBoxColumn { HeaderText = "Code",         DataPropertyName = "EmployeeCode",    Width = 90  },
            new DataGridViewTextBoxColumn { HeaderText = "Name",         DataPropertyName = "EmployeeName"               },
            new DataGridViewTextBoxColumn { HeaderText = "Dept",         DataPropertyName = "Department",      Width = 120 },
            new DataGridViewTextBoxColumn { HeaderText = "Check In",     DataPropertyName = "CheckInDisplay",  Width = 90  },
            new DataGridViewTextBoxColumn { HeaderText = "Check Out",    DataPropertyName = "CheckOutDisplay", Width = 90  },
            new DataGridViewTextBoxColumn { HeaderText = "Status",       DataPropertyName = "StatusDisplay",   Width = 90  },
            new DataGridViewTextBoxColumn { HeaderText = "Late (min)",   DataPropertyName = "LateMinutes",     Width = 90  },
            new DataGridViewTextBoxColumn { HeaderText = "Hours",        DataPropertyName = "WorkingHours",    Width = 70  },
            new DataGridViewTextBoxColumn { HeaderText = "Remarks",      DataPropertyName = "Remarks"                     }
        );
    }

    private async Task LoadTodayAsync()
    {
        var r = await _attendanceService.GetTodayAsync();
        if (!r.IsSuccess) return;
        _grid.DataSource = null;
        _grid.DataSource = r.Data!.ToList();
        ColorizeStatusRows(_grid);
    }

    private async Task LoadHistoryAsync(DateTime from, DateTime to)
    {
        if (DesktopSession.EmployeeId.HasValue)
        {
            var r = await _attendanceService.GetByEmployeeAndDateRangeAsync(DesktopSession.EmployeeId.Value, from, to);
            if (r.IsSuccess) { _gridHistory.DataSource = null; _gridHistory.DataSource = r.Data!.ToList(); ColorizeStatusRows(_gridHistory); }
        }
    }

    private void ColorizeStatusRows(AppDataGrid grid)
    {
        foreach (DataGridViewRow row in grid.Rows)
        {
            var status = row.Cells["StatusDisplay"]?.Value?.ToString();
            row.DefaultCellStyle.ForeColor = status switch
            {
                "Late"    => AppTheme.WarningColor,
                "Absent"  => AppTheme.DangerColor,
                "Present" => AppTheme.SuccessColor,
                _ => AppTheme.BodyText
            };
        }
    }

    private async Task DoCheckIn()
    {
        var employees = await _empService.GetAllAsync();
        if (!employees.IsSuccess) return;
        using var dlg = new CheckInDialog(employees.Data!.ToList());
        if (dlg.ShowDialog() != DialogResult.OK) return;
        var r = await _attendanceService.CheckInAsync(dlg.GetDto());
        if (r.IsSuccess) { await LoadTodayAsync(); MessageBox.Show("Check-in recorded.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private async Task DoCheckOut()
    {
        if (_grid.SelectedRows.Count == 0) return;
        var selected = (AttendanceLogDto)_grid.SelectedRows[0].DataBoundItem;
        if (selected.CheckOut != null) { MessageBox.Show("Already checked out.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        var r = await _attendanceService.CheckOutAsync(new CheckOutDto { AttendanceLogId = selected.Id, CheckOutTime = DateTime.Now });
        if (r.IsSuccess) await LoadTodayAsync();
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private async Task EditAttendance()
    {
        if (_grid.SelectedRows.Count == 0) return;
        var selected = (AttendanceLogDto)_grid.SelectedRows[0].DataBoundItem;
        using var dlg = new EditAttendanceDialog(selected);
        if (dlg.ShowDialog() != DialogResult.OK) return;
        var r = await _attendanceService.EditAsync(dlg.GetDto(), DesktopSession.UserId);
        if (r.IsSuccess) await LoadTodayAsync();
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private async Task DeleteAttendance()
    {
        if (_grid.SelectedRows.Count == 0) return;
        var selected = (AttendanceLogDto)_grid.SelectedRows[0].DataBoundItem;
        if (MessageBox.Show("Delete this attendance record?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        var r = await _attendanceService.DeleteAsync(selected.Id, DesktopSession.UserId);
        if (r.IsSuccess) await LoadTodayAsync();
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

// Stub references needed for DI registrations
public class AttendanceHistoryForm : Form
{
    public AttendanceHistoryForm() { BackColor = AppTheme.FormBg; }
}

internal class CheckInDialog : Form
{
    private readonly ComboBox _cmbEmp;
    private readonly DateTimePicker _dtpTime;
    private readonly LabeledTextBox _txtRemarks;
    private readonly List<EmployeeListItemDto> _employees;

    public CheckInDialog(List<EmployeeListItemDto> employees)
    {
        _employees = employees;
        Text = "Manual Check In"; Size = new Size(380, 280);
        StartPosition = FormStartPosition.CenterParent; BackColor = AppTheme.CardBg;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;

        var lblEmp = new Label { Text = "Employee *", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(20, 20), AutoSize = true };
        _cmbEmp = new ComboBox { Location = new Point(20, 38), Width = 320, Font = AppTheme.BodyFont, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbEmp.DataSource = employees; _cmbEmp.DisplayMember = "FullName"; _cmbEmp.ValueMember = "Id";

        var lblTime = new Label { Text = "Check In Time *", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(20, 74), AutoSize = true };
        _dtpTime = new DateTimePicker { Location = new Point(20, 92), Width = 220, Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MMM-yyyy hh:mm tt", ShowUpDown = true, Font = AppTheme.BodyFont, Value = DateTime.Now };

        _txtRemarks = new LabeledTextBox { LabelText = "Remarks", Location = new Point(20, 130), Width = 320 };
        var btnSave   = new AppButton { Text = "✅ Check In", Width = 110, Location = new Point(100, 210) };
        var btnCancel = new AppButton { Text = "Cancel",      Width = 80,  Location = new Point(220, 210) };
        btnCancel.SetSecondary();
        btnSave.Click   += (s, e) => DialogResult = DialogResult.OK;
        btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;
        Controls.AddRange([lblEmp, _cmbEmp, lblTime, _dtpTime, _txtRemarks, btnSave, btnCancel]);
    }

    public CheckInDto GetDto() => new() { EmployeeId = (int)_cmbEmp.SelectedValue!, CheckInTime = _dtpTime.Value, Remarks = _txtRemarks.Value };
}

internal class EditAttendanceDialog : Form
{
    private readonly DateTimePicker _dtpCheckIn;
    private readonly DateTimePicker _dtpCheckOut;
    private readonly ComboBox _cmbStatus;
    private readonly LabeledTextBox _txtRemarks;
    private readonly AttendanceLogDto _existing;

    public EditAttendanceDialog(AttendanceLogDto existing)
    {
        _existing = existing;
        Text = "Edit Attendance"; Size = new Size(400, 360);
        StartPosition = FormStartPosition.CenterParent; BackColor = AppTheme.CardBg;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;

        var lblIn = new Label { Text = "Check In", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(20, 20), AutoSize = true };
        _dtpCheckIn = new DateTimePicker { Location = new Point(20, 38), Width = 220, Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MMM-yyyy hh:mm tt", ShowUpDown = true, Font = AppTheme.BodyFont };

        var lblOut = new Label { Text = "Check Out", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(20, 78), AutoSize = true };
        _dtpCheckOut = new DateTimePicker { Location = new Point(20, 96), Width = 220, Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MMM-yyyy hh:mm tt", ShowUpDown = true, Font = AppTheme.BodyFont };

        var lblStatus = new Label { Text = "Status", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(20, 136), AutoSize = true };
        _cmbStatus = new ComboBox { Location = new Point(20, 154), Width = 200, Font = AppTheme.BodyFont, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var s in Enum.GetValues<AttendanceStatus>()) _cmbStatus.Items.Add(s);

        _txtRemarks = new LabeledTextBox { LabelText = "Remarks", Location = new Point(20, 190), Width = 340 };

        var btnSave   = new AppButton { Text = "💾 Save", Width = 100, Location = new Point(120, 280) };
        var btnCancel = new AppButton { Text = "Cancel",  Width = 80,  Location = new Point(230, 280) };
        btnCancel.SetSecondary();
        btnSave.Click   += (s, e) => DialogResult = DialogResult.OK;
        btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;

        _dtpCheckIn.Value  = existing.CheckIn  ?? DateTime.Now;
        _dtpCheckOut.Value = existing.CheckOut ?? DateTime.Now;
        _cmbStatus.SelectedItem = existing.Status;
        _txtRemarks.Value = existing.Remarks ?? "";

        Controls.AddRange([lblIn, _dtpCheckIn, lblOut, _dtpCheckOut, lblStatus, _cmbStatus, _txtRemarks, btnSave, btnCancel]);
    }

    public EditAttendanceDto GetDto() => new()
    {
        Id = _existing.Id,
        CheckIn  = _dtpCheckIn.Value,
        CheckOut = _dtpCheckOut.Value,
        Status   = (AttendanceStatus)_cmbStatus.SelectedItem!,
        Remarks  = _txtRemarks.Value
    };
}
