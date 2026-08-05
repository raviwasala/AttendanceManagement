/* ── Admin Reports JavaScript ── */

$(function () {
    var today = new Date().toISOString().split('T')[0];
    $('#lFrom,#lvFrom').val(new Date(new Date().setDate(1)).toISOString().split('T')[0]);
    $('#lTo,#lvTo').val(today);
    $.getJSON('/api/departments', function (d) {
        var o = '<option value="">All Departments</option>';
        (d || []).forEach(function (x) {
            o += '<option value="' + esc(x.Id) + '">' + esc(x.Name) + '</option>';
        });
        $('#mDept,#lDept,#lvDept,#eDept').html(o);
    });
});

function dt(v) { return v ? new Date(v).toLocaleDateString() : '—'; }

function loadMonthly() {
    var m = $('#mMonth').val(), y = $('#mYear').val(), d = $('#mDept').val();
    $.getJSON('/api/reports/monthly?month=' + m + '&year=' + y + (d ? '&departmentId=' + d : ''), function (data) {
        amsPage('#mBody', data, function (r) {
            var pct = r.AttendancePercentage || 0;
            return '<tr><td class="text-muted small">' + esc(r.EmployeeCode) + '</td>'
                + '<td>' + esc(r.EmployeeName) + '</td>'
                + '<td class="text-muted">' + esc(r.Department) + '</td>'
                + '<td class="text-success">' + esc(r.PresentDays) + '</td>'
                + '<td class="text-danger">' + esc(r.AbsentDays) + '</td>'
                // "3 of 3" once an allowance is set, red past it — reporting only.
                + '<td class="' + (r.IsOverLateAllowance ? 'text-danger fw-semibold' : 'text-warning') + '">'
                + esc(r.LateAllowanceDisplay) + '</td>'
                + '<td class="text-info">' + esc(r.LeaveDays) + '</td>'
                + '<td>' + esc(r.HolidayDays) + '</td>'
                + '<td>' + (r.TotalWorkingHours || 0).toFixed(1) + '</td>'
                + '<td><span class="badge bg-' + (pct >= 90 ? 'success' : pct >= 75 ? 'warning' : 'danger') + '">'
                + pct.toFixed(1) + '%</span></td></tr>';
        }, { colspan: 10, empty: 'No data for this month.', label: 'employee' });
    }).fail(function () { $('#mBody').html('<tr><td colspan="10" class="text-danger text-center py-3">Failed to load.</td></tr>'); });
}

function loadDaily() {
    var d = $('#dDate').val();
    if (!d) { notifyError('Select a date first.'); return; }
    $.getJSON('/api/reports/daily?date=' + d, function (data) {
        amsPage('#dBody', data, function (r) {
            var b = r.StatusDisplay === 'Present' ? 'success'
                  : r.StatusDisplay === 'Absent'  ? 'danger'
                  : r.StatusDisplay === 'Late'    ? 'warning' : 'secondary';
            return '<tr><td class="text-muted small">' + esc(r.EmployeeCode) + '</td>'
                + '<td>' + esc(r.EmployeeName) + '</td>'
                + '<td class="text-muted">' + esc(r.Department) + '</td>'
                + '<td>' + esc(r.CheckInDisplay) + '</td>'
                + '<td>' + esc(r.CheckOutDisplay) + '</td>'
                + '<td>' + (r.WorkingHours ? r.WorkingHours.toFixed(1) + 'h' : '—') + '</td>'
                + '<td><span class="badge bg-' + b + '">' + esc(r.StatusDisplay) + '</span></td>'
                + '<td>' + (r.IsLate ? '<span class="badge bg-warning text-dark">' + esc(r.LateMinutes) + 'm</span>' : '—')
                + '</td></tr>';
        }, { colspan: 8, empty: 'No data for this date.', label: 'employee' });
    }).fail(function () { $('#dBody').html('<tr><td colspan="8" class="text-danger text-center py-3">Failed to load.</td></tr>'); });
}

function loadLate() {
    var f = $('#lFrom').val(), t = $('#lTo').val(), d = $('#lDept').val();
    if (!f || !t) { notifyError('Select a date range first.'); return; }
    $.getJSON('/api/reports/late?from=' + f + '&to=' + t + (d ? '&departmentId=' + d : ''), function (data) {
        amsPage('#lBody', data, function (r) {
            return '<tr><td>' + dt(r.AttendanceDate) + '</td>'
                + '<td class="text-muted small">' + esc(r.EmployeeCode) + '</td>'
                + '<td>' + esc(r.EmployeeName) + '</td>'
                + '<td class="text-muted">' + esc(r.Department) + '</td>'
                + '<td>' + esc(r.CheckInDisplay) + '</td>'
                + '<td><span class="badge bg-warning text-dark">' + esc(r.LateMinutes) + ' min</span></td></tr>';
        }, { colspan: 6, empty: 'Nobody was late in this range.', label: 'late arrival' });
    }).fail(function () { $('#lBody').html('<tr><td colspan="6" class="text-danger text-center py-3">Failed to load.</td></tr>'); });
}

function loadLeaveRpt() {
    var f = $('#lvFrom').val(), t = $('#lvTo').val(), d = $('#lvDept').val();
    if (!f || !t) { notifyError('Select a date range first.'); return; }
    $.getJSON('/api/reports/leave?from=' + f + '&to=' + t + (d ? '&departmentId=' + d : ''), function (data) {
        amsPage('#lvBody', data, function (r) {
            var b = r.StatusDisplay === 'Approved' ? 'success'
                  : r.StatusDisplay === 'Rejected' ? 'danger'
                  : r.StatusDisplay === 'Pending'  ? 'warning' : 'secondary';
            return '<tr><td>' + esc(r.EmployeeName) + '</td>'
                + '<td class="text-muted">' + esc(r.Department) + '</td>'
                + '<td>' + esc(r.LeaveTypeName) + '</td>'
                + '<td>' + dt(r.FromDate) + '</td>'
                + '<td>' + dt(r.ToDate) + '</td>'
                + '<td>' + esc(r.TotalDays) + '</td>'
                + '<td><span class="badge bg-' + b + '">' + esc(r.StatusDisplay) + '</span></td></tr>';
        }, { colspan: 7, empty: 'No leave in this range.', label: 'request' });
    }).fail(function () { $('#lvBody').html('<tr><td colspan="7" class="text-danger text-center py-3">Failed to load.</td></tr>'); });
}

function loadEmpList() {
    var d = $('#eDept').val();
    $.getJSON('/api/reports/employees' + (d ? '?departmentId=' + d : ''), function (data) {
        amsPage('#eBody', data, function (e) {
            return '<tr><td class="fw-semibold text-primary">' + esc(e.EmployeeCode) + '</td>'
                + '<td>' + esc(e.FullName) + '</td>'
                + '<td class="text-muted">' + esc(e.Department) + '</td>'
                + '<td class="text-muted">' + esc(e.Designation) + '</td>'
                + '<td class="text-muted">' + esc(e.Branch) + '</td>'
                + '<td>' + (e.Phone ? esc(e.Phone) : '—') + '</td>'
                + '<td class="small">' + (e.Email ? esc(e.Email) : '—') + '</td>'
                + '<td>' + (e.IsActive
                    ? '<span class="badge bg-success">Active</span>'
                    : '<span class="badge bg-secondary">Inactive</span>') + '</td></tr>';
        }, { colspan: 8, empty: 'No employees for this filter.', label: 'employee' });
    }).fail(function () { $('#eBody').html('<tr><td colspan="8" class="text-danger text-center py-3">Failed to load.</td></tr>'); });
}
