/* ── Admin Data Management: export, employee import, backup & restore ── */

var importPreview = null;   // last preview from the server
var restoreFileBytes = null; // the archive the operator checked, re-sent on confirm

/* ── Export ───────────────────────────────────────────────────────────────────
   A plain navigation rather than an AJAX call: the browser's own download
   handling gives a progress indicator and a Save dialog for free, and the
   session cookie travels with it. */
function exportData(dataset) {
    var from = $('#exFrom').val(), to = $('#exTo').val();

    if (dataset !== 'employees' && (!from || !to)) {
        notifyError('Choose both a start and an end date.'); return;
    }
    if (from && to && to < from) {
        notifyError('End date must be on or after the start date.'); return;
    }

    var url = '/api/data/export/' + dataset
            + (dataset === 'employees' ? '' : '?from=' + from + '&to=' + to);

    window.location.href = url;
}

function downloadBackup() {
    notifySuccess('Preparing the archive — the download will start shortly.');
    window.location.href = '/api/data/backup';
}

function downloadTemplate() {
    window.location.href = '/api/data/employees/template';
}

/* ── Employee import ─────────────────────────────────────────────────────── */

function previewImport() {
    var input = $('#impFile')[0];
    if (!input || !input.files || !input.files.length) {
        notifyError('Choose a CSV or Excel file first.'); return;
    }

    var fd = new FormData();
    fd.append('file', input.files[0]);

    $('#impBody').html('<tr><td colspan="6" class="text-center py-4 text-muted">' +
        '<i class="fa fa-spinner fa-spin me-2"></i>Reading file…</td></tr>');
    $('#impApply').addClass('d-none');

    $.ajax({
        url: '/api/data/employees/preview', type: 'POST',
        data: fd, processData: false, contentType: false,
        success: function (d) { importPreview = d; renderImportPreview(d); },
        error: function (xhr) {
            importPreview = null;
            $('#impBody').html('<tr><td colspan="6" class="text-danger text-center py-3">' +
                esc(xhr.responseText || 'Could not read that file.') + '</td></tr>');
        }
    });
}

function renderImportPreview(d) {
    var rows = d.Rows || [];

    var summary =
        '<div class="d-flex flex-column gap-1">'
      + line('Rows read', d.TotalRead, '')
      + line('Will be created', d.ToCreate, 'text-success')
      + line('Will be updated', d.ToUpdate, 'text-primary')
      + line('Rejected', d.Invalid, d.Invalid ? 'text-danger' : 'text-muted')
      + '</div>';

    if (d.UnknownLookups && d.UnknownLookups.length) {
        summary += '<div class="alert alert-warning py-2 mt-3 mb-0 small">'
                 + '<strong>Not found on record:</strong><br>'
                 + d.UnknownLookups.map(esc).join('<br>')
                 + '<div class="mt-1">Create these first, or correct the spelling in the file.</div></div>';
    }
    if (d.FileWarnings && d.FileWarnings.length) {
        summary += '<div class="alert alert-danger py-2 mt-2 mb-0 small">'
                 + d.FileWarnings.map(esc).join('<br>') + '</div>';
    }
    $('#impSummary').html(summary);

    if (!rows.length) {
        $('#impBody').html('<tr><td colspan="6" class="text-center py-4 text-muted">No rows found.</td></tr>');
        return;
    }

    // Problem rows first: with 240 rows the eight that fail are what needs attention,
    // and they would otherwise be scattered through ten screens of scrolling.
    var ordered = rows.slice().sort(function (a, b) {
        return (a.IsValid === b.IsValid) ? a.RowNumber - b.RowNumber : (a.IsValid ? 1 : -1);
    });

    var badge = function (action) {
        var cls = action === 'Create' ? 'bg-success' : action === 'Update' ? 'bg-primary' : 'bg-danger';
        return '<span class="badge ' + cls + '">' + esc(action) + '</span>';
    };

    $('#impBody').html(ordered.map(function (r) {
        var problems = (r.Errors || []).concat(r.Warnings || []);
        return '<tr' + (r.IsValid ? '' : ' class="table-danger"') + '>'
             + '<td class="ps-3 text-muted">' + esc(r.RowNumber) + '</td>'
             + '<td>' + badge(r.Action) + '</td>'
             + '<td>' + esc(r.FullName || '—')
             + (r.EmployeeCode ? '<div class="text-muted" style="font-size:.72rem;">' + esc(r.EmployeeCode) + '</div>' : '')
             + '</td>'
             + '<td class="text-muted small">' + esc(r.DepartmentName || '—') + '</td>'
             + '<td class="text-muted small">' + esc(r.BiometricEnrollId == null ? '—' : r.BiometricEnrollId) + '</td>'
             + '<td class="small' + (r.IsValid ? ' text-muted' : ' text-danger') + '">'
             + (problems.length ? problems.map(esc).join('<br>') : '—') + '</td>'
             + '</tr>';
    }).join(''));

    $('#impApply').toggleClass('d-none', (d.ToCreate + d.ToUpdate) === 0);
}

function line(label, value, cls) {
    return '<div class="d-flex justify-content-between"><span>' + esc(label)
         + '</span><span class="fw-bold ' + cls + '">' + esc(value) + '</span></div>';
}

function applyImport() {
    if (!importPreview) { notifyError('Preview a file first.'); return; }

    var valid = (importPreview.Rows || []).filter(function (r) { return r.IsValid; });
    if (!valid.length) { notifyError('No valid rows to import.'); return; }

    notifyConfirm({
        title: 'Import employees',
        text: valid.length + ' row(s) will be written: ' + importPreview.ToCreate +
              ' created, ' + importPreview.ToUpdate + ' updated.',
        confirmText: 'Import', icon: 'question'
    }, function () {
        $.ajax({
            url: '/api/data/employees/import', type: 'POST',
            contentType: 'application/json', data: JSON.stringify(valid),
            success: function (res) {
                notifySuccess('Created ' + res.Created + ', updated ' + res.Updated +
                              (res.Skipped ? ', skipped ' + res.Skipped : '') + '.');
                $('#impApply').addClass('d-none');
                previewImport();   // re-read so the rows now show as updates
            },
            error: function (xhr) { notifyError(xhr.responseText || 'Import failed.'); }
        });
    });
}

/* ── Restore ─────────────────────────────────────────────────────────────── */

function previewRestore() {
    var input = $('#resFile')[0];
    if (!input || !input.files || !input.files.length) {
        notifyError('Choose a backup archive first.'); return;
    }

    restoreFileBytes = input.files[0];

    var fd = new FormData();
    fd.append('file', restoreFileBytes);

    $('#resPreview').html('<div class="text-muted small"><i class="fa fa-spinner fa-spin me-2"></i>Reading archive…</div>');
    $('#resConfirmBox').addClass('d-none');

    $.ajax({
        url: '/api/data/restore/preview', type: 'POST',
        data: fd, processData: false, contentType: false,
        success: function (d) { renderRestorePreview(d); },
        error: function (xhr) {
            $('#resPreview').html('<div class="alert alert-danger py-2 mb-0 small">' +
                esc(xhr.responseText || 'Could not read the archive.') + '</div>');
        }
    });
}

function renderRestorePreview(d) {
    var html = '';

    if (d.CreatedAtUtc) {
        html += '<div class="small text-muted mb-2">Archive created ' + esc(d.CreatedAtUtc)
              + (d.SourceVersion ? ' · version ' + esc(d.SourceVersion) : '') + '</div>';
    }

    if (d.Errors && d.Errors.length) {
        html += '<div class="alert alert-danger py-2 small">' + d.Errors.map(esc).join('<br>') + '</div>';
        $('#resPreview').html(html);
        $('#resConfirmBox').addClass('d-none');
        return;
    }

    // Both counts side by side: this is what lets someone notice they picked last
    // year's archive before it replaces this year's data, rather than afterwards.
    html += '<div class="table-responsive" style="max-height:220px;overflow-y:auto">'
          + '<table class="table table-sm mb-0"><thead class="table-light"><tr>'
          + '<th>Table</th><th class="text-end">In archive</th>'
          + '<th class="text-end">In database</th></tr></thead><tbody>';

    (d.Tables || []).forEach(function (t) {
        var cls = !t.Recognised ? ' class="text-muted"' : '';
        html += '<tr' + cls + '><td>' + esc(t.Table) + (t.Recognised ? '' : ' <span class="badge bg-secondary">skipped</span>') + '</td>'
              + '<td class="text-end">' + esc(t.RowsInFile) + '</td>'
              + '<td class="text-end">' + esc(t.RowsInDatabase) + '</td></tr>';
    });
    html += '</tbody></table></div>';

    if (d.Warnings && d.Warnings.length) {
        html += '<div class="alert alert-warning py-2 mt-2 mb-0 small">'
              + d.Warnings.map(esc).join('<br>') + '</div>';
    }

    $('#resPreview').html(html);
    $('#resConfirmBox').toggleClass('d-none', !d.CanRestore);
    $('#resConfirm').val('');
}

function doRestore() {
    if (!restoreFileBytes) { notifyError('Check the archive first.'); return; }

    var typed = ($('#resConfirm').val() || '').trim();
    if (typed !== 'RESTORE') {
        notifyError('Type RESTORE exactly to confirm.'); return;
    }

    notifyConfirm({
        title: 'Replace data from this archive?',
        text: 'This cannot be undone. It runs in one transaction, so a failure changes nothing.',
        confirmText: 'Yes, restore', icon: 'warning'
    }, function () {
        var fd = new FormData();
        fd.append('file', restoreFileBytes);
        fd.append('confirm', 'RESTORE');

        $('#resPreview').html('<div class="text-muted small"><i class="fa fa-spinner fa-spin me-2"></i>Restoring…</div>');

        $.ajax({
            url: '/api/data/restore', type: 'POST',
            data: fd, processData: false, contentType: false,
            success: function (res) {
                notifySuccess('Restored ' + res.TotalRowsWritten + ' rows across ' +
                              (res.Tables || []).length + ' tables.');
                $('#resConfirmBox').addClass('d-none');
                $('#resPreview').html('<div class="alert alert-success py-2 mb-0 small">'
                    + 'Restore complete — ' + esc(res.TotalRowsWritten) + ' rows written.<br>'
                    + (res.Warnings || []).map(esc).join('<br>') + '</div>');
            },
            error: function (xhr) {
                $('#resPreview').html('<div class="alert alert-danger py-2 mb-0 small">'
                    + esc(xhr.responseText || 'Restore failed.') + '</div>');
            }
        });
    });
}
