/* ── Admin Users Management JavaScript ── */

var allItems = [], roles = [], employees = [];

$(function () {
    $.when(
        $.getJSON('/api/users/roles', function (d) { roles = d || []; }),
        $.getJSON('/api/employees', function (d) { employees = d || []; })
    ).always(function () { loadItems(); });
});

function loadItems() {
    $.getJSON('/api/users', function (d) { allItems = d || []; filterTable(); })
     .fail(function () { $('#tbody').html('<tr><td colspan="8" class="text-danger text-center py-3">Failed to load users.</td></tr>'); });
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
    if (!data || !data.length) { $('#tbody').html('<tr><td colspan="8" class="text-center text-muted py-3">No users found.</td></tr>'); return; }
    var html = '';
    data.forEach(function (u) {
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
        html += '<tr>'
            + '<td class="fw-semibold">' + username + '</td>'
            + '<td>' + fullName + '</td>'
            + '<td class="text-muted small">' + email + '</td>'
            + '<td><span class="badge bg-primary">' + roleName + '</span></td>'
            + '<td class="text-muted small">' + employeeName + '</td>'
            + '<td class="text-muted small">' + (lastLogin ? new Date(lastLogin).toLocaleString() : 'Never') + '</td>'
            + '<td>' + status + '</td>'
            + '<td>'
            + '<button class="btn btn-sm btn-outline-primary me-1" onclick="editUser(' + id + ')" title="Edit"><i class="fa fa-pencil"></i></button>'
            + '<button class="btn btn-sm btn-outline-warning me-1" onclick="openPwModal(' + id + ')" title="Reset Password"><i class="fa fa-key"></i></button>'
            + (isLocked ? '<button class="btn btn-sm btn-outline-success me-1" onclick="unlock(' + id + ')" title="Unlock"><i class="fa fa-unlock"></i></button>' : '')
            + '<button class="btn btn-sm btn-outline-danger" onclick="deleteUser(' + id + ')" title="Delete"><i class="fa fa-trash"></i></button>'
            + '</td></tr>';
    });
    $('#tbody').html(html);
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

function openModal() {
    buildDropdowns();
    $('#userId').val(0); $('#uUsername').val('').prop('disabled', false); $('#uFullName').val('');
    $('#uEmail').val(''); $('#uPassword').val(''); $('#uActive').prop('checked', true);
    $('#passwordField').show(); $('#modalTitle').text('Add User');
    new bootstrap.Modal('#editModal').show();
}

function editUser(id) {
    var u = allItems.find(function(x){ return (x.Id !== undefined ? x.Id : x.id) === id; });
    if (!u) return;
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
    $('#uActive').prop('checked', isActive); $('#passwordField').hide();
    $('#modalTitle').text('Edit User');
    new bootstrap.Modal('#editModal').show();
}

function saveUser() {
    var id = parseInt($('#userId').val()) || 0;
    if (id) {
        var dto = { Id: id, Email: $('#uEmail').val().trim(), FullName: $('#uFullName').val().trim(), RoleId: parseInt($('#uRole').val()), EmployeeId: parseInt($('#uEmployee').val())||null, IsActive: $('#uActive').is(':checked') };
        if (!dto.Email || !dto.FullName || !dto.RoleId) { notifyError('Email, Full Name and Role are required.', 'Validation Error'); return; }
        $.ajax({ url: '/api/users/' + id, type: 'PUT', contentType: 'application/json', data: JSON.stringify(dto),
            success: function () { 
                bootstrap.Modal.getInstance('#editModal').hide(); 
                notifySuccess('User updated successfully.');
                loadItems(); 
            },
            error: function (xhr) { notifyError(xhr.responseText || 'Save failed.'); }
        });
    } else {
        var dto = { Username: $('#uUsername').val().trim(), FullName: $('#uFullName').val().trim(), Email: $('#uEmail').val().trim(), Password: $('#uPassword').val(), RoleId: parseInt($('#uRole').val()), EmployeeId: parseInt($('#uEmployee').val())||null, IsActive: $('#uActive').is(':checked') };
        if (!dto.Username || !dto.Email || !dto.FullName || !dto.Password || !dto.RoleId) { notifyError('All fields are required.', 'Validation Error'); return; }
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
