/* ── Admin Branches Management JavaScript ── */

var allItems = [];
$(function () { loadItems(); });

function loadItems() {
    // filterTable, not renderTable: a reload after save/delete must keep the active filters.
    $.getJSON('/api/branches', function (data) { allItems = data || []; filterTable(); })
     .fail(function () { $('#tbody').html('<tr><td colspan="6" class="text-danger text-center py-3">Failed to load.</td></tr>'); });
}

function renderTable(data) {
    amsPage('#tbody', data, function (d, i) {
        return '<tr>'
            + '<td class="text-muted">' + (i+1) + '</td>'
            + '<td class="fw-semibold">' + esc(d.Name) + '</td>'
            + '<td class="text-muted small">' + (d.Address ? esc(d.Address) : '—') + '</td>'
            + '<td>' + (d.Phone ? esc(d.Phone) : '—') + '</td>'
            + '<td>' + (d.IsActive ? '<span class="badge bg-success">Active</span>' : '<span class="badge bg-danger">Inactive</span>') + '</td>'
            + '<td>'
            + '<button class="btn btn-sm btn-outline-primary me-1" onclick="editItem(' + d.Id + ')" title="Edit"><i class="fa fa-pencil"></i></button>'
            + '<button class="btn btn-sm btn-outline-danger" onclick="deleteItem(' + d.Id + ')" title="Delete"><i class="fa fa-trash"></i></button>'
            + '</td></tr>';
    }, { colspan: 6, empty: 'No branches found.', label: 'branch', labelPlural: 'branches' });
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
    if (!name) { notifyError('Branch name is required.', 'Validation Error'); return; }
    var dto = { Id: parseInt($('#itemId').val()) || 0, Name: name, Address: $('#itemAddr').val().trim(), Phone: $('#itemPhone').val().trim(), IsActive: $('#itemActive').is(':checked') };
    $.ajax({ url: '/api/branches', type: 'POST', contentType: 'application/json', data: JSON.stringify(dto),
        success: function () { 
            bootstrap.Modal.getInstance('#editModal').hide(); 
            notifySuccess('Branch saved successfully.');
            loadItems(); 
        },
        error: function (xhr) { notifyError(xhr.responseText || 'Save failed.'); }
    });
}

function deleteItem(id) {
    notifyConfirm({ title: 'Delete Branch', text: 'Are you sure you want to delete this branch?', confirmText: 'Delete', icon: 'warning' }, function () {
        $.ajax({ url: '/api/branches/' + id, type: 'DELETE',
            success: function () { 
                notifySuccess('Branch deleted successfully.');
                loadItems(); 
            },
            error: function (xhr) { notifyError(xhr.responseText || 'Delete failed.'); }
        });
    });
}
