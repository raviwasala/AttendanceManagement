/* ── Admin Holidays Management JavaScript ── */

$(function () {
    var yr = new Date().getFullYear();
    var o = '';
    for (var y = yr + 1; y >= yr - 3; y--) { o += '<option value="' + y + '"' + (y===yr?' selected':'') + '>' + y + '</option>'; }
    $('#yearFilter').html(o);
    loadHolidays();
});

function loadHolidays() {
    var yr = $('#yearFilter').val();
    $.getJSON('/api/holidays/year/' + yr, function (data) { renderTable(data); })
     .fail(function () { $('#tbody').html('<tr><td colspan="8" class="text-danger text-center py-3">Failed to load.</td></tr>'); });
}

function renderTable(data) {
    amsPage('#tbody', data, function (h, i) {
        return '<tr>'
            + '<td class="text-muted">' + (i+1) + '</td>'
            + '<td class="fw-semibold">' + esc(h.Name) + '</td>'
            + '<td>' + esc(h.DateDisplay) + '</td>'
            + '<td class="text-muted">' + esc(h.DayName) + '</td>'
            + '<td><span class="badge bg-info text-dark">' + esc(h.HolidayTypeDisplay) + '</span></td>'
            + '<td>' + (h.IsRecurring ? '<i class="fa fa-repeat text-success"></i>' : '—') + '</td>'
            + '<td class="text-muted small">' + (h.Description ? esc(h.Description) : '—') + '</td>'
            + '<td>'
            + '<button class="btn btn-sm btn-outline-primary me-1" onclick="editItem(' + h.Id + ')" title="Edit"><i class="fa fa-pencil"></i></button>'
            + '<button class="btn btn-sm btn-outline-danger" onclick="deleteItem(' + h.Id + ')" title="Delete"><i class="fa fa-trash"></i></button>'
            + '</td></tr>';
    }, { colspan: 8, empty: 'No holidays for this year.', label: 'holiday' });
}

function openModal() {
    $('#itemId').val(0); $('#itemName').val(''); $('#itemDate').val(''); $('#itemType').val(0); $('#itemDesc').val(''); $('#itemRecurring').prop('checked', false);
    $('#modalTitle').text('Add Holiday');
    new bootstrap.Modal('#editModal').show();
}

function editItem(id) {
    $.getJSON('/api/holidays', function (all) {
        var h = all.find(function(x){return x.Id===id;});
        if (!h) return;
        $('#itemId').val(h.Id); $('#itemName').val(h.Name);
        $('#itemDate').val(h.HolidayDate.split('T')[0]); $('#itemType').val(h.HolidayType);
        $('#itemDesc').val(h.Description || ''); $('#itemRecurring').prop('checked', h.IsRecurring);
        $('#modalTitle').text('Edit Holiday');
        new bootstrap.Modal('#editModal').show();
    });
}

function saveItem() {
    var name = $('#itemName').val().trim(), date = $('#itemDate').val();
    if (!name || !date) { notifyError('Name and Date are required.', 'Validation Error'); return; }
    var dto = { Id: parseInt($('#itemId').val())||0, Name: name, HolidayDate: date, HolidayType: parseInt($('#itemType').val()), Description: $('#itemDesc').val().trim()||null, IsRecurring: $('#itemRecurring').is(':checked') };
    $.ajax({ url: '/api/holidays', type: 'POST', contentType: 'application/json', data: JSON.stringify(dto),
        success: function () { 
            bootstrap.Modal.getInstance('#editModal').hide(); 
            notifySuccess('Holiday saved successfully.');
            loadHolidays(); 
        },
        error: function (xhr) { notifyError(xhr.responseText || 'Save failed.'); }
    });
}

function deleteItem(id) {
    notifyConfirm({ title: 'Delete Holiday', text: 'Are you sure you want to delete this holiday?', confirmText: 'Delete', icon: 'warning' }, function () {
        $.ajax({ url: '/api/holidays/' + id, type: 'DELETE',
            success: function () { 
                notifySuccess('Holiday deleted successfully.');
                loadHolidays(); 
            },
            error: function (xhr) { notifyError(xhr.responseText || 'Delete failed.'); }
        });
    });
}
