/* ── Admin Reports JavaScript ── */

$(function () {
    var today = new Date().toISOString().split('T')[0];
    $('#lFrom,#lvFrom').val(new Date(new Date().setDate(1)).toISOString().split('T')[0]);
    $('#lTo,#lvTo').val(today);
    $.getJSON('/api/departments', function (d) {
        var o = '<option value="">All</option>';
        d.forEach(function (x) { o += '<option value="' + x.Id + '">' + x.Name + '</option>'; });
        $('#mDept,#lDept,#lvDept,#eDept').html(o.replace('<option value="">All</option>','<option value="">All Departments</option>'));
    });
});

function loadMonthly() {
    var m = $('#mMonth').val(), y = $('#mYear').val(), d = $('#mDept').val();
    $.getJSON('/api/reports/monthly?month=' + m + '&year=' + y + (d ? '&departmentId=' + d : ''), function (data) {
        if (!data.length) { $('#mBody').html('<tr><td colspan="10" class="text-center text-muted py-3">No data.</td></tr>'); return; }
        var html = '';
        data.forEach(function (r) {
            html += '<tr><td class="text-muted small">' + r.EmployeeCode + '</td><td>' + r.EmployeeName + '</td><td class="text-muted">' + r.Department + '</td>'
                + '<td class="text-success">' + r.PresentDays + '</td><td class="text-danger">' + r.AbsentDays + '</td>'
                + '<td class="text-warning">' + r.LateDays + '</td><td class="text-info">' + r.LeaveDays + '</td>'
                + '<td>' + r.HolidayDays + '</td><td>' + r.TotalWorkingHours.toFixed(1) + '</td>'
                + '<td><span class="badge bg-' + (r.AttendancePercentage>=90?'success':r.AttendancePercentage>=75?'warning':'danger') + '">' + r.AttendancePercentage.toFixed(1) + '%</span></td></tr>';
        });
        $('#mBody').html(html);
    }).fail(function () { $('#mBody').html('<tr><td colspan="10" class="text-danger text-center py-3">Failed to load.</td></tr>'); });
}

function loadDaily() {
    var d = $('#dDate').val(); if (!d) { alert('Select date.'); return; }
    $.getJSON('/api/reports/daily?date=' + d, function (data) {
        if (!data.length) { $('#dBody').html('<tr><td colspan="8" class="text-center text-muted py-3">No data.</td></tr>'); return; }
        var html = '';
        data.forEach(function (r) {
            var b = r.StatusDisplay==='Present'?'success':r.StatusDisplay==='Absent'?'danger':r.StatusDisplay==='Late'?'warning':'secondary';
            html += '<tr><td class="text-muted small">' + r.EmployeeCode + '</td><td>' + r.EmployeeName + '</td><td class="text-muted">' + r.Department + '</td>'
                + '<td>' + r.CheckInDisplay + '</td><td>' + r.CheckOutDisplay + '</td>'
                + '<td>' + (r.WorkingHours?r.WorkingHours.toFixed(1)+'h':'—') + '</td>'
                + '<td><span class="badge bg-' + b + '">' + r.StatusDisplay + '</span></td>'
                + '<td>' + (r.IsLate?'<span class="badge bg-warning text-dark">'+r.LateMinutes+'m</span>':'—') + '</td></tr>';
        });
        $('#dBody').html(html);
    }).fail(function () { $('#dBody').html('<tr><td colspan="8" class="text-danger text-center py-3">Failed to load.</td></tr>'); });
}

function loadLate() {
    var f=$('#lFrom').val(),t=$('#lTo').val(),d=$('#lDept').val(); if(!f||!t){alert('Select date range.');return;}
    $.getJSON('/api/reports/late?from='+f+'&to='+t+(d?'&departmentId='+d:''), function (data) {
        if(!data.length){$('#lBody').html('<tr><td colspan="6" class="text-center text-muted py-3">No data.</td></tr>');return;}
        var html='';
        data.forEach(function(r){html+='<tr><td>'+new Date(r.AttendanceDate).toLocaleDateString()+'</td><td class="text-muted small">'+r.EmployeeCode+'</td><td>'+r.EmployeeName+'</td><td class="text-muted">'+r.Department+'</td><td>'+r.CheckInDisplay+'</td><td><span class="badge bg-warning text-dark">'+r.LateMinutes+' min</span></td></tr>';});
        $('#lBody').html(html);
    }).fail(function(){$('#lBody').html('<tr><td colspan="6" class="text-danger text-center py-3">Failed.</td></tr>');});
}

function loadLeaveRpt() {
    var f=$('#lvFrom').val(),t=$('#lvTo').val(),d=$('#lvDept').val(); if(!f||!t){alert('Select date range.');return;}
    $.getJSON('/api/reports/leave?from='+f+'&to='+t+(d?'&departmentId='+d:''), function (data) {
        if(!data.length){$('#lvBody').html('<tr><td colspan="7" class="text-center text-muted py-3">No data.</td></tr>');return;}
        var html='';
        data.forEach(function(r){
            var b=r.StatusDisplay==='Approved'?'success':r.StatusDisplay==='Rejected'?'danger':r.StatusDisplay==='Pending'?'warning':'secondary';
            html+='<tr><td>'+r.EmployeeName+'</td><td class="text-muted">'+r.Department+'</td><td>'+r.LeaveTypeName+'</td><td>'+new Date(r.FromDate).toLocaleDateString()+'</td><td>'+new Date(r.ToDate).toLocaleDateString()+'</td><td>'+r.TotalDays+'</td><td><span class="badge bg-'+b+'">'+r.StatusDisplay+'</span></td></tr>';
        });
        $('#lvBody').html(html);
    }).fail(function(){$('#lvBody').html('<tr><td colspan="7" class="text-danger text-center py-3">Failed.</td></tr>');});
}

function loadEmpList() {
    var d=$('#eDept').val();
    $.getJSON('/api/reports/employees'+(d?'?departmentId='+d:''), function (data) {
        if(!data.length){$('#eBody').html('<tr><td colspan="8" class="text-center text-muted py-3">No data.</td></tr>');return;}
        var html='';
        data.forEach(function(e){html+='<tr><td class="fw-semibold text-primary">'+e.EmployeeCode+'</td><td>'+e.FullName+'</td><td class="text-muted">'+e.Department+'</td><td class="text-muted">'+e.Designation+'</td><td class="text-muted">'+e.Branch+'</td><td>'+(e.Phone||'—')+'</td><td class="small">'+(e.Email||'—')+'</td><td>'+(e.IsActive?'<span class="badge bg-success">Active</span>':'<span class="badge bg-secondary">Inactive</span>')+'</td></tr>';});
        $('#eBody').html(html);
    }).fail(function(){$('#eBody').html('<tr><td colspan="8" class="text-danger text-center py-3">Failed.</td></tr>');});
}
