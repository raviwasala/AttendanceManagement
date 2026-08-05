/* ── Admin Biometric Import JavaScript ── */

var employeesMap = {}; // EnrollId -> Name mapping

$(function () {
    var today = new Date().toISOString().split('T')[0];
    var firstDay = new Date(new Date().setDate(1)).toISOString().split('T')[0];
    $('#importFrom').val(firstDay); 
    $('#importTo').val(today);
    loadEmployees();
});

function loadEmployees() {
    $.getJSON('/api/employees', function (data) {
        if (data && data.length) {
            data.forEach(function (e) {
                if (e.biometricEnrollId || e.BiometricEnrollId) {
                    var bId = e.biometricEnrollId || e.BiometricEnrollId;
                    var name = (e.firstName || e.FirstName || '') + ' ' + (e.lastName || e.LastName || '');
                    employeesMap[bId] = name.trim();
                }
            });
        }
    });
}

function getFormData() {
    var fileInput = $('#importFile')[0];
    if (!fileInput || !fileInput.files || fileInput.files.length === 0) {
        alert('Please select a biometric file (.csv, .xlsx, .xls, .mdb, .accdb).');
        return null;
    }
    var fd = new FormData();
    fd.append('file', fileInput.files[0]);
    fd.append('fromDate', $('#importFrom').val());
    fd.append('toDate', $('#importTo').val());
    return fd;
}

function previewFile() {
    var fd = getFormData(); if (!fd) return;
    $('#previewCard').hide(); 
    $('#previewBody').html('');
    $('#resultPanel').html('<div class="text-muted"><i class="fa fa-spinner fa-spin me-2"></i>Reading biometric file...</div>');

    $.ajax({
        url: '/api/import/preview', 
        type: 'POST', 
        data: fd, 
        processData: false, 
        contentType: false,
        success: function (data) {
            if (!data || !data.length) {
                $('#resultPanel').html('<div class="alert alert-warning">No valid punch records found in file.</div>');
                return;
            }

            renderEditableGrid(data);
            $('#resultPanel').html('<div class="alert alert-info py-2 mb-0"><i class="fa fa-info-circle me-1"></i>Loaded <strong>' + data.length + '</strong> punch entries into the grid below. You can edit times, enroll IDs, or select specific rows before importing.</div>');
        },
        error: function (xhr) {
            var msg = xhr.responseText || 'Unknown error occurred.';
            $('#resultPanel').html('<div class="alert alert-danger"><i class="fa fa-exclamation-triangle me-1"></i>Preview failed: ' + msg + '</div>');
        }
    });
}

function renderEditableGrid(data) {
    var html = '';
    data.forEach(function (p, i) {
        var enrollId = p.enrollId !== undefined ? p.enrollId : (p.EnrollId !== undefined ? p.EnrollId : '');
        var empName = p.empName || p.EmpName || employeesMap[enrollId] || '—';
        var rawTime = p.punchTime || p.PunchTime || '';
        var isoTime = formatIsoDateTime(rawTime);
        var deviceId = p.deviceId || p.DeviceId || '—';

        html += createGridRowHtml(i + 1, enrollId, empName, isoTime, deviceId, true);
    });

    $('#previewBody').html(html); 
    $('#previewCount').text(data.length + ' records'); 
    $('#previewCard').show();
}

function createGridRowHtml(rowNum, enrollId, empName, isoTime, deviceId, checked) {
    var chkAttr = checked ? 'checked' : '';
    return '<tr class="punch-row">'
        + '<td class="text-center ps-3"><input type="checkbox" class="form-check-input row-chk" ' + chkAttr + '></td>'
        + '<td class="text-muted row-num">' + rowNum + '</td>'
        + '<td><input type="number" class="form-control form-control-sm cell-enroll" value="' + enrollId + '" onchange="onEnrollChange(this)"></td>'
        + '<td class="cell-emp-name text-truncate" style="max-width:180px;">' + empName + '</td>'
        + '<td><input type="datetime-local" class="form-control form-control-sm cell-time" value="' + isoTime + '"></td>'
        + '<td class="text-muted"><input type="text" class="form-control form-control-sm cell-device" value="' + deviceId + '"></td>'
        + '<td class="text-center">'
        + '  <button class="btn btn-xs btn-outline-danger py-0 px-2" onclick="removeRow(this)"><i class="fa fa-trash"></i></button>'
        + '</td>'
        + '</tr>';
}

function formatIsoDateTime(dateStr) {
    if (!dateStr) return '';
    try {
        var d = new Date(dateStr);
        if (isNaN(d.getTime())) return '';
        var pad = function(n) { return n < 10 ? '0' + n : n; };
        return d.getFullYear() + '-' 
            + pad(d.getMonth() + 1) + '-' 
            + pad(d.getDate()) + 'T' 
            + pad(d.getHours()) + ':' 
            + pad(d.getMinutes());
    } catch(e) {
        return '';
    }
}

function onEnrollChange(input) {
    var val = $(input).val();
    var row = $(input).closest('tr');
    var name = employeesMap[val] || '—';
    row.find('.cell-emp-name').text(name);
}

function addManualRow() {
    var count = $('#previewBody tr').length + 1;
    var nowIso = formatIsoDateTime(new Date());
    var html = createGridRowHtml(count, '', '—', nowIso, 'DEV-1', true);
    $('#previewBody').append(html);
    $('#previewCount').text($('#previewBody tr').length + ' records');
    $('#previewCard').show();
}

function removeRow(btn) {
    $(btn).closest('tr').remove();
    reindexRows();
}

function reindexRows() {
    $('#previewBody tr').each(function (idx, tr) {
        $(tr).find('.row-num').text(idx + 1);
    });
    $('#previewCount').text($('#previewBody tr').length + ' records');
}

function toggleSelectAll(check) {
    $('.row-chk').prop('checked', check);
}

function importEditedPunches() {
    var selectedPunches = [];
    $('#previewBody tr').each(function () {
        var row = $(this);
        if (row.find('.row-chk').is(':checked')) {
            var enrollId = parseInt(row.find('.cell-enroll').val());
            var punchTime = row.find('.cell-time').val();
            var deviceId = row.find('.cell-device').val();
            var empName = row.find('.cell-emp-name').text();

            if (enrollId && punchTime) {
                selectedPunches.push({
                    EnrollId: enrollId,
                    PunchTime: punchTime,
                    EmpName: empName !== '—' ? empName : null,
                    DeviceId: deviceId !== '—' ? deviceId : null
                });
            }
        }
    });

    if (selectedPunches.length === 0) {
        alert('Please check at least one valid row with Enroll ID and Punch Time.');
        return;
    }

    $('#resultPanel').html('<div class="text-muted"><i class="fa fa-spinner fa-spin me-2"></i>Processing ' + selectedPunches.length + ' selected punch records...</div>');

    $.ajax({
        url: '/api/import/process-edited',
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(selectedPunches),
        success: function (result) {
            showImportSummary(result);
        },
        error: function (xhr) {
            var msg = xhr.responseText || 'Error processing punches.';
            $('#resultPanel').html('<div class="alert alert-danger"><i class="fa fa-exclamation-triangle me-1"></i>Import failed: ' + msg + '</div>');
        }
    });
}

function importDirectFile() {
    var fd = getFormData(); if (!fd) return;
    $('#resultPanel').html('<div class="text-muted"><i class="fa fa-spinner fa-spin me-2"></i>Importing direct file...</div>');

    $.ajax({
        url: '/api/import/file', 
        type: 'POST', 
        data: fd, 
        processData: false, 
        contentType: false,
        success: function (result) {
            showImportSummary(result);
        },
        error: function (xhr) {
            var msg = xhr.responseText || 'Unknown error occurred.';
            $('#resultPanel').html('<div class="alert alert-danger">Import failed: ' + msg + '</div>');
        }
    });
}

function showImportSummary(result) {
    // The API has been seen to serialise either casing, so both are read.
    var pick = function (lower, upper) {
        return result[lower] !== undefined ? result[lower] : (result[upper] || 0);
    };

    var total     = pick('totalRead', 'TotalRead');
    var inserted  = pick('inserted', 'Inserted');
    var updated   = pick('updated', 'Updated');
    var skipped   = pick('skipped', 'Skipped');
    var manual    = pick('skippedManual', 'SkippedManual');
    var unmatched = pick('unmatchedPunches', 'UnmatchedPunches');
    var failed    = pick('failed', 'Failed');
    var errors    = result.errors || result.Errors || [];
    var warnings  = result.warnings || result.Warnings || [];

    var row = function (label, value, cls, hint) {
        return '<tr><td>' + esc(label)
             + (hint ? '<div class="text-muted" style="font-size:.72rem;">' + esc(hint) + '</div>' : '')
             + '</td><td class="fw-bold ' + (cls || '') + '">' + esc(value) + '</td></tr>';
    };

    var html = '<div class="alert alert-success mb-3"><strong><i class="fa fa-check-circle me-1"></i>'
             + 'Biometric Import Complete</strong></div>'
        + '<table class="table table-sm border mb-2">'
        + row('Punches read', total)
        + row('Days created', inserted, 'text-success')
        + row('Days updated', updated, 'text-primary',
              'Already imported, refreshed from the device — usually a check-out that had not happened yet.')
        + row('Unchanged', skipped, 'text-muted', 'Same punches as the last import.')
        + row('Left as manually corrected', manual, 'text-warning',
              'Someone edited these by hand, so the device did not overwrite them.')
        + row('Punches matching no employee', unmatched, unmatched ? 'text-danger' : 'text-muted',
              'Set the Biometric Enroll ID on the employee record to import these.')
        + row('Failed', failed, failed ? 'text-danger' : 'text-muted')
        + '</table>';

    if (inserted + updated > 0) {
        html += '<div class="alert alert-light border py-2 mb-2 small">'
              + '<i class="feather icon-check-circle text-success me-1"></i>'
              + 'Lateness, early leave, working hours and overtime were calculated against each '
              + 'employee\'s rostered shift. Review them in '
              + '<a href="/Admin/AttendanceReview">Attendance Review</a>.</div>';
    }

    if (warnings && warnings.length > 0) {
        html += '<div class="alert alert-warning py-2 mb-2"><strong class="small">Warnings ('
              + warnings.length + '):</strong><br><span class="small">'
              + warnings.map(esc).join('<br>') + '</span></div>';
    }

    if (errors && errors.length > 0) {
        html += '<div class="alert alert-danger py-2 mb-0"><strong class="small">Errors ('
              + errors.length + '):</strong><br><span class="small">'
              + errors.map(esc).join('<br>') + '</span></div>';
    }

    $('#resultPanel').html(html);
}
