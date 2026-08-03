/* ── Profile Page JavaScript ── */

document.addEventListener('DOMContentLoaded', function () {
    /* ── Show/Hide password toggle ── */
    document.querySelectorAll('.toggle-pw').forEach(function (btn) {
        btn.addEventListener('click', function () {
            var targetInput = document.getElementById(this.dataset.target);
            var icon = this.querySelector('i');
            if (targetInput.type === 'password') {
                targetInput.type = 'text';
                icon.classList.replace('fa-eye-slash', 'fa-eye');
            } else {
                targetInput.type = 'password';
                icon.classList.replace('fa-eye', 'fa-eye-slash');
            }
        });
    });

    /* ── Password strength evaluation ── */
    var newPw = document.getElementById('newPw');
    var bar = document.getElementById('strengthBar');
    var lbl = document.getElementById('strengthLabel');
    if (newPw) {
        newPw.addEventListener('input', function () {
            var v = this.value;
            var score = 0;
            if (v.length >= 8) score++;
            if (/[A-Z]/.test(v)) score++;
            if (/[0-9]/.test(v)) score++;
            if (/[^A-Za-z0-9]/.test(v)) score++;
            var pct = score * 25;
            var cols = ['#fe5d70', '#fe9365', '#f4c22b', '#0ac282'];
            var lbls = ['Weak', 'Fair', 'Good', 'Strong'];
            bar.style.width = pct + '%';
            bar.style.backgroundColor = cols[score - 1] || '#ddd';
            lbl.textContent = score > 0 ? lbls[score - 1] : '';
            lbl.style.color = cols[score - 1] || '#aaa';
        });
    }

    /* ── Confirm password match check ── */
    var conPw = document.getElementById('conPw');
    var msg = document.getElementById('matchMsg');
    if (conPw && newPw) {
        conPw.addEventListener('input', function () {
            if (this.value === '' || newPw.value === '') { msg.textContent = ''; return; }
            if (this.value === newPw.value) {
                msg.textContent = '✓ Passwords match';
                msg.style.color = '#0ac282';
            } else {
                msg.textContent = '✗ Passwords do not match';
                msg.style.color = '#fe5d70';
            }
        });
    }
});
