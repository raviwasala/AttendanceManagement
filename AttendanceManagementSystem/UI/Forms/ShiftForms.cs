using AttendanceManagementSystem.UI.Controls;
using AttendanceManagementSystem.UI.Theme;
using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Session;

namespace AttendanceManagementSystem.UI.Forms;

/// <summary>Shift definition management + employee shift assignment.</summary>
public class ShiftForm : Form
{
    private AppDataGrid _gridShifts = null!;
    private AppDataGrid _gridAssigned = null!;
    private TabControl _tabs = null!;

    private readonly IShiftService _shiftService;
    private readonly IEmployeeService _empService;

    public ShiftForm(IShiftService shiftService, IEmployeeService empService)
    {
        _shiftService = shiftService; _empService = empService;
        Build(); _ = LoadAsync();
    }

    private void Build()
    {
        BackColor = AppTheme.FormBg;
        _tabs = new TabControl { Dock = DockStyle.Fill, Font = AppTheme.BodyFont };

        // ── Tab 1: Shifts ─────────────────────────────────────────────────────
        var tabShifts = new TabPage("Shift Definitions") { BackColor = AppTheme.FormBg };
        var toolbar1 = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = AppTheme.CardBg, Padding = new Padding(8, 8, 8, 0) };
        var btnAdd    = new AppButton { Text = "➕ Add",    Width = 90,  Location = new Point(8,   8) };
        var btnEdit   = new AppButton { Text = "✏ Edit",   Width = 90,  Location = new Point(104, 8) };
        var btnDelete = new AppButton { Text = "🗑 Delete", Width = 90,  Location = new Point(200, 8) };
        btnEdit.SetSecondary(); btnDelete.SetDanger();
        btnAdd.Click    += async (s, e) => await OpenShiftDialog(null);
        btnEdit.Click   += async (s, e) => await EditShift();
        btnDelete.Click += async (s, e) => await DeleteShift();
        toolbar1.Controls.AddRange([btnAdd, btnEdit, btnDelete]);

        _gridShifts = new AppDataGrid { Dock = DockStyle.Fill };
        _gridShifts.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "#",            DataPropertyName = "Id",              Width = 50 },
            new DataGridViewTextBoxColumn { HeaderText = "Shift Name",   DataPropertyName = "Name" },
            new DataGridViewTextBoxColumn { HeaderText = "Start",        DataPropertyName = "StartTimeDisplay",Width = 90 },
            new DataGridViewTextBoxColumn { HeaderText = "End",          DataPropertyName = "EndTimeDisplay",  Width = 90 },
            new DataGridViewTextBoxColumn { HeaderText = "Grace (min)",  DataPropertyName = "GraceMinutes",    Width = 100 },
            new DataGridViewTextBoxColumn { HeaderText = "Weekly Off",   DataPropertyName = "WeeklyOffDays" },
            new DataGridViewCheckBoxColumn { HeaderText = "Active",      DataPropertyName = "IsActive",        Width = 70 }
        );
        tabShifts.Controls.Add(_gridShifts); tabShifts.Controls.Add(toolbar1);

        // ── Tab 2: Assigned Shifts ────────────────────────────────────────────
        var tabAssigned = new TabPage("Employee Shifts") { BackColor = AppTheme.FormBg };
        var toolbar2 = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = AppTheme.CardBg, Padding = new Padding(8, 8, 8, 0) };
        var btnAssign = new AppButton { Text = "➕ Assign Shift", Width = 130, Location = new Point(8, 8) };
        btnAssign.Click += async (s, e) => await AssignShift();
        toolbar2.Controls.Add(btnAssign);

        _gridAssigned = new AppDataGrid { Dock = DockStyle.Fill };
        _gridAssigned.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "Code",         DataPropertyName = "EmployeeCode",    Width = 100 },
            new DataGridViewTextBoxColumn { HeaderText = "Employee",     DataPropertyName = "EmployeeName" },
            new DataGridViewTextBoxColumn { HeaderText = "Shift",        DataPropertyName = "ShiftName" },
            new DataGridViewTextBoxColumn { HeaderText = "Start",        DataPropertyName = "StartTimeDisplay",Width = 90 },
            new DataGridViewTextBoxColumn { HeaderText = "End",          DataPropertyName = "EndTimeDisplay",  Width = 90 },
            new DataGridViewTextBoxColumn { HeaderText = "Effective From",DataPropertyName = "EffectiveFrom",  Width = 110 },
            new DataGridViewTextBoxColumn { HeaderText = "Effective To", DataPropertyName = "EffectiveTo",    Width = 110 }
        );
        tabAssigned.Controls.Add(_gridAssigned); tabAssigned.Controls.Add(toolbar2);

        _tabs.TabPages.AddRange([tabShifts, tabAssigned]);
        Controls.Add(_tabs);
        _tabs.SelectedIndexChanged += async (s, e) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var shifts = await _shiftService.GetAllAsync();
        if (shifts.IsSuccess) { _gridShifts.DataSource = null; _gridShifts.DataSource = shifts.Data!.ToList(); }

        if (_tabs.SelectedIndex == 1)
        {
            var assigned = await _shiftService.GetEmployeeShiftsAsync();
            if (assigned.IsSuccess) { _gridAssigned.DataSource = null; _gridAssigned.DataSource = assigned.Data!.ToList(); }
        }
    }

    private async Task OpenShiftDialog(ShiftDto? existing)
    {
        using var dlg = new ShiftEditDialog(existing);
        if (dlg.ShowDialog() != DialogResult.OK) return;
        var r = await _shiftService.SaveAsync(dlg.GetDto());
        if (r.IsSuccess) await LoadAsync();
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private async Task EditShift()
    {
        if (_gridShifts.SelectedRows.Count == 0) return;
        var s = (ShiftDto)_gridShifts.SelectedRows[0].DataBoundItem;
        var r = await _shiftService.GetByIdAsync(s.Id);
        if (r.IsSuccess) await OpenShiftDialog(r.Data!);
    }

    private async Task DeleteShift()
    {
        if (_gridShifts.SelectedRows.Count == 0) return;
        var s = (ShiftDto)_gridShifts.SelectedRows[0].DataBoundItem;
        if (MessageBox.Show($"Delete shift '{s.Name}'?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        var r = await _shiftService.DeleteAsync(s.Id, AppSession.UserId);
        if (r.IsSuccess) await LoadAsync();
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private async Task AssignShift()
    {
        var employees = await _empService.GetAllAsync();
        var shifts    = await _shiftService.GetAllAsync();
        if (!employees.IsSuccess || !shifts.IsSuccess) return;

        using var dlg = new AssignShiftForm(employees.Data!.ToList(), shifts.Data!.ToList(), _shiftService);
        dlg.ShowDialog();
        await LoadAsync();
    }
}

internal class ShiftEditDialog : Form
{
    private readonly LabeledTextBox _txtName;
    private readonly DateTimePicker _dtpStart;
    private readonly DateTimePicker _dtpEnd;
    private readonly NumericUpDown _nudGrace;
    private readonly CheckedListBox _clbWeeklyOff;
    private readonly CheckBox _chkActive;
    private readonly ShiftDto? _existing;

    public ShiftEditDialog(ShiftDto? existing)
    {
        _existing = existing;
        Text = existing == null ? "Add Shift" : "Edit Shift";
        Size = new Size(440, 420); StartPosition = FormStartPosition.CenterParent;
        BackColor = AppTheme.CardBg; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;

        _txtName  = new LabeledTextBox { LabelText = "Shift Name *", Location = new Point(20, 20), Width = 380 };

        var lblStart = new Label { Text = "Start Time", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(20, 100), AutoSize = true };
        _dtpStart = new DateTimePicker { Location = new Point(20, 118), Width = 160, Format = DateTimePickerFormat.Time, ShowUpDown = true, Font = AppTheme.BodyFont };

        var lblEnd = new Label { Text = "End Time", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(200, 100), AutoSize = true };
        _dtpEnd   = new DateTimePicker { Location = new Point(200, 118), Width = 160, Format = DateTimePickerFormat.Time, ShowUpDown = true, Font = AppTheme.BodyFont };

        var lblGrace = new Label { Text = "Grace (minutes)", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(20, 152), AutoSize = true };
        _nudGrace = new NumericUpDown { Location = new Point(20, 170), Width = 80, Minimum = 0, Maximum = 60, Font = AppTheme.BodyFont };

        var lblOff = new Label { Text = "Weekly Off Days", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(20, 204), AutoSize = true };
        _clbWeeklyOff = new CheckedListBox { Location = new Point(20, 222), Width = 380, Height = 90, Font = AppTheme.BodyFont, CheckOnClick = true };
        foreach (var day in Enum.GetNames<DayOfWeek>()) _clbWeeklyOff.Items.Add(day);

        _chkActive = new CheckBox { Text = "Active", Location = new Point(20, 322), Checked = true, Font = AppTheme.BodyFont, ForeColor = AppTheme.BodyText };
        var btnSave   = new AppButton { Text = "💾 Save", Width = 100, Location = new Point(140, 354) };
        var btnCancel = new AppButton { Text = "Cancel",  Width = 80,  Location = new Point(250, 354) };
        btnCancel.SetSecondary();
        btnSave.Click   += (s, e) => { if (!string.IsNullOrWhiteSpace(_txtName.Value)) DialogResult = DialogResult.OK; else _txtName.ShowError("Required"); };
        btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;

        if (existing != null)
        {
            _txtName.Value = existing.Name;
            _dtpStart.Value = DateTime.Today.Add(existing.StartTime);
            _dtpEnd.Value   = DateTime.Today.Add(existing.EndTime);
            _nudGrace.Value = existing.GraceMinutes;
            var offDays = existing.WeeklyOffDays.Split(',').Select(d => d.Trim());
            for (int i = 0; i < _clbWeeklyOff.Items.Count; i++)
                if (offDays.Contains(_clbWeeklyOff.Items[i]!.ToString()))
                    _clbWeeklyOff.SetItemChecked(i, true);
            _chkActive.Checked = existing.IsActive;
        }
        else
        {
            _clbWeeklyOff.SetItemChecked((int)DayOfWeek.Saturday, true);
            _clbWeeklyOff.SetItemChecked((int)DayOfWeek.Sunday, true);
        }

        Controls.AddRange([_txtName, lblStart, _dtpStart, lblEnd, _dtpEnd,
            lblGrace, _nudGrace, lblOff, _clbWeeklyOff, _chkActive, btnSave, btnCancel]);
    }

    public SaveShiftDto GetDto()
    {
        var offDays = string.Join(",", _clbWeeklyOff.CheckedItems.Cast<object>().Select(o => o.ToString()!));
        return new SaveShiftDto
        {
            Id = _existing?.Id ?? 0, Name = _txtName.Value.Trim(),
            StartTime = _dtpStart.Value.TimeOfDay, EndTime = _dtpEnd.Value.TimeOfDay,
            GraceMinutes = (int)_nudGrace.Value, WeeklyOffDays = offDays, IsActive = _chkActive.Checked
        };
    }
}

/// <summary>Assign shift to an employee.</summary>
public class AssignShiftForm : Form
{
    private ComboBox _cmbEmployee = null!;
    private ComboBox _cmbShift = null!;
    private DateTimePicker _dtpFrom = null!;
    private DateTimePicker _dtpTo = null!;
    private CheckBox _chkNoEnd = null!;

    private readonly List<EmployeeListItemDto> _employees;
    private readonly List<ShiftDto> _shifts;
    private readonly IShiftService _shiftService;

    public AssignShiftForm(List<EmployeeListItemDto> employees, List<ShiftDto> shifts, IShiftService shiftService)
    {
        _employees = employees; _shifts = shifts; _shiftService = shiftService;
        Build();
    }

    private void Build()
    {
        Text = "Assign Shift to Employee";
        Size = new Size(420, 340); StartPosition = FormStartPosition.CenterParent;
        BackColor = AppTheme.CardBg; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;

        var lblEmp = new Label { Text = "Employee *", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(20, 20), AutoSize = true };
        _cmbEmployee = new ComboBox { Location = new Point(20, 38), Width = 360, Font = AppTheme.BodyFont, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbEmployee.DataSource = _employees; _cmbEmployee.DisplayMember = "FullName"; _cmbEmployee.ValueMember = "Id";

        var lblShift = new Label { Text = "Shift *", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(20, 74), AutoSize = true };
        _cmbShift = new ComboBox { Location = new Point(20, 92), Width = 360, Font = AppTheme.BodyFont, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbShift.DataSource = _shifts; _cmbShift.DisplayMember = "Name"; _cmbShift.ValueMember = "Id";

        var lblFrom = new Label { Text = "Effective From *", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(20, 128), AutoSize = true };
        _dtpFrom = new DateTimePicker { Location = new Point(20, 146), Width = 160, Format = DateTimePickerFormat.Short, Font = AppTheme.BodyFont };

        var lblTo = new Label { Text = "Effective To", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(200, 128), AutoSize = true };
        _dtpTo = new DateTimePicker { Location = new Point(200, 146), Width = 160, Format = DateTimePickerFormat.Short, Font = AppTheme.BodyFont };

        _chkNoEnd = new CheckBox { Text = "No end date", Location = new Point(20, 180), Font = AppTheme.BodyFont, ForeColor = AppTheme.BodyText, Checked = true };
        _chkNoEnd.CheckedChanged += (s, e) => _dtpTo.Enabled = !_chkNoEnd.Checked;
        _dtpTo.Enabled = false;

        var btnSave   = new AppButton { Text = "💾 Assign", Width = 100, Location = new Point(120, 220) };
        var btnCancel = new AppButton { Text = "Cancel",    Width = 80,  Location = new Point(230, 220) };
        btnCancel.SetSecondary();
        btnSave.Click   += async (s, e) => await SaveAsync();
        btnCancel.Click += (s, e) => Close();

        Controls.AddRange([lblEmp, _cmbEmployee, lblShift, _cmbShift, lblFrom, _dtpFrom, lblTo, _dtpTo, _chkNoEnd, btnSave, btnCancel]);
    }

    private async Task SaveAsync()
    {
        var dto = new AssignShiftDto
        {
            EmployeeId    = (int)_cmbEmployee.SelectedValue!,
            ShiftId       = (int)_cmbShift.SelectedValue!,
            EffectiveFrom = _dtpFrom.Value,
            EffectiveTo   = _chkNoEnd.Checked ? null : _dtpTo.Value
        };
        var r = await _shiftService.AssignShiftAsync(dto);
        if (r.IsSuccess) { MessageBox.Show("Shift assigned successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information); Close(); }
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
