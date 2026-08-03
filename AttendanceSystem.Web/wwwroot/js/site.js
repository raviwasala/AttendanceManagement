/* ── Shared Site-wide JavaScript ── */

window.getCurrentUserId = function () {
    return parseInt(document.body.dataset.userId) || 1;
};

/* ── Toastr Global Configuration & Notification Helpers ── */
if (typeof toastr !== 'undefined') {
    toastr.options = {
        "closeButton": true,
        "progressBar": true,
        "positionClass": "toast-top-right",
        "showDuration": "300",
        "hideDuration": "500",
        "timeOut": "4000"
    };
}

window.notifySuccess = function (msg, title) {
    if (typeof toastr !== 'undefined') toastr.success(msg, title || 'Success');
    else alert(msg);
};

window.notifyError = function (msg, title) {
    if (typeof toastr !== 'undefined') toastr.error(msg, title || 'Error');
    else alert(msg);
};

window.notifyConfirm = function (options, onConfirm) {
    var title = typeof options === 'string' ? options : (options.title || 'Are you sure?');
    var text = typeof options === 'object' ? (options.text || '') : '';
    var confirmText = typeof options === 'object' ? (options.confirmText || 'Yes, proceed!') : 'Yes, proceed!';
    var icon = typeof options === 'object' ? (options.icon || 'warning') : 'warning';

    if (typeof Swal !== 'undefined') {
        Swal.fire({
            title: title,
            text: text,
            icon: icon,
            showCancelButton: true,
            confirmButtonColor: '#00acac',
            cancelButtonColor: '#6c757d',
            confirmButtonText: confirmText,
            cancelButtonText: 'Cancel',
            customClass: {
                confirmButton: 'btn btn-primary me-2',
                cancelButton: 'btn btn-secondary'
            },
            buttonsStyling: false
        }).then(function (result) {
            if (result.isConfirmed && typeof onConfirm === 'function') {
                onConfirm();
            }
        });
    } else {
        if (confirm(title + (text ? '\n' + text : ''))) {
            if (typeof onConfirm === 'function') onConfirm();
        }
    }
};

document.addEventListener('DOMContentLoaded', function () {
    /* ── Logout confirmation ── */
    var trigger = document.getElementById('logoutTrigger');
    if (trigger) {
        trigger.addEventListener('click', function (e) {
            e.preventDefault();
            window.notifyConfirm({
                title: 'Log Out',
                text: 'Are you sure you want to end your current session?',
                confirmText: 'Log Out',
                icon: 'question'
            }, function () {
                document.getElementById('logoutForm').submit();
            });
        });
    }
});
