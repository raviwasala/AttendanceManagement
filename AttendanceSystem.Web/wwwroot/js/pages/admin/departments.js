/* ── Admin Departments Management JavaScript ── */

var allDepts = [];

$(function () { loadDepts(); });

function loadDepts() {
    $.getJSON('/api/departments', function (data) {
        allDepts = data || [];
        // Re-apply whatever is in the filter boxes. Rendering the raw list here meant that
        // after a save or delete the table silently ignored the search the user had typed.
        filterTable();
    }).fail(function () {
        $('#deptBody').html('<tr><td colspan="6" class="text-danger text-center">Failed to load departments.</td></tr>');
    });
}

function renderTable(data) {
    amsPage('#deptBody', data, function (d, i) {
        var id = d.Id !== undefined ? d.Id : d.id;
        var name = d.Name || d.name || '';
        var desc = d.Description || d.description || '';
        var count = d.EmployeeCount !== undefined ? d.EmployeeCount : (d.employeeCount || 0);
        var isActive = d.IsActive !== undefined ? d.IsActive : d.isActive;

        return '<tr>'
            + '<td class="text-muted">' + (i + 1) + '</td>'
            + '<td class="fw-semibold">' + esc(name) + '</td>'
            + '<td class="text-muted small">' + (desc ? esc(desc) : '—') + '</td>'
            + '<td><span class="badge bg-secondary">' + esc(count) + '</span></td>'
            + '<td>' + (isActive ? '<span class="badge bg-success">Active</span>' : '<span class="badge bg-danger">Inactive</span>') + '</td>'
            + '<td>'
            + '<button class="btn btn-sm btn-outline-secondary me-1" onclick="openApprovers(' + id + ', \''
            + esc(name).replace(/'/g, "\\'") + '\')" title="Leave and overtime approvers">'
            // fa-shield, not fa-user-shield: this theme ships Font Awesome 4, where the
            // latter does not exist and renders as an empty button.
            + '<i class="fa fa-shield"></i></button>'
            + '<button class="btn btn-sm btn-outline-primary me-1" onclick="editDept(' + id + ')" title="Edit"><i class="fa fa-pencil"></i></button>'
            + '<button class="btn btn-sm btn-outline-danger" onclick="deleteDept(' + id + ')" title="Delete"><i class="fa fa-trash"></i></button>'
            + '</td></tr>';
    }, { colspan: 6, empty: 'No departments found.', label: 'department' });
}

function filterTable() {
    var q = $('#searchBox').val().toLowerCase();
    var s = $('#statusFilter').val();
    var filtered = allDepts.filter(function (d) {
        var name = (d.Name || d.name || '').toLowerCase();
        var desc = (d.Description || d.description || '').toLowerCase();
        var isActive = String(d.IsActive !== undefined ? d.IsActive : d.isActive);
        var matchQ = !q || name.includes(q) || desc.includes(q);
        var matchS = s === '' || isActive === s;
        return matchQ && matchS;
    });
    renderTable(filtered);
}

// Loaded once for the head picker rather than on every modal open.
var deptEmployees = [];

$(function () {
    $.getJSON('/api/employees', function (d) {
        deptEmployees = (d || []);
        $('#deptHead').append(deptEmployees.map(function (e) {
            return '<option value="' + esc(e.Id) + '">'
                 + esc(e.FullName) + ' (' + esc(e.EmployeeCode) + ')</option>';
        }).join(''));
    });
});

function openModal(id, name, desc, active, headId) {
    $('#deptId').val(id || 0);
    $('#deptName').val(name || '');
    $('#deptDesc').val(desc || '');
    $('#deptActive').prop('checked', active !== false);
    $('#deptHead').val(headId || '');
    $('#modalTitle').text(id ? 'Edit Department' : 'Add Department');
    new bootstrap.Modal('#deptModal').show();
}

function editDept(id) {
    $.getJSON('/api/departments/' + id, function (d) {
        var idVal = d.Id !== undefined ? d.Id : d.id;
        var name = d.Name || d.name || '';
        var desc = d.Description || d.description || '';
        var isActive = d.IsActive !== undefined ? d.IsActive : d.isActive;
        openModal(idVal, name, desc, isActive, d.HeadEmployeeId);
    });
}

/* ── Approvers ────────────────────────────────────────────────────────────────
   Kept out of the department form: editing a department is routine, deciding who
   may approve its leave is a permission change. */
var apDeptId = 0;
// UserId → true when that user already approves everywhere. Naming such a person here
// changes nothing, so the dialog says so instead of implying it granted something.
var apCompanyWide = {};

function openApprovers(id, name) {
    apDeptId = id;
    $('#apDeptName').text(name);
    $('#apBody').html('<tr><td colspan="3" class="text-center py-3 text-muted">Loading…</td></tr>');

    $.getJSON('/api/users', function (users) {
        apCompanyWide = {};
        $('#apUser').html((users || [])
            .filter(function (u) { return u.IsActive; })
            .map(function (u) {
                var wide = (u.ApprovalScope || 1) === 1;
                apCompanyWide[u.Id] = wide;
                return '<option value="' + esc(u.Id) + '">'
                     + esc(u.FullName) + ' (' + esc(u.Username) + ')'
                     + (wide ? ' — already approves all departments' : '') + '</option>';
            }).join(''));
        loadApprovers();
    });

    loadApprovers();
    new bootstrap.Modal('#approverModal').show();
}

function loadApprovers() {
    $.getJSON('/api/departments/' + apDeptId + '/approvers', function (rows) {
        if (!rows || !rows.length) {
            $('#apBody').html('<tr><td colspan="3" class="text-center py-3 text-muted">'
                + 'No named approvers. The department head, and anyone who approves '
                + 'company-wide, can still decide requests here.</td></tr>');
            return;
        }
        $('#apBody').html(rows.map(function (a) {
            var wide = apCompanyWide[a.UserId];
            return '<tr><td class="ps-3 small">' + esc(a.FullName)
                 + (wide ? ' <span class="badge bg-light text-muted fw-normal">'
                         + 'approves all departments</span>' : '') + '</td>'
                 + '<td class="small text-muted">' + esc(a.UserName) + '</td>'
                 + '<td class="pe-3 text-center">'
                 + '<button class="btn btn-sm btn-outline-danger py-0 px-2"'
                 + ' onclick="removeApprover(' + a.Id + ')" title="Remove">'
                 + '<i class="fa fa-times"></i></button></td></tr>';
        }).join(''));
    }).fail(function (xhr) {
        $('#apBody').html('<tr><td colspan="3" class="text-danger text-center py-3">'
            + esc(xhr.responseText || 'Failed to load.') + '</td></tr>');
    });
}

function addApprover() {
    var ids = $('#apUser').val() || [];
    if (!ids.length) { notifyError('Choose at least one user.'); return; }

    // One request per user rather than a batch endpoint: the service refuses a duplicate by
    // name ("X is already an approver"), and reporting that per person is more useful than a
    // single failure that hides which of the five was the problem.
    var added = 0, failures = [];

    var next = function (i) {
        if (i >= ids.length) {
            if (added) notifySuccess(added + ' approver(s) added.');
            if (failures.length) notifyError(failures.join('  '));
            $('#apUser').val([]);
            loadApprovers();
            return;
        }
        $.ajax({
            url: '/api/departments/approvers', type: 'POST', contentType: 'application/json',
            data: JSON.stringify({ DepartmentId: apDeptId, UserId: parseInt(ids[i]) }),
            success: function () { added++; },
            error: function (xhr) { failures.push(xhr.responseText || 'One user could not be added.'); }
        }).always(function () { next(i + 1); });
    };

    next(0);
}

function removeApprover(id) {
    notifyConfirm({
        title: 'Remove approver',
        text: 'They will no longer decide requests for this department. If this was their only '
            + 'department, they go back to approving company-wide.',
        confirmText: 'Remove', icon: 'warning'
    }, function () {
        $.ajax({
            url: '/api/departments/approvers/' + id, type: 'DELETE',
            success: function () { notifySuccess('Approver removed.'); loadApprovers(); },
            error: function (xhr) { notifyError(xhr.responseText || 'Could not remove.'); }
        });
    });
}

function saveDept() {
    var name = $('#deptName').val().trim();
    if (!name) { notifyError('Department name is required.', 'Validation Error'); return; }
    var dto = {
        Id: parseInt($('#deptId').val()) || 0,
        Name: name,
        Description: $('#deptDesc').val().trim(),
        IsActive: $('#deptActive').is(':checked'),
        HeadEmployeeId: $('#deptHead').val() ? parseInt($('#deptHead').val()) : null
    };
    $.ajax({
        url: '/api/departments', type: 'POST',
        contentType: 'application/json', data: JSON.stringify(dto),
        success: function () { 
            bootstrap.Modal.getInstance('#deptModal').hide(); 
            notifySuccess('Department saved successfully.');
            loadDepts(); 
        },
        error: function (xhr) { notifyError(xhr.responseText || 'Save failed.'); }
    });
}

function deleteDept(id) {
    notifyConfirm({ title: 'Delete Department', text: 'Are you sure you want to delete this department?', confirmText: 'Delete', icon: 'warning' }, function () {
        $.ajax({
            url: '/api/departments/' + id, type: 'DELETE',
            success: function () { 
                notifySuccess('Department deleted successfully.');
                loadDepts(); 
            },
            error: function (xhr) { notifyError(xhr.responseText || 'Delete failed.'); }
        });
    });
}
