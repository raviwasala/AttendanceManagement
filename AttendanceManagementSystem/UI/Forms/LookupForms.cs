using AttendanceManagementSystem.UI.Controls;
using AttendanceManagementSystem.UI.Theme;
using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Session;

namespace AttendanceManagementSystem.UI.Forms;

/// <summary>Designation management — CRUD.</summary>
public class DesignationForm : Form
{
    private AppDataGrid _grid = null!;
    private SearchBar _search = null!;
    private List<DesignationDto> _data = new();
    private readonly IDesignationService _service;
    public DesignationForm(IDesignationService service) { _service = service; Build(); _ = LoadAsync(); }

    private void Build()
    {
        BackColor = AppTheme.FormBg;
        var toolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = AppTheme.CardBg, Padding = new Padding(8, 8, 8, 0) };
        _search = new SearchBar { Width = 260, Location = new Point(8, 8) };
        _search.SearchChanged += (s, e) => FilterGrid(_search.SearchText);
        var btnAdd    = new AppButton { Text = "➕ Add",    Width = 90,  Location = new Point(276, 8) };
        var btnEdit   = new AppButton { Text = "✏ Edit",   Width = 90,  Location = new Point(372, 8) };
        var btnDelete = new AppButton { Text = "🗑 Delete", Width = 90,  Location = new Point(468, 8) };
        btnEdit.SetSecondary(); btnDelete.SetDanger();
        btnAdd.Click    += async (s, e) => await OpenDialog(null);
        btnEdit.Click   += async (s, e) => await EditSelected();
        btnDelete.Click += async (s, e) => await DeleteSelected();
        toolbar.Controls.AddRange([_search, btnAdd, btnEdit, btnDelete]);

        _grid = new AppDataGrid { Dock = DockStyle.Fill };
        _grid.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "#",           DataPropertyName = "Id",          Width = 50 },
            new DataGridViewTextBoxColumn { HeaderText = "Designation", DataPropertyName = "Name" },
            new DataGridViewTextBoxColumn { HeaderText = "Description", DataPropertyName = "Description" },
            new DataGridViewCheckBoxColumn { HeaderText = "Active",     DataPropertyName = "IsActive",    Width = 70 }
        );
        _grid.CellDoubleClick += async (s, e) => await EditSelected();
        Controls.Add(_grid); Controls.Add(toolbar);
    }

    private async Task LoadAsync() { var r = await _service.GetAllAsync(); if (r.IsSuccess) { _data = r.Data!.ToList(); BindGrid(_data); } }
    private void BindGrid(IEnumerable<DesignationDto> d) { _grid.DataSource = null; _grid.DataSource = d.ToList(); }
    private void FilterGrid(string q) { if (string.IsNullOrWhiteSpace(q)) BindGrid(_data); else BindGrid(_data.Where(d => d.Name.ToLower().Contains(q.ToLower()))); }

    private async Task OpenDialog(DesignationDto? existing)
    {
        using var dlg = new DesignationEditDialog(existing);
        if (dlg.ShowDialog() != DialogResult.OK) return;
        var r = await _service.SaveAsync(dlg.GetDto());
        if (r.IsSuccess) await LoadAsync();
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private async Task EditSelected()
    {
        if (_grid.SelectedRows.Count == 0) return;
        var s = (DesignationDto)_grid.SelectedRows[0].DataBoundItem;
        var r = await _service.GetByIdAsync(s.Id);
        if (r.IsSuccess) await OpenDialog(r.Data!);
    }

    private async Task DeleteSelected()
    {
        if (_grid.SelectedRows.Count == 0) return;
        var s = (DesignationDto)_grid.SelectedRows[0].DataBoundItem;
        if (MessageBox.Show($"Delete '{s.Name}'?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        var r = await _service.DeleteAsync(s.Id, AppSession.UserId);
        if (r.IsSuccess) await LoadAsync();
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

internal class DesignationEditDialog : Form
{
    private readonly LabeledTextBox _txtName;
    private readonly LabeledTextBox _txtDesc;
    private readonly CheckBox _chkActive;
    private readonly DesignationDto? _existing;

    public DesignationEditDialog(DesignationDto? existing)
    {
        _existing = existing;
        Text = existing == null ? "Add Designation" : "Edit Designation";
        Size = new Size(400, 270); StartPosition = FormStartPosition.CenterParent;
        BackColor = AppTheme.CardBg; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;

        _txtName = new LabeledTextBox { LabelText = "Designation Name *", Location = new Point(20, 20), Width = 340 };
        _txtDesc = new LabeledTextBox { LabelText = "Description",        Location = new Point(20, 100), Width = 340 };
        _chkActive = new CheckBox { Text = "Active", Location = new Point(20, 178), Checked = true, Font = AppTheme.BodyFont, ForeColor = AppTheme.BodyText };
        var btnSave   = new AppButton { Text = "💾 Save", Width = 100, Location = new Point(130, 210) };
        var btnCancel = new AppButton { Text = "Cancel",  Width = 80,  Location = new Point(240, 210) };
        btnCancel.SetSecondary();
        btnSave.Click   += (s, e) => { if (!string.IsNullOrWhiteSpace(_txtName.Value)) DialogResult = DialogResult.OK; else _txtName.ShowError("Required"); };
        btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;
        if (existing != null) { _txtName.Value = existing.Name; _txtDesc.Value = existing.Description ?? ""; _chkActive.Checked = existing.IsActive; }
        Controls.AddRange([_txtName, _txtDesc, _chkActive, btnSave, btnCancel]);
    }
    public SaveDesignationDto GetDto() => new() { Id = _existing?.Id ?? 0, Name = _txtName.Value.Trim(), Description = _txtDesc.Value.Trim(), IsActive = _chkActive.Checked };
}

/// <summary>Branch management — CRUD.</summary>
public class BranchForm : Form
{
    private AppDataGrid _grid = null!;
    private readonly IBranchService _service;
    public BranchForm(IBranchService service) { _service = service; Build(); _ = LoadAsync(); }

    private void Build()
    {
        BackColor = AppTheme.FormBg;
        var toolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = AppTheme.CardBg, Padding = new Padding(8, 8, 8, 0) };
        var btnAdd    = new AppButton { Text = "➕ Add",    Width = 90,  Location = new Point(8,   8) };
        var btnEdit   = new AppButton { Text = "✏ Edit",   Width = 90,  Location = new Point(104, 8) };
        var btnDelete = new AppButton { Text = "🗑 Delete", Width = 90,  Location = new Point(200, 8) };
        btnEdit.SetSecondary(); btnDelete.SetDanger();
        btnAdd.Click    += async (s, e) => await OpenDialog(null);
        btnEdit.Click   += async (s, e) => await EditSelected();
        btnDelete.Click += async (s, e) => await DeleteSelected();
        toolbar.Controls.AddRange([btnAdd, btnEdit, btnDelete]);

        _grid = new AppDataGrid { Dock = DockStyle.Fill };
        _grid.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "#",       DataPropertyName = "Id",       Width = 50 },
            new DataGridViewTextBoxColumn { HeaderText = "Branch",  DataPropertyName = "Name" },
            new DataGridViewTextBoxColumn { HeaderText = "Address", DataPropertyName = "Address" },
            new DataGridViewTextBoxColumn { HeaderText = "Phone",   DataPropertyName = "Phone",    Width = 120 },
            new DataGridViewCheckBoxColumn { HeaderText = "Active", DataPropertyName = "IsActive", Width = 70 }
        );
        _grid.CellDoubleClick += async (s, e) => await EditSelected();
        Controls.Add(_grid); Controls.Add(toolbar);
    }

    private async Task LoadAsync() { var r = await _service.GetAllAsync(); if (r.IsSuccess) { _grid.DataSource = null; _grid.DataSource = r.Data!.ToList(); } }

    private async Task OpenDialog(BranchDto? existing)
    {
        using var dlg = new BranchEditDialog(existing);
        if (dlg.ShowDialog() != DialogResult.OK) return;
        var r = await _service.SaveAsync(dlg.GetDto());
        if (r.IsSuccess) await LoadAsync();
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private async Task EditSelected()
    {
        if (_grid.SelectedRows.Count == 0) return;
        var s = (BranchDto)_grid.SelectedRows[0].DataBoundItem;
        var r = await _service.GetByIdAsync(s.Id);
        if (r.IsSuccess) await OpenDialog(r.Data!);
    }

    private async Task DeleteSelected()
    {
        if (_grid.SelectedRows.Count == 0) return;
        var s = (BranchDto)_grid.SelectedRows[0].DataBoundItem;
        if (MessageBox.Show($"Delete '{s.Name}'?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        var r = await _service.DeleteAsync(s.Id, AppSession.UserId);
        if (r.IsSuccess) await LoadAsync();
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

internal class BranchEditDialog : Form
{
    private readonly LabeledTextBox _txtName;
    private readonly LabeledTextBox _txtAddress;
    private readonly LabeledTextBox _txtPhone;
    private readonly CheckBox _chkActive;
    private readonly BranchDto? _existing;

    public BranchEditDialog(BranchDto? existing)
    {
        _existing = existing;
        Text = existing == null ? "Add Branch" : "Edit Branch";
        Size = new Size(400, 320); StartPosition = FormStartPosition.CenterParent;
        BackColor = AppTheme.CardBg; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;

        _txtName    = new LabeledTextBox { LabelText = "Branch Name *", Location = new Point(20, 20),  Width = 340 };
        _txtAddress = new LabeledTextBox { LabelText = "Address",       Location = new Point(20, 100), Width = 340 };
        _txtPhone   = new LabeledTextBox { LabelText = "Phone",         Location = new Point(20, 180), Width = 200 };
        _chkActive  = new CheckBox { Text = "Active", Location = new Point(20, 254), Checked = true, Font = AppTheme.BodyFont, ForeColor = AppTheme.BodyText };
        var btnSave   = new AppButton { Text = "💾 Save", Width = 100, Location = new Point(140, 256) };
        var btnCancel = new AppButton { Text = "Cancel",  Width = 80,  Location = new Point(250, 256) };
        btnCancel.SetSecondary();
        btnSave.Click   += (s, e) => { if (!string.IsNullOrWhiteSpace(_txtName.Value)) DialogResult = DialogResult.OK; else _txtName.ShowError("Required"); };
        btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;
        if (existing != null) { _txtName.Value = existing.Name; _txtAddress.Value = existing.Address ?? ""; _txtPhone.Value = existing.Phone ?? ""; _chkActive.Checked = existing.IsActive; }
        Controls.AddRange([_txtName, _txtAddress, _txtPhone, _chkActive, btnSave, btnCancel]);
    }
    public SaveBranchDto GetDto() => new() { Id = _existing?.Id ?? 0, Name = _txtName.Value.Trim(), Address = _txtAddress.Value.Trim(), Phone = _txtPhone.Value.Trim(), IsActive = _chkActive.Checked };
}
