using AttendanceManagementSystem.UI.Controls;
using AttendanceManagementSystem.UI.Theme;
using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using ClosedXML.Excel;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace AttendanceManagementSystem.UI.Forms;

/// <summary>Reporting form — daily, monthly, late, absent, leave, employee list. Export to Excel and PDF.</summary>
public class ReportForm : Form
{
    private TabControl _tabs = null!;
    private AppDataGrid _grid = null!;
    private DateTimePicker _dtpFrom = null!;
    private DateTimePicker _dtpTo = null!;
    private ComboBox _cmbReport = null!;
    private Label _lblCount = null!;

    private readonly IReportService _reportService;
    private readonly IDepartmentService _deptService;

    public ReportForm(IReportService reportService, IDepartmentService deptService)
    {
        _reportService = reportService; _deptService = deptService;
        Build();
    }

    private void Build()
    {
        BackColor = AppTheme.FormBg;

        // ── Filter toolbar ────────────────────────────────────────────────────
        var toolbar = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = AppTheme.CardBg, Padding = new Padding(8, 8, 8, 0) };

        var lblRpt = new Label { Text = "Report:", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(8, 14), AutoSize = true };
        _cmbReport = new ComboBox { Location = new Point(56, 8), Width = 180, Font = AppTheme.BodyFont, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbReport.Items.AddRange(["Daily Attendance", "Late Report", "Absent Report", "Monthly Summary", "Leave Report", "Employee List"]);
        _cmbReport.SelectedIndex = 0;

        var lblFrom = new Label { Text = "From:", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(248, 14), AutoSize = true };
        _dtpFrom = new DateTimePicker { Location = new Point(284, 8), Width = 130, Format = DateTimePickerFormat.Short, Font = AppTheme.BodyFont, Value = DateTime.Today };
        var lblTo = new Label { Text = "To:", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(424, 14), AutoSize = true };
        _dtpTo = new DateTimePicker { Location = new Point(448, 8), Width = 130, Format = DateTimePickerFormat.Short, Font = AppTheme.BodyFont, Value = DateTime.Today };

        var btnLoad    = new AppButton { Text = "🔍 Generate", Width = 110, Location = new Point(590, 8) };
        var btnExcelc  = new AppButton { Text = "📊 Excel",   Width = 90,  Location = new Point(706, 8) };
        var btnPdf     = new AppButton { Text = "📄 PDF",     Width = 80,  Location = new Point(802, 8) };
        btnExcelc.SetSuccess(); btnPdf.SetDanger();
        btnLoad.Click   += async (s, e) => await GenerateReport();
        btnExcelc.Click += (s, e) => ExportExcel();
        btnPdf.Click    += (s, e) => ExportPdf();
        toolbar.Controls.AddRange([lblRpt, _cmbReport, lblFrom, _dtpFrom, lblTo, _dtpTo, btnLoad, btnExcelc, btnPdf]);

        _grid = new AppDataGrid { Dock = DockStyle.Fill };

        _lblCount = new Label { Dock = DockStyle.Bottom, Height = 24, Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0), BackColor = AppTheme.CardBg };

        Controls.Add(_grid);
        Controls.Add(toolbar);
        Controls.Add(_lblCount);
    }

    private async Task GenerateReport()
    {
        _grid.Columns.Clear();
        _grid.DataSource = null;
        var name = _cmbReport.SelectedItem?.ToString() ?? "";

        switch (name)
        {
            case "Daily Attendance":
                var daily = await _reportService.GetDailyAttendanceReportAsync(_dtpFrom.Value);
                BindAttendanceLogs(daily.ToList());
                break;
            case "Late Report":
                var late = await _reportService.GetLateReportAsync(_dtpFrom.Value, _dtpTo.Value);
                BindAttendanceLogs(late.ToList());
                break;
            case "Absent Report":
                var absent = await _reportService.GetAbsentReportAsync(_dtpFrom.Value, _dtpTo.Value);
                BindAttendanceLogs(absent.ToList());
                break;
            case "Monthly Summary":
                var summary = await _reportService.GetMonthlyAttendanceReportAsync(_dtpFrom.Value.Month, _dtpFrom.Value.Year);
                BindSummary(summary.ToList());
                break;
            case "Leave Report":
                var leave = await _reportService.GetLeaveReportAsync(_dtpFrom.Value, _dtpTo.Value);
                BindLeave(leave.ToList());
                break;
            case "Employee List":
                var emps = await _reportService.GetEmployeeListReportAsync();
                BindEmployees(emps.ToList());
                break;
        }
        _lblCount.Text = $"  Total records: {_grid.RowCount}";
    }

    private void BindAttendanceLogs(List<AttendanceLogDto> data)
    {
        _grid.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "Date",       DataPropertyName = "AttendanceDate",  Width = 100 },
            new DataGridViewTextBoxColumn { HeaderText = "Code",       DataPropertyName = "EmployeeCode",    Width = 90  },
            new DataGridViewTextBoxColumn { HeaderText = "Name",       DataPropertyName = "EmployeeName"                },
            new DataGridViewTextBoxColumn { HeaderText = "Department", DataPropertyName = "Department",      Width = 130 },
            new DataGridViewTextBoxColumn { HeaderText = "Check In",   DataPropertyName = "CheckInDisplay",  Width = 90  },
            new DataGridViewTextBoxColumn { HeaderText = "Check Out",  DataPropertyName = "CheckOutDisplay", Width = 90  },
            new DataGridViewTextBoxColumn { HeaderText = "Status",     DataPropertyName = "StatusDisplay",   Width = 90  },
            new DataGridViewTextBoxColumn { HeaderText = "Late min",   DataPropertyName = "LateMinutes",     Width = 80  },
            new DataGridViewTextBoxColumn { HeaderText = "Hours",      DataPropertyName = "WorkingHours",    Width = 70  }
        );
        _grid.DataSource = data;
    }

    private void BindSummary(List<AttendanceSummaryDto> data)
    {
        _grid.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "Code",       DataPropertyName = "EmployeeCode",       Width = 90 },
            new DataGridViewTextBoxColumn { HeaderText = "Name",       DataPropertyName = "EmployeeName"                  },
            new DataGridViewTextBoxColumn { HeaderText = "Department", DataPropertyName = "Department",          Width = 130 },
            new DataGridViewTextBoxColumn { HeaderText = "Total",      DataPropertyName = "TotalDays",           Width = 60  },
            new DataGridViewTextBoxColumn { HeaderText = "Present",    DataPropertyName = "PresentDays",         Width = 70  },
            new DataGridViewTextBoxColumn { HeaderText = "Absent",     DataPropertyName = "AbsentDays",          Width = 70  },
            new DataGridViewTextBoxColumn { HeaderText = "Late",       DataPropertyName = "LateDays",            Width = 60  },
            new DataGridViewTextBoxColumn { HeaderText = "Leave",      DataPropertyName = "LeaveDays",           Width = 60  },
            new DataGridViewTextBoxColumn { HeaderText = "Hours",      DataPropertyName = "TotalWorkingHours",   Width = 70  },
            new DataGridViewTextBoxColumn { HeaderText = "Attendance%",DataPropertyName = "AttendancePercentage",Width = 100 }
        );
        _grid.DataSource = data;
    }

    private void BindLeave(List<LeaveRequestDto> data)
    {
        _grid.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "Employee",    DataPropertyName = "EmployeeName"                 },
            new DataGridViewTextBoxColumn { HeaderText = "Leave Type",  DataPropertyName = "LeaveTypeName",  Width = 120   },
            new DataGridViewTextBoxColumn { HeaderText = "From",        DataPropertyName = "FromDate",        Width = 100  },
            new DataGridViewTextBoxColumn { HeaderText = "To",          DataPropertyName = "ToDate",          Width = 100  },
            new DataGridViewTextBoxColumn { HeaderText = "Days",        DataPropertyName = "TotalDays",       Width = 60   },
            new DataGridViewTextBoxColumn { HeaderText = "Status",      DataPropertyName = "StatusDisplay",   Width = 90   }
        );
        _grid.DataSource = data;
    }

    private void BindEmployees(List<EmployeeListItemDto> data)
    {
        _grid.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "Code",        DataPropertyName = "EmployeeCode" },
            new DataGridViewTextBoxColumn { HeaderText = "Name",        DataPropertyName = "FullName" },
            new DataGridViewTextBoxColumn { HeaderText = "Department",  DataPropertyName = "Department" },
            new DataGridViewTextBoxColumn { HeaderText = "Designation", DataPropertyName = "Designation" },
            new DataGridViewTextBoxColumn { HeaderText = "Branch",      DataPropertyName = "Branch" },
            new DataGridViewTextBoxColumn { HeaderText = "Phone",       DataPropertyName = "Phone",   Width = 110 },
            new DataGridViewTextBoxColumn { HeaderText = "Email",       DataPropertyName = "Email" }
        );
        _grid.DataSource = data;
    }

    private void ExportExcel()
    {
        if (_grid.RowCount == 0) { MessageBox.Show("No data to export.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        using var dlg = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = $"Report_{DateTime.Now:yyyyMMdd_HHmm}.xlsx" };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Report");
        for (int c = 0; c < _grid.Columns.Count; c++)
            ws.Cell(1, c + 1).Value = _grid.Columns[c].HeaderText;
        ws.Row(1).Style.Font.Bold = true;
        ws.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1976D2");
        ws.Row(1).Style.Font.FontColor = XLColor.White;

        for (int r = 0; r < _grid.Rows.Count; r++)
            for (int c = 0; c < _grid.Columns.Count; c++)
                ws.Cell(r + 2, c + 1).Value = _grid.Rows[r].Cells[c].Value?.ToString() ?? string.Empty;

        ws.Columns().AdjustToContents();
        wb.SaveAs(dlg.FileName);
        MessageBox.Show($"Exported to:\n{dlg.FileName}", "Excel Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ExportPdf()
    {
        if (_grid.RowCount == 0) { MessageBox.Show("No data to export.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        using var dlg = new SaveFileDialog { Filter = "PDF|*.pdf", FileName = $"Report_{DateTime.Now:yyyyMMdd_HHmm}.pdf" };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        using var fs = new FileStream(dlg.FileName, FileMode.Create);
        var doc = new Document(PageSize.A4.Rotate(), 20, 20, 20, 20);
        PdfWriter.GetInstance(doc, fs);
        doc.Open();

        doc.Add(new Paragraph($"Attendance Report — {_cmbReport.SelectedItem}",
            FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14)));
        doc.Add(new Paragraph($"Generated: {DateTime.Now:dd-MMM-yyyy HH:mm}",
            FontFactory.GetFont(FontFactory.HELVETICA, 9)));
        doc.Add(new Paragraph(" "));

        var table = new PdfPTable(_grid.Columns.Count) { WidthPercentage = 100 };
        var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8f, new BaseColor(255, 255, 255));
        var cellFont   = FontFactory.GetFont(FontFactory.HELVETICA, 7);

        foreach (DataGridViewColumn col in _grid.Columns)
        {
            var cell = new PdfPCell(new Phrase(col.HeaderText, headerFont))
            { BackgroundColor = new BaseColor(25, 118, 210), Padding = 4 };
            table.AddCell(cell);
        }

        foreach (DataGridViewRow row in _grid.Rows)
            for (int c = 0; c < _grid.Columns.Count; c++)
                table.AddCell(new PdfPCell(new Phrase(row.Cells[c].Value?.ToString() ?? "", cellFont)) { Padding = 3 });

        doc.Add(table);
        doc.Close();
        MessageBox.Show($"Exported to:\n{dlg.FileName}", "PDF Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
