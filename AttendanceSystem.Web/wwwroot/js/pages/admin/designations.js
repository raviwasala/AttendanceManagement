/* ── Admin Designations Management JavaScript ── */

var allItems = [];

$(function () { loadItems(); });

function loadItems() {
    $.getJSON('/api/designations', function (data) {
        allItems = data;
        renderTable(data);
    }).fail(function () {
        $('#tbody').html('<tr><td colspan="5" class="text-danger text-center py-3">Failed to load.</td></tr>');
    });
}

function renderTable(data) {
    if (!data.length) { $('#tbody').html('<tr><td colspan="5" class="text-center text-muted py-3">No records found.</td></tr>'); return; }
    var html = '';
    data.forEach(function (d, i) {
        html += '<tr>'
            + '<td class="text-muted">' + (i+1) + '</td>'
            + '<td class="fw-semibold">' + d.Name + '</td>'
            + '<td class="text-muted small">' + (d.Description || '—') + '</td>'
            + '<td>' + (d.IsActive ? '<span class="badge bg-success">Active</span>' : '<span class="badge bg-danger">Inactive</span>') + '</td>'
            + '<td>'
            + '<button class="btn btn-sm btn-outline-primary me-1" onclick="editItem(' + d.Id + ')" title="Edit"><i class="fa fa-pencil"></i></button>'
            + '<button class="btn btn-sm btn-outline-danger" onclick="deleteItem(' + d.Id + ')" title="Delete"><i class="fa fa-trash"></i></button>'
            + '</td></tr>';
    });
    $('#tbody').html(html);
}

function filterTable() {
    var q = $('#searchBox').val().toLowerCase();
    var s = $('#statusFilter').val();
    renderTable(allItems.filter(function (d) {
        return (!q || d.Name.toLowerCase().includes(q)) && (s === '' || String(d.IsActive) === s);
    }));
}

function openModal(id, name, desc, active) {
    $('#itemId').val(id || 0);
    $('#itemName').val(name || '');
    $('#itemDesc').val(desc || '');
    $('#itemActive').prop('checked', active !== false);
    $('#modalTitle').text(id ? 'Edit Designation' : 'Add Designation');
    new bootstrap.Modal('#editModal').show();
}

function editItem(id) {
    $.getJSON('/api/designations/' + id, function (d) { openModal(d.Id, d.Name, d.Description, d.IsActive); });
}

function saveItem() {
    var name = $('#itemName').val().trim();
    if (!name) { alert('Name is required.'); return; }
    var dto = { Id: parseInt($('#itemId').val()) || 0, Name: name, Description: $('#itemDesc').val().trim(), IsActive: $('#itemActive').is(':checked') };
    $.ajax({ url: '/api/designations', type: 'POST', contentType: 'application/json', data: JSON.stringify(dto),
        success: function () { bootstrap.Modal.getInstance('#editModal').hide(); loadItems(); },
        error: function (xhr) { alert('Error: ' + (xhr.responseText || 'Save failed.')); }
    });
}

function deleteItem(id) {
    if (!confirm('Delete this designation?')) return;
    var uid = window.getCurrentUserId ? window.getCurrentUserId() : 1;
    $.ajax({ url: '/api/designations/' + id + '?deletedBy=' + uid, type: 'DELETE',
        success: function () { loadItems(); },
        error: function (xhr) { alert('Error: ' + (xhr.responseText || 'Delete failed.')); }
    });
}
