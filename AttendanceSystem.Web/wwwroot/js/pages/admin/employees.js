/* ── Admin Employees Management JavaScript ── */

var empData = null, empPage = 1, depts = [], desigs = [], branches = [];
var empSearchTimer = null;

$(function () {
    // Search is a round trip now, so it is debounced rather than firing per keystroke.
    $('#searchBox').on('input', function () {
        clearTimeout(empSearchTimer);
        empSearchTimer = setTimeout(function () { loadEmps(1); }, 300);
    });

    $.when(
        $.getJSON('/api/departments', function (d) { depts = (d || []).filter(function(x){ return x.IsActive || x.isActive; }); }),
        $.getJSON('/api/designations', function (d) { desigs = (d || []).filter(function(x){ return x.IsActive || x.isActive; }); }),
        $.getJSON('/api/branches', function (d) { branches = (d || []).filter(function(x){ return x.IsActive || x.isActive; }); })
    ).always(function () {
        var opts = '<option value="">All Departments</option>';
        depts.forEach(function (d) {
            var id = d.Id !== undefined ? d.Id : d.id;
            var name = d.Name || d.name || '';
            opts += '<option value="' + esc(id) + '">' + esc(name) + '</option>';
        });
        $('#deptFilter').html(opts);
        loadEmps();
    });
});

/*
 * Server-paged: search, department and status are applied in SQL and only one page of rows
 * reaches the browser. Headcount can grow without the screen getting slower.
 */
function loadEmps(page) {
    empPage = amsPageNo(page, empPage);

    var q = ($('#searchBox').val() || '').trim();
    var dept = $('#deptFilter').val();
    var status = $('#statusFilter').val();

    var url = '/api/employees/paged?page=' + empPage + '&pageSize=' + (amsPageSize() || 25)
            + (q ? '&search=' + encodeURIComponent(q) : '')
            + (dept ? '&departmentId=' + encodeURIComponent(dept) : '')
            // '' means All; only send the flag when one of the two states is chosen.
            + (status === '' ? '' : '&isActive=' + encodeURIComponent(status));

    $('#empBody').html('<tr><td colspan="9" class="text-center py-4 text-muted">Loading…</td></tr>');

    $.getJSON(url, function (d) { empData = d; renderTable(); })
     .fail(function (xhr) {
         $('#empBody').html('<tr><td colspan="9" class="text-danger text-center py-3">'
             + esc(xhr.responseText || 'Failed to load employees.') + '</td></tr>');
     });
}

function renderTable() {
    var data = empData || { Items: [], TotalCount: 0, Page: 1, PageSize: 25 };

    amsPage('#empBody', data.Items, function (e) {
        return '<tr>'
            + '<td class="fw-semibold text-primary">' + esc(e.EmployeeCode)
              + (e.UserCode
                    ? '<div class="text-muted fw-normal" style="font-size:.7rem;">' + esc(e.UserCode) + '</div>'
                    : '') + '</td>'
            // Full name on top, the abbreviated form beneath: some of these names run to eight
            // words, and the initialled form is what appears on internal paperwork.
            + '<td>' + esc(e.FullName)
              + (e.NameWithInitials && e.NameWithInitials !== e.FullName
                    ? '<div class="text-muted" style="font-size:.7rem;">' + esc(e.NameWithInitials) + '</div>'
                    : '') + '</td>'
            + '<td class="text-muted small">' + (e.Nic ? esc(e.Nic) : '—') + '</td>'
            + '<td class="text-muted">' + esc(e.Department) + '</td>'
            + '<td class="text-muted">' + esc(e.Designation) + '</td>'
            + '<td class="text-muted">' + esc(e.Branch) + '</td>'
            // A missing enrol id is the single most common cause of "the import did nothing",
            // so make its absence loud rather than showing an empty cell.
            + '<td>' + (e.BiometricEnrollId
                ? '<span class="badge bg-light text-dark">' + esc(e.BiometricEnrollId) + '</span>'
                : '<span class="badge bg-warning text-dark" title="Biometric imports cannot match this employee">not set</span>') + '</td>'
            + '<td>' + (e.IsActive ? '<span class="badge bg-success">Active</span>' : '<span class="badge bg-secondary">Inactive</span>') + '</td>'
            + '<td>'
            + '<button class="btn btn-sm btn-outline-primary me-1" onclick="editEmp(' + e.Id + ')" title="Edit"><i class="fa fa-pencil"></i></button>'
            + '<button class="btn btn-sm btn-outline-' + (e.IsActive ? 'warning' : 'success') + ' me-1" title="' + (e.IsActive ? 'Deactivate' : 'Activate') + '" onclick="toggleEmp(' + e.Id + ')"><i class="fa fa-' + (e.IsActive ? 'toggle-on' : 'toggle-off') + '"></i></button>'
            + '<button class="btn btn-sm btn-outline-danger" onclick="deleteEmp(' + e.Id + ')" title="Delete"><i class="fa fa-trash"></i></button>'
            + '</td></tr>';
    }, {
        colspan: 9,
        empty: 'No employees match these filters.',
        label: 'employee',
        server: {
            total: data.TotalCount,
            page: data.Page,
            pageSize: data.PageSize,
            onPage: loadEmps
        }
    });
}

/* Kept as the name every filter control already calls; a filter change goes back to page 1. */
function filterTable() {
    loadEmps(1);
}

function fillDropdowns() {
    var dOpts = '<option value="">-- Department --</option>';
    depts.forEach(function (d) { dOpts += '<option value="' + esc(d.Id) + '">' + esc(d.Name) + '</option>'; });
    var deOpts = '<option value="">-- Designation --</option>';
    desigs.forEach(function (d) { deOpts += '<option value="' + esc(d.Id) + '">' + esc(d.Name) + '</option>'; });
    var bOpts = '<option value="">-- Branch --</option>';
    branches.forEach(function (b) { bOpts += '<option value="' + esc(b.Id) + '">' + esc(b.Name) + '</option>'; });
    $('#empDept').html(dOpts); $('#empDesig').html(deOpts); $('#empBranch').html(bOpts);
}

/* ── Photo ───────────────────────────────────────────────────────────────────
   Photos are stored on the employee row as bytes, so they travel in the same JSON
   payload as everything else — base64 in, base64 out, no upload endpoint and no
   files on disk to go missing when the record is deleted.

   A phone camera JPEG is several megabytes; storing that per employee would bloat
   both the table and every list query that touches it. Each picture is therefore
   drawn onto a 400x400 canvas before it is encoded, which lands around 30-60 KB. */
var PHOTO_MAX_PX = 400;
var PHOTO_MAX_UPLOAD_BYTES = 5 * 1024 * 1024;

// Drawn rather than fetched: a data URI cannot 404, so the placeholder survives
// any future reshuffle of the theme's image folders.
var DEFAULT_AVATAR =
    'data:image/svg+xml;utf8,' + encodeURIComponent(
        '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 118 118">' +
        '<rect width="118" height="118" fill="%23eef1f4"/>' +
        '<circle cx="59" cy="45" r="21" fill="%23b6c0cc"/>' +
        '<path d="M18 112c0-22 18-33 41-33s41 11 41 33z" fill="%23b6c0cc"/></svg>');

function setPhoto(dataUri) {
    var has = !!dataUri;
    $('#empPhotoData').val(dataUri || '');
    $('#empPhotoPreview').attr('src', has ? dataUri : DEFAULT_AVATAR);
    $('#empPhotoClear').toggleClass('d-none', !has);
}

function clearPhoto() {
    setPhoto('');
    $('#empPhotoFile').val('');   // lets the same file be picked again after removing
}

function onPhotoPicked(input) {
    var file = input.files && input.files[0];
    if (!file) return;

    if (file.size > PHOTO_MAX_UPLOAD_BYTES) {
        notifyError('That image is larger than 5 MB. Please choose a smaller one.', 'Photo too large');
        input.value = '';
        return;
    }

    var reader = new FileReader();
    reader.onload = function (e) {
        var img = new Image();
        img.onload = function () {
            // Square centre-crop, so portrait and landscape shots both fill the circle
            // instead of being squashed into it.
            var side = Math.min(img.width, img.height);
            var sx = (img.width - side) / 2;
            var sy = (img.height - side) / 2;

            var canvas = document.createElement('canvas');
            canvas.width = canvas.height = PHOTO_MAX_PX;
            canvas.getContext('2d').drawImage(img, sx, sy, side, side, 0, 0, PHOTO_MAX_PX, PHOTO_MAX_PX);

            setPhoto(canvas.toDataURL('image/jpeg', 0.85));
        };
        img.onerror = function () {
            notifyError('That file could not be read as an image.', 'Invalid photo');
            input.value = '';
        };
        img.src = e.target.result;
    };
    reader.readAsDataURL(file);
}

/* Strips the "data:image/jpeg;base64," prefix. The API binds byte[], and
   System.Text.Json expects bare base64 for that.

   Returns "" rather than null when there is no photo, which is what makes Remove
   work: the service treats null as "leave the existing photo alone", so a null
   here would quietly ignore the removal. The form is the authority on the photo,
   so it always states the full current position. */
function photoToBase64() {
    var v = $('#empPhotoData').val();
    if (!v) return '';
    var comma = v.indexOf(',');
    return comma >= 0 ? v.substring(comma + 1) : v;
}

/* ── Full name assembly ──────────────────────────────────────────────────────
   Full Name is composed from Name with Initials + Last Name while it is untouched.
   Once somebody types in it directly, it is theirs and this stops: the imported
   records carry a full name that is not the initialled form plus a surname, and
   silently rewriting those on edit would corrupt them. */
var fullNameIsAuto = true;

function onNamePartChanged() {
    if (!fullNameIsAuto) return;
    var parts = [$('#empInitials').val().trim(), $('#empLast').val().trim()];
    $('#empFirst').val(parts.filter(Boolean).join(' '));
}

function onFullNameEdited() {
    // An emptied box is a request to go back to automatic, not a manual blank.
    fullNameIsAuto = $('#empFirst').val().trim() === '';
    if (fullNameIsAuto) onNamePartChanged();
    updateFullNameHint();
}

function updateFullNameHint() {
    $('#empFirstHint').text(fullNameIsAuto
        ? 'Filled in from Name with Initials and Last Name. Type here to set it yourself.'
        : 'Set by hand. Clear this box to go back to filling it in automatically.');
}

function openModal() {
    fillDropdowns();
    $('#empId').val(0); $('#empCode').val(''); $('#empFirst').val(''); $('#empLast').val('');
    $('#empUserCode').val(''); $('#empInitials').val(''); $('#empNic').val('');
    $('#empEmail').val(''); $('#empPhone').val(''); $('#empGender').val('');
    $('#empJoin').val(new Date().toISOString().split('T')[0]); $('#empDob').val('');
    $('#empAddr').val(''); $('#empEnrollId').val(''); $('#empActive').prop('checked', true);
    clearPhoto();
    fullNameIsAuto = true; updateFullNameHint();
    $('#empModalTitle').text('Add Employee');
    new bootstrap.Modal('#empModal').show();
}

function editEmp(id) {
    $.getJSON('/api/employees/' + id, function (e) {
        fillDropdowns();
        $('#empId').val(e.Id); $('#empCode').val(e.EmployeeCode);
        $('#empFirst').val(e.FirstName); $('#empLast').val(e.LastName);
        $('#empUserCode').val(e.UserCode || '');
        $('#empInitials').val(e.NameWithInitials || '');
        $('#empNic').val(e.Nic || '');
        $('#empEmail').val(e.Email); $('#empPhone').val(e.Phone);
        $('#empGender').val(e.Gender);
        $('#empJoin').val(e.JoiningDate ? e.JoiningDate.split('T')[0] : '');
        $('#empDob').val(e.DateOfBirth ? e.DateOfBirth.split('T')[0] : '');
        $('#empAddr').val(e.Address); $('#empEnrollId').val(e.BiometricEnrollId || '');
        $('#empActive').prop('checked', e.IsActive);
        $('#empDept').val(e.DepartmentId); $('#empDesig').val(e.DesignationId); $('#empBranch').val(e.BranchId);

        // Photo comes back as bare base64 from the byte[] column.
        setPhoto(e.Photo ? 'data:image/jpeg;base64,' + e.Photo : '');

        // An existing name is the record's own, not something to recompute from the
        // other two boxes — leave it be unless the user clears it.
        fullNameIsAuto = !$('#empFirst').val().trim();
        updateFullNameHint();

        $('#empModalTitle').text('Edit Employee');
        new bootstrap.Modal('#empModal').show();
    });
}

function saveEmp() {
    // Last name is no longer required: the imported records carry the whole name in Full Name,
    // and demanding a surname would make every one of them impossible to edit.
    if (!$('#empFirst').val().trim() || !$('#empDept').val() || !$('#empDesig').val() || !$('#empBranch').val() || !$('#empJoin').val()) {
        notifyError('Full Name, Department, Designation, Branch and Joining Date are required.', 'Validation Error'); return;
    }
    var dto = {
        Id: parseInt($('#empId').val()) || 0, EmployeeCode: $('#empCode').val().trim(),
        UserCode: $('#empUserCode').val().trim() || null,
        NameWithInitials: $('#empInitials').val().trim() || null,
        Nic: $('#empNic').val().trim() || null,
        FirstName: $('#empFirst').val().trim(), LastName: $('#empLast').val().trim(),
        Email: $('#empEmail').val().trim() || null, Phone: $('#empPhone').val().trim() || null,
        Gender: $('#empGender').val() || null,
        JoiningDate: $('#empJoin').val(), DateOfBirth: $('#empDob').val() || null,
        Address: $('#empAddr').val().trim() || null,
        // Blank means "not enrolled", which is a legitimate state — send null, not 0.
        BiometricEnrollId: $('#empEnrollId').val() === '' ? null : parseInt($('#empEnrollId').val()),
        DepartmentId: parseInt($('#empDept').val()), DesignationId: parseInt($('#empDesig').val()),
        BranchId: parseInt($('#empBranch').val()), IsActive: $('#empActive').is(':checked'),
        Photo: photoToBase64()
    };
    $.ajax({ url: '/api/employees', type: 'POST', contentType: 'application/json', data: JSON.stringify(dto),
        success: function () { 
            bootstrap.Modal.getInstance('#empModal').hide(); 
            notifySuccess('Employee saved successfully.');
            loadEmps(); 
        },
        error: function (xhr) { notifyError(xhr.responseText || 'Save failed.'); }
    });
}

function toggleEmp(id) {
    $.ajax({ url: '/api/employees/' + id + '/toggle', type: 'POST',
        success: function () { 
            notifySuccess('Employee status updated.');
            loadEmps(); 
        },
        error: function (xhr) { notifyError(xhr.responseText || 'Toggle failed.'); }
    });
}

function deleteEmp(id) {
    notifyConfirm({ title: 'Delete Employee', text: 'Are you sure you want to delete this employee? This cannot be undone.', confirmText: 'Delete', icon: 'warning' }, function () {
        $.ajax({ url: '/api/employees/' + id, type: 'DELETE',
            success: function () { 
                notifySuccess('Employee deleted successfully.');
                loadEmps(); 
            },
            error: function (xhr) { notifyError(xhr.responseText || 'Delete failed.'); }
        });
    });
}
