/* ── Payroll: employee list ── */

var epRows = [];

$(function () {
    $.getJSON('/api/departments', function (d) {
        (d || []).filter(function (x) { return x.IsActive; }).forEach(function (x) {
            $('#epDept').append('<option value="' + esc(x.Id) + '">' + esc(x.Name) + '</option>');
        });
    });
    epLoad();
});

function epLoad() {
    $('#epBody').html('<tr><td colspan="10" class="text-center py-4 text-muted">Loading…</td></tr>');

    // Unfiltered on the wire, filtered in the browser: the whole list is a few hundred rows
    // and the summary counts have to describe everyone, not the current filter.
    $.getJSON('/api/employee-payroll/list', function (rows) {
        epRows = rows || [];
        epRenderSummary();
        epFilter();
    }).fail(function (xhr) {
        $('#epBody').html('<tr><td colspan="10" class="text-danger text-center py-4">'
            + esc(xhr.responseText || 'Failed to load.') + '</td></tr>');
    });
}

/* Leads with what is outstanding. "How many people cannot be paid" is the question this
   screen exists to answer, and it should not need a filter to surface it. */
function epRenderSummary() {
    var total = epRows.length;
    var ready = epRows.filter(function (r) { return r.IsReady; }).length;
    var noGrade = epRows.filter(function (r) { return !r.GradeName; }).length;

    var tile = function (label, value, colour, filter) {
        return '<div class="col-6 col-md-3">'
             + '<div class="card stat-card ' + colour + (filter !== null ? ' ep-tile' : '') + '"'
             + (filter !== null ? ' data-filter="' + filter + '" style="cursor:pointer"' : '') + '>'
             + '<div class="card-body stat-card-body py-2"><div class="stat-card-text">'
             + '<p class="stat-card-label mb-0">' + label + '</p>'
             + '<h3 class="stat-card-value" style="font-size:1.35rem;">' + value + '</h3>'
             + '</div></div></div></div>';
    };

    $('#epSummary').html(
        tile('Employees', total, 'bg-c-blue', null)
      + tile('Ready for payroll', ready, 'bg-c-green', 'true')
      + tile('Not ready', total - ready, 'bg-c-pink', 'false')
      + tile('No grade set', noGrade, 'bg-c-yellow', null));

    $('#epSummary').off('click.ep').on('click.ep', '.ep-tile', function () {
        $('#epReady').val(this.getAttribute('data-filter'));
        epFilter();
    });
}

function epFilter() {
    var q = ($('#epSearch').val() || '').toLowerCase();
    var dept = $('#epDept').find('option:selected').text();
    var deptId = $('#epDept').val();
    var ready = $('#epReady').val();

    epRender(epRows.filter(function (r) {
        return (!q || (r.EmployeeName || '').toLowerCase().indexOf(q) >= 0
                   || (r.EmployeeCode || '').toLowerCase().indexOf(q) >= 0
                   || (r.EpfNumber || '').toLowerCase().indexOf(q) >= 0)
            && (!deptId || r.Department === dept)
            && (ready === '' || String(r.IsReady) === ready);
    }));
}

function epRender(rows) {
    amsPage('#epBody', rows, function (r) {
        // The reason sits in the tooltip rather than a column: it is several sentences, and
        // only matters for the rows that are not ready.
        var status = r.IsReady
            ? '<span class="badge bg-success">Ready</span>'
            : '<span class="badge bg-warning text-dark" title="' + esc(r.Missing.join(' ')) + '">'
              + esc(r.Missing.length) + ' missing</span>';

        return '<tr>'
             + '<td class="ps-3 fw-semibold">' + esc(r.EmployeeCode) + '</td>'
             + '<td>' + esc(r.EmployeeName) + '</td>'
             + '<td class="text-muted small">' + esc(r.Department) + '</td>'
             + '<td class="text-muted small">' + esc(r.CategoryName || '—') + '</td>'
             + '<td>' + (r.GradeName
                    ? esc(r.GradeName)
                    : '<span class="text-danger small">not set</span>') + '</td>'
             + '<td class="text-end">' + (r.BasicSalary ? r.BasicSalary.toFixed(2) : '—') + '</td>'
             + '<td class="small">' + (r.EpfNumber
                    ? esc(r.EpfNumber)
                    : (r.IsEpfMember ? '<span class="text-danger">missing</span>'
                                     : '<span class="text-muted">not a member</span>')) + '</td>'
             + '<td class="text-muted small">' + esc(r.BankAccount || '—') + '</td>'
             + '<td class="text-center">' + status + '</td>'
             + '<td class="text-end pe-3">'
             // Straight to the Payroll tab of the profile rather than a second copy of the
             // form here — one form means one place a field can be wrong.
             + '<a href="/Admin/EmployeeProfile/' + esc(r.EmployeeId) + '#pf-payroll" '
             + 'class="btn btn-sm btn-outline-primary">Set up</a>'
             + '</td></tr>';
    }, { colspan: 10, empty: 'No employees match these filters.', label: 'employee' });
}
