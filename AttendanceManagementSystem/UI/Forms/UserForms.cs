using AttendanceManagementSystem.UI.Controls;
using AttendanceManagementSystem.UI.Theme;
using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Models;
using AttendanceManagementSystem.Session;

namespace AttendanceManagementSystem.UI.Forms;

/// <summary>User list + create/edit/delete + role management.</summary>
public class UserForm : Form
{
    private AppDataGrid _gridUsers = null!;
    private AppDataGrid _gridRoles = null!;
    private TabControl _tabs = null!;

    private readonly IUserService _userService;
    private readonly IRoleService _roleService;
    private readonly IEmployeeService _empService;

    public UserForm(IUserService userService, IRoleService roleService, IEmployeeService empService)
    {
        _userService = userService; _roleService = roleService; _empService = empService;
        Build(); _ = LoadAsync();
    }

    private void Build()
    {
        BackColor = AppTheme.FormBg;
        _tabs = new TabControl { Dock = DockStyle.Fill, Font = AppTheme.BodyFont };

        // ── Users ─────────────────────────────────────────────────────────────
        var tabUsers = new TabPage("Users") { BackColor = AppTheme.FormBg };
        var t1 = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = AppTheme.CardBg, Padding = new Padding(8, 8, 8, 0) };
        var btnAdd     = new AppButton { Text = "➕ Add",          Width = 90,  Location = new Point(8,   8) };
        var btnEdit    = new AppButton { Text = "✏ Edit",          Width = 90,  Location = new Point(104, 8) };
        var btnDelete  = new AppButton { Text = "🗑 Delete",        Width = 90,  Location = new Point(200, 8) };
        var btnLock    = new AppButton { Text = "🔒 Lock",          Width = 80,  Location = new Point(296, 8) };
        var btnUnlock  = new AppButton { Text = "🔓 Unlock",        Width = 80,  Location = new Point(382, 8) };
        var btnReset   = new AppButton { Text = "🔑 Reset Pwd",     Width = 100, Location = new Point(468, 8) };
        btnEdit.SetSecondary(); btnDelete.SetDanger(); btnLock.SetWarning(); btnUnlock.SetSuccess(); btnReset.SetSecondary();

        btnAdd.Click    += async (s, e) => await OpenUserDialog(null);
        btnEdit.Click   += async (s, e) => await EditUser();
        btnDelete.Click += async (s, e) => await DeleteUser();
        btnLock.Click   += async (s, e) => await LockUser(true);
        btnUnlock.Click += async (s, e) => await LockUser(false);
        btnReset.Click  += async (s, e) => await ResetPassword();
        t1.Controls.AddRange([btnAdd, btnEdit, btnDelete, btnLock, btnUnlock, btnReset]);

        _gridUsers = new AppDataGrid { Dock = DockStyle.Fill };
        _gridUsers.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "#",           DataPropertyName = "Id",           Width = 50 },
            new DataGridViewTextBoxColumn { HeaderText = "Username",    DataPropertyName = "Username",     Width = 120 },
            new DataGridViewTextBoxColumn { HeaderText = "Full Name",   DataPropertyName = "FullName" },
            new DataGridViewTextBoxColumn { HeaderText = "Email",       DataPropertyName = "Email" },
            new DataGridViewTextBoxColumn { HeaderText = "Role",        DataPropertyName = "RoleName",     Width = 120 },
            new DataGridViewCheckBoxColumn { HeaderText = "Active",     DataPropertyName = "IsActive",     Width = 60 },
            new DataGridViewCheckBoxColumn { HeaderText = "Locked",     DataPropertyName = "IsLocked",     Width = 60 },
            new DataGridViewTextBoxColumn { HeaderText = "Last Login",  DataPropertyName = "LastLoginAt",  Width = 140 }
        );
        tabUsers.Controls.Add(_gridUsers); tabUsers.Controls.Add(t1);

        // ── Roles ─────────────────────────────────────────────────────────────
        var tabRoles = new TabPage("Roles") { BackColor = AppTheme.FormBg };
        var t2 = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = AppTheme.CardBg, Padding = new Padding(8, 8, 8, 0) };
        var btnAddRole    = new AppButton { Text = "➕ Add Role",    Width = 100, Location = new Point(8,   8) };
        var btnEditRole   = new AppButton { Text = "✏ Edit",         Width = 80,  Location = new Point(114, 8) };
        var btnDeleteRole = new AppButton { Text = "🗑 Delete",       Width = 80,  Location = new Point(200, 8) };
        var btnPerms      = new AppButton { Text = "🔐 Permissions", Width = 120, Location = new Point(286, 8) };
        btnEditRole.SetSecondary(); btnDeleteRole.SetDanger(); btnPerms.SetSecondary();
        btnAddRole.Click    += async (s, e) => await OpenRoleDialog(null);
        btnEditRole.Click   += async (s, e) => await EditRole();
        btnDeleteRole.Click += async (s, e) => await DeleteRole();
        btnPerms.Click      += async (s, e) => await ManagePermissions();
        t2.Controls.AddRange([btnAddRole, btnEditRole, btnDeleteRole, btnPerms]);

        _gridRoles = new AppDataGrid { Dock = DockStyle.Fill };
        _gridRoles.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "#",           DataPropertyName = "Id",          Width = 50 },
            new DataGridViewTextBoxColumn { HeaderText = "Role Name",   DataPropertyName = "Name" },
            new DataGridViewTextBoxColumn { HeaderText = "Description", DataPropertyName = "Description" }
        );
        tabRoles.Controls.Add(_gridRoles); tabRoles.Controls.Add(t2);

        _tabs.TabPages.AddRange([tabUsers, tabRoles]);
        Controls.Add(_tabs);
        _tabs.SelectedIndexChanged += async (s, e) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var users = await _userService.GetAllAsync();
        if (users.IsSuccess) { _gridUsers.DataSource = null; _gridUsers.DataSource = users.Data!.ToList(); }
        if (_tabs.SelectedIndex == 1)
        {
            var roles = await _roleService.GetAllAsync();
            if (roles.IsSuccess) { _gridRoles.DataSource = null; _gridRoles.DataSource = roles.Data!.ToList(); }
        }
    }

    private async Task OpenUserDialog(UserDto? existing)
    {
        var roles = await _roleService.GetAllAsync();
        var emps  = await _empService.GetAllAsync();
        if (!roles.IsSuccess) return;
        using var dlg = new UserEditDialog(existing, roles.Data!.ToList(), emps.IsSuccess ? emps.Data!.ToList() : new());
        if (dlg.ShowDialog() != DialogResult.OK) return;
        Result r;
        if (existing == null)
        {
            var createResult = await _userService.CreateAsync(dlg.GetCreateDto());
            r = createResult.IsSuccess ? Result.Success() : Result.Failure(createResult.ErrorMessage!);
        }
        else r = await _userService.UpdateAsync(dlg.GetUpdateDto());
        if (r.IsSuccess) await LoadAsync();
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private async Task EditUser()
    {
        if (_gridUsers.SelectedRows.Count == 0) return;
        await OpenUserDialog((UserDto)_gridUsers.SelectedRows[0].DataBoundItem);
    }

    private async Task DeleteUser()
    {
        if (_gridUsers.SelectedRows.Count == 0) return;
        var u = (UserDto)_gridUsers.SelectedRows[0].DataBoundItem;
        if (MessageBox.Show($"Delete user '{u.Username}'?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        var r = await _userService.DeleteAsync(u.Id, DesktopSession.UserId);
        if (r.IsSuccess) await LoadAsync();
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private async Task LockUser(bool lockUser)
    {
        if (_gridUsers.SelectedRows.Count == 0) return;
        var u = (UserDto)_gridUsers.SelectedRows[0].DataBoundItem;
        var r = lockUser ? await _userService.LockAsync(u.Id) : await _userService.UnlockAsync(u.Id);
        if (r.IsSuccess) await LoadAsync();
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private async Task ResetPassword()
    {
        if (_gridUsers.SelectedRows.Count == 0) return;
        var u = (UserDto)_gridUsers.SelectedRows[0].DataBoundItem;
        var pwd = Microsoft.VisualBasic.Interaction.InputBox("Enter new password (min 8 chars, upper, digit):", "Reset Password", "");
        if (string.IsNullOrWhiteSpace(pwd)) return;
        var r = await _userService.ResetPasswordAsync(u.Id, pwd, DesktopSession.UserId);
        if (r.IsSuccess) MessageBox.Show("Password reset successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private async Task OpenRoleDialog(RoleDto? existing)
    {
        using var dlg = new RoleEditDialog(existing);
        if (dlg.ShowDialog() != DialogResult.OK) return;
        var r = await _roleService.SaveAsync(dlg.GetDto());
        if (r.IsSuccess) await LoadAsync();
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private async Task EditRole()
    {
        if (_gridRoles.SelectedRows.Count == 0) return;
        await OpenRoleDialog((RoleDto)_gridRoles.SelectedRows[0].DataBoundItem);
    }

    private async Task DeleteRole()
    {
        if (_gridRoles.SelectedRows.Count == 0) return;
        var role = (RoleDto)_gridRoles.SelectedRows[0].DataBoundItem;
        if (MessageBox.Show($"Delete role '{role.Name}'?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        var r = await _roleService.DeleteAsync(role.Id);
        if (r.IsSuccess) await LoadAsync();
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private async Task ManagePermissions()
    {
        if (_gridRoles.SelectedRows.Count == 0) return;
        var role = (RoleDto)_gridRoles.SelectedRows[0].DataBoundItem;
        var permsResult = await _roleService.GetPermissionsForRoleAsync(role.Id);
        if (!permsResult.IsSuccess) return;
        using var dlg = new PermissionsDialog(role, permsResult.Data!.ToList(), _roleService);
        dlg.ShowDialog();
    }
}

public class RoleForm : Form { public RoleForm() { BackColor = AppTheme.FormBg; } }

internal class UserEditDialog : Form
{
    private readonly LabeledTextBox _txtUsername;
    private readonly LabeledTextBox _txtFullName;
    private readonly LabeledTextBox _txtEmail;
    private readonly LabeledTextBox _txtPassword;
    private readonly ComboBox _cmbRole;
    private readonly ComboBox _cmbEmployee;
    private readonly CheckBox _chkActive;
    private readonly UserDto? _existing;

    public UserEditDialog(UserDto? existing, List<RoleDto> roles, List<EmployeeListItemDto> employees)
    {
        _existing = existing;
        Text = existing == null ? "Add User" : "Edit User";
        Size = new Size(440, 420); StartPosition = FormStartPosition.CenterParent;
        BackColor = AppTheme.CardBg; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;

        _txtUsername = new LabeledTextBox { LabelText = "Username *",  Location = new Point(20, 20),  Width = 380 };
        _txtFullName = new LabeledTextBox { LabelText = "Full Name *", Location = new Point(20, 100), Width = 380 };
        _txtEmail    = new LabeledTextBox { LabelText = "Email *",     Location = new Point(20, 180), Width = 380 };
        _txtPassword = new LabeledTextBox { LabelText = "Password *",  Location = new Point(20, 260), Width = 380 };
        _txtPassword.IsPassword = true;

        var lblRole = new Label { Text = "Role *", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(20, 336), AutoSize = true };
        _cmbRole = new ComboBox { Location = new Point(20, 354), Width = 180, Font = AppTheme.BodyFont, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbRole.DataSource = roles; _cmbRole.DisplayMember = "Name"; _cmbRole.ValueMember = "Id";

        _chkActive = new CheckBox { Text = "Active", Location = new Point(220, 354), Checked = true, Font = AppTheme.BodyFont, ForeColor = AppTheme.BodyText };

        var btnSave   = new AppButton { Text = "💾 Save", Width = 100, Location = new Point(130, 386) };
        var btnCancel = new AppButton { Text = "Cancel",  Width = 80,  Location = new Point(240, 386) };
        btnCancel.SetSecondary();
        btnSave.Click   += (s, e) => { if (ValidateFields()) DialogResult = DialogResult.OK; };
        btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;

        if (existing != null)
        {
            _txtUsername.Value = existing.Username; _txtFullName.Value = existing.FullName;
            _txtEmail.Value = existing.Email; _txtPassword.Value = "********";
            _cmbRole.SelectedValue = existing.RoleId; _chkActive.Checked = existing.IsActive;
        }
        _cmbEmployee = new ComboBox(); // hidden

        Controls.AddRange([_txtUsername, _txtFullName, _txtEmail, _txtPassword, lblRole, _cmbRole, _chkActive, btnSave, btnCancel]);
    }

    private bool ValidateFields()
    {
        _txtUsername.ClearError(); _txtFullName.ClearError(); _txtEmail.ClearError(); _txtPassword.ClearError();
        var ok = true;
        if (string.IsNullOrWhiteSpace(_txtUsername.Value)) { _txtUsername.ShowError("Required"); ok = false; }
        if (string.IsNullOrWhiteSpace(_txtFullName.Value)) { _txtFullName.ShowError("Required"); ok = false; }
        if (string.IsNullOrWhiteSpace(_txtEmail.Value))    { _txtEmail.ShowError("Required"); ok = false; }
        return ok;
    }

    public CreateUserDto GetCreateDto() => new()
    {
        Username = _txtUsername.Value.Trim(), FullName = _txtFullName.Value.Trim(),
        Email = _txtEmail.Value.Trim(), Password = _txtPassword.Value,
        RoleId = (int)_cmbRole.SelectedValue!, IsActive = _chkActive.Checked
    };

    public UpdateUserDto GetUpdateDto() => new()
    {
        Id = _existing!.Id, FullName = _txtFullName.Value.Trim(),
        Email = _txtEmail.Value.Trim(), RoleId = (int)_cmbRole.SelectedValue!, IsActive = _chkActive.Checked
    };
}

internal class RoleEditDialog : Form
{
    private readonly LabeledTextBox _txtName;
    private readonly LabeledTextBox _txtDesc;
    private readonly RoleDto? _existing;
    public RoleEditDialog(RoleDto? existing) { _existing = existing; Text = existing == null ? "Add Role" : "Edit Role"; Size = new Size(380, 240); StartPosition = FormStartPosition.CenterParent; BackColor = AppTheme.CardBg; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
        _txtName = new LabeledTextBox { LabelText = "Role Name *", Location = new Point(20, 20), Width = 320 };
        _txtDesc = new LabeledTextBox { LabelText = "Description", Location = new Point(20, 100), Width = 320 };
        var btnSave = new AppButton { Text = "💾 Save", Width = 100, Location = new Point(90, 180) }; var btnCancel = new AppButton { Text = "Cancel", Width = 80, Location = new Point(200, 180) }; btnCancel.SetSecondary();
        btnSave.Click += (s, e) => { if (!string.IsNullOrWhiteSpace(_txtName.Value)) DialogResult = DialogResult.OK; else _txtName.ShowError("Required"); }; btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;
        if (existing != null) { _txtName.Value = existing.Name; _txtDesc.Value = existing.Description ?? ""; }
        Controls.AddRange([_txtName, _txtDesc, btnSave, btnCancel]); }
    public RoleDto GetDto() => new() { Id = _existing?.Id ?? 0, Name = _txtName.Value.Trim(), Description = _txtDesc.Value.Trim() };
}

internal class PermissionsDialog : Form
{
    private readonly CheckedListBox _clb;
    private readonly List<PermissionDto> _permissions;
    private readonly IRoleService _roleService;
    private readonly RoleDto _role;

    public PermissionsDialog(RoleDto role, List<PermissionDto> permissions, IRoleService roleService)
    {
        _role = role; _permissions = permissions; _roleService = roleService;
        Text = $"Permissions — {role.Name}"; Size = new Size(440, 480);
        StartPosition = FormStartPosition.CenterParent; BackColor = AppTheme.CardBg;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;

        var lblTitle = new Label { Text = $"Set permissions for role: {role.Name}", Font = AppTheme.BodyFont, ForeColor = AppTheme.BodyText, Location = new Point(20, 14), AutoSize = true };
        _clb = new CheckedListBox { Location = new Point(20, 40), Size = new Size(380, 360), CheckOnClick = true, Font = AppTheme.BodyFont };
        foreach (var p in permissions) { _clb.Items.Add(p.DisplayName, p.IsGranted); }

        var btnSave   = new AppButton { Text = "💾 Save", Width = 100, Location = new Point(120, 412) };
        var btnCancel = new AppButton { Text = "Cancel",  Width = 80,  Location = new Point(230, 412) };
        btnCancel.SetSecondary();
        btnSave.Click   += async (s, e) => await SaveAsync();
        btnCancel.Click += (s, e) => Close();
        Controls.AddRange([lblTitle, _clb, btnSave, btnCancel]);
    }

    private async Task SaveAsync()
    {
        var grantedIds = _clb.CheckedIndices.Cast<int>().Select(i => _permissions[i].Id).ToList();
        var r = await _roleService.SavePermissionsAsync(_role.Id, grantedIds);
        if (r.IsSuccess) { MessageBox.Show("Permissions saved.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information); Close(); }
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
