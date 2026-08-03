using AttendanceManagementSystem.UI.Controls;
using AttendanceManagementSystem.UI.Theme;
using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceManagementSystem.Session;

namespace AttendanceManagementSystem.UI.Forms;

/// <summary>Department management — list, add, edit, delete, search.</summary>
public class DepartmentForm : Form
{
    private AppDataGrid _grid = null!;
    private SearchBar _search = null!;
    private AppButton _btnAdd = null!;
    private AppButton _btnEdit = null!;
    private AppButton _btnDelete = null!;
    private AppButton _btnRefresh = null!;
    private List<DepartmentDto> _data = new();

    private readonly IDepartmentService _deptService;
    public DepartmentForm(IDepartmentService deptService) { _deptService = deptService; Build(); _ = LoadAsync(); }

    private void Build()
    {
        BackColor = AppTheme.FormBg;
        // Toolbar
        var toolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = AppTheme.CardBg, Padding = new Padding(8, 8, 8, 0) };
        _search = new SearchBar { Width = 260, Location = new Point(8, 8) };
        _search.SearchChanged += (s, e) => FilterGrid(_search.SearchText);
        _btnAdd     = new AppButton { Text = "➕ Add",    Width = 90,  Location = new Point(276, 8) };
        _btnEdit    = new AppButton { Text = "✏ Edit",   Width = 90,  Location = new Point(372, 8) };
        _btnDelete  = new AppButton { Text = "🗑 Delete", Width = 90,  Location = new Point(468, 8) };
        _btnRefresh = new AppButton { Text = "↻",         Width = 40,  Location = new Point(564, 8) };
        _btnEdit.SetSecondary(); _btnDelete.SetDanger(); _btnRefresh.SetSecondary();
        _btnAdd.Click     += async (s, e) => await OpenEditDialog(null);
        _btnEdit.Click    += async (s, e) => await EditSelected();
        _btnDelete.Click  += async (s, e) => await DeleteSelected();
        _btnRefresh.Click += async (s, e) => await LoadAsync();
        toolbar.Controls.AddRange([_search, _btnAdd, _btnEdit, _btnDelete, _btnRefresh]);

        _grid = new AppDataGrid { Dock = DockStyle.Fill };
        _grid.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "#",            DataPropertyName = "Id",           Width = 50 },
            new DataGridViewTextBoxColumn { HeaderText = "Department",   DataPropertyName = "Name" },
            new DataGridViewTextBoxColumn { HeaderText = "Description",  DataPropertyName = "Description" },
            new DataGridViewCheckBoxColumn { HeaderText = "Active",      DataPropertyName = "IsActive",     Width = 70 },
            new DataGridViewTextBoxColumn { HeaderText = "Employees",    DataPropertyName = "EmployeeCount",Width = 90 }
        );
        _grid.CellDoubleClick += async (s, e) => await EditSelected();
        Controls.Add(_grid);
        Controls.Add(toolbar);
    }

    private async Task LoadAsync()
    {
        var r = await _deptService.GetAllAsync();
        if (r.IsSuccess) { _data = r.Data!.ToList(); BindGrid(_data); }
    }

    private void BindGrid(IEnumerable<DepartmentDto> data)
    {
        _grid.DataSource = null;
        _grid.DataSource = data.ToList();
    }

    private void FilterGrid(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) BindGrid(_data);
        else
        {
            var lower = keyword.ToLower();
            BindGrid(_data.Where(d => d.Name.ToLower().Contains(lower) ||
                                     (d.Description ?? "").ToLower().Contains(lower)));
        }
    }

    private async Task OpenEditDialog(DepartmentDto? existing)
    {
        using var dlg = new DepartmentEditDialog(existing);
        if (dlg.ShowDialog() != DialogResult.OK) return;
        var dto = dlg.GetDto();
        var r = await _deptService.SaveAsync(dto);
        if (r.IsSuccess) { await LoadAsync(); MessageBox.Show(existing == null ? "Department added." : "Department updated.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private async Task EditSelected()
    {
        if (_grid.SelectedRows.Count == 0) return;
        var selected = (DepartmentDto)_grid.SelectedRows[0].DataBoundItem;
        var r = await _deptService.GetByIdAsync(selected.Id);
        if (r.IsSuccess) await OpenEditDialog(r.Data!);
    }

    private async Task DeleteSelected()
    {
        if (_grid.SelectedRows.Count == 0) return;
        var selected = (DepartmentDto)_grid.SelectedRows[0].DataBoundItem;
        if (MessageBox.Show($"Delete department '{selected.Name}'?", "Confirm",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        var r = await _deptService.DeleteAsync(selected.Id, DesktopSession.UserId);
        if (r.IsSuccess) await LoadAsync();
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

/// <summary>Inline edit dialog for department.</summary>
internal class DepartmentEditDialog : Form
{
    private readonly LabeledTextBox _txtName;
    private readonly LabeledTextBox _txtDesc;
    private readonly CheckBox _chkActive;
    private readonly DepartmentDto? _existing;

    public DepartmentEditDialog(DepartmentDto? existing)
    {
        _existing = existing;
        Text = existing == null ? "Add Department" : "Edit Department";
        Size = new Size(400, 300); StartPosition = FormStartPosition.CenterParent;
        BackColor = AppTheme.CardBg; FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;

        _txtName = new LabeledTextBox { LabelText = "Department Name *", Location = new Point(20, 20), Width = 340 };
        _txtDesc = new LabeledTextBox { LabelText = "Description",       Location = new Point(20, 100), Width = 340 };
        _chkActive = new CheckBox { Text = "Active", Location = new Point(20, 178), Checked = true, Font = AppTheme.BodyFont, ForeColor = AppTheme.BodyText };

        var btnSave = new AppButton { Text = "💾 Save", Width = 100, Location = new Point(140, 218) };
        var btnCancel = new AppButton { Text = "Cancel", Width = 80, Location = new Point(250, 218) };
        btnCancel.SetSecondary();
        btnSave.Click   += (s, e) => { if (Validate2()) DialogResult = DialogResult.OK; };
        btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;

        if (existing != null) { _txtName.Value = existing.Name; _txtDesc.Value = existing.Description ?? ""; _chkActive.Checked = existing.IsActive; }

        Controls.AddRange([_txtName, _txtDesc, _chkActive, btnSave, btnCancel]);
    }

    private bool Validate2()
    {
        _txtName.ClearError();
        if (string.IsNullOrWhiteSpace(_txtName.Value)) { _txtName.ShowError("Name is required."); return false; }
        return true;
    }

    public SaveDepartmentDto GetDto() => new()
    {
        Id = _existing?.Id ?? 0,
        Name = _txtName.Value.Trim(),
        Description = _txtDesc.Value.Trim(),
        IsActive = _chkActive.Checked
    };
}
