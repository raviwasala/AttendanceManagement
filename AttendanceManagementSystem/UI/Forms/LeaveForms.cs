using AttendanceManagementSystem.UI.Controls;
using AttendanceManagementSystem.UI.Theme;
using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Session;

namespace AttendanceManagementSystem.UI.Forms;

/// <summary>Leave types, application, approval and balance.</summary>
public class LeaveTypeForm : Form
{
    private TabControl _tabs = null!;
    private AppDataGrid _gridTypes = null!;
    private AppDataGrid _gridRequests = null!;
    private AppDataGrid _gridBalance = null!;

    private readonly ILeaveService _leaveService;
    private readonly IEmployeeService _empService;

    public LeaveTypeForm(ILeaveService leaveService, IEmployeeService empService)
    {
        _leaveService = leaveService; _empService = empService;
        Build(); _ = LoadAsync();
    }

    private void Build()
    {
        BackColor = AppTheme.FormBg;
        _tabs = new TabControl { Dock = DockStyle.Fill, Font = AppTheme.BodyFont };

        // ── Leave Types ───────────────────────────────────────────────────────
        var tabTypes = new TabPage("Leave Types") { BackColor = AppTheme.FormBg };
        var t1 = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = AppTheme.CardBg, Padding = new Padding(8, 8, 8, 0) };
        var btnAdd    = new AppButton { Text = "➕ Add",    Width = 90,  Location = new Point(8,   8) };
        var btnEdit   = new AppButton { Text = "✏ Edit",   Width = 90,  Location = new Point(104, 8) };
        var btnDelete = new AppButton { Text = "🗑 Delete", Width = 90,  Location = new Point(200, 8) };
        btnEdit.SetSecondary(); btnDelete.SetDanger();
        btnAdd.Click    += async (s, e) => await OpenTypeDialog(null);
        btnEdit.Click   += async (s, e) => await EditType();
        btnDelete.Click += async (s, e) => await DeleteType();
        t1.Controls.AddRange([btnAdd, btnEdit, btnDelete]);
        _gridTypes = new AppDataGrid { Dock = DockStyle.Fill };
        _gridTypes.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "#",         DataPropertyName = "Id",        Width = 50 },
            new DataGridViewTextBoxColumn { HeaderText = "Type",      DataPropertyName = "Name" },
            new DataGridViewTextBoxColumn { HeaderText = "Days",      DataPropertyName = "TotalDays", Width = 80 },
            new DataGridViewCheckBoxColumn { HeaderText = "Paid",     DataPropertyName = "IsPaid",    Width = 60 },
            new DataGridViewCheckBoxColumn { HeaderText = "Active",   DataPropertyName = "IsActive",  Width = 70 }
        );
        tabTypes.Controls.Add(_gridTypes); tabTypes.Controls.Add(t1);

        // ── Leave Requests ────────────────────────────────────────────────────
        var tabReq = new TabPage("Leave Requests") { BackColor = AppTheme.FormBg };
        var t2 = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = AppTheme.CardBg, Padding = new Padding(8, 8, 8, 0) };
        var btnApply   = new AppButton { Text = "➕ Apply Leave",  Width = 120, Location = new Point(8,   8) };
        var btnApprove = new AppButton { Text = "✅ Approve",      Width = 90,  Location = new Point(134, 8) };
        var btnReject  = new AppButton { Text = "❌ Reject",       Width = 90,  Location = new Point(230, 8) };
        var btnCancel  = new AppButton { Text = "🚫 Cancel",       Width = 90,  Location = new Point(326, 8) };
        btnApprove.SetSuccess(); btnReject.SetDanger(); btnCancel.SetSecondary();
        btnApply.Click   += async (s, e) => await ApplyLeave();
        btnApprove.Click += async (s, e) => await ApproveReject(true);
        btnReject.Click  += async (s, e) => await ApproveReject(false);
        btnCancel.Click  += async (s, e) => await CancelLeave();
        t2.Controls.AddRange([btnApply, btnApprove, btnReject, btnCancel]);
        _gridRequests = new AppDataGrid { Dock = DockStyle.Fill };
        _gridRequests.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "Employee",    DataPropertyName = "EmployeeName" },
            new DataGridViewTextBoxColumn { HeaderText = "Leave Type",  DataPropertyName = "LeaveTypeName",Width = 120 },
            new DataGridViewTextBoxColumn { HeaderText = "From",        DataPropertyName = "FromDate",     Width = 100 },
            new DataGridViewTextBoxColumn { HeaderText = "To",          DataPropertyName = "ToDate",       Width = 100 },
            new DataGridViewTextBoxColumn { HeaderText = "Days",        DataPropertyName = "TotalDays",    Width = 60  },
            new DataGridViewTextBoxColumn { HeaderText = "Reason",      DataPropertyName = "Reason" },
            new DataGridViewTextBoxColumn { HeaderText = "Status",      DataPropertyName = "StatusDisplay",Width = 90  }
        );
        tabReq.Controls.Add(_gridRequests); tabReq.Controls.Add(t2);

        // ── Leave Balance ─────────────────────────────────────────────────────
        var tabBal = new TabPage("Leave Balance") { BackColor = AppTheme.FormBg };
        var t3 = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = AppTheme.CardBg, Padding = new Padding(8, 8, 8, 0) };
        var btnLoadBal = new AppButton { Text = "🔄 Load My Balance", Width = 140, Location = new Point(8, 8) };
        btnLoadBal.Click += async (s, e) => await LoadBalance();
        t3.Controls.Add(btnLoadBal);
        _gridBalance = new AppDataGrid { Dock = DockStyle.Fill };
        _gridBalance.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "Leave Type",  DataPropertyName = "LeaveTypeName" },
            new DataGridViewTextBoxColumn { HeaderText = "Allowed",     DataPropertyName = "TotalAllowed",  Width = 90 },
            new DataGridViewTextBoxColumn { HeaderText = "Used",        DataPropertyName = "UsedDays",       Width = 80 },
            new DataGridViewTextBoxColumn { HeaderText = "Remaining",   DataPropertyName = "RemainingDays",  Width = 90 }
        );
        tabBal.Controls.Add(_gridBalance); tabBal.Controls.Add(t3);

        _tabs.TabPages.AddRange([tabTypes, tabReq, tabBal]);
        Controls.Add(_tabs);
        _tabs.SelectedIndexChanged += async (s, e) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var types = await _leaveService.GetLeaveTypesAsync();
        if (types.IsSuccess) { _gridTypes.DataSource = null; _gridTypes.DataSource = types.Data!.ToList(); }

        if (_tabs.SelectedIndex == 1)
        {
            var reqs = await _leaveService.GetAllRequestsAsync();
            if (reqs.IsSuccess) { _gridRequests.DataSource = null; _gridRequests.DataSource = reqs.Data!.ToList(); }
        }
    }

    private async Task LoadBalance()
    {
        if (!AppSession.EmployeeId.HasValue) return;
        var r = await _leaveService.GetBalancesAsync(AppSession.EmployeeId.Value);
        if (r.IsSuccess) { _gridBalance.DataSource = null; _gridBalance.DataSource = r.Data!.ToList(); }
    }

    private async Task OpenTypeDialog(LeaveTypeDto? existing)
    {
        using var dlg = new LeaveTypeEditDialog(existing);
        if (dlg.ShowDialog() != DialogResult.OK) return;
        var r = await _leaveService.SaveLeaveTypeAsync(dlg.GetDto());
        if (r.IsSuccess) await LoadAsync();
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private async Task EditType()
    {
        if (_gridTypes.SelectedRows.Count == 0) return;
        var t = (LeaveTypeDto)_gridTypes.SelectedRows[0].DataBoundItem;
        await OpenTypeDialog(t);
    }

    private async Task DeleteType()
    {
        if (_gridTypes.SelectedRows.Count == 0) return;
        var t = (LeaveTypeDto)_gridTypes.SelectedRows[0].DataBoundItem;
        if (MessageBox.Show($"Delete '{t.Name}'?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        var r = await _leaveService.DeleteLeaveTypeAsync(t.Id);
        if (r.IsSuccess) await LoadAsync();
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private async Task ApplyLeave()
    {
        var employees = await _empService.GetAllAsync();
        var types     = await _leaveService.GetLeaveTypesAsync();
        if (!employees.IsSuccess || !types.IsSuccess) return;
        using var dlg = new ApplyLeaveDialog(employees.Data!.ToList(), types.Data!.ToList());
        if (dlg.ShowDialog() != DialogResult.OK) return;
        var r = await _leaveService.ApplyLeaveAsync(dlg.GetDto());
        if (r.IsSuccess) await LoadAsync();
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private async Task ApproveReject(bool approve)
    {
        if (_gridRequests.SelectedRows.Count == 0) return;
        var req = (LeaveRequestDto)_gridRequests.SelectedRows[0].DataBoundItem;
        string? reason = null;
        if (!approve)
        {
            reason = Microsoft.VisualBasic.Interaction.InputBox("Enter rejection reason:", "Reject Leave", "");
            if (string.IsNullOrWhiteSpace(reason)) return;
        }
        var r = await _leaveService.ApproveRejectAsync(new ApproveRejectLeaveDto { LeaveRequestId = req.Id, IsApproved = approve, RejectionReason = reason }, AppSession.UserId);
        if (r.IsSuccess) await LoadAsync();
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private async Task CancelLeave()
    {
        if (_gridRequests.SelectedRows.Count == 0) return;
        var req = (LeaveRequestDto)_gridRequests.SelectedRows[0].DataBoundItem;
        var r = await _leaveService.CancelAsync(req.Id, AppSession.UserId);
        if (r.IsSuccess) await LoadAsync();
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

public class LeaveRequestForm : Form { public LeaveRequestForm() { BackColor = AppTheme.FormBg; } }
public class LeaveApprovalForm : Form { public LeaveApprovalForm() { BackColor = AppTheme.FormBg; } }

internal class LeaveTypeEditDialog : Form
{
    private readonly LabeledTextBox _txtName;
    private readonly NumericUpDown _nudDays;
    private readonly CheckBox _chkPaid;
    private readonly CheckBox _chkActive;
    private readonly LeaveTypeDto? _existing;

    public LeaveTypeEditDialog(LeaveTypeDto? existing)
    {
        _existing = existing;
        Text = existing == null ? "Add Leave Type" : "Edit Leave Type";
        Size = new Size(380, 280); StartPosition = FormStartPosition.CenterParent;
        BackColor = AppTheme.CardBg; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;

        _txtName = new LabeledTextBox { LabelText = "Leave Type Name *", Location = new Point(20, 20), Width = 320 };
        var lblDays = new Label { Text = "Total Days per Year", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(20, 100), AutoSize = true };
        _nudDays = new NumericUpDown { Location = new Point(20, 118), Width = 80, Minimum = 1, Maximum = 365, Font = AppTheme.BodyFont, Value = 14 };
        _chkPaid   = new CheckBox { Text = "Paid Leave",  Location = new Point(20, 154), Checked = true, Font = AppTheme.BodyFont, ForeColor = AppTheme.BodyText };
        _chkActive = new CheckBox { Text = "Active",      Location = new Point(120, 154), Checked = true, Font = AppTheme.BodyFont, ForeColor = AppTheme.BodyText };
        var btnSave   = new AppButton { Text = "💾 Save", Width = 100, Location = new Point(100, 196) };
        var btnCancel = new AppButton { Text = "Cancel",  Width = 80,  Location = new Point(210, 196) };
        btnCancel.SetSecondary();
        btnSave.Click   += (s, e) => { if (!string.IsNullOrWhiteSpace(_txtName.Value)) DialogResult = DialogResult.OK; else _txtName.ShowError("Required"); };
        btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;
        if (existing != null) { _txtName.Value = existing.Name; _nudDays.Value = existing.TotalDays; _chkPaid.Checked = existing.IsPaid; _chkActive.Checked = existing.IsActive; }
        Controls.AddRange([_txtName, lblDays, _nudDays, _chkPaid, _chkActive, btnSave, btnCancel]);
    }

    public SaveLeaveTypeDto GetDto() => new() { Id = _existing?.Id ?? 0, Name = _txtName.Value.Trim(), TotalDays = (int)_nudDays.Value, IsPaid = _chkPaid.Checked, IsActive = _chkActive.Checked };
}

internal class ApplyLeaveDialog : Form
{
    private readonly ComboBox _cmbEmployee;
    private readonly ComboBox _cmbType;
    private readonly DateTimePicker _dtpFrom;
    private readonly DateTimePicker _dtpTo;
    private readonly TextBox _txtReason;

    public ApplyLeaveDialog(List<EmployeeListItemDto> employees, List<LeaveTypeDto> types)
    {
        Text = "Apply Leave"; Size = new Size(420, 380);
        StartPosition = FormStartPosition.CenterParent; BackColor = AppTheme.CardBg;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;

        var lblEmp  = new Label { Text = "Employee *",   Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(20, 20), AutoSize = true };
        _cmbEmployee = new ComboBox { Location = new Point(20, 38), Width = 360, Font = AppTheme.BodyFont, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbEmployee.DataSource = employees; _cmbEmployee.DisplayMember = "FullName"; _cmbEmployee.ValueMember = "Id";

        var lblType = new Label { Text = "Leave Type *", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(20, 74), AutoSize = true };
        _cmbType = new ComboBox { Location = new Point(20, 92), Width = 360, Font = AppTheme.BodyFont, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbType.DataSource = types; _cmbType.DisplayMember = "Name"; _cmbType.ValueMember = "Id";

        var lblFrom = new Label { Text = "From *", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(20, 128), AutoSize = true };
        _dtpFrom = new DateTimePicker { Location = new Point(20, 146), Width = 160, Format = DateTimePickerFormat.Short, Font = AppTheme.BodyFont };
        var lblTo = new Label { Text = "To *", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(200, 128), AutoSize = true };
        _dtpTo = new DateTimePicker { Location = new Point(200, 146), Width = 160, Format = DateTimePickerFormat.Short, Font = AppTheme.BodyFont };

        var lblReason = new Label { Text = "Reason *", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(20, 182), AutoSize = true };
        _txtReason = new TextBox { Location = new Point(20, 200), Width = 360, Height = 70, Multiline = true, Font = AppTheme.BodyFont, ScrollBars = ScrollBars.Vertical };

        var btnSave   = new AppButton { Text = "📝 Submit", Width = 100, Location = new Point(120, 290) };
        var btnCancel = new AppButton { Text = "Cancel",    Width = 80,  Location = new Point(230, 290) };
        btnCancel.SetSecondary();
        btnSave.Click   += (s, e) => { if (!string.IsNullOrWhiteSpace(_txtReason.Text)) DialogResult = DialogResult.OK; };
        btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;
        Controls.AddRange([lblEmp, _cmbEmployee, lblType, _cmbType, lblFrom, _dtpFrom, lblTo, _dtpTo, lblReason, _txtReason, btnSave, btnCancel]);
    }

    public ApplyLeaveDto GetDto() => new()
    {
        EmployeeId  = (int)_cmbEmployee.SelectedValue!,
        LeaveTypeId = (int)_cmbType.SelectedValue!,
        FromDate    = _dtpFrom.Value,
        ToDate      = _dtpTo.Value,
        Reason      = _txtReason.Text.Trim()
    };
}
