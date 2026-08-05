/* ── Admin Biometric Import JavaScript ── */

var employeesMap = {};      // EnrollId -> Name
var previewedPunches = [];  // last preview, imported as-is

// A .mdb can yield well over a hundred thousand punches. Rendering them all locks
// the browser for seconds and tells the operator nothing the count does not; the
// preview exists to confirm the file was read correctly, not to be read in full.
var PREVIEW_ROW_LIMIT = 500;

$(function () {
    var today = new Date().toISOString().split('T')[0];
    var firstDay = new Date(new Date().setDate(1)).toISOString().split('T')[0];
    $('#importFrom').val(firstDay);
    $('#importTo').val(today);
    loadEmployees();
});

function loadEmployees() {
    $.getJSON('/api/employees', function (data) {
        (data || []).forEach(function (e) {
            var bId = e.BiometricEnrollId || e.biometricEnrollId;
            if (!bId) return;
            var name = ((e.FirstName || e.firstName || '') + ' ' + (e.LastName || e.lastName || '')).trim();
            employeesMap[bId] = name;
        });
    });
}

function getFormData() {
    var fileInput = $('#importFile')[0];
    if (!fileInput || !fileInput.files || fileInput.files.length === 0) {
        notifyError('Please select a biometric file (.csv, .xlsx, .xls, .mdb, .accdb).');
        return null;
    }
    var fd = new FormData();
    fd.append('file', fileInput.files[0]);
    fd.append('fromDate', $('#importFrom').val());
    fd.append('toDate', $('#importTo').val());
    return fd;
}

/* ── Progress ─────────────────────────────────────────────────────────────────
   Two phases, shown honestly.

   Upload is measurable, and on a 35 MB Access file it is most of the wait — so it
   gets a real percentage. Once the bytes are on the server the work is a single
   opaque call, so the bar goes indeterminate rather than inventing a number that
   creeps to 99% and stops. An elapsed counter carries "still working" instead. */
var progressTimer = null;
var progressStart = 0;

function progressStart_(label, hint) {
    progressStart = Date.now();
    $('#importProgress').removeClass('d-none');
    $('#ipLabel').text(label);
    $('#ipHint').text(hint || '');
    $('#ipElapsed').text('0s');
    setBar(0, false);

    $('#btnPreview, #btnImport').prop('disabled', true);

    clearInterval(progressTimer);
    progressTimer = setInterval(function () {
        $('#ipElapsed').text(Math.round((Date.now() - progressStart) / 1000) + 's');
    }, 500);
}

function setBar(pct, indeterminate) {
    var $bar = $('#ipBar');
    $bar.css('width', indeterminate ? '100%' : pct + '%').attr('aria-valuenow', indeterminate ? 100 : pct);
    $bar.toggleClass('progress-bar-striped progress-bar-animated', !!indeterminate);
}

function progressStop_() {
    clearInterval(progressTimer);
    progressTimer = null;
    $('#importProgress').addClass('d-none');
    $('#btnPreview, #btnImport').prop('disabled', false);
}

/**
 * POSTs a FormData with upload progress.
 *
 * jQuery's .ajax does not expose the upload's progress events, so the XHR is built
 * here rather than wrapping it — the percentage is the whole reason this exists.
 */
function postWithProgress(url, fd, opts) {
    var xhr = new XMLHttpRequest();
    xhr.open('POST', url, true);

    xhr.upload.onprogress = function (e) {
        if (!e.lengthComputable) return;
        var pct = Math.round(e.loaded / e.total * 100);
        setBar(pct, false);
        $('#ipLabel').text('Uploading… ' + pct + '%');
        $('#ipHint').text(fmtBytes(e.loaded) + ' of ' + fmtBytes(e.total));
    };

    // Upload finished; the server now reads and processes the file, and there is
    // nothing further to measure from here.
    xhr.upload.onload = function () {
        setBar(100, true);
        $('#ipLabel').text(opts.processingLabel || 'Processing on the server…');
        $('#ipHint').text(opts.processingHint || '');
    };

    xhr.onload = function () {
        progressStop_();
        if (xhr.status >= 200 && xhr.status < 300) {
            var data = null;
            try { data = xhr.responseText ? JSON.parse(xhr.responseText) : null; } catch (e) { }
            opts.success(data);
        } else {
            opts.error(xhr.responseText || 'Request failed (' + xhr.status + ').');
        }
    };

    xhr.onerror = function () {
        progressStop_();
        opts.error('The connection failed during upload.');
    };

    xhr.send(fd);
    return xhr;
}

function fmtBytes(n) {
    return n < 1024 ? n + ' B'
         : n < 1048576 ? (n / 1024).toFixed(0) + ' KB'
         : (n / 1048576).toFixed(1) + ' MB';
}

/* ── Preview ──────────────────────────────────────────────────────────────── */

function previewFile() {
    var fd = getFormData(); if (!fd) return;

    $('#previewCard').hide();
    $('#previewBody').html('');
    previewedPunches = [];
    $('#resultPanel').html('<div class="text-muted small">Reading the file…</div>');

    progressStart_('Uploading…', 'Large Access files take a moment.');

    postWithProgress('/api/import/preview', fd, {
        processingLabel: 'Reading punches…',
        processingHint: 'Opening the file and filtering to the selected date range.',
        success: function (data) {
            if (!data || !data.length) {
                $('#resultPanel').html('<div class="alert alert-warning py-2 mb-0">'
                    + 'No punch records found in this file for the selected date range.</div>');
                return;
            }
            previewedPunches = data;
            renderPreview(data);
            $('#resultPanel').html('<div class="alert alert-info py-2 mb-0">'
                + '<i class="fa fa-info-circle me-1"></i>Read <strong>' + data.length
                + '</strong> punch entries. Nothing has been saved — use '
                + '<strong>Import these punches</strong> below to save them.</div>');
        },
        error: function (msg) {
            $('#resultPanel').html('<div class="alert alert-danger py-2 mb-0">'
                + '<i class="fa fa-exclamation-triangle me-1"></i>Preview failed: ' + esc(msg) + '</div>');
        }
    });
}

function renderPreview(data) {
    var shown = Math.min(data.length, PREVIEW_ROW_LIMIT);
    var html = '';

    for (var i = 0; i < shown; i++) {
        var p = data[i];
        var enrollId = p.EnrollId !== undefined ? p.EnrollId : p.enrollId;
        var name = p.EmpName || p.empName || employeesMap[enrollId];
        var time = p.PunchTime || p.punchTime;
        var device = p.DeviceId || p.deviceId;

        html += '<tr>'
            + '<td class="ps-3 text-muted small">' + (i + 1) + '</td>'
            + '<td class="small">' + esc(enrollId == null ? '—' : enrollId) + '</td>'
            // An unmatched enrol id is the reason a punch will not import, so it is
            // called out here rather than left as a blank cell.
            + '<td class="small">' + (name
                ? esc(name)
                : '<span class="badge bg-warning text-dark">no matching employee</span>') + '</td>'
            + '<td class="small">' + esc(fmtPunch(time)) + '</td>'
            + '<td class="pe-3 small text-muted">' + esc(device || '—') + '</td>'
            + '</tr>';
    }

    $('#previewBody').html(html);
    $('#previewCount').text(data.length.toLocaleString() + ' records');
    $('#previewShown').text(shown.toLocaleString());
    $('#previewCard').show();
}

function fmtPunch(v) {
    if (!v) return '—';
    var d = new Date(v);
    if (isNaN(d.getTime())) return String(v);
    var pad = function (n) { return n < 10 ? '0' + n : n; };
    return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate())
         + ' ' + pad(d.getHours()) + ':' + pad(d.getMinutes());
}

/* ── Importing ────────────────────────────────────────────────────────────── */

/** Imports exactly what was previewed, without re-reading the file. */
function importPreviewed() {
    if (!previewedPunches.length) { notifyError('Preview a file first.'); return; }

    notifyConfirm({
        title: 'Import punches',
        text: previewedPunches.length.toLocaleString() + ' punch record(s) will be processed into attendance.',
        confirmText: 'Import', icon: 'question'
    }, function () {
        progressStart_('Processing punches…', 'Pairing punches and calculating attendance.');
        setBar(100, true);

        $.ajax({
            url: '/api/import/process-edited', type: 'POST',
            contentType: 'application/json', data: JSON.stringify(previewedPunches),
            success: function (result) { progressStop_(); showImportSummary(result); },
            error: function (xhr) {
                progressStop_();
                $('#resultPanel').html('<div class="alert alert-danger py-2 mb-0">'
                    + '<i class="fa fa-exclamation-triangle me-1"></i>Import failed: '
                    + esc(xhr.responseText || 'Unknown error.') + '</div>');
            }
        });
    });
}

function importDirectFile() {
    var fd = getFormData(); if (!fd) return;

    progressStart_('Uploading…', 'Large Access files take a moment.');

    postWithProgress('/api/import/file', fd, {
        processingLabel: 'Importing…',
        processingHint: 'Reading punches, pairing them and calculating attendance.',
        success: function (result) { showImportSummary(result); },
        error: function (msg) {
            $('#resultPanel').html('<div class="alert alert-danger py-2 mb-0">'
                + '<i class="fa fa-exclamation-triangle me-1"></i>Import failed: ' + esc(msg) + '</div>');
        }
    });
}

function showImportSummary(result) {
    if (!result) { $('#resultPanel').html('<div class="alert alert-warning py-2 mb-0">No response from the server.</div>'); return; }

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
    $('#previewCard').hide();
    previewedPunches = [];
}
