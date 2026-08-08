/* ── Admin Shifts Management JavaScript ── */

var allShifts = [], allAssign = [], allEmployees = [];
$(function () { loadShifts(); loadAssign(); loadEmployees(); });

// ── Shifts ───────────────────────────────────────────────────────────────────
function loadShifts() {
    $.getJSON('/api/shifts', function (d) { allShifts = d; renderShifts(d); })
     .fail(function () { $('#shiftBody').html('<tr><td colspan="8" class="text-danger text-center py-3">Failed to load.</td></tr>'); });
}

function renderShifts(data) {
    amsPage('#shiftBody', data, function (s) {
        var timing = esc(s.StartTimeDisplay) + ' – ' + esc(s.EndTimeDisplay);
        if (s.IsNightShift) {
            timing += ' <span class="badge bg-dark" title="Ends the following day">night</span>';
        }

        var ot = s.IsOtEnabled
            ? '<span class="badge bg-success">On</span>'
              + '<div class="text-muted" style="font-size:.68rem;">'
              // "after end only" rather than "after end": the word that matters is the one
              // saying an early start is excluded.
              + (s.OtCountsFromShiftEnd
                    ? 'after end only' + (s.OtStartAfterMinutes ? ' +' + s.OtStartAfterMinutes + 'm' : '')
                    : 'over ' + s.EffectiveStandardHours + 'h, incl. early')
              + '</div>'
            : '<span class="badge bg-light text-muted">Off</span>';

        return '<tr>'
            + '<td>' + (s.ShiftCode ? '<code>' + esc(s.ShiftCode) + '</code>' : '<span class="text-muted">—</span>') + '</td>'
            + '<td class="fw-semibold">' + esc(s.Name) + '</td>'
            + '<td class="small">' + timing + '</td>'
            + '<td class="small">' + s.SpanHours + 'h</td>'
            + '<td class="small">' + s.GraceMinutes + ' / ' + s.GraceOutMinutes
              + (s.AllowedLateDaysPerMonth
                    ? '<div class="text-muted" style="font-size:.68rem;">'
                      + esc(s.AllowedLateDaysPerMonth) + ' late days/mth</div>'
                    : '')
              + '</td>'
            + '<td class="small">' + (s.BreakMinutes ? s.BreakMinutes + 'm' : '—') + '</td>'
            + '<td class="small">' + ot + '</td>'
            + '<td class="small text-muted">' + esc(s.WeeklyOffDays) + '</td>'
            + '<td>' + (s.IsActive ? '<span class="badge bg-success">Active</span>' : '<span class="badge bg-danger">Inactive</span>') + '</td>'
            + '<td>'
            + '<button class="btn btn-sm btn-outline-primary me-1" onclick="editShift(' + s.Id + ')" title="Edit"><i class="fa fa-pencil"></i></button>'
            + '<button class="btn btn-sm btn-outline-danger" onclick="deleteShift(' + s.Id + ')" title="Delete"><i class="fa fa-trash"></i></button>'
            + '</td></tr>';
    }, { colspan: 10, empty: 'No shifts defined.', label: 'shift' });
}

function openShiftModal(s) {
    s = s || {};
    $('#shiftError').addClass('d-none').text('');
    $('#shiftId').val(s.Id || 0);
    $('#shiftCode').val(s.ShiftCode || '');
    $('#shiftName').val(s.Name || '');
    $('#shiftStart').val(s.StartTime ? s.StartTime.substring(0, 5) : '09:00');
    $('#shiftEnd').val(s.EndTime ? s.EndTime.substring(0, 5) : '18:00');
    $('#shiftNight').prop('checked', !!s.IsNightShift);
    $('#shiftGrace').val(s.GraceMinutes || 0);
    $('#shiftGraceOut').val(s.GraceOutMinutes || 0);
    $('#shiftBreak').val(s.BreakMinutes || 0);
    $('#shiftStdHours').val(s.StandardWorkingHours || 0);
    $('#shiftLateAllowance').val(s.AllowedLateDaysPerMonth || 0);
    $('#shiftWorkingDays').val(s.WorkingDaysPerMonth || 0);
    $('#shiftOtEnabled').prop('checked', s.Id ? !!s.IsOtEnabled : true);
    $('#shiftOtAfter').val(s.OtStartAfterMinutes || 0);
    $('#shiftOtBasis').val(String(s.Id ? !!s.OtCountsFromShiftEnd : true));
    updateOtBasisHelp();
    $('#shiftWeeklyOff').val(s.WeeklyOffDays || 'Saturday,Sunday');
    $('#shiftActive').prop('checked', s.Id ? !!s.IsActive : true);

    $('#shiftModalTitle').text(s.Id ? 'Edit Shift' : 'Add Shift');
    toggleOtFields();
    updateShiftSummary();
    new bootstrap.Modal('#shiftModal').show();
}

function editShift(id) {
    var s = allShifts.find(function (x) { return x.Id === id; });
    if (s) openShiftModal(s);
}

/* Keeps the night-shift flag honest: the server rejects a mismatch between the flag and the
   times, so set it from the times rather than making the user work it out. */
function onShiftTimeChange() {
    var st = $('#shiftStart').val(), et = $('#shiftEnd').val();
    if (st && et) $('#shiftNight').prop('checked', et <= st);
    updateShiftSummary();
}

function toggleOtFields() {
    $('.ot-field').toggle($('#shiftOtEnabled').is(':checked'));
    updateShiftSummary();
}

/* Spells out what the chosen basis means for someone who starts early, and greys the
   threshold field when it no longer applies. The two options differ only in a case that is
   invisible from the labels alone — an early start — so the consequence is stated here
   rather than left to be discovered in a payslip. */
function updateOtBasisHelp() {
    var fromEnd = $('#shiftOtBasis').val() === 'true';

    $('#shiftOtBasisHelp').html(fromEnd
        ? 'Only time worked <strong>after the shift ends</strong> is paid as overtime. '
          + 'Clocking in early earns nothing.'
        : 'Any time beyond the shift\'s standard hours is paid as overtime, '
          + '<strong>including an early start</strong>.');

    // The threshold is measured from the shift end, so it means nothing on the other basis.
    $('#shiftOtAfter').prop('disabled', !fromEnd)
                      .closest('.ot-field').toggleClass('opacity-50', !fromEnd);
}

$(document).on('change', '#shiftOtBasis', updateOtBasisHelp);

/* Shows the arithmetic the shift implies, so a night shift or a long break is obvious
   before saving rather than surfacing later as odd attendance. */
function updateShiftSummary() {
    var st = $('#shiftStart').val(), et = $('#shiftEnd').val();
    if (!st || !et) { $('#shiftSummary').text(''); return; }

    var toMin = function (t) { var p = t.split(':'); return (+p[0]) * 60 + (+p[1]); };
    var s = toMin(st), e = toMin(et);
    var span = e > s ? e - s : (e + 1440) - s;
    var brk = parseInt($('#shiftBreak').val()) || 0;
    var paid = Math.max(0, span - brk);

    var txt = 'Span ' + (span / 60).toFixed(2) + ' h';
    if (brk) txt += ' · break ' + brk + 'm · paid ' + (paid / 60).toFixed(2) + ' h';
    if (e <= s) txt += ' · crosses midnight, ends the next day';
    if (!$('#shiftOtEnabled').is(':checked')) txt += ' · overtime not recorded';

    $('#shiftSummary').text(txt);
}

function saveShift() {
    var name = $('#shiftName').val().trim();
    if (!name || !$('#shiftStart').val() || !$('#shiftEnd').val()) {
        showShiftError('Name, Start Time and End Time are required.');
        return;
    }
    var dto = {
        Id: parseInt($('#shiftId').val()) || 0,
        ShiftCode: $('#shiftCode').val().trim() || null,
        Name: name,
        StartTime: $('#shiftStart').val() + ':00',
        EndTime: $('#shiftEnd').val() + ':00',
        IsNightShift: $('#shiftNight').is(':checked'),
        GraceMinutes: parseInt($('#shiftGrace').val()) || 0,
        GraceOutMinutes: parseInt($('#shiftGraceOut').val()) || 0,
        BreakMinutes: parseInt($('#shiftBreak').val()) || 0,
        StandardWorkingHours: parseFloat($('#shiftStdHours').val()) || 0,
        AllowedLateDaysPerMonth: parseInt($('#shiftLateAllowance').val(), 10) || 0,
        WorkingDaysPerMonth: parseInt($('#shiftWorkingDays').val(), 10) || 0,
        IsOtEnabled: $('#shiftOtEnabled').is(':checked'),
        OtStartAfterMinutes: parseInt($('#shiftOtAfter').val()) || 0,
        OtCountsFromShiftEnd: $('#shiftOtBasis').val() === 'true',
        WeeklyOffDays: $('#shiftWeeklyOff').val(),
        IsActive: $('#shiftActive').is(':checked')
    };
    $.ajax({ url: '/api/shifts', type: 'POST', contentType: 'application/json', data: JSON.stringify(dto),
        success: function () {
            bootstrap.Modal.getInstance('#shiftModal').hide();
            notifySuccess('Shift saved successfully.');
            loadShifts();
        },
        error: function (xhr) { showShiftError(xhr.responseText || 'Save failed.'); }
    });
}

function showShiftError(msg) { $('#shiftError').removeClass('d-none').text(msg); }

function deleteShift(id) {
    notifyConfirm({ title: 'Delete Shift', text: 'Are you sure you want to delete this shift schedule?', confirmText: 'Delete', icon: 'warning' }, function () {
        $.ajax({ url: '/api/shifts/' + id, type: 'DELETE',
            success: function () { 
                notifySuccess('Shift deleted successfully.');
                loadShifts(); 
            },
            error: function (xhr) { notifyError(xhr.responseText || 'Delete failed.'); }
        });
    });
}

// ── Assignments ───────────────────────────────────────────────────────────────
function loadAssign() {
    // filterAssign, not renderAssign: reloading after an assignment must keep the search.
    $.getJSON('/api/shifts/assignments', function (d) { allAssign = d || []; filterAssign(); })
     .fail(function () { $('#assignBody').html('<tr><td colspan="8" class="text-danger text-center py-3">Failed to load assignments.</td></tr>'); });
}

function filterAssign() {
    var q = $('#assignSearch').val().toLowerCase();
    renderAssign(allAssign.filter(function (a) { return !q || a.EmployeeName.toLowerCase().includes(q) || a.EmployeeCode.toLowerCase().includes(q); }));
}

function renderAssign(data) {
    amsPage('#assignBody', data, function (a) {
        return '<tr>'
            + '<td class="text-muted small">' + esc(a.EmployeeCode) + '</td>'
            + '<td>' + esc(a.EmployeeName) + '</td>'
            + '<td class="fw-semibold">' + esc(a.ShiftName) + '</td>'
            + '<td class="text-muted">' + esc(a.StartTimeDisplay) + '</td>'
            + '<td class="text-muted">' + esc(a.EndTimeDisplay) + '</td>'
            + '<td>' + new Date(a.EffectiveFrom).toLocaleDateString() + '</td>'
            + '<td>' + (a.EffectiveTo ? new Date(a.EffectiveTo).toLocaleDateString() : '—') + '</td>'
            + '<td></td></tr>';
    }, { colspan: 8, empty: 'No shift assignments.', label: 'assignment' });
}

function loadEmployees() {
    $.getJSON('/api/employees', function (data) {
        allEmployees = data;
        var opts = '<option value="">-- Select Employee --</option>';
        data.forEach(function (e) { opts += '<option value="' + esc(e.Id) + '">' + esc(e.EmployeeCode) + ' - ' + esc(e.FullName) + '</option>'; });
        $('#assignEmp').html(opts);
    });
}

function openAssignModal() {
    var shiftOpts = '<option value="">-- Select Shift --</option>';
    allShifts.filter(function (s) { return s.IsActive; }).forEach(function (s) { shiftOpts += '<option value="' + esc(s.Id) + '">' + esc(s.Name) + '</option>'; });
    $('#assignShift').html(shiftOpts);
    $('#assignFrom').val(new Date().toISOString().split('T')[0]);
    $('#assignTo').val('');
    new bootstrap.Modal('#assignModal').show();
}

function saveAssign() {
    var emp = parseInt($('#assignEmp').val()); var shf = parseInt($('#assignShift').val()); var frm = $('#assignFrom').val();
    if (!emp || !shf || !frm) { notifyError('Employee, Shift and Effective From are required.', 'Validation Error'); return; }
    var dto = { EmployeeId: emp, ShiftId: shf, EffectiveFrom: frm, EffectiveTo: $('#assignTo').val() || null };
    $.ajax({ url: '/api/shifts/assign', type: 'POST', contentType: 'application/json', data: JSON.stringify(dto),
        success: function () { 
            bootstrap.Modal.getInstance('#assignModal').hide(); 
            notifySuccess('Shift assigned successfully.');
            loadAssign(); 
        },
        error: function (xhr) { notifyError(xhr.responseText || 'Assign failed.'); }
    });
}
