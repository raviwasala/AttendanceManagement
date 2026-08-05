/* ── Admin Settings JavaScript ── */

$(function () {
    $.getJSON('/api/settings', function (d) {
        $('#companyName').val(d.CompanyName);
        $('#website').val(d.Website || '');
        $('#phone').val(d.Phone || '');
        $('#email').val(d.Email || '');
        $('#address').val(d.Address || '');
        $('#workStart').val(d.WorkStartTime ? d.WorkStartTime.substring(0,5) : '');
        $('#workEnd').val(d.WorkEndTime ? d.WorkEndTime.substring(0,5) : '');
        $('#maxLate').val(d.MaxLateMinutes);
        $('#weekendDays').val(d.WeekendDays);
        // A saved size that is not one of the presets (set by an earlier build or by hand)
        // would otherwise leave the select blank; add it so the real value is shown.
        var ps = String(d.DefaultPageSize);
        if (!$('#pageSize option[value="' + ps + '"]').length) {
            $('#pageSize').append('<option value="' + esc(ps) + '">' + esc(ps) + '</option>');
        }
        $('#pageSize').val(ps);
        $('#confirmDelete').prop('checked', d.ConfirmBeforeDelete !== false);
    }).fail(function () {
        $('#settingsAlert').html('<div class="alert alert-danger">Failed to load settings.</div>');
    });
});

function uploadCompanyLogo() {
    var fileInput = document.getElementById('logoFileInput');
    if (!fileInput.files || fileInput.files.length === 0) {
        alert('Please select an image file first.');
        return;
    }
    var formData = new FormData();
    formData.append('file', fileInput.files[0]);

    $.ajax({
        url: '/api/settings/upload-logo',
        type: 'POST',
        data: formData,
        contentType: false,
        processData: false,
        success: function (res) {
            $('#logoUploadAlert').html('<div class="alert alert-success alert-dismissible fade show py-2">Company Logo updated successfully! <button type="button" class="btn-close" data-bs-dismiss="alert"></button></div>');
            var newUrl = res.logoUrl;
            $('#currentLogoPreview').attr('src', newUrl);
            $('.header-brand-logo').attr('src', newUrl);
            fileInput.value = '';
        },
        error: function (xhr) {
            $('#logoUploadAlert').html('<div class="alert alert-danger py-2">Upload failed: ' + (xhr.responseText || 'Error uploading logo') + '</div>');
        }
    });
}

function saveSettings() {
    var name = $('#companyName').val().trim();
    if (!name) { alert('Company Name is required.'); return; }
    var dto = {
        CompanyName: name, Website: $('#website').val().trim()||null,
        Phone: $('#phone').val().trim()||null, Email: $('#email').val().trim()||null,
        Address: $('#address').val().trim()||null,
        WorkStartTime: $('#workStart').val() ? $('#workStart').val() + ':00' : '00:00:00',
        WorkEndTime: $('#workEnd').val() ? $('#workEnd').val() + ':00' : '00:00:00',
        MaxLateMinutes: parseInt($('#maxLate').val()) || 0,
        WeekendDays: $('#weekendDays').val(),
        DefaultPageSize: parseInt($('#pageSize').val(), 10) || 0,
        ConfirmBeforeDelete: $('#confirmDelete').is(':checked')
    };
    // No modifiedBy on the wire: the server attributes the change to the session user.
    $.ajax({ url: '/api/settings', type: 'POST', contentType: 'application/json', data: JSON.stringify(dto),
        success: function () { $('#settingsAlert').html('<div class="alert alert-success alert-dismissible fade show">Settings saved successfully! <button type="button" class="btn-close" data-bs-dismiss="alert"></button></div>'); },
        error: function (xhr) { $('#settingsAlert').html('<div class="alert alert-danger">Error: ' + (xhr.responseText || 'Save failed.') + '</div>'); }
    });
}
