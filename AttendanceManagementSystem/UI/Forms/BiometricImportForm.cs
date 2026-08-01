using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Data;

namespace AttendanceManagementSystem.UI.Forms;

/// <summary>
/// Biometric device attendance import form.
/// Tab 1 — Direct Connect: reads punch data directly from an MS Access .mdb/.accdb file.
/// Tab 2 — File Import: imports from a CSV or Excel file exported from device software.
/// </summary>
public class BiometricImportForm : Form
{
    private readonly IBiometricImportService _importService;

    // ── Controls ──────────────────────────────────────────────────────
    private TabControl tabControl;
    private TabPage tabDirect, tabFile;

    // Direct-connect tab
    private Label lblMdbPath, lblDirectFrom, lblDirectTo, lblDirectStatus;
    private TextBox txtMdbPath;
    private Button btnBrowseMdb, btnDirectImport, btnDirectPreview;
    private DateTimePicker dtpDirectFrom, dtpDirectTo;
    private DataGridView dgvDirectPreview;
    private ProgressBar pbDirect;

    // File-import tab
    private Label lblFilePath, lblFileFrom, lblFileTo, lblFileStatus;
    private TextBox txtFilePath;
    private Button btnBrowseFile, btnFileImport, btnFilePreview;
    private DateTimePicker dtpFileFrom, dtpFileTo;
    private DataGridView dgvFilePreview;
    private ProgressBar pbFile;

    // Shared result panel
    private Panel pnlResult;
    private Label lblResultTitle, lblInserted, lblSkipped, lblFailed;
    private ListBox lstErrors;
    private Button btnClose;

    public BiometricImportForm(IBiometricImportService importService)
    {
        _importService = importService;
        InitializeComponent();
    }

    // ──────────────────────────────────────────────────────────────────
    // INIT
    // ──────────────────────────────────────────────────────────────────

    private void InitializeComponent()
    {
        Text            = "Biometric Device Import";
        Size            = new Size(900, 680);
        StartPosition   = FormStartPosition.CenterScreen;
        MinimumSize     = new Size(780, 560);
        Font            = new Font("Segoe UI", 9.5f);
        BackColor       = Color.FromArgb(245, 247, 250);

        tabControl = new TabControl { Dock = DockStyle.Top, Height = 460, Font = new Font("Segoe UI", 9.5f) };
        tabDirect  = new TabPage("  Direct Connect (MDB File)  ");
        tabFile    = new TabPage("  File Import (CSV / Excel)  ");

        BuildDirectTab();
        BuildFileTab();

        tabControl.TabPages.AddRange(new[] { tabDirect, tabFile });

        pnlResult = BuildResultPanel();

        Controls.Add(pnlResult);
        Controls.Add(tabControl);
    }

    // ──────────────────────────────────────────────────────────────────
    // DIRECT CONNECT TAB
    // ──────────────────────────────────────────────────────────────────

    private void BuildDirectTab()
    {
        tabDirect.BackColor = Color.FromArgb(245, 247, 250);
        tabDirect.Padding   = new Padding(10);

        // Path row
        lblMdbPath  = Lbl("Device MDB File Path:");
        txtMdbPath  = new TextBox { Left = 10, Top = 40, Width = 580, ReadOnly = true };
        btnBrowseMdb = Btn("Browse...", 600, 37, 120, btnBrowseMdb_Click);

        // Date row
        lblDirectFrom = Lbl("From:");
        lblDirectFrom.Location = new Point(10, 80);
        dtpDirectFrom = new DateTimePicker { Left = 60, Top = 77, Width = 140, Format = DateTimePickerFormat.Short };
        dtpDirectFrom.Value = DateTime.Today.AddDays(-7);

        lblDirectTo = Lbl("To:");
        lblDirectTo.Location = new Point(215, 80);
        dtpDirectTo = new DateTimePicker { Left = 245, Top = 77, Width = 140, Format = DateTimePickerFormat.Short };
        dtpDirectTo.Value = DateTime.Today;

        btnDirectPreview = Btn("View Enroll", 410, 74, 100, btnDirectPreview_Click);
        btnDirectImport  = Btn("Import →", 520, 74, 110, btnDirectImport_Click);
        btnDirectImport.BackColor = Color.FromArgb(39, 174, 96);
        btnDirectImport.ForeColor = Color.White;

        pbDirect = new ProgressBar { Left = 10, Top = 115, Width = 840, Height = 6, Style = ProgressBarStyle.Marquee, Visible = false };

        dgvDirectPreview = BuildGrid();
        dgvDirectPreview.Location = new Point(10, 130);
        dgvDirectPreview.Size     = new Size(840, 270);

        lblDirectStatus = new Label { Left = 10, Top = 410, Width = 840, Height = 20, ForeColor = Color.Gray };

        tabDirect.Controls.AddRange(new Control[]
        {
            lblMdbPath, txtMdbPath, btnBrowseMdb,
            lblDirectFrom, dtpDirectFrom, lblDirectTo, dtpDirectTo,
            btnDirectPreview, btnDirectImport,
            pbDirect, dgvDirectPreview, lblDirectStatus
        });
    }

    // ──────────────────────────────────────────────────────────────────
    // FILE IMPORT TAB
    // ──────────────────────────────────────────────────────────────────

    private void BuildFileTab()
    {
        tabFile.BackColor = Color.FromArgb(245, 247, 250);
        tabFile.Padding   = new Padding(10);

        lblFilePath  = Lbl("File Path (CSV / Excel):");
        txtFilePath  = new TextBox { Left = 10, Top = 40, Width = 580, ReadOnly = true };
        btnBrowseFile = Btn("Browse...", 600, 37, 120, btnBrowseFile_Click);

        lblFileFrom = Lbl("From:");
        lblFileFrom.Location = new Point(10, 80);
        dtpFileFrom = new DateTimePicker { Left = 60, Top = 77, Width = 140, Format = DateTimePickerFormat.Short };
        dtpFileFrom.Value = DateTime.Today.AddDays(-30);

        lblFileTo = Lbl("To:");
        lblFileTo.Location = new Point(215, 80);
        dtpFileTo = new DateTimePicker { Left = 245, Top = 77, Width = 140, Format = DateTimePickerFormat.Short };
        dtpFileTo.Value = DateTime.Today;

        btnFilePreview = Btn("Preview", 410, 74, 100, btnFilePreview_Click);
        btnFileImport  = Btn("Import →", 520, 74, 110, btnFileImport_Click);
        btnFileImport.BackColor = Color.FromArgb(39, 174, 96);
        btnFileImport.ForeColor = Color.White;

        pbFile = new ProgressBar { Left = 10, Top = 115, Width = 840, Height = 6, Style = ProgressBarStyle.Marquee, Visible = false };

        dgvFilePreview = BuildGrid();
        dgvFilePreview.Location = new Point(10, 130);
        dgvFilePreview.Size     = new Size(840, 270);

        lblFileStatus = new Label { Left = 10, Top = 410, Width = 840, Height = 20, ForeColor = Color.Gray };

        tabFile.Controls.AddRange(new Control[]
        {
            lblFilePath, txtFilePath, btnBrowseFile,
            lblFileFrom, dtpFileFrom, lblFileTo, dtpFileTo,
            btnFilePreview, btnFileImport,
            pbFile, dgvFilePreview, lblFileStatus
        });
    }

    // ──────────────────────────────────────────────────────────────────
    // RESULT PANEL
    // ──────────────────────────────────────────────────────────────────

    private Panel BuildResultPanel()
    {
        var pnl = new Panel
        {
            Dock        = DockStyle.Bottom,
            Height      = 185,
            BackColor   = Color.White,
            Padding     = new Padding(10),
            Visible     = false
        };

        lblResultTitle = new Label
        {
            Text      = "Import Result",
            Font      = new Font("Segoe UI", 10, FontStyle.Bold),
            Left = 10, Top = 8, Width = 300
        };

        lblInserted = new Label { Left = 10,  Top = 32, Width = 200, ForeColor = Color.DarkGreen };
        lblSkipped  = new Label { Left = 220, Top = 32, Width = 200, ForeColor = Color.DarkOrange };
        lblFailed   = new Label { Left = 430, Top = 32, Width = 200, ForeColor = Color.Red };

        lstErrors = new ListBox { Left = 10, Top = 58, Width = 840, Height = 85, Font = new Font("Segoe UI", 8.5f) };

        btnClose = Btn("Close", 780, 150, 80, (s, e) => pnl.Visible = false);
        btnClose.BackColor = Color.FromArgb(52, 73, 94);
        btnClose.ForeColor = Color.White;

        pnl.Controls.AddRange(new Control[] { lblResultTitle, lblInserted, lblSkipped, lblFailed, lstErrors, btnClose });
        return pnl;
    }

    // ──────────────────────────────────────────────────────────────────
    // DIRECT CONNECT EVENTS
    // ──────────────────────────────────────────────────────────────────

    private void btnBrowseMdb_Click(object? s, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title  = "Select Biometric Device Database File",
            Filter = "Access Database (*.mdb;*.accdb)|*.mdb;*.accdb|All Files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            txtMdbPath.Text = dlg.FileName;
    }

    private async void btnDirectPreview_Click(object? s, EventArgs e)
    {
        if (!ValidateMdbPath()) return;
        SetBusy(pbDirect, btnDirectPreview, btnDirectImport, lblDirectStatus, "Reading Enroll table...");
        try
        {
            // Use preview only — don't save; re-read raw data for grid
            var enrollments = await _importService.ReadEnrollTableAsync(txtMdbPath.Text);

            // Filter out binary/image columns that cannot be displayed in DataGridView
            var filteredTable = FilterBinaryColumns(enrollments);

            dgvDirectPreview.DataSource = filteredTable;
            lblDirectStatus.Text = $"{filteredTable.Rows.Count} records read from the Enroll table.";
        }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { SetIdle(pbDirect, btnDirectPreview, btnDirectImport); }
    }

    private async void btnDirectImport_Click(object? s, EventArgs e)
    {
        if (!ValidateMdbPath()) return;
        if (MessageBox.Show(
            $"Import attendance punches from:\n{txtMdbPath.Text}\n\nDate range: {dtpDirectFrom.Value:d} to {dtpDirectTo.Value:d}\n\nContinue?",
            "Confirm Import", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        SetBusy(pbDirect, btnDirectPreview, btnDirectImport, lblDirectStatus, "Importing from device file...");
        try
        {
            var result = await _importService.ImportFromAccessFileAsync(
                txtMdbPath.Text, dtpDirectFrom.Value.Date, dtpDirectTo.Value.Date);
            ShowResult(result);
            lblDirectStatus.Text = $"Done. Inserted: {result.Inserted}  Skipped: {result.Skipped}  Failed: {result.Failed}";
        }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { SetIdle(pbDirect, btnDirectPreview, btnDirectImport); }
    }

    private bool ValidateMdbPath()
    {
        if (string.IsNullOrWhiteSpace(txtMdbPath.Text) || !File.Exists(txtMdbPath.Text))
        {
            MessageBox.Show("Please select a valid Access database file.", "Validation",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        return true;
    }

    // ──────────────────────────────────────────────────────────────────
    // FILE IMPORT EVENTS
    // ──────────────────────────────────────────────────────────────────

    private void btnBrowseFile_Click(object? s, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title  = "Select Exported Attendance File",
            Filter = "Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|CSV Files (*.csv;*.txt)|*.csv;*.txt|All Files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            txtFilePath.Text = dlg.FileName;
    }

    private async void btnFilePreview_Click(object? s, EventArgs e)
    {
        if (!ValidateFilePath()) return;
        SetBusy(pbFile, btnFilePreview, btnFileImport, lblFileStatus, "Parsing file...");
        try
        {
            var preview = await _importService.PreviewFileAsync(txtFilePath.Text);
            BindGrid(dgvFilePreview, preview);
            lblFileStatus.Text = $"{preview.Count} records found in file.";
        }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { SetIdle(pbFile, btnFilePreview, btnFileImport); }
    }

    private async void btnFileImport_Click(object? s, EventArgs e)
    {
        if (!ValidateFilePath()) return;
        if (MessageBox.Show(
            $"Import attendance punches from:\n{txtFilePath.Text}\n\nDate range: {dtpFileFrom.Value:d} to {dtpFileTo.Value:d}\n\nContinue?",
            "Confirm Import", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        SetBusy(pbFile, btnFilePreview, btnFileImport, lblFileStatus, "Importing file...");
        try
        {
            var result = await _importService.ImportFromFileAsync(
                txtFilePath.Text, dtpFileFrom.Value.Date, dtpFileTo.Value.Date);
            ShowResult(result);
            lblFileStatus.Text = $"Done. Inserted: {result.Inserted}  Skipped: {result.Skipped}  Failed: {result.Failed}";
        }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { SetIdle(pbFile, btnFilePreview, btnFileImport); }
    }

    private bool ValidateFilePath()
    {
        if (string.IsNullOrWhiteSpace(txtFilePath.Text) || !File.Exists(txtFilePath.Text))
        {
            MessageBox.Show("Please select a valid file.", "Validation",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        return true;
    }

    // ──────────────────────────────────────────────────────────────────
    // HELPERS
    // ──────────────────────────────────────────────────────────────────

    private void ShowResult(BiometricImportResultDto result)
    {
        lblInserted.Text = $"✔ Inserted : {result.Inserted}";
        lblSkipped.Text  = $"⊘ Skipped  : {result.Skipped}";
        lblFailed.Text   = $"✖ Failed   : {result.Failed}";
        lstErrors.Items.Clear();
        foreach (var w in result.Warnings) lstErrors.Items.Add($"⚠ {w}");
        foreach (var err in result.Errors)  lstErrors.Items.Add($"✖ {err}");
        pnlResult.Visible = true;
    }

    private void ShowError(string message) =>
        MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

    private static DataTable FilterBinaryColumns(DataTable table)
    {
        // Create a new DataTable with only displayable columns
        var filtered = new DataTable();

        foreach (DataColumn column in table.Columns)
        {
            // Skip binary/image columns
            if (column.DataType == typeof(byte[]))
                continue;

            // Skip columns with common binary/template names
            if (column.ColumnName.Contains("Template", StringComparison.OrdinalIgnoreCase) ||
                column.ColumnName.Contains("Features", StringComparison.OrdinalIgnoreCase) ||
                column.ColumnName.Contains("Image", StringComparison.OrdinalIgnoreCase) ||
                column.ColumnName.Contains("Fingerprint", StringComparison.OrdinalIgnoreCase) ||
                column.ColumnName.Contains("Photo", StringComparison.OrdinalIgnoreCase) ||
                column.ColumnName.Contains("Duress", StringComparison.OrdinalIgnoreCase))
                continue;

            // Add the column to filtered table
            filtered.Columns.Add(column.ColumnName, column.DataType);
        }

        // Copy rows with only the filtered columns
        foreach (DataRow row in table.Rows)
        {
            var newRow = filtered.NewRow();
            foreach (DataColumn column in filtered.Columns)
            {
                newRow[column.ColumnName] = row[column.ColumnName];
            }
            filtered.Rows.Add(newRow);
        }

        return filtered;
    }

    private static void BindGrid(DataGridView grid, List<BiometricPunchDto> punches)
    {
        grid.DataSource = punches.Select(p => new
        {
            p.EnrollId,
            p.EmpName,
            PunchTime = p.PunchTime.ToString("yyyy-MM-dd HH:mm:ss"),
            p.FingerNumber,
            p.CardNo
        }).ToList();
    }

    private static void SetBusy(ProgressBar pb, Button b1, Button b2, Label lbl, string msg)
    {
        pb.Visible = true;
        b1.Enabled = b2.Enabled = false;
        lbl.Text   = msg;
        lbl.ForeColor = Color.DimGray;
    }

    private static void SetIdle(ProgressBar pb, Button b1, Button b2)
    {
        pb.Visible = false;
        b1.Enabled = b2.Enabled = true;
    }

    private static Label Lbl(string text) =>
        new Label { Text = text, Left = 10, Top = 12, AutoSize = true, Font = new Font("Segoe UI", 9.5f) };

    private static Button Btn(string text, int left, int top, int width, EventHandler onClick)
    {
        var btn = new Button
        {
            Text      = text,
            Left      = left,
            Top       = top,
            Width     = width,
            Height    = 30,
            FlatStyle = FlatStyle.Flat,
            Cursor    = Cursors.Hand
        };
        btn.FlatAppearance.BorderColor = Color.Silver;
        btn.Click += onClick;
        return btn;
    }

    private static DataGridView BuildGrid()
    {
        var grid = new DataGridView
        {
            ReadOnly              = true,
            AllowUserToAddRows    = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
            BorderStyle           = BorderStyle.None,
            BackgroundColor       = Color.White,
            RowHeadersVisible     = false,
            Font                  = new Font("Segoe UI", 9f)
        };
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(52, 73, 94);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.Font      = new Font("Segoe UI", 9f, FontStyle.Bold);
        grid.EnableHeadersVisualStyles               = false;

        // Handle DataError events to prevent displaying errors for binary/unsupported columns
        grid.DataError += (sender, e) =>
        {
            // Suppress the error dialog and continue
            e.ThrowException = false;
        };

        return grid;
    }
}
