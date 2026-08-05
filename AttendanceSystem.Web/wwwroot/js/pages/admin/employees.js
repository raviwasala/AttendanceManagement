/* ── Admin Employees Management JavaScript ── */

var empData = null, empPage = 1, depts = [], desigs = [], branches = [];
var empSearchTimer = null;

$(function () {
    // Search is a round trip now, so it is debounced rather than firing per keystroke.
    $('#searchBox').on('input', function () {
        clearTimeout(empSearchTimer);
        empSearchTimer = setTimeout(function () { loadEmps(1); }, 300);
    });

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

/*
 * Server-paged: search, department and status are applied in SQL and only one page of rows
 * reaches the browser. Headcount can grow without the screen getting slower.
 */
function loadEmps(page) {
    empPage = amsPageNo(page, empPage);

    var q = ($('#searchBox').val() || '').trim();
    var dept = $('#deptFilter').val();
    var status = $('#statusFilter').val();

    var url = '/api/employees/paged?page=' + empPage + '&pageSize=' + (amsPageSize() || 25)
            + (q ? '&search=' + encodeURIComponent(q) : '')
            + (dept ? '&departmentId=' + encodeURIComponent(dept) : '')
            // '' means All; only send the flag when one of the two states is chosen.
            + (status === '' ? '' : '&isActive=' + encodeURIComponent(status));

    $('#empBody').html('<tr><td colspan="9" class="text-center py-4 text-muted">Loading…</td></tr>');

    $.getJSON(url, function (d) { empData = d; renderTable(); })
     .fail(function (xhr) {
         $('#empBody').html('<tr><td colspan="9" class="text-danger text-center py-3">'
             + esc(xhr.responseText || 'Failed to load employees.') + '</td></tr>');
     });
}

function renderTable() {
    var data = empData || { Items: [], TotalCount: 0, Page: 1, PageSize: 25 };

    amsPage('#empBody', data.Items, function (e) {
        return '<tr>'
            + '<td class="fw-semibold text-primary">' + esc(e.EmployeeCode)
              + (e.UserCode
                    ? '<div class="text-muted fw-normal" style="font-size:.7rem;">' + esc(e.UserCode) + '</div>'
                    : '') + '</td>'
            // Full name on top, the abbreviated form beneath: some of these names run to eight
            // words, and the initialled form is what appears on internal paperwork.
            + '<td>' + esc(e.FullName)
              + (e.NameWithInitials && e.NameWithInitials !== e.FullName
                    ? '<div class="text-muted" style="font-size:.7rem;">' + esc(e.NameWithInitials) + '</div>'
                    : '') + '</td>'
            + '<td class="text-muted small">' + (e.Nic ? esc(e.Nic) : '—') + '</td>'
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
    }, {
        colspan: 9,
        empty: 'No employees match these filters.',
        label: 'employee',
        server: {
            total: data.TotalCount,
            page: data.Page,
            pageSize: data.PageSize,
            onPage: loadEmps
        }
    });
}

/* Kept as the name every filter control already calls; a filter change goes back to page 1. */
function filterTable() {
    loadEmps(1);
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
    $('#empUserCode').val(''); $('#empInitials').val(''); $('#empNic').val('');
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
        $('#empUserCode').val(e.UserCode || '');
        $('#empInitials').val(e.NameWithInitials || '');
        $('#empNic').val(e.Nic || '');
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
    // Last name is no longer required: the imported records carry the whole name in Full Name,
    // and demanding a surname would make every one of them impossible to edit.
    if (!$('#empFirst').val().trim() || !$('#empDept').val() || !$('#empDesig').val() || !$('#empBranch').val() || !$('#empJoin').val()) {
        notifyError('Full Name, Department, Designation, Branch and Joining Date are required.', 'Validation Error'); return;
    }
    var dto = {
        Id: parseInt($('#empId').val()) || 0, EmployeeCode: $('#empCode').val().trim(),
        UserCode: $('#empUserCode').val().trim() || null,
        NameWithInitials: $('#empInitials').val().trim() || null,
        Nic: $('#empNic').val().trim() || null,
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
