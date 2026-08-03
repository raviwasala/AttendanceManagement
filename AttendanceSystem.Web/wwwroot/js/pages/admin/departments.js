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
    if (!data.length) {
        $('#deptBody').html('<tr><td colspan="6" class="text-center text-muted py-3">No departments found.</td></tr>');
        return;
    }
    var html = '';
    data.forEach(function (d, i) {
        html += '<tr>'
            + '<td class="text-muted">' + (i + 1) + '</td>'
            + '<td class="fw-semibold">' + d.Name + '</td>'
            + '<td class="text-muted small">' + (d.Description || '—') + '</td>'
            + '<td><span class="badge bg-secondary">' + d.EmployeeCount + '</span></td>'
            + '<td>' + (d.IsActive ? '<span class="badge bg-success">Active</span>' : '<span class="badge bg-danger">Inactive</span>') + '</td>'
            + '<td>'
            + '<button class="btn btn-sm btn-outline-primary me-1" onclick="editDept(' + d.Id + ')" title="Edit"><i class="fa fa-pencil"></i></button>'
            + '<button class="btn btn-sm btn-outline-danger" onclick="deleteDept(' + d.Id + ')" title="Delete"><i class="fa fa-trash"></i></button>'
            + '</td></tr>';
    });
    $('#deptBody').html(html);
}

function filterTable() {
    var q = $('#searchBox').val().toLowerCase();
    var s = $('#statusFilter').val();
    var filtered = allDepts.filter(function (d) {
        var matchQ = !q || d.Name.toLowerCase().includes(q) || (d.Description || '').toLowerCase().includes(q);
        var matchS = s === '' || String(d.IsActive) === s;
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
        openModal(d.Id, d.Name, d.Description, d.IsActive);
    });
}

function saveDept() {
    var name = $('#deptName').val().trim();
    if (!name) { alert('Name is required.'); return; }
    var dto = {
        Id: parseInt($('#deptId').val()) || 0,
        Name: name,
        Description: $('#deptDesc').val().trim(),
        IsActive: $('#deptActive').is(':checked')
    };
    $.ajax({
        url: '/api/departments', type: 'POST',
        contentType: 'application/json', data: JSON.stringify(dto),
        success: function () { bootstrap.Modal.getInstance('#deptModal').hide(); loadDepts(); },
        error: function (xhr) { alert('Error: ' + (xhr.responseText || 'Save failed.')); }
    });
}

function deleteDept(id) {
    if (!confirm('Delete this department?')) return;
    var uid = window.getCurrentUserId ? window.getCurrentUserId() : 1;
    $.ajax({
        url: '/api/departments/' + id + '?deletedBy=' + uid, type: 'DELETE',
        success: function () { loadDepts(); },
        error: function (xhr) { alert('Error: ' + (xhr.responseText || 'Delete failed.')); }
    });
}
