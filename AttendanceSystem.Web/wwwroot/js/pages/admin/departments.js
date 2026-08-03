/* ── Admin Departments Management JavaScript ── */

var allDepts = [];

$(function () { loadDepts(); });

function loadDepts() {
    $.getJSON('/api/departments', function (data) {
        allDepts = data;
        renderTable(data);
    }).fail(function () {
        $('#deptBody').html('<tr><td colspan="6" class="text-danger text-center">Failed to load departments.</td></tr>');
    });
}

function renderTable(data) {
    if (!data || !data.length) {
        $('#deptBody').html('<tr><td colspan="6" class="text-center text-muted py-3">No departments found.</td></tr>');
        return;
    }
    var html = '';
    data.forEach(function (d, i) {
        var id = d.Id !== undefined ? d.Id : d.id;
        var name = d.Name || d.name || '';
        var desc = d.Description || d.description || '';
        var count = d.EmployeeCount !== undefined ? d.EmployeeCount : (d.employeeCount || 0);
        var isActive = d.IsActive !== undefined ? d.IsActive : d.isActive;

        html += '<tr>'
            + '<td class="text-muted">' + (i + 1) + '</td>'
            + '<td class="fw-semibold">' + name + '</td>'
            + '<td class="text-muted small">' + (desc || '—') + '</td>'
            + '<td><span class="badge bg-secondary">' + count + '</span></td>'
            + '<td>' + (isActive ? '<span class="badge bg-success">Active</span>' : '<span class="badge bg-danger">Inactive</span>') + '</td>'
            + '<td>'
            + '<button class="btn btn-sm btn-outline-primary me-1" onclick="editDept(' + id + ')" title="Edit"><i class="fa fa-pencil"></i></button>'
            + '<button class="btn btn-sm btn-outline-danger" onclick="deleteDept(' + id + ')" title="Delete"><i class="fa fa-trash"></i></button>'
            + '</td></tr>';
    });
    $('#deptBody').html(html);
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

function openModal(id, name, desc, active) {
    $('#deptId').val(id || 0);
    $('#deptName').val(name || '');
    $('#deptDesc').val(desc || '');
    $('#deptActive').prop('checked', active !== false);
    $('#modalTitle').text(id ? 'Edit Department' : 'Add Department');
    new bootstrap.Modal('#deptModal').show();
}

function editDept(id) {
    $.getJSON('/api/departments/' + id, function (d) {
        var idVal = d.Id !== undefined ? d.Id : d.id;
        var name = d.Name || d.name || '';
        var desc = d.Description || d.description || '';
        var isActive = d.IsActive !== undefined ? d.IsActive : d.isActive;
        openModal(idVal, name, desc, isActive);
    });
}

function saveDept() {
    var name = $('#deptName').val().trim();
    if (!name) { notifyError('Department name is required.', 'Validation Error'); return; }
    var dto = {
        Id: parseInt($('#deptId').val()) || 0,
        Name: name,
        Description: $('#deptDesc').val().trim(),
        IsActive: $('#deptActive').is(':checked')
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
