/* ── Admin Dashboard JavaScript ── */

let pieChart = null;

async function loadDashboard() {
    try {
        const res = await fetch('/api/attendance/dashboard');
        if (!res.ok) throw new Error('Failed to load dashboard data');
        const d = await res.json();

        // Stat cards — grouped digits so larger headcounts stay readable (1,240 not 1240)
        const num = v => (v ?? 0).toLocaleString();
        document.getElementById('statTotal').textContent   = num(d.TotalEmployees);
        document.getElementById('statPresent').textContent = num(d.PresentToday);
        document.getElementById('statAbsent').textContent  = num(d.AbsentToday);
        document.getElementById('statLeave').textContent   = num(d.OnLeaveToday);

        // Pie labels
        const pct = d.AttendancePercentage ?? 0;
        document.getElementById('pctLabel').textContent  = pct.toFixed(1) + '%';
        document.getElementById('lblPresent').textContent = d.PresentToday  ?? 0;
        document.getElementById('lblAbsent').textContent  = d.AbsentToday   ?? 0;
        document.getElementById('lblLate').textContent    = d.LateToday     ?? 0;
        document.getElementById('lblLeave').textContent   = d.OnLeaveToday  ?? 0;

        // Doughnut chart
        const ctx = document.getElementById('attendancePie').getContext('2d');
        if (pieChart) pieChart.destroy();
        pieChart = new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: ['Present', 'Absent', 'Late', 'On Leave'],
                datasets: [{
                    data: [d.PresentToday, d.AbsentToday, d.LateToday, d.OnLeaveToday],
                    backgroundColor: ['#28a745','#dc3545','#ffc107','#17a2b8'],
                    borderWidth: 2
                }]
            },
            options: {
                cutout: '70%',
                plugins: { legend: { display: false } },
                animation: { duration: 600 }
            }
        });

        // Recent attendance table
        const tbody = document.getElementById('recentBody');
        const rows  = d.RecentAttendance ?? [];
        if (rows.length === 0) {
            tbody.innerHTML = '<tr><td colspan="5" class="text-center py-3 text-muted">No attendance records for today.</td></tr>';
            return;
        }
        tbody.innerHTML = rows.slice(0, 15).map(r => {
            const statusBadge = {
                'Present' : '<span class="badge bg-success">Present</span>',
                'Absent'  : '<span class="badge bg-danger">Absent</span>',
                'Late'    : '<span class="badge bg-warning text-dark">Late</span>',
                'OnLeave' : '<span class="badge bg-info">On Leave</span>',
                'Holiday' : '<span class="badge bg-secondary">Holiday</span>'
            }[r.Status] ?? `<span class="badge bg-light text-dark">${r.Status ?? ''}</span>`;

            const fmt = v => v ? new Date(v).toLocaleTimeString([], {hour:'2-digit',minute:'2-digit'}) : '—';
            const date = r.Date ? new Date(r.Date).toLocaleDateString() : '—';
            return `<tr>
                <td>${r.EmployeeName ?? '—'}</td>
                <td>${date}</td>
                <td>${fmt(r.CheckIn)}</td>
                <td>${fmt(r.CheckOut)}</td>
                <td>${statusBadge}</td>
            </tr>`;
        }).join('');
    } catch (e) {
        document.getElementById('recentBody').innerHTML =
            '<tr><td colspan="5" class="text-center text-danger py-3">Failed to load data.</td></tr>';
        console.error(e);
    }
}

document.addEventListener('DOMContentLoaded', function () {
    loadDashboard();
});
