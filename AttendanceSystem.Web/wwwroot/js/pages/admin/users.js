/* ── Admin Users Management JavaScript ── */

var allItems = [], roles = [], employees = [];

$(function () {
    $.when(
        $.getJSON('/api/users/roles', function (d) { roles = d || []; }),
        $.getJSON('/api/employees', function (d) { employees = d || []; })
    ).always(function () { loadItems(); });

    // Delegated and bound once: Select2 re-creates the element, so binding inside
    // buildDropdowns would stack a fresh handler on every modal open.
    $(document).on('change', '#uRole', syncApprovalFields);
});

function loadItems() {
    $.getJSON('/api/users', function (d) { allItems = d || []; filterTable(); })
     .fail(function () { $('#tbody').html('<tr><td colspan="9" class="text-danger text-center py-3">Failed to load users.</td></tr>'); });
}

function filterTable() {
    var q = $('#searchBox').val().toLowerCase(); var s = $('#statusFilter').val();
    renderTable(allItems.filter(function (u) {
        var username = (u.Username || u.username || '').toLowerCase();
        var fullName = (u.FullName || u.fullName || '').toLowerCase();
        var email = (u.Email || u.email || '').toLowerCase();
        var activeStr = String(u.IsActive !== undefined ? u.IsActive : u.isActive);
        return (!q || username.includes(q) || fullName.includes(q) || email.includes(q))
            && (s === '' || activeStr === s);
    }));
}

function renderTable(data) {
    amsPage('#tbody', data, function (u) {
        var id = u.Id !== undefined ? u.Id : u.id;
        var username = u.Username || u.username || '';
        var fullName = u.FullName || u.fullName || '';
        var email = u.Email || u.email || '';
        var roleName = u.RoleName || u.roleName || '—';
        var employeeName = u.EmployeeName || u.employeeName || '—';
        var isActive = u.IsActive !== undefined ? u.IsActive : u.isActive;
        var isLocked = u.IsLocked !== undefined ? u.IsLocked : u.isLocked;
        var lastLogin = u.LastLoginAt || u.lastLoginAt;

        var status = isLocked ? '<span class="badge bg-danger">Locked</span>' : isActive ? '<span class="badge bg-success">Active</span>' : '<span class="badge bg-secondary">Inactive</span>';
        return '<tr>'
            + '<td class="fw-semibold">' + esc(username) + '</td>'
            + '<td>' + esc(fullName) + '</td>'
            + '<td class="text-muted small">' + esc(email) + '</td>'
            + '<td><span class="badge bg-primary">' + esc(roleName) + '</span></td>'
            + '<td class="text-muted small">' + esc(employeeName) + '</td>'
            + '<td class="text-muted small">' + esc(u.ApprovalScopeDisplay || u.approvalScopeDisplay || '—') + '</td>'
            + '<td class="text-muted small">' + (lastLogin ? new Date(lastLogin).toLocaleString() : 'Never') + '</td>'
            + '<td>' + status + '</td>'
            + '<td>'
            + '<button class="btn btn-sm btn-outline-primary me-1" onclick="editUser(' + id + ')" title="Edit"><i class="fa fa-pencil"></i></button>'
            + '<button class="btn btn-sm btn-outline-warning me-1" onclick="openPwModal(' + id + ')" title="Reset Password"><i class="fa fa-key"></i></button>'
            + (isLocked ? '<button class="btn btn-sm btn-outline-success me-1" onclick="unlock(' + id + ')" title="Unlock"><i class="fa fa-unlock"></i></button>' : '')
            + '<button class="btn btn-sm btn-outline-danger" onclick="deleteUser(' + id + ')" title="Delete"><i class="fa fa-trash"></i></button>'
            + '</td></tr>';
    }, { colspan: 9, empty: 'No users found.', label: 'user' });
}

function buildDropdowns() {
    var rOpts = '<option value="">-- Role --</option>';
    roles.forEach(function (r) {
        var id = r.Id !== undefined ? r.Id : r.id;
        var name = r.Name || r.name || '';
        rOpts += '<option value="' + id + '">' + name + '</option>';
    });
    $('#uRole').html(rOpts);
    var eOpts = '<option value="">-- None --</option>';
    employees.forEach(function (e) {
        var id = e.Id !== undefined ? e.Id : e.id;
        var code = e.EmployeeCode || e.employeeCode || '';
        var name = e.FullName || e.fullName || '';
        eOpts += '<option value="' + id + '">' + (code ? code + ' - ' : '') + name + '</option>';
    });
    $('#uEmployee').html(eOpts);
}

/* Approval scope and the employee link only matter for roles that can approve something.
   Driven off the role dropdown so the form re-shapes as soon as the role changes, rather
   than asking everyone a question that applies to a handful of people. */
function roleCanApprove() {
    var rid = parseInt($('#uRole').val()) || 0;
    var r = roles.find(function (x) { return (x.Id !== undefined ? x.Id : x.id) === rid; });
    return !!(r && (r.CanApprove !== undefined ? r.CanApprove : r.canApprove));
}

function syncApprovalFields() {
    var can = roleCanApprove();
    $('#uScopeWrap').toggleClass('d-none', !can);
    $('#uEmployeeReq').toggleClass('d-none', !can);
    $('#uEmployeeHint').toggleClass('d-none', !can);
    // A hidden picker must not keep a narrowed value: dropping the approve permission and
    // leaving the user silently restricted is the confusing half of this.
    if (!can) $('#uApprovalScope').val('1');
}

/* Re-reads roles before showing the modal. Whether a role can approve is decided on the
   Roles screen, so a set cached at page load goes stale the moment somebody ticks an
   Authorise box — and the stale answer decides whether this form asks for approval scope
   at all. Falls back to what is already cached if the refresh fails. */
function withFreshRoles(then) {
    $.getJSON('/api/users/roles')
        .done(function (d) { if (d) roles = d; })
        .always(then);
}

function openModal() {
    withFreshRoles(function () {
        buildDropdowns();
        $('#userId').val(0); $('#uUsername').val('').prop('disabled', false); $('#uFullName').val('');
        $('#uEmail').val(''); $('#uPassword').val(''); $('#uActive').prop('checked', true);
        $('#uApprovalScope').val('1'); syncApprovalFields();
        $('#passwordField').show(); $('#modalTitle').text('Add User');
        new bootstrap.Modal('#editModal').show();
    });
}

function editUser(id) {
    var u = allItems.find(function(x){ return (x.Id !== undefined ? x.Id : x.id) === id; });
    if (!u) return;
    withFreshRoles(function () {
        buildDropdowns();
        var uid = u.Id !== undefined ? u.Id : u.id;
        var username = u.Username || u.username || '';
        var fullName = u.FullName || u.fullName || '';
        var email = u.Email || u.email || '';
        var roleId = u.RoleId !== undefined ? u.RoleId : u.roleId;
        var employeeId = u.EmployeeId !== undefined ? u.EmployeeId : u.employeeId;
        var isActive = u.IsActive !== undefined ? u.IsActive : u.isActive;

        $('#userId').val(uid); $('#uUsername').val(username).prop('disabled', true);
        $('#uFullName').val(fullName); $('#uEmail').val(email);
        $('#uRole').val(roleId); $('#uEmployee').val(employeeId || '');
        $('#uActive').prop('checked', isActive);
        $('#uApprovalScope').val(String(u.ApprovalScope !== undefined ? u.ApprovalScope : (u.approvalScope || 1)));
        syncApprovalFields();
        $('#passwordField').hide();
        $('#modalTitle').text('Edit User');
        new bootstrap.Modal('#editModal').show();
    });
}

/* Mirrors the server rule so the message arrives before the round trip. The server still
   enforces it — this check only exists to be quicker, not to be the only guard. */
function employeeLinkOk(employeeId) {
    if (employeeId || !roleCanApprove()) return true;
    notifyError('This role can approve leave or overtime, so the user must be linked to an '
        + 'employee. Without it the system cannot tell whose requests are their own.',
        'Employee Required');
    return false;
}

function saveUser() {
    var id = parseInt($('#userId').val()) || 0;
    if (id) {
        var dto = { Id: id, Email: $('#uEmail').val().trim(), FullName: $('#uFullName').val().trim(), RoleId: parseInt($('#uRole').val()), EmployeeId: parseInt($('#uEmployee').val())||null, IsActive: $('#uActive').is(':checked'), ApprovalScope: parseInt($('#uApprovalScope').val()) || 1 };
        if (!dto.Email || !dto.FullName || !dto.RoleId) { notifyError('Email, Full Name and Role are required.', 'Validation Error'); return; }
        if (!employeeLinkOk(dto.EmployeeId)) return;
        $.ajax({ url: '/api/users/' + id, type: 'PUT', contentType: 'application/json', data: JSON.stringify(dto),
            success: function () { 
                bootstrap.Modal.getInstance('#editModal').hide(); 
                notifySuccess('User updated successfully.');
                loadItems(); 
            },
            error: function (xhr) { notifyError(xhr.responseText || 'Save failed.'); }
        });
    } else {
        var dto = { Username: $('#uUsername').val().trim(), FullName: $('#uFullName').val().trim(), Email: $('#uEmail').val().trim(), Password: $('#uPassword').val(), RoleId: parseInt($('#uRole').val()), EmployeeId: parseInt($('#uEmployee').val())||null, IsActive: $('#uActive').is(':checked'), ApprovalScope: parseInt($('#uApprovalScope').val()) || 1 };
        if (!dto.Username || !dto.Email || !dto.FullName || !dto.Password || !dto.RoleId) { notifyError('All fields are required.', 'Validation Error'); return; }
        if (!employeeLinkOk(dto.EmployeeId)) return;
        $.ajax({ url: '/api/users', type: 'POST', contentType: 'application/json', data: JSON.stringify(dto),
            success: function () { 
                bootstrap.Modal.getInstance('#editModal').hide(); 
                notifySuccess('User created successfully.');
                loadItems(); 
            },
            error: function (xhr) { notifyError(xhr.responseText || 'Save failed.'); }
        });
    }
}

function openPwModal(id) { $('#pwUserId').val(id); $('#pwNew').val(''); new bootstrap.Modal('#pwModal').show(); }

function doResetPw() {
    var pw = $('#pwNew').val();
    if (!pw || pw.length < 8) { notifyError('Password must be at least 8 characters.', 'Validation Error'); return; }
    $.ajax({ url: '/api/users/' + $('#pwUserId').val() + '/reset-password', type: 'POST', contentType: 'application/json', data: JSON.stringify({ NewPassword: pw }),
        success: function () { 
            bootstrap.Modal.getInstance('#pwModal').hide(); 
            notifySuccess('Password reset successfully.'); 
        },
        error: function (xhr) { notifyError(xhr.responseText || 'Reset failed.'); }
    });
}

function unlock(id) {
    $.ajax({ url: '/api/users/' + id + '/unlock', type: 'POST',
        success: function () { 
            notifySuccess('User account unlocked successfully.');
            loadItems(); 
        },
        error: function (xhr) { notifyError(xhr.responseText || 'Unlock failed.'); }
    });
}

function deleteUser(id) {
    notifyConfirm({ title: 'Delete User Account', text: 'Are you sure you want to delete this user account?', confirmText: 'Delete', icon: 'warning' }, function () {
        $.ajax({ url: '/api/users/' + id, type: 'DELETE',
            success: function () { 
                notifySuccess('User deleted successfully.');
                loadItems(); 
            },
            error: function (xhr) { notifyError(xhr.responseText || 'Delete failed.'); }
        });
    });
}
