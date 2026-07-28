using AttendanceManagementSystem.UI.Controls;
using AttendanceManagementSystem.UI.Theme;
using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Common.Exceptions;

namespace AttendanceManagementSystem.UI.Forms;

/// <summary>Dashboard — real-time summary tiles + recent attendance grid.</summary>
public class DashboardForm : Form
{
    private CardPanel _cardTotal = null!;
    private CardPanel _cardPresent = null!;
    private CardPanel _cardAbsent = null!;
    private CardPanel _cardLate = null!;
    private CardPanel _cardLeave = null!;
    private CardPanel _cardPct = null!;
    private AppDataGrid _gridRecent = null!;
    private System.Windows.Forms.Timer _refreshTimer = null!;
    private Label _lblLastRefresh = null!;

    private readonly IAttendanceService _attendance;

    public DashboardForm(IAttendanceService attendance)
    {
        _attendance = attendance;
        InitializeComponent();
        _ = LoadDataAsync();
        StartAutoRefresh();
    }

    private void InitializeComponent()
    {
        BackColor = AppTheme.FormBg;
        Padding = new Padding(8);

        // Section title
        var lblTitle = new Label
        {
            Text = "Today's Overview",
            Font = AppTheme.HeaderFont,
            ForeColor = AppTheme.BodyText,
            AutoSize = true,
            Location = new Point(0, 4)
        };

        _lblLastRefresh = new Label
        {
            Font = AppTheme.SmallFont,
            ForeColor = AppTheme.SubText,
            AutoSize = true,
            Location = new Point(0, 28)
        };

        // ── Stat cards ────────────────────────────────────────────────────────
        _cardTotal   = CreateCard("Total Employees",    "0", AppTheme.PrimaryColor, 0);
        _cardPresent = CreateCard("Present Today",      "0", AppTheme.SuccessColor, 210);
        _cardAbsent  = CreateCard("Absent Today",       "0", AppTheme.DangerColor,  420);
        _cardLate    = CreateCard("Late Arrivals",      "0", AppTheme.WarningColor, 630);
        _cardLeave   = CreateCard("On Leave",           "0", AppTheme.InfoColor,    840);
        _cardPct     = CreateCard("Attendance %",       "0%", AppTheme.PrimaryColor, 1050);

        // ── Recent attendance grid ────────────────────────────────────────────
        var lblRecent = new Label
        {
            Text = "Recent Attendance",
            Font = AppTheme.HeaderFont,
            ForeColor = AppTheme.BodyText,
            AutoSize = true,
            Location = new Point(0, 164)
        };

        _gridRecent = new AppDataGrid { Location = new Point(0, 192), Height = 320, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom };
        SetupGridColumns();

        // Refresh button
        var btnRefresh = new AppButton { Text = "↻ Refresh", Width = 100, Location = new Point(912, 158) };
        btnRefresh.Click += async (s, e) => await LoadDataAsync();

        Controls.AddRange([lblTitle, _lblLastRefresh,
            _cardTotal, _cardPresent, _cardAbsent, _cardLate, _cardLeave, _cardPct,
            lblRecent, _gridRecent, btnRefresh]);

        Resize += (s, e) => RepositionCards();
    }

    private CardPanel CreateCard(string title, string value, Color accent, int xBase)
    {
        return new CardPanel
        {
            CardTitle = title,
            CardValue = value,
            AccentColor = accent,
            Size = new Size(195, 115),
            Location = new Point(xBase, 42)
        };
    }

    private void RepositionCards()
    {
        int w = (ClientSize.Width - 10) / 6;
        _cardTotal.Width   = w; _cardTotal.Left   = 0;
        _cardPresent.Width = w; _cardPresent.Left = w + 2;
        _cardAbsent.Width  = w; _cardAbsent.Left  = (w + 2) * 2;
        _cardLate.Width    = w; _cardLate.Left    = (w + 2) * 3;
        _cardLeave.Width   = w; _cardLeave.Left   = (w + 2) * 4;
        _cardPct.Width     = w; _cardPct.Left     = (w + 2) * 5;
        _gridRecent.Width  = ClientSize.Width - 16;
    }

    private void SetupGridColumns()
    {
        _gridRecent.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "Employee Code", DataPropertyName = "EmployeeCode", Width = 120 },
            new DataGridViewTextBoxColumn { HeaderText = "Employee Name", DataPropertyName = "EmployeeName" },
            new DataGridViewTextBoxColumn { HeaderText = "Department", DataPropertyName = "Department" },
            new DataGridViewTextBoxColumn { HeaderText = "Check In", DataPropertyName = "CheckInDisplay", Width = 100 },
            new DataGridViewTextBoxColumn { HeaderText = "Check Out", DataPropertyName = "CheckOutDisplay", Width = 100 },
            new DataGridViewTextBoxColumn { HeaderText = "Status", DataPropertyName = "StatusDisplay", Width = 100 },
            new DataGridViewTextBoxColumn { HeaderText = "Late (min)", DataPropertyName = "LateMinutes", Width = 90 },
            new DataGridViewTextBoxColumn { HeaderText = "Hours", DataPropertyName = "WorkingHours", Width = 80 }
        );
    }

    private async Task LoadDataAsync()
    {
        try
        {
            var result = await _attendance.GetDashboardStatsAsync();
            if (!result.IsSuccess) return;
            var stats = result.Data!;

            _cardTotal.CardValue   = stats.TotalEmployees.ToString();
            _cardPresent.CardValue = stats.PresentToday.ToString();
            _cardAbsent.CardValue  = stats.AbsentToday.ToString();
            _cardLate.CardValue    = stats.LateToday.ToString();
            _cardLeave.CardValue   = stats.OnLeaveToday.ToString();
            _cardPct.CardValue     = $"{stats.AttendancePercentage}%";

            // Refresh card displays
            foreach (Control c in Controls)
                if (c is CardPanel card) card.Invalidate();

            _gridRecent.DataSource = null;
            _gridRecent.DataSource = stats.RecentAttendance.ToList();

            // Colour status column
            foreach (DataGridViewRow row in _gridRecent.Rows)
            {
                var statusCell = row.Cells["StatusDisplay"];
                if (statusCell?.Value?.ToString() == "Late")
                    statusCell.Style.ForeColor = AppTheme.WarningColor;
                else if (statusCell?.Value?.ToString() == "Absent")
                    statusCell.Style.ForeColor = AppTheme.DangerColor;
            }

            _lblLastRefresh.Text = $"Last refreshed: {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            GlobalExceptionHandler.HandleUI(ex, nameof(DashboardForm));
        }
    }

    private void StartAutoRefresh()
    {
        _refreshTimer = new System.Windows.Forms.Timer { Interval = 60_000 };
        _refreshTimer.Tick += async (s, e) => await LoadDataAsync();
        _refreshTimer.Start();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _refreshTimer.Stop();
        base.OnHandleDestroyed(e);
    }
}
