/* ── Admin Holidays Management JavaScript ── */

$(function () {
    var yr = new Date().getFullYear();
    var o = '';
    for (var y = yr + 1; y >= yr - 3; y--) { o += '<option value="' + y + '"' + (y===yr?' selected':'') + '>' + y + '</option>'; }
    $('#yearFilter').html(o);
    loadHolidays();
});

// Held so the calendar draws from the same list as the table; two sources would
// eventually disagree about what counts as a holiday.
var allHolidays = [];

function loadHolidays() {
    var yr = $('#yearFilter').val();
    $.getJSON('/api/holidays/year/' + yr, function (data) {
        allHolidays = data || [];
        renderTable(allHolidays);
        // Redraw only if the calendar is the visible tab; otherwise it is drawn on open.
        if ($('#tab-calendar').hasClass('active')) renderCalendar();
    })
     .fail(function () { $('#tbody').html('<tr><td colspan="8" class="text-danger text-center py-3">Failed to load.</td></tr>'); });
}

var typeBadge = { Public: 'bg-primary', Company: 'bg-success', Special: 'bg-danger' };
var typeValue = { Public: 1, Company: 2, Special: 3 };

/* ── Calendar ─────────────────────────────────────────────────────────────────
   Twelve month grids for the selected year, built from the same list the table
   shows — so the two can never disagree about what is a holiday.

   Rendered on demand rather than on load: it is the second tab, and drawing 365
   cells nobody has asked to see costs more than it saves. */
function renderCalendar() {
    var year = parseInt($('#yearFilter').val());
    if (!year) return;

    // Keyed by month-day so a lookup per cell is a hash hit, not a scan of the list.
    var byDate = {};
    (allHolidays || []).forEach(function (h) {
        var d = new Date(h.HolidayDate);
        byDate[(d.getMonth() + 1) + '-' + d.getDate()] = h;
    });

    var months = ['January','February','March','April','May','June',
                  'July','August','September','October','November','December'];
    var today = new Date();
    var html = '';

    for (var m = 0; m < 12; m++) {
        var first = new Date(year, m, 1);
        var daysInMonth = new Date(year, m + 1, 0).getDate();
        // Monday-first: the working week is what this calendar is read against.
        var lead = (first.getDay() + 6) % 7;
        var count = 0;

        var cells = '';
        for (var i = 0; i < lead; i++) cells += '<td></td>';

        for (var day = 1; day <= daysInMonth; day++) {
            var date = new Date(year, m, day);
            var dow = date.getDay();
            var hol = byDate[(m + 1) + '-' + day];
            var cls = 'hol-day';

            if (hol) {
                cls += ' is-holiday t-' + (typeValue[hol.HolidayTypeDisplay] || 1);
                if (hol.IsProjected) cls += ' is-projected';
                count++;
            } else if (dow === 0 || dow === 6) {
                cls += ' is-weekend';
            }

            if (date.toDateString() === today.toDateString()) cls += ' is-today';

            cells += '<td>' + (hol
                ? '<span class="' + cls + '" title="' + esc(hol.Name)
                  + (hol.IsProjected ? ' (carried forward from ' + esc(hol.DeclaredYear) + ')' : '')
                  + '">' + day + '</span>'
                : '<span class="' + cls + '">' + day + '</span>') + '</td>';

            if ((lead + day) % 7 === 0) cells += '</tr><tr>';
        }

        html += '<div class="col-12 col-md-6 col-xl-4 col-xxl-3">'
              + '<div class="card h-100 hol-month"><div class="card-body p-2">'
              + '<div class="d-flex justify-content-between align-items-center mb-1">'
              + '<strong class="small">' + months[m] + '</strong>'
              + (count ? '<span class="badge bg-light text-dark">' + count + '</span>' : '')
              + '</div>'
              + '<table><thead><tr><th>M</th><th>T</th><th>W</th><th>T</th><th>F</th><th>S</th><th>S</th></tr></thead>'
              + '<tbody><tr>' + cells + '</tr></tbody></table>'
              + '</div></div></div>';
    }

    $('#calendarGrid').html(html);
}

function renderTable(data) {
    amsPage('#tbody', data, function (h, i) {
        return '<tr' + (h.IsProjected ? ' class="table-light"' : '') + '>'
            + '<td class="text-muted">' + (i+1) + '</td>'
            + '<td class="fw-semibold">' + esc(h.Name)
            // A projected entry has no row of its own for this year. Saying so is what stops
            // somebody deleting "next year's Christmas" and removing it from every year.
            + (h.IsProjected
                ? '<div class="text-muted" style="font-size:.7rem;">carried forward from '
                  + esc(h.DeclaredYear) + '</div>' : '')
            + '</td>'
            + '<td>' + esc(h.DateDisplay) + '</td>'
            + '<td class="text-muted">' + esc(h.DayName) + '</td>'
            + '<td><span class="badge ' + (typeBadge[h.HolidayTypeDisplay] || 'bg-secondary') + '">'
            + esc(h.HolidayTypeDisplay) + '</span></td>'
            + '<td>' + (h.IsRecurring ? '<i class="fa fa-repeat text-success" title="Repeats every year"></i>' : '—') + '</td>'
            + '<td class="text-muted small">' + (h.Description ? esc(h.Description) : '—') + '</td>'
            + '<td>'
            + '<button class="btn btn-sm btn-outline-primary me-1" onclick="editItem(' + h.Id + ')" title="'
            + (h.IsProjected ? 'Edit the original, which affects every year' : 'Edit') + '"><i class="fa fa-pencil"></i></button>'
            + '<button class="btn btn-sm btn-outline-danger" onclick="deleteItem(' + h.Id + ', '
            + (h.IsProjected ? 'true' : 'false') + ')" title="Delete"><i class="fa fa-trash"></i></button>'
            + '</td></tr>';
    }, { colspan: 8, empty: 'No holidays for this year.', label: 'holiday' });
}

function openModal() {
    // Type defaults to 1 (Public) — 0 is not a valid HolidayType.
    $('#itemId').val(0); $('#itemName').val(''); $('#itemDate').val('');
    $('#itemType').val(1); $('#itemDesc').val(''); $('#itemRecurring').prop('checked', false);
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

function deleteItem(id, isProjected) {
    notifyConfirm({
        title: 'Delete Holiday',
        // A projected entry is a view of a row stored in an earlier year; deleting it removes
        // the holiday from every year, not just the one on screen.
        text: isProjected
            ? 'This is a recurring holiday carried forward from an earlier year. Deleting it removes it from every year, including the ones already recorded.'
            : 'Are you sure you want to delete this holiday?',
        confirmText: 'Delete', icon: 'warning'
    }, function () {
        $.ajax({ url: '/api/holidays/' + id, type: 'DELETE',
            success: function () { 
                notifySuccess('Holiday deleted successfully.');
                loadHolidays(); 
            },
            error: function (xhr) { notifyError(xhr.responseText || 'Delete failed.'); }
        });
    });
}
