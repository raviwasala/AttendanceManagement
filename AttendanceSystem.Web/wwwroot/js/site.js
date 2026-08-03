/* ── Shared Site-wide JavaScript ── */

window.getCurrentUserId = function () {
    return parseInt(document.body.dataset.userId) || 1;
};

document.addEventListener('DOMContentLoaded', function () {
    /* ── Logout confirmation ── */
    var trigger = document.getElementById('logoutTrigger');
    if (trigger) {
        trigger.addEventListener('click', function (e) {
            e.preventDefault();
            if (confirm('Are you sure you want to log out?')) {
                document.getElementById('logoutForm').submit();
            }
        });
    }
});
