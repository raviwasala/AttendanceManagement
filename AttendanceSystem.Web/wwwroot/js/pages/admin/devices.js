/* ── Admin Fingerprint Devices ── */

var allDevices = [], branches = [];

$(function () {
    $.getJSON('/api/branches', function (d) {
        branches = (d || []).filter(function (b) { return b.IsActive; });
    }).always(loadDevices);
});

function statusBadge(d) {
    var map = {
        Online:  { cls: 'success',   text: 'Online' },
        Offline: { cls: 'secondary', text: 'Offline' },
        Error:   { cls: 'danger',    text: 'Error' },
        Unknown: { cls: 'light text-dark', text: 'Not tested' }
    };
    var s = map[d.StatusDisplay] || map.Unknown;
    var badge = '<span class="badge bg-' + s.cls + '">' + s.text + '</span>';

    // The error text is the actionable part - surface it rather than hiding it in a tooltip.
    if (d.StatusDisplay === 'Error' && d.LastError) {
        badge += '<div class="small text-danger mt-1">' + esc(d.LastError) + '</div>';
    }
    return badge;
}

function relative(iso) {
    if (!iso) return '<span class="text-muted">never</span>';
    var mins = Math.round((Date.now() - new Date(iso).getTime()) / 60000);
    if (mins < 1)    return 'just now';
    if (mins < 60)   return mins + ' min ago';
    if (mins < 1440) return Math.round(mins / 60) + ' h ago';
    return new Date(iso).toLocaleDateString();
}

function loadDevices() {
    $.getJSON('/api/devices', function (d) { allDevices = d || []; renderDevices(); })
     .fail(function () {
         $('#deviceBody').html('<tr><td colspan="7" class="text-danger text-center py-3">Failed to load devices.</td></tr>');
     });
}

function renderDevices() {
    var empty = 'No devices registered yet.'
              + (window.devicePerms.create
                    ? ' Use <strong>Add Device</strong> to register your first terminal.' : '');

    amsPage('#deviceBody', allDevices, function (d) {
        var actions = '<button class="btn btn-sm btn-outline-secondary me-1" onclick="testDevice(' + d.Id + ')" title="Test connection">' +
                      '<i class="feather icon-activity"></i></button>';
        if (window.devicePerms.edit) {
            actions += '<button class="btn btn-sm btn-outline-primary me-1" onclick="openDevice(' + d.Id + ')" title="Edit">' +
                       '<i class="fa fa-pencil"></i></button>';
        }
        if (window.devicePerms.delete) {
            actions += '<button class="btn btn-sm btn-outline-danger" onclick="deleteDevice(' + d.Id + ')" title="Delete">' +
                       '<i class="fa fa-trash"></i></button>';
        }

        return '<tr' + (d.IsActive ? '' : ' class="text-muted"') + '>'
            + '<td class="ps-3 fw-semibold">' + esc(d.Name)
              + (d.IsActive ? '' : ' <span class="badge bg-light text-muted">inactive</span>')
              + (d.Model ? '<br><small class="text-muted">' + esc(d.Model) + '</small>' : '')
            + '</td>'
            + '<td class="text-muted">' + esc(d.BranchName) + '</td>'
            + '<td><code>' + esc(d.Endpoint) + '</code></td>'
            + '<td>' + statusBadge(d) + '</td>'
            + '<td class="small text-muted">' + relative(d.LastSeenAt) + '</td>'
            + '<td>' + (d.AutoSyncEnabled
                ? '<span class="badge bg-success">On</span>'
                : '<span class="badge bg-light text-muted">Off</span>') + '</td>'
            + '<td class="text-end pe-3">' + actions + '</td>'
            + '</tr>';
    }, { colspan: 7, empty: empty, label: 'device' });
}

function fillBranches(selected) {
    var opts = '<option value="">-- Select Branch --</option>';
    branches.forEach(function (b) {
        opts += '<option value="' + esc(b.Id) + '">' + esc(b.Name) + '</option>';
    });
    $('#devBranch').html(opts).val(selected || '');
}

function openDevice(id) {
    clearFieldErrors();

    if (!id) {
        $('#devId').val(0);
        $('#devName').val(''); $('#devIp').val(''); $('#devPort').val(4370);
        $('#devCommKey').val('');
        $('#devCommKeyHint').text('Device communication password. Leave blank if unset.');
        $('#devActive').prop('checked', true);
        $('#devAutoSync').prop('checked', true);
        fillBranches();
        $('#deviceModalTitle').text('Add Device');
        new bootstrap.Modal('#deviceModal').show();
        return;
    }

    $.getJSON('/api/devices/' + id, function (d) {
        $('#devId').val(d.Id);
        $('#devName').val(d.Name);
        $('#devIp').val(d.IpAddress);
        $('#devPort').val(d.Port);
        // The comm key is never sent to the browser. Blank means "leave unchanged".
        $('#devCommKey').val('');
        $('#devCommKeyHint').text(d.HasCommKey
            ? 'A comm key is set. Leave blank to keep it, or enter 0 to clear it.'
            : 'Device communication password. Leave blank if unset.');
        $('#devActive').prop('checked', d.IsActive);
        $('#devAutoSync').prop('checked', d.AutoSyncEnabled);
        fillBranches(d.BranchId);
        $('#deviceModalTitle').text('Edit Device');
        new bootstrap.Modal('#deviceModal').show();
    });
}

function saveDevice() {
    var id = parseInt($('#devId').val()) || 0;
    var commRaw = $('#devCommKey').val();

    var dto = {
        Id: id,
        Name: $('#devName').val().trim(),
        IpAddress: $('#devIp').val().trim(),
        Port: parseInt($('#devPort').val()) || 0,
        // null tells the server to leave a stored key alone.
        CommKey: commRaw === '' ? null : parseInt(commRaw),
        BranchId: parseInt($('#devBranch').val()) || 0,
        IsActive: $('#devActive').is(':checked'),
        AutoSyncEnabled: $('#devAutoSync').is(':checked')
    };

    // Report only what is actually missing, and mark the field. Listing every required
    // field when two of three are filled makes the user re-check the ones that are fine.
    clearFieldErrors();
    var missing = [];
    if (!dto.Name)      { missing.push('Device name');  markInvalid('#devName'); }
    if (!dto.IpAddress) { missing.push('IP address');   markInvalid('#devIp'); }
    if (!dto.BranchId)  { missing.push('Branch');       markInvalid('#devBranch'); }

    if (missing.length) {
        showDevError(missing.length === 1
            ? missing[0] + ' is required.'
            : missing.join(' and ') + ' are required.');
        $('#deviceModal').find('.is-invalid').first().trigger('focus');
        return;
    }

    if (dto.Port < 1 || dto.Port > 65535) {
        markInvalid('#devPort');
        showDevError('Port must be between 1 and 65535.');
        return;
    }

    $.ajax({
        url: id ? '/api/devices/' + id : '/api/devices',
        type: id ? 'PUT' : 'POST',
        contentType: 'application/json',
        data: JSON.stringify(dto),
        success: function () {
            bootstrap.Modal.getInstance('#deviceModal').hide();
            notifySuccess('Device saved.');
            loadDevices();
        },
        error: function (xhr) { showDevError(xhr.responseText || 'Save failed.'); }
    });
}

function showDevError(msg) {
    $('#devError').removeClass('d-none').text(msg);
}

function markInvalid(sel) {
    $(sel).addClass('is-invalid');
}

function clearFieldErrors() {
    $('#deviceModal').find('.is-invalid').removeClass('is-invalid');
    $('#devError').addClass('d-none').text('');
}

function testDevice(id) {
    $('#testBody').html('<div class="py-3"><div class="spinner-border text-primary" role="status"></div>' +
                        '<div class="small text-muted mt-2">Contacting device…</div></div>');
    new bootstrap.Modal('#testModal').show();

    $.ajax({
        url: '/api/devices/' + id + '/test',
        type: 'POST',
        success: function (r) {
            var html;
            if (r.IsReachable) {
                html = '<div class="text-success mb-2"><i class="feather icon-check-circle" style="font-size:2rem;"></i></div>'
                     + '<div class="fw-semibold mb-1">Device reachable</div>'
                     + '<div class="small text-muted">Responded in ' + r.ResponseMs + ' ms</div>';
                if (r.ClockDriftWarning) {
                    html += '<div class="alert alert-warning small mt-3 mb-0">Device clock differs from the server by '
                          + Math.round(r.ClockDriftSeconds) + 's. Punch times will be wrong by that amount.</div>';
                }
            } else {
                html = '<div class="text-danger mb-2"><i class="feather icon-x-circle" style="font-size:2rem;"></i></div>'
                     + '<div class="fw-semibold mb-1">Not reachable</div>'
                     + '<div class="small text-muted">' + esc(r.Message || '') + '</div>';
            }
            $('#testBody').html(html);
            loadDevices();   // status was just updated server-side
        },
        error: function (xhr) {
            $('#testBody').html('<div class="text-danger small">' + esc(xhr.responseText || 'Test failed.') + '</div>');
        }
    });
}

function deleteDevice(id) {
    var d = allDevices.filter(function (x) { return x.Id === id; })[0];
    notifyConfirm({
        title: 'Delete Device',
        text: 'Remove "' + (d ? d.Name : 'this device') + '"? Attendance already collected from it is kept.',
        confirmText: 'Delete',
        icon: 'warning'
    }, function () {
        $.ajax({
            url: '/api/devices/' + id,
            type: 'DELETE',
            success: function () { notifySuccess('Device deleted.'); loadDevices(); },
            error: function (xhr) { notifyError(xhr.responseText || 'Delete failed.'); }
        });
    });
}
