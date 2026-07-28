using AttendanceManagementSystem.UI.Controls;
using AttendanceManagementSystem.UI.Theme;
using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Session;
using Microsoft.Extensions.DependencyInjection;

namespace AttendanceManagementSystem.UI.Forms;

/// <summary>Employee list with search, filter and navigation to profile.</summary>
public class EmployeeListForm : Form
{
    private AppDataGrid _grid = null!;
    private SearchBar _search = null!;
    private ComboBox _cmbDept = null!;
    private List<EmployeeListItemDto> _data = new();

    private readonly IEmployeeService _empService;
    private readonly IDepartmentService _deptService;
    private readonly IServiceProvider _services;

    public EmployeeListForm(IEmployeeService empService, IDepartmentService deptService, IServiceProvider services)
    {
        _empService = empService; _deptService = deptService; _services = services;
        Build(); _ = LoadAsync();
    }

    private void Build()
    {
        BackColor = AppTheme.FormBg;
        var toolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = AppTheme.CardBg, Padding = new Padding(8, 8, 8, 0) };
        _search = new SearchBar { Width = 220, Location = new Point(8, 8) };
        _search.SearchChanged += (s, e) => ApplyFilter();

        _cmbDept = new ComboBox { Width = 160, Location = new Point(236, 10), Font = AppTheme.BodyFont, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbDept.SelectedIndexChanged += (s, e) => ApplyFilter();

        var btnAdd    = new AppButton { Text = "➕ Add",      Width = 90,  Location = new Point(404, 8) };
        var btnEdit   = new AppButton { Text = "✏ Edit",     Width = 90,  Location = new Point(500, 8) };
        var btnDelete = new AppButton { Text = "🗑 Delete",   Width = 90,  Location = new Point(596, 8) };
        var btnToggle = new AppButton { Text = "🔄 Status",   Width = 90,  Location = new Point(692, 8) };
        btnEdit.SetSecondary(); btnDelete.SetDanger(); btnToggle.SetWarning();

        btnAdd.Click    += (s, e) => OpenEditForm(0);
        btnEdit.Click   += (s, e) => OpenEditForm(GetSelectedId());
        btnDelete.Click += async (s, e) => await DeleteSelected();
        btnToggle.Click += async (s, e) => await ToggleStatus();

        toolbar.Controls.AddRange([_search, _cmbDept, btnAdd, btnEdit, btnDelete, btnToggle]);

        _grid = new AppDataGrid { Dock = DockStyle.Fill };
        _grid.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "Code",        DataPropertyName = "EmployeeCode",  Width = 100 },
            new DataGridViewTextBoxColumn { HeaderText = "Full Name",   DataPropertyName = "FullName" },
            new DataGridViewTextBoxColumn { HeaderText = "Department",  DataPropertyName = "Department" },
            new DataGridViewTextBoxColumn { HeaderText = "Designation", DataPropertyName = "Designation" },
            new DataGridViewTextBoxColumn { HeaderText = "Branch",      DataPropertyName = "Branch" },
            new DataGridViewTextBoxColumn { HeaderText = "Phone",       DataPropertyName = "Phone",         Width = 120 },
            new DataGridViewTextBoxColumn { HeaderText = "Email",       DataPropertyName = "Email" },
            new DataGridViewCheckBoxColumn { HeaderText = "Active",     DataPropertyName = "IsActive",      Width = 60 }
        );
        _grid.CellDoubleClick += (s, e) => OpenEditForm(GetSelectedId());
        Controls.Add(_grid); Controls.Add(toolbar);
    }

    private async Task LoadAsync()
    {
        var r = await _empService.GetAllAsync();
        if (!r.IsSuccess) return;
        _data = r.Data!.ToList();

        var depts = await _deptService.GetAllAsync();
        _cmbDept.Items.Clear();
        _cmbDept.Items.Add(new { Id = 0, Name = "All Departments" });
        if (depts.IsSuccess)
            foreach (var d in depts.Data!) _cmbDept.Items.Add(new { d.Id, d.Name });
        _cmbDept.DisplayMember = "Name";
        _cmbDept.ValueMember = "Id";
        _cmbDept.SelectedIndex = 0;

        BindGrid(_data);
    }

    private void ApplyFilter()
    {
        var q = _search.SearchText.ToLower();
        var deptId = (_cmbDept.SelectedItem is { } item)
            ? (int)(item.GetType().GetProperty("Id")?.GetValue(item) ?? 0) : 0;

        var filtered = _data.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(q))
            filtered = filtered.Where(e => e.FullName.ToLower().Contains(q) ||
                                           e.EmployeeCode.ToLower().Contains(q) ||
                                           (e.Email ?? "").ToLower().Contains(q) ||
                                           (e.Phone ?? "").Contains(q));
        if (deptId > 0)
            filtered = filtered.Where(e => e.Department == _data.FirstOrDefault(d => d.Id == e.Id)?.Department);

        BindGrid(filtered);
    }

    private void BindGrid(IEnumerable<EmployeeListItemDto> data) { _grid.DataSource = null; _grid.DataSource = data.ToList(); }

    private int GetSelectedId()
    {
        if (_grid.SelectedRows.Count == 0) return 0;
        return ((EmployeeListItemDto)_grid.SelectedRows[0].DataBoundItem).Id;
    }

    private void OpenEditForm(int id)
    {
        var form = _services.GetRequiredService<EmployeeEditForm>();
        form.SetEmployee(id);
        if (form.ShowDialog() == DialogResult.OK) _ = LoadAsync();
    }

    private async Task DeleteSelected()
    {
        if (_grid.SelectedRows.Count == 0) return;
        var emp = (EmployeeListItemDto)_grid.SelectedRows[0].DataBoundItem;
        if (MessageBox.Show($"Delete employee '{emp.FullName}'?", "Confirm",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        var r = await _empService.DeleteAsync(emp.Id, AppSession.UserId);
        if (r.IsSuccess) await LoadAsync();
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private async Task ToggleStatus()
    {
        if (_grid.SelectedRows.Count == 0) return;
        var emp = (EmployeeListItemDto)_grid.SelectedRows[0].DataBoundItem;
        var r = await _empService.ToggleActiveAsync(emp.Id, AppSession.UserId);
        if (r.IsSuccess) await LoadAsync();
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

/// <summary>Employee create / edit form with photo, all fields and validation.</summary>
public class EmployeeEditForm : Form
{
    private int _employeeId;
    private LabeledTextBox _txtFirst = null!;
    private LabeledTextBox _txtLast = null!;
    private LabeledTextBox _txtEmail = null!;
    private LabeledTextBox _txtPhone = null!;
    private LabeledTextBox _txtAddress = null!;
    private DateTimePicker _dtpDob = null!;
    private DateTimePicker _dtpJoin = null!;
    private ComboBox _cmbGender = null!;
    private ComboBox _cmbDept = null!;
    private ComboBox _cmbDesig = null!;
    private ComboBox _cmbBranch = null!;
    private CheckBox _chkActive = null!;
    private PictureBox _photo = null!;
    private byte[]? _photoBytes;
    private Label _lblCode = null!;

    private readonly IEmployeeService _empService;
    private readonly IDepartmentService _deptService;
    private readonly IDesignationService _desigService;
    private readonly IBranchService _branchService;

    public EmployeeEditForm(IEmployeeService empService, IDepartmentService deptService,
        IDesignationService desigService, IBranchService branchService)
    {
        _empService = empService; _deptService = deptService;
        _desigService = desigService; _branchService = branchService;
        Build();
    }

    public void SetEmployee(int id) { _employeeId = id; _ = LoadAsync(); }

    private void Build()
    {
        Text = "Employee Profile";
        Size = new Size(860, 620); StartPosition = FormStartPosition.CenterParent;
        BackColor = AppTheme.CardBg; FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        // Photo panel left
        var photoPanel = new Panel { Width = 180, Dock = DockStyle.Left, BackColor = AppTheme.FormBg, Padding = new Padding(20) };
        _photo = new PictureBox { Size = new Size(140, 140), Location = new Point(20, 30), SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.WhiteSmoke };
        var btnPhoto = new AppButton { Text = "📷 Photo", Width = 140, Location = new Point(20, 178) };
        btnPhoto.SetSecondary();
        btnPhoto.Click += PickPhoto;
        _lblCode = new Label { Text = "EMP-XXXXX", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(20, 220), AutoSize = true };
        photoPanel.Controls.AddRange([_photo, btnPhoto, _lblCode]);

        // Fields panel
        var fields = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };

        int x1 = 10, x2 = 330, y = 10, gap = 80;

        _txtFirst   = new LabeledTextBox { LabelText = "First Name *",  Location = new Point(x1, y), Width = 300 };
        _txtLast    = new LabeledTextBox { LabelText = "Last Name *",   Location = new Point(x2, y), Width = 300 };
        y += gap;
        _txtEmail   = new LabeledTextBox { LabelText = "Email",         Location = new Point(x1, y), Width = 300 };
        _txtPhone   = new LabeledTextBox { LabelText = "Phone",         Location = new Point(x2, y), Width = 300 };
        y += gap;

        var lblDob  = new Label { Text = "Date of Birth", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(x1, y),     AutoSize = true };
        var lblJoin = new Label { Text = "Joining Date *",Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(x2, y),     AutoSize = true };
        _dtpDob  = new DateTimePicker { Width = 200, Location = new Point(x1, y + 18), Format = DateTimePickerFormat.Short, Font = AppTheme.BodyFont };
        _dtpJoin = new DateTimePicker { Width = 200, Location = new Point(x2, y + 18), Format = DateTimePickerFormat.Short, Font = AppTheme.BodyFont };
        y += gap;

        var lblGender = new Label { Text = "Gender", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(x1, y), AutoSize = true };
        _cmbGender = new ComboBox { Width = 200, Location = new Point(x1, y + 18), Font = AppTheme.BodyFont, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbGender.Items.AddRange(["Male", "Female", "Other"]);
        _cmbGender.SelectedIndex = 0;

        var lblDept = new Label { Text = "Department *", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(x2, y), AutoSize = true };
        _cmbDept = new ComboBox { Width = 200, Location = new Point(x2, y + 18), Font = AppTheme.BodyFont, DropDownStyle = ComboBoxStyle.DropDownList };
        y += gap;

        var lblDesig = new Label { Text = "Designation *", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(x1, y), AutoSize = true };
        _cmbDesig = new ComboBox { Width = 200, Location = new Point(x1, y + 18), Font = AppTheme.BodyFont, DropDownStyle = ComboBoxStyle.DropDownList };

        var lblBranch = new Label { Text = "Branch *", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(x2, y), AutoSize = true };
        _cmbBranch = new ComboBox { Width = 200, Location = new Point(x2, y + 18), Font = AppTheme.BodyFont, DropDownStyle = ComboBoxStyle.DropDownList };
        y += gap;

        _txtAddress = new LabeledTextBox { LabelText = "Address", Location = new Point(x1, y), Width = 620 };
        y += gap;

        _chkActive = new CheckBox { Text = "Active Employee", Location = new Point(x1, y), Checked = true, Font = AppTheme.BodyFont, ForeColor = AppTheme.BodyText };

        var btnSave   = new AppButton { Text = "💾 Save",   Width = 110, Location = new Point(x1,      y + 28) };
        var btnCancel = new AppButton { Text = "✖ Cancel",  Width = 90,  Location = new Point(x1 + 120, y + 28) };
        btnCancel.SetSecondary();
        btnSave.Click   += async (s, e) => await SaveAsync();
        btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;

        fields.Controls.AddRange([_txtFirst, _txtLast, _txtEmail, _txtPhone,
            lblDob, _dtpDob, lblJoin, _dtpJoin, lblGender, _cmbGender,
            lblDept, _cmbDept, lblDesig, _cmbDesig, lblBranch, _cmbBranch,
            _txtAddress, _chkActive, btnSave, btnCancel]);

        Controls.Add(fields);
        Controls.Add(photoPanel);

        _ = LoadLookupsAsync();
    }

    private async Task LoadLookupsAsync()
    {
        var depts  = await _deptService.GetAllAsync();
        var desigs = await _desigService.GetAllAsync();
        var branches = await _branchService.GetAllAsync();

        if (depts.IsSuccess)  { _cmbDept.DataSource   = depts.Data!.ToList();   _cmbDept.DisplayMember   = "Name"; _cmbDept.ValueMember   = "Id"; }
        if (desigs.IsSuccess) { _cmbDesig.DataSource  = desigs.Data!.ToList();  _cmbDesig.DisplayMember  = "Name"; _cmbDesig.ValueMember  = "Id"; }
        if (branches.IsSuccess) { _cmbBranch.DataSource = branches.Data!.ToList(); _cmbBranch.DisplayMember = "Name"; _cmbBranch.ValueMember = "Id"; }
    }

    private async Task LoadAsync()
    {
        if (_employeeId == 0) { Text = "Add Employee"; return; }
        Text = "Edit Employee";
        var r = await _empService.GetByIdAsync(_employeeId);
        if (!r.IsSuccess) return;
        var e = r.Data!;
        _lblCode.Text    = e.EmployeeCode;
        _txtFirst.Value  = e.FirstName;
        _txtLast.Value   = e.LastName;
        _txtEmail.Value  = e.Email ?? "";
        _txtPhone.Value  = e.Phone ?? "";
        _txtAddress.Value = e.Address ?? "";
        _dtpDob.Value    = e.DateOfBirth ?? DateTime.Today.AddYears(-25);
        _dtpJoin.Value   = e.JoiningDate;
        _cmbGender.SelectedItem = e.Gender;
        _cmbDept.SelectedValue   = e.DepartmentId;
        _cmbDesig.SelectedValue  = e.DesignationId;
        _cmbBranch.SelectedValue = e.BranchId;
        _chkActive.Checked = e.IsActive;
        if (e.Photo != null) { _photoBytes = e.Photo; _photo.Image = Image.FromStream(new MemoryStream(e.Photo)); }
    }

    private void PickPhoto(object? s, EventArgs e)
    {
        using var ofd = new OpenFileDialog { Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp", Title = "Select Employee Photo" };
        if (ofd.ShowDialog() != DialogResult.OK) return;
        _photoBytes = File.ReadAllBytes(ofd.FileName);
        _photo.Image = Image.FromStream(new MemoryStream(_photoBytes));
    }

    private bool ValidateForm()
    {
        _txtFirst.ClearError(); _txtLast.ClearError();
        var ok = true;
        if (string.IsNullOrWhiteSpace(_txtFirst.Value)) { _txtFirst.ShowError("First name required."); ok = false; }
        if (string.IsNullOrWhiteSpace(_txtLast.Value))  { _txtLast.ShowError("Last name required."); ok = false; }
        return ok;
    }

    private async Task SaveAsync()
    {
        if (!ValidateForm()) return;
        var dto = new SaveEmployeeDto
        {
            Id = _employeeId,
            FirstName   = _txtFirst.Value.Trim(),
            LastName    = _txtLast.Value.Trim(),
            Email       = _txtEmail.Value.Trim(),
            Phone       = _txtPhone.Value.Trim(),
            Address     = _txtAddress.Value.Trim(),
            DateOfBirth = _dtpDob.Value,
            JoiningDate = _dtpJoin.Value,
            Gender      = _cmbGender.SelectedItem?.ToString(),
            DepartmentId  = (int)(_cmbDept.SelectedValue ?? 1),
            DesignationId = (int)(_cmbDesig.SelectedValue ?? 1),
            BranchId      = (int)(_cmbBranch.SelectedValue ?? 1),
            Photo         = _photoBytes,
            IsActive      = _chkActive.Checked
        };
        var r = await _empService.SaveAsync(dto);
        if (r.IsSuccess)
        {
            MessageBox.Show("Employee saved successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
        }
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
