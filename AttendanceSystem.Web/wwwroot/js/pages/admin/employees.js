/* ── Admin Employees Management JavaScript ── */

var allEmps = [], depts = [], desigs = [], branches = [];

$(function () {
    $.when(
        $.getJSON('/api/departments', function (d) { depts = (d || []).filter(function(x){ return x.IsActive || x.isActive; }); }),
        $.getJSON('/api/designations', function (d) { desigs = (d || []).filter(function(x){ return x.IsActive || x.isActive; }); }),
        $.getJSON('/api/branches', function (d) { branches = (d || []).filter(function(x){ return x.IsActive || x.isActive; }); })
    ).always(function () {
        var opts = '<option value="">All Departments</option>';
        depts.forEach(function (d) {
            var id = d.Id !== undefined ? d.Id : d.id;
            var name = d.Name || d.name || '';
            opts += '<option value="' + esc(id) + '">' + esc(name) + '</option>';
        });
        $('#deptFilter').html(opts);
        loadEmps();
    });
});

function loadEmps() {
    // filterTable rather than renderTable: after a save, delete or activate the table must
    // still honour the search and filters the user has set, not silently reset to everything.
    $.getJSON('/api/employees', function (d) { allEmps = d || []; filterTable(); })
     .fail(function () { $('#empBody').html('<tr><td colspan="8" class="text-danger text-center py-3">Failed to load.</td></tr>'); });
}

function renderTable(data) {
    amsPage('#empBody', data, function (e) {
        return '<tr>'
            + '<td class="fw-semibold text-primary">' + esc(e.EmployeeCode) + '</td>'
            + '<td>' + esc(e.FullName) + '</td>'
            + '<td class="text-muted">' + esc(e.Department) + '</td>'
            + '<td class="text-muted">' + esc(e.Designation) + '</td>'
            + '<td class="text-muted">' + esc(e.Branch) + '</td>'
            // A missing enrol id is the single most common cause of "the import did nothing",
            // so make its absence loud rather than showing an empty cell.
            + '<td>' + (e.BiometricEnrollId
                ? '<span class="badge bg-light text-dark">' + esc(e.BiometricEnrollId) + '</span>'
                : '<span class="badge bg-warning text-dark" title="Biometric imports cannot match this employee">not set</span>') + '</td>'
            + '<td>' + (e.IsActive ? '<span class="badge bg-success">Active</span>' : '<span class="badge bg-secondary">Inactive</span>') + '</td>'
            + '<td>'
            + '<button class="btn btn-sm btn-outline-primary me-1" onclick="editEmp(' + e.Id + ')" title="Edit"><i class="fa fa-pencil"></i></button>'
            + '<button class="btn btn-sm btn-outline-' + (e.IsActive ? 'warning' : 'success') + ' me-1" title="' + (e.IsActive ? 'Deactivate' : 'Activate') + '" onclick="toggleEmp(' + e.Id + ')"><i class="fa fa-' + (e.IsActive ? 'toggle-on' : 'toggle-off') + '"></i></button>'
            + '<button class="btn btn-sm btn-outline-danger" onclick="deleteEmp(' + e.Id + ')" title="Delete"><i class="fa fa-trash"></i></button>'
            + '</td></tr>';
    }, { colspan: 8, empty: 'No employees found.', label: 'employee' });
}

function filterTable() {
    var q = $('#searchBox').val().toLowerCase();
    var dept = $('#deptFilter').val();
    var s = $('#statusFilter').val();
    renderTable(allEmps.filter(function (e) {
        return (!q || e.FullName.toLowerCase().includes(q) || e.EmployeeCode.toLowerCase().includes(q) || (e.Email||'').toLowerCase().includes(q))
            && (!dept || String(e.DepartmentId) === dept || e.Department.includes($('#deptFilter option:selected').text()))
            && (s === '' || String(e.IsActive) === s);
    }));
}

function fillDropdowns() {
    var dOpts = '<option value="">-- Department --</option>';
    depts.forEach(function (d) { dOpts += '<option value="' + esc(d.Id) + '">' + esc(d.Name) + '</option>'; });
    var deOpts = '<option value="">-- Designation --</option>';
    desigs.forEach(function (d) { deOpts += '<option value="' + esc(d.Id) + '">' + esc(d.Name) + '</option>'; });
    var bOpts = '<option value="">-- Branch --</option>';
    branches.forEach(function (b) { bOpts += '<option value="' + esc(b.Id) + '">' + esc(b.Name) + '</option>'; });
    $('#empDept').html(dOpts); $('#empDesig').html(deOpts); $('#empBranch').html(bOpts);
}

function openModal() {
    fillDropdowns();
    $('#empId').val(0); $('#empCode').val(''); $('#empFirst').val(''); $('#empLast').val('');
    $('#empEmail').val(''); $('#empPhone').val(''); $('#empGender').val('');
    $('#empJoin').val(new Date().toISOString().split('T')[0]); $('#empDob').val('');
    $('#empAddr').val(''); $('#empEnrollId').val(''); $('#empActive').prop('checked', true);
    $('#empModalTitle').text('Add Employee');
    new bootstrap.Modal('#empModal').show();
}

function editEmp(id) {
    $.getJSON('/api/employees/' + id, function (e) {
        fillDropdowns();
        $('#empId').val(e.Id); $('#empCode').val(e.EmployeeCode);
        $('#empFirst').val(e.FirstName); $('#empLast').val(e.LastName);
        $('#empEmail').val(e.Email); $('#empPhone').val(e.Phone);
        $('#empGender').val(e.Gender);
        $('#empJoin').val(e.JoiningDate ? e.JoiningDate.split('T')[0] : '');
        $('#empDob').val(e.DateOfBirth ? e.DateOfBirth.split('T')[0] : '');
        $('#empAddr').val(e.Address); $('#empEnrollId').val(e.BiometricEnrollId || '');
        $('#empActive').prop('checked', e.IsActive);
        $('#empDept').val(e.DepartmentId); $('#empDesig').val(e.DesignationId); $('#empBranch').val(e.BranchId);
        $('#empModalTitle').text('Edit Employee');
        new bootstrap.Modal('#empModal').show();
    });
}

function saveEmp() {
    if (!$('#empFirst').val().trim() || !$('#empLast').val().trim() || !$('#empDept').val() || !$('#empDesig').val() || !$('#empBranch').val() || !$('#empJoin').val()) {
        notifyError('First Name, Last Name, Department, Designation, Branch and Joining Date are required.', 'Validation Error'); return;
    }
    var dto = {
        Id: parseInt($('#empId').val()) || 0, EmployeeCode: $('#empCode').val().trim(),
        FirstName: $('#empFirst').val().trim(), LastName: $('#empLast').val().trim(),
        Email: $('#empEmail').val().trim() || null, Phone: $('#empPhone').val().trim() || null,
        Gender: $('#empGender').val() || null,
        JoiningDate: $('#empJoin').val(), DateOfBirth: $('#empDob').val() || null,
        Address: $('#empAddr').val().trim() || null,
        // Blank means "not enrolled", which is a legitimate state — send null, not 0.
        BiometricEnrollId: $('#empEnrollId').val() === '' ? null : parseInt($('#empEnrollId').val()),
        DepartmentId: parseInt($('#empDept').val()), DesignationId: parseInt($('#empDesig').val()),
        BranchId: parseInt($('#empBranch').val()), IsActive: $('#empActive').is(':checked')
    };
    $.ajax({ url: '/api/employees', type: 'POST', contentType: 'application/json', data: JSON.stringify(dto),
        success: function () { 
            bootstrap.Modal.getInstance('#empModal').hide(); 
            notifySuccess('Employee saved successfully.');
            loadEmps(); 
        },
        error: function (xhr) { notifyError(xhr.responseText || 'Save failed.'); }
    });
}

function toggleEmp(id) {
    $.ajax({ url: '/api/employees/' + id + '/toggle', type: 'POST',
        success: function () { 
            notifySuccess('Employee status updated.');
            loadEmps(); 
        },
        error: function (xhr) { notifyError(xhr.responseText || 'Toggle failed.'); }
    });
}

function deleteEmp(id) {
    notifyConfirm({ title: 'Delete Employee', text: 'Are you sure you want to delete this employee? This cannot be undone.', confirmText: 'Delete', icon: 'warning' }, function () {
        $.ajax({ url: '/api/employees/' + id, type: 'DELETE',
            success: function () { 
                notifySuccess('Employee deleted successfully.');
                loadEmps(); 
            },
            error: function (xhr) { notifyError(xhr.responseText || 'Delete failed.'); }
        });
    });
}
