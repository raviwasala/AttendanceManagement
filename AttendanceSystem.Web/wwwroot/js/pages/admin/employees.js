/* ── Admin Employees Management JavaScript ── */

var allEmps = [], depts = [], desigs = [], branches = [];

$(function () {
    $.when(
        $.getJSON('/api/departments', function (d) { depts = d.filter(function(x){return x.IsActive;}); }),
        $.getJSON('/api/designations', function (d) { desigs = d.filter(function(x){return x.IsActive;}); }),
        $.getJSON('/api/branches', function (d) { branches = d.filter(function(x){return x.IsActive;}); })
    ).then(function () {
        var opts = '<option value="">All Departments</option>';
        depts.forEach(function (d) { opts += '<option value="' + d.Id + '">' + d.Name + '</option>'; });
        $('#deptFilter').html(opts);
        loadEmps();
    });
});

function loadEmps() {
    $.getJSON('/api/employees', function (d) { allEmps = d; renderTable(d); })
     .fail(function () { $('#empBody').html('<tr><td colspan="8" class="text-danger text-center py-3">Failed to load.</td></tr>'); });
}

function renderTable(data) {
    if (!data.length) { $('#empBody').html('<tr><td colspan="8" class="text-center text-muted py-3">No employees found.</td></tr>'); return; }
    var html = '';
    data.forEach(function (e) {
        html += '<tr>'
            + '<td class="fw-semibold text-primary">' + e.EmployeeCode + '</td>'
            + '<td>' + e.FullName + '</td>'
            + '<td class="text-muted">' + e.Department + '</td>'
            + '<td class="text-muted">' + e.Designation + '</td>'
            + '<td class="text-muted">' + e.Branch + '</td>'
            + '<td>' + (e.Phone || '—') + '</td>'
            + '<td>' + (e.IsActive ? '<span class="badge bg-success">Active</span>' : '<span class="badge bg-secondary">Inactive</span>') + '</td>'
            + '<td>'
            + '<button class="btn btn-sm btn-outline-primary me-1" onclick="editEmp(' + e.Id + ')" title="Edit"><i class="fa fa-pencil"></i></button>'
            + '<button class="btn btn-sm btn-outline-' + (e.IsActive ? 'warning' : 'success') + ' me-1" title="' + (e.IsActive ? 'Deactivate' : 'Activate') + '" onclick="toggleEmp(' + e.Id + ')"><i class="fa fa-' + (e.IsActive ? 'toggle-on' : 'toggle-off') + '"></i></button>'
            + '<button class="btn btn-sm btn-outline-danger" onclick="deleteEmp(' + e.Id + ')" title="Delete"><i class="fa fa-trash"></i></button>'
            + '</td></tr>';
    });
    $('#empBody').html(html);
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
    depts.forEach(function (d) { dOpts += '<option value="' + d.Id + '">' + d.Name + '</option>'; });
    var deOpts = '<option value="">-- Designation --</option>';
    desigs.forEach(function (d) { deOpts += '<option value="' + d.Id + '">' + d.Name + '</option>'; });
    var bOpts = '<option value="">-- Branch --</option>';
    branches.forEach(function (b) { bOpts += '<option value="' + b.Id + '">' + b.Name + '</option>'; });
    $('#empDept').html(dOpts); $('#empDesig').html(deOpts); $('#empBranch').html(bOpts);
}

function openModal() {
    fillDropdowns();
    $('#empId').val(0); $('#empCode').val(''); $('#empFirst').val(''); $('#empLast').val('');
    $('#empEmail').val(''); $('#empPhone').val(''); $('#empGender').val('');
    $('#empJoin').val(new Date().toISOString().split('T')[0]); $('#empDob').val('');
    $('#empAddr').val(''); $('#empActive').prop('checked', true);
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
        $('#empAddr').val(e.Address); $('#empActive').prop('checked', e.IsActive);
        $('#empDept').val(e.DepartmentId); $('#empDesig').val(e.DesignationId); $('#empBranch').val(e.BranchId);
        $('#empModalTitle').text('Edit Employee');
        new bootstrap.Modal('#empModal').show();
    });
}

function saveEmp() {
    if (!$('#empFirst').val().trim() || !$('#empLast').val().trim() || !$('#empDept').val() || !$('#empDesig').val() || !$('#empBranch').val() || !$('#empJoin').val()) {
        alert('First Name, Last Name, Department, Designation, Branch and Joining Date are required.'); return;
    }
    var dto = {
        Id: parseInt($('#empId').val()) || 0, EmployeeCode: $('#empCode').val().trim(),
        FirstName: $('#empFirst').val().trim(), LastName: $('#empLast').val().trim(),
        Email: $('#empEmail').val().trim() || null, Phone: $('#empPhone').val().trim() || null,
        Gender: $('#empGender').val() || null,
        JoiningDate: $('#empJoin').val(), DateOfBirth: $('#empDob').val() || null,
        Address: $('#empAddr').val().trim() || null,
        DepartmentId: parseInt($('#empDept').val()), DesignationId: parseInt($('#empDesig').val()),
        BranchId: parseInt($('#empBranch').val()), IsActive: $('#empActive').is(':checked')
    };
    $.ajax({ url: '/api/employees', type: 'POST', contentType: 'application/json', data: JSON.stringify(dto),
        success: function () { bootstrap.Modal.getInstance('#empModal').hide(); loadEmps(); },
        error: function (xhr) { alert('Error: ' + (xhr.responseText || 'Save failed.')); }
    });
}

function toggleEmp(id) {
    var uid = window.getCurrentUserId();
    $.ajax({ url: '/api/employees/' + id + '/toggle?modifiedBy=' + uid, type: 'POST',
        success: function () { loadEmps(); },
        error: function (xhr) { alert('Error: ' + (xhr.responseText || 'Toggle failed.')); }
    });
}

function deleteEmp(id) {
    if (!confirm('Delete this employee? This cannot be undone.')) return;
    var uid = window.getCurrentUserId();
    $.ajax({ url: '/api/employees/' + id + '?deletedBy=' + uid, type: 'DELETE',
        success: function () { loadEmps(); },
        error: function (xhr) { alert('Error: ' + (xhr.responseText || 'Delete failed.')); }
    });
}
