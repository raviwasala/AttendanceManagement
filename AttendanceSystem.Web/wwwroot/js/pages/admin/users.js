/* ── Admin Users Management JavaScript ── */

var allItems = [], roles = [], employees = [];

$(function () {
    $.when(
        $.getJSON('/api/users/roles', function (d) { roles = d; }),
        $.getJSON('/api/employees', function (d) { employees = d; })
    ).then(function () { loadItems(); });
});

function loadItems() {
    $.getJSON('/api/users', function (d) { allItems = d; filterTable(); })
     .fail(function () { $('#tbody').html('<tr><td colspan="8" class="text-danger text-center py-3">Failed to load.</td></tr>'); });
}

function filterTable() {
    var q = $('#searchBox').val().toLowerCase(); var s = $('#statusFilter').val();
    renderTable(allItems.filter(function (u) {
        return (!q || u.Username.toLowerCase().includes(q) || u.FullName.toLowerCase().includes(q) || u.Email.toLowerCase().includes(q))
            && (s === '' || String(u.IsActive) === s);
    }));
}

function renderTable(data) {
    if (!data.length) { $('#tbody').html('<tr><td colspan="8" class="text-center text-muted py-3">No users found.</td></tr>'); return; }
    var html = '';
    data.forEach(function (u) {
        var status = u.IsLocked ? '<span class="badge bg-danger">Locked</span>' : u.IsActive ? '<span class="badge bg-success">Active</span>' : '<span class="badge bg-secondary">Inactive</span>';
        html += '<tr>'
            + '<td class="fw-semibold">' + u.Username + '</td>'
            + '<td>' + u.FullName + '</td>'
            + '<td class="text-muted small">' + u.Email + '</td>'
            + '<td><span class="badge bg-primary">' + u.RoleName + '</span></td>'
            + '<td class="text-muted small">' + (u.EmployeeName || '—') + '</td>'
            + '<td class="text-muted small">' + (u.LastLoginAt ? new Date(u.LastLoginAt).toLocaleString() : 'Never') + '</td>'
            + '<td>' + status + '</td>'
            + '<td>'
            + '<button class="btn btn-sm btn-outline-primary me-1" onclick="editUser(' + u.Id + ')" title="Edit"><i class="fa fa-pencil"></i></button>'
            + '<button class="btn btn-sm btn-outline-warning me-1" onclick="openPwModal(' + u.Id + ')" title="Reset Password"><i class="fa fa-key"></i></button>'
            + (u.IsLocked ? '<button class="btn btn-sm btn-outline-success me-1" onclick="unlock(' + u.Id + ')" title="Unlock"><i class="fa fa-unlock"></i></button>' : '')
            + '<button class="btn btn-sm btn-outline-danger" onclick="deleteUser(' + u.Id + ')" title="Delete"><i class="fa fa-trash"></i></button>'
            + '</td></tr>';
    });
    $('#tbody').html(html);
}

function buildDropdowns() {
    var rOpts = '<option value="">-- Role --</option>';
    roles.forEach(function (r) { rOpts += '<option value="' + r.Id + '">' + r.Name + '</option>'; });
    $('#uRole').html(rOpts);
    var eOpts = '<option value="">-- None --</option>';
    employees.forEach(function (e) { eOpts += '<option value="' + e.Id + '">' + e.EmployeeCode + ' - ' + e.FullName + '</option>'; });
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
    var u = allItems.find(function(x){return x.Id===id;});
    buildDropdowns();
    $('#userId').val(u.Id); $('#uUsername').val(u.Username).prop('disabled', true);
    $('#uFullName').val(u.FullName); $('#uEmail').val(u.Email);
    $('#uRole').val(u.RoleId); $('#uEmployee').val(u.EmployeeId || '');
    $('#uActive').prop('checked', u.IsActive); $('#passwordField').hide();
    $('#modalTitle').text('Edit User');
    new bootstrap.Modal('#editModal').show();
}

function saveUser() {
    var id = parseInt($('#userId').val()) || 0;
    var uid = window.getCurrentUserId();
    if (id) {
        var dto = { Id: id, Email: $('#uEmail').val().trim(), FullName: $('#uFullName').val().trim(), RoleId: parseInt($('#uRole').val()), EmployeeId: parseInt($('#uEmployee').val())||null, IsActive: $('#uActive').is(':checked') };
        if (!dto.Email || !dto.FullName || !dto.RoleId) { alert('Email, Full Name and Role are required.'); return; }
        $.ajax({ url: '/api/users/' + id, type: 'PUT', contentType: 'application/json', data: JSON.stringify(dto),
            success: function () { bootstrap.Modal.getInstance('#editModal').hide(); loadItems(); },
            error: function (xhr) { alert('Error: ' + (xhr.responseText || 'Save failed.')); }
        });
    } else {
        var dto = { Username: $('#uUsername').val().trim(), FullName: $('#uFullName').val().trim(), Email: $('#uEmail').val().trim(), Password: $('#uPassword').val(), RoleId: parseInt($('#uRole').val()), EmployeeId: parseInt($('#uEmployee').val())||null, IsActive: $('#uActive').is(':checked') };
        if (!dto.Username || !dto.Email || !dto.FullName || !dto.Password || !dto.RoleId) { alert('All fields are required.'); return; }
        $.ajax({ url: '/api/users', type: 'POST', contentType: 'application/json', data: JSON.stringify(dto),
            success: function () { bootstrap.Modal.getInstance('#editModal').hide(); loadItems(); },
            error: function (xhr) { alert('Error: ' + (xhr.responseText || 'Save failed.')); }
        });
    }
}

function openPwModal(id) { $('#pwUserId').val(id); $('#pwNew').val(''); new bootstrap.Modal('#pwModal').show(); }

function doResetPw() {
    var pw = $('#pwNew').val();
    if (!pw || pw.length < 8) { alert('Password must be at least 8 characters.'); return; }
    var uid = window.getCurrentUserId();
    $.ajax({ url: '/api/users/' + $('#pwUserId').val() + '/reset-password?resetBy=' + uid, type: 'POST', contentType: 'application/json', data: JSON.stringify({ NewPassword: pw }),
        success: function () { bootstrap.Modal.getInstance('#pwModal').hide(); alert('Password reset successfully.'); },
        error: function (xhr) { alert('Error: ' + (xhr.responseText || 'Reset failed.')); }
    });
}

function unlock(id) {
    $.ajax({ url: '/api/users/' + id + '/unlock', type: 'POST',
        success: function () { loadItems(); },
        error: function (xhr) { alert('Error: ' + (xhr.responseText || 'Unlock failed.')); }
    });
}

function deleteUser(id) {
    if (!confirm('Delete this user?')) return;
    var uid = window.getCurrentUserId();
    $.ajax({ url: '/api/users/' + id + '?deletedBy=' + uid, type: 'DELETE',
        success: function () { loadItems(); },
        error: function (xhr) { alert('Error: ' + (xhr.responseText || 'Delete failed.')); }
    });
}
