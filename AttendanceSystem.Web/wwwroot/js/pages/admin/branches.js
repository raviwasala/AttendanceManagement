/* ── Admin Branches Management JavaScript ── */

var allItems = [];
$(function () { loadItems(); });

function loadItems() {
    $.getJSON('/api/branches', function (data) { allItems = data; renderTable(data); })
     .fail(function () { $('#tbody').html('<tr><td colspan="6" class="text-danger text-center py-3">Failed to load.</td></tr>'); });
}

function renderTable(data) {
    if (!data.length) { $('#tbody').html('<tr><td colspan="6" class="text-center text-muted py-3">No records found.</td></tr>'); return; }
    var html = '';
    data.forEach(function (d, i) {
        html += '<tr>'
            + '<td class="text-muted">' + (i+1) + '</td>'
            + '<td class="fw-semibold">' + d.Name + '</td>'
            + '<td class="text-muted small">' + (d.Address || '—') + '</td>'
            + '<td>' + (d.Phone || '—') + '</td>'
            + '<td>' + (d.IsActive ? '<span class="badge bg-success">Active</span>' : '<span class="badge bg-danger">Inactive</span>') + '</td>'
            + '<td>'
            + '<button class="btn btn-sm btn-outline-primary me-1" onclick="editItem(' + d.Id + ')" title="Edit"><i class="fa fa-pencil"></i></button>'
            + '<button class="btn btn-sm btn-outline-danger" onclick="deleteItem(' + d.Id + ')" title="Delete"><i class="fa fa-trash"></i></button>'
            + '</td></tr>';
    });
    $('#tbody').html(html);
}

function filterTable() {
    var q = $('#searchBox').val().toLowerCase(); var s = $('#statusFilter').val();
    renderTable(allItems.filter(function (d) { return (!q || d.Name.toLowerCase().includes(q)) && (s === '' || String(d.IsActive) === s); }));
}

function openModal(id, name, addr, phone, active) {
    $('#itemId').val(id || 0); $('#itemName').val(name || ''); $('#itemAddr').val(addr || '');
    $('#itemPhone').val(phone || ''); $('#itemActive').prop('checked', active !== false);
    $('#modalTitle').text(id ? 'Edit Branch' : 'Add Branch');
    new bootstrap.Modal('#editModal').show();
}

function editItem(id) {
    $.getJSON('/api/branches/' + id, function (d) { openModal(d.Id, d.Name, d.Address, d.Phone, d.IsActive); });
}

function saveItem() {
    var name = $('#itemName').val().trim();
    if (!name) { alert('Name is required.'); return; }
    var dto = { Id: parseInt($('#itemId').val()) || 0, Name: name, Address: $('#itemAddr').val().trim(), Phone: $('#itemPhone').val().trim(), IsActive: $('#itemActive').is(':checked') };
    $.ajax({ url: '/api/branches', type: 'POST', contentType: 'application/json', data: JSON.stringify(dto),
        success: function () { bootstrap.Modal.getInstance('#editModal').hide(); loadItems(); },
        error: function (xhr) { alert('Error: ' + (xhr.responseText || 'Save failed.')); }
    });
}

function deleteItem(id) {
    if (!confirm('Delete this branch?')) return;
    var uid = window.getCurrentUserId ? window.getCurrentUserId() : 1;
    $.ajax({ url: '/api/branches/' + id + '?deletedBy=' + uid, type: 'DELETE',
        success: function () { loadItems(); },
        error: function (xhr) { alert('Error: ' + (xhr.responseText || 'Delete failed.')); }
    });
}
