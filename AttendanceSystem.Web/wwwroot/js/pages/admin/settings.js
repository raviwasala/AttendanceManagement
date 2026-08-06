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

        // A stored value outside the preset list (set by an earlier build, or by hand)
        // must still be selectable, or saving would silently change it.
        var lock = d.ScreenLockMinutes == null ? 15 : d.ScreenLockMinutes;
        if (!$('#screenLock option[value="' + lock + '"]').length) {
            $('#screenLock').append('<option value="' + esc(lock) + '">' + esc(lock) + ' minutes</option>');
        }
        $('#screenLock').val(lock);

        $('#smtpEnabled').prop('checked', !!d.SmtpEnabled);
        $('#smtpHost').val(d.SmtpHost || '');
        $('#smtpPort').val(d.SmtpPort || 587);
        $('#smtpUsername').val(d.SmtpUsername || '');
        $('#smtpSsl').prop('checked', d.SmtpEnableSsl !== false);
        $('#smtpFrom').val(d.SmtpFromAddress || '');
        $('#smtpFromName').val(d.SmtpFromName || '');

        // The password is never sent down — only whether one exists. The field stays blank
        // and blank means "keep it", so this line is the only feedback that one is stored.
        $('#smtpPasswordState').text(d.HasSmtpPassword
            ? 'A password is saved. Leave blank to keep it.'
            : 'No password saved.');

        $('#smsEnabled').prop('checked', !!d.SmsEnabled);
        $('#smsProvider').val(d.SmsProvider || '');
        $('#smsSenderId').val(d.SmsSenderId || '');
        $('#smsApiUrl').val(d.SmsApiUrl || '');
        $('#smsMethod').val(d.SmsHttpMethod || 'POST');
        $('#smsContentType').val(d.SmsContentType || 'application/json');
        $('#smsAuthHeader').val(d.SmsAuthHeader || '');
        $('#smsTemplate').val(d.SmsRequestTemplate || '');
        $('#smsApiKeyState').text(d.HasSmsApiKey
            ? 'An API key is saved. Leave blank to keep it.'
            : 'No API key saved.');

        // On the tab itself, so an administrator can see at a glance which channels are live
        // without opening each one.
        setChannelBadge('#emailStatusBadge', d.SmtpEnabled && d.SmtpHost);
        setChannelBadge('#smsStatusBadge', d.SmsEnabled && d.SmsApiUrl);
    }).fail(function () {
        $('#settingsAlert').html('<div class="alert alert-danger">Failed to load settings.</div>');
    });
});

function setChannelBadge(selector, on) {
    $(selector).removeClass('d-none bg-secondary bg-success')
               .addClass(on ? 'bg-success' : 'bg-secondary')
               .text(on ? 'On' : 'Off');
}

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

function buildSettingsDto() {
    var name = $('#companyName').val().trim();
    if (!name) { alert('Company Name is required.'); return null; }
    return {
        CompanyName: name, Website: $('#website').val().trim()||null,
        Phone: $('#phone').val().trim()||null, Email: $('#email').val().trim()||null,
        Address: $('#address').val().trim()||null,
        WorkStartTime: $('#workStart').val() ? $('#workStart').val() + ':00' : '00:00:00',
        WorkEndTime: $('#workEnd').val() ? $('#workEnd').val() + ':00' : '00:00:00',
        MaxLateMinutes: parseInt($('#maxLate').val()) || 0,
        WeekendDays: $('#weekendDays').val(),
        DefaultPageSize: parseInt($('#pageSize').val(), 10) || 0,
        ConfirmBeforeDelete: $('#confirmDelete').is(':checked'),
        ScreenLockMinutes: parseInt($('#screenLock').val(), 10) || 0,

        SmtpEnabled: $('#smtpEnabled').is(':checked'),
        SmtpHost: $('#smtpHost').val().trim() || null,
        SmtpPort: parseInt($('#smtpPort').val(), 10) || 587,
        SmtpUsername: $('#smtpUsername').val().trim() || null,
        SmtpEnableSsl: $('#smtpSsl').is(':checked'),
        SmtpFromAddress: $('#smtpFrom').val().trim() || null,
        SmtpFromName: $('#smtpFromName').val().trim() || null,
        // Blank is "keep the stored one" — the server treats it that way too.
        SmtpPassword: $('#smtpPassword').val() || null,

        SmsEnabled: $('#smsEnabled').is(':checked'),
        SmsProvider: $('#smsProvider').val().trim() || null,
        SmsApiUrl: $('#smsApiUrl').val().trim() || null,
        SmsHttpMethod: $('#smsMethod').val(),
        SmsContentType: $('#smsContentType').val(),
        SmsSenderId: $('#smsSenderId').val().trim() || null,
        SmsRequestTemplate: $('#smsTemplate').val().trim() || null,
        SmsAuthHeader: $('#smsAuthHeader').val().trim() || null,
        SmsApiKey: $('#smsApiKey').val() || null
    };
}

/* Saving returns a promise so the test button can save first and only then send —
   otherwise the test would exercise the previously stored settings while the
   administrator reads the result as confirming what they just typed. */
function postSettings() {
    var dto = buildSettingsDto();
    if (!dto) return null;

    // No modifiedBy on the wire: the server attributes the change to the session user.
    return $.ajax({ url: '/api/settings', type: 'POST', contentType: 'application/json',
                    data: JSON.stringify(dto) });
}

function saveSettings() {
    var req = postSettings();
    if (!req) return;
    req.done(function () {
        $('#settingsAlert').html('<div class="alert alert-success alert-dismissible fade show">Settings saved successfully! <button type="button" class="btn-close" data-bs-dismiss="alert"></button></div>');
        // Typed once, then stored — clearing it stops a stale value being resubmitted.
        if ($('#smtpPassword').val()) {
            $('#smtpPassword').val('');
            $('#smtpPasswordState').text('A password is saved. Leave blank to keep it.');
        }
        if ($('#smsApiKey').val()) {
            $('#smsApiKey').val('');
            $('#smsApiKeyState').text('An API key is saved. Leave blank to keep it.');
        }
        setChannelBadge('#emailStatusBadge', $('#smtpEnabled').is(':checked') && $('#smtpHost').val().trim());
        setChannelBadge('#smsStatusBadge', $('#smsEnabled').is(':checked') && $('#smsApiUrl').val().trim());
    }).fail(function (xhr) {
        $('#settingsAlert').html('<div class="alert alert-danger">Error: ' + (xhr.responseText || 'Save failed.') + '</div>');
    });
}

function sendTestEmail() {
    var to = $('#testEmailTo').val().trim();
    if (!to) { $('#settingsAlert').html('<div class="alert alert-warning">Enter an address to send the test to.</div>'); return; }

    var req = postSettings();
    if (!req) return;

    var $btn = $('#testEmailBtn').prop('disabled', true);
    var original = $btn.html();
    $btn.html('<i class="feather icon-loader me-1"></i>Sending…');

    req.then(function () {
        return $.ajax({ url: '/api/settings/test-email', type: 'POST',
                        contentType: 'application/json', data: JSON.stringify({ ToEmail: to }) });
    }).done(function () {
        $('#settingsAlert').html('<div class="alert alert-success alert-dismissible fade show">'
            + 'Settings saved and a test message was sent to ' + esc(to)
            + '. If it does not arrive, check the spam folder before changing anything.'
            + '<button type="button" class="btn-close" data-bs-dismiss="alert"></button></div>');
        $('#smtpPassword').val('');
    }).fail(function (xhr) {
        // The SMTP error is shown verbatim — it is the answer the administrator came for,
        // and "sending failed" would leave them guessing between host, port and password.
        $('#settingsAlert').html('<div class="alert alert-danger">'
            + 'Could not send: ' + esc(xhr.responseText || 'unknown error') + '</div>');
    }).always(function () {
        $btn.prop('disabled', false).html(original);
    });
}

function sendTestSms() {
    var to = $('#testSmsTo').val().trim();
    if (!to) { $('#settingsAlert').html('<div class="alert alert-warning">Enter a mobile number to send the test to.</div>'); return; }

    var req = postSettings();
    if (!req) return;

    var $btn = $('#testSmsBtn').prop('disabled', true);
    var original = $btn.html();
    $btn.html('<i class="feather icon-loader me-1"></i>Sending…');

    req.then(function () {
        return $.ajax({ url: '/api/settings/test-sms', type: 'POST',
                        contentType: 'application/json', data: JSON.stringify({ ToNumber: to }) });
    }).done(function () {
        $('#settingsAlert').html('<div class="alert alert-success alert-dismissible fade show">'
            + 'Settings saved and the gateway accepted a message for ' + esc(to) + '.'
            + '<button type="button" class="btn-close" data-bs-dismiss="alert"></button></div>');
        $('#smsApiKey').val('');
    }).fail(function (xhr) {
        // The gateway's own response is shown: "insufficient credit" and "invalid sender id"
        // are both rejections, and only its wording tells them apart.
        $('#settingsAlert').html('<div class="alert alert-danger">'
            + 'Could not send: ' + esc(xhr.responseText || 'unknown error') + '</div>');
    }).always(function () {
        $btn.prop('disabled', false).html(original);
    });
}
