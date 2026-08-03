/* ── Admin Shifts Management JavaScript ── */

var allShifts = [], allAssign = [], allEmployees = [];
$(function () { loadShifts(); loadAssign(); loadEmployees(); });

// ── Shifts ───────────────────────────────────────────────────────────────────
function loadShifts() {
    $.getJSON('/api/shifts', function (d) { allShifts = d; renderShifts(d); })
     .fail(function () { $('#shiftBody').html('<tr><td colspan="8" class="text-danger text-center py-3">Failed to load.</td></tr>'); });
}

function renderShifts(data) {
    if (!data.length) { $('#shiftBody').html('<tr><td colspan="8" class="text-center text-muted py-3">No shifts defined.</td></tr>'); return; }
    var html = '';
    data.forEach(function (s, i) {
        html += '<tr>'
            + '<td class="text-muted">' + (i+1) + '</td>'
            + '<td class="fw-semibold">' + s.Name + '</td>'
            + '<td>' + s.StartTimeDisplay + '</td>'
            + '<td>' + s.EndTimeDisplay + '</td>'
            + '<td>' + s.GraceMinutes + '</td>'
            + '<td class="small text-muted">' + s.WeeklyOffDays + '</td>'
            + '<td>' + (s.IsActive ? '<span class="badge bg-success">Active</span>' : '<span class="badge bg-danger">Inactive</span>') + '</td>'
            + '<td>'
            + '<button class="btn btn-sm btn-outline-primary me-1" onclick="editShift(' + s.Id + ')" title="Edit"><i class="fa fa-pencil"></i></button>'
            + '<button class="btn btn-sm btn-outline-danger" onclick="deleteShift(' + s.Id + ')" title="Delete"><i class="fa fa-trash"></i></button>'
            + '</td></tr>';
    });
    $('#shiftBody').html(html);
}

function openShiftModal(id, name, start, end, grace, weekoff, active) {
    $('#shiftId').val(id || 0); $('#shiftName').val(name || '');
    $('#shiftStart').val(start || '09:00'); $('#shiftEnd').val(end || '18:00');
    $('#shiftGrace').val(grace || 0); $('#shiftWeeklyOff').val(weekoff || 'Saturday,Sunday');
    $('#shiftActive').prop('checked', active !== false);
    $('#shiftModalTitle').text(id ? 'Edit Shift' : 'Add Shift');
    new bootstrap.Modal('#shiftModal').show();
}

function editShift(id) {
    var s = allShifts.find(function (x) { return x.Id === id; });
    if (!s) return;
    var st = (s.StartTime || '').substring(0, 5);
    var et = (s.EndTime || '').substring(0, 5);
    openShiftModal(s.Id, s.Name, st, et, s.GraceMinutes, s.WeeklyOffDays, s.IsActive);
}

function saveShift() {
    var name = $('#shiftName').val().trim();
    if (!name || !$('#shiftStart').val() || !$('#shiftEnd').val()) { alert('Name, Start Time and End Time are required.'); return; }
    var dto = {
        Id: parseInt($('#shiftId').val()) || 0, Name: name,
        StartTime: $('#shiftStart').val() + ':00', EndTime: $('#shiftEnd').val() + ':00',
        GraceMinutes: parseInt($('#shiftGrace').val()) || 0,
        WeeklyOffDays: $('#shiftWeeklyOff').val(), IsActive: $('#shiftActive').is(':checked')
    };
    $.ajax({ url: '/api/shifts', type: 'POST', contentType: 'application/json', data: JSON.stringify(dto),
        success: function () { bootstrap.Modal.getInstance('#shiftModal').hide(); loadShifts(); },
        error: function (xhr) { alert('Error: ' + (xhr.responseText || 'Save failed.')); }
    });
}

function deleteShift(id) {
    if (!confirm('Delete this shift?')) return;
    var uid = window.getCurrentUserId();
    $.ajax({ url: '/api/shifts/' + id + '?deletedBy=' + uid, type: 'DELETE',
        success: function () { loadShifts(); },
        error: function (xhr) { alert('Error: ' + (xhr.responseText || 'Delete failed.')); }
    });
}

// ── Assignments ───────────────────────────────────────────────────────────────
function loadAssign() {
    $.getJSON('/api/shifts/assignments', function (d) { allAssign = d; renderAssign(d); })
     .fail(function () { $('#assignBody').html('<tr><td colspan="8" class="text-danger text-center py-3">Failed to load assignments.</td></tr>'); });
}

function filterAssign() {
    var q = $('#assignSearch').val().toLowerCase();
    renderAssign(allAssign.filter(function (a) { return !q || a.EmployeeName.toLowerCase().includes(q) || a.EmployeeCode.toLowerCase().includes(q); }));
}

function renderAssign(data) {
    if (!data.length) { $('#assignBody').html('<tr><td colspan="8" class="text-center text-muted py-3">No assignments.</td></tr>'); return; }
    var html = '';
    data.forEach(function (a) {
        html += '<tr>'
            + '<td class="text-muted small">' + a.EmployeeCode + '</td>'
            + '<td>' + a.EmployeeName + '</td>'
            + '<td class="fw-semibold">' + a.ShiftName + '</td>'
            + '<td class="text-muted">' + a.StartTimeDisplay + '</td>'
            + '<td class="text-muted">' + a.EndTimeDisplay + '</td>'
            + '<td>' + new Date(a.EffectiveFrom).toLocaleDateString() + '</td>'
            + '<td>' + (a.EffectiveTo ? new Date(a.EffectiveTo).toLocaleDateString() : '—') + '</td>'
            + '<td></td></tr>';
    });
    $('#assignBody').html(html);
}

function loadEmployees() {
    $.getJSON('/api/employees', function (data) {
        allEmployees = data;
        var opts = '<option value="">-- Select Employee --</option>';
        data.forEach(function (e) { opts += '<option value="' + e.Id + '">' + e.EmployeeCode + ' - ' + e.FullName + '</option>'; });
        $('#assignEmp').html(opts);
    });
}

function openAssignModal() {
    var shiftOpts = '<option value="">-- Select Shift --</option>';
    allShifts.filter(function (s) { return s.IsActive; }).forEach(function (s) { shiftOpts += '<option value="' + s.Id + '">' + s.Name + '</option>'; });
    $('#assignShift').html(shiftOpts);
    $('#assignFrom').val(new Date().toISOString().split('T')[0]);
    $('#assignTo').val('');
    new bootstrap.Modal('#assignModal').show();
}

function saveAssign() {
    var emp = parseInt($('#assignEmp').val()); var shf = parseInt($('#assignShift').val()); var frm = $('#assignFrom').val();
    if (!emp || !shf || !frm) { alert('Employee, Shift and Effective From are required.'); return; }
    var dto = { EmployeeId: emp, ShiftId: shf, EffectiveFrom: frm, EffectiveTo: $('#assignTo').val() || null };
    $.ajax({ url: '/api/shifts/assign', type: 'POST', contentType: 'application/json', data: JSON.stringify(dto),
        success: function () { bootstrap.Modal.getInstance('#assignModal').hide(); loadAssign(); },
        error: function (xhr) { alert('Error: ' + (xhr.responseText || 'Assign failed.')); }
    });
}
