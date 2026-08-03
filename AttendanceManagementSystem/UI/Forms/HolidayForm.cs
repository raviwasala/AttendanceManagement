using AttendanceManagementSystem.UI.Controls;
using AttendanceManagementSystem.UI.Theme;
using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceManagementSystem.Session;
using AttendanceSystem.Domain.Enums;

namespace AttendanceManagementSystem.UI.Forms;

/// <summary>Holiday management — list, add, edit, delete, calendar view.</summary>
public class HolidayForm : Form
{
    private AppDataGrid _grid = null!;
    private ComboBox _cmbYear = null!;
    private MonthCalendar _calendar = null!;
    private List<HolidayDto> _data = new();

    private readonly IHolidayService _service;
    public HolidayForm(IHolidayService service) { _service = service; Build(); _ = LoadAsync(); }

    private void Build()
    {
        BackColor = AppTheme.FormBg;
        var left  = new Panel { Dock = DockStyle.Left, Width = 340, BackColor = AppTheme.FormBg, Padding = new Padding(8) };
        var right = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.FormBg };

        // Calendar
        _calendar = new MonthCalendar { Location = new Point(8, 8), MaxSelectionCount = 1, Font = AppTheme.BodyFont };
        _calendar.DateSelected += (s, e) => HighlightHoliday(e.Start);
        left.Controls.Add(_calendar);

        // Toolbar
        var toolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = AppTheme.CardBg, Padding = new Padding(8, 8, 8, 0) };
        var lblYear = new Label { Text = "Year:", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(8, 14), AutoSize = true };
        _cmbYear = new ComboBox { Location = new Point(42, 8), Width = 80, Font = AppTheme.BodyFont, DropDownStyle = ComboBoxStyle.DropDownList };
        for (int y = DateTime.Now.Year - 2; y <= DateTime.Now.Year + 2; y++) _cmbYear.Items.Add(y);
        _cmbYear.SelectedItem = DateTime.Now.Year;
        _cmbYear.SelectedIndexChanged += async (s, e) => await LoadAsync();

        var btnAdd    = new AppButton { Text = "➕ Add",    Width = 90, Location = new Point(132, 8) };
        var btnEdit   = new AppButton { Text = "✏ Edit",   Width = 90, Location = new Point(228, 8) };
        var btnDelete = new AppButton { Text = "🗑 Delete", Width = 90, Location = new Point(324, 8) };
        btnEdit.SetSecondary(); btnDelete.SetDanger();
        btnAdd.Click    += async (s, e) => await OpenDialog(null);
        btnEdit.Click   += async (s, e) => await EditSelected();
        btnDelete.Click += async (s, e) => await DeleteSelected();
        toolbar.Controls.AddRange([lblYear, _cmbYear, btnAdd, btnEdit, btnDelete]);

        _grid = new AppDataGrid { Dock = DockStyle.Fill };
        _grid.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "Date",        DataPropertyName = "DateDisplay",       Width = 110 },
            new DataGridViewTextBoxColumn { HeaderText = "Day",         DataPropertyName = "DayName",           Width = 90  },
            new DataGridViewTextBoxColumn { HeaderText = "Holiday",     DataPropertyName = "Name"                           },
            new DataGridViewTextBoxColumn { HeaderText = "Type",        DataPropertyName = "HolidayTypeDisplay",Width = 90  },
            new DataGridViewCheckBoxColumn { HeaderText = "Recurring",  DataPropertyName = "IsRecurring",        Width = 80  },
            new DataGridViewTextBoxColumn { HeaderText = "Description", DataPropertyName = "Description"                    }
        );
        right.Controls.Add(_grid); right.Controls.Add(toolbar);
        Controls.Add(right); Controls.Add(left);
    }

    private async Task LoadAsync()
    {
        int year = _cmbYear.SelectedItem is int y ? y : DateTime.Now.Year;
        var r = await _service.GetByYearAsync(year);
        if (!r.IsSuccess) return;
        _data = r.Data!.ToList();
        _grid.DataSource = null; _grid.DataSource = _data;

        // Mark holidays on calendar
        _calendar.BoldedDates = _data.Select(h => h.HolidayDate).ToArray();
        _calendar.UpdateBoldedDates();
    }

    private void HighlightHoliday(DateTime date)
    {
        var h = _data.FirstOrDefault(x => x.HolidayDate.Date == date.Date);
        if (h != null) MessageBox.Show($"{h.Name}\n{h.HolidayTypeDisplay}", "Holiday", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task OpenDialog(HolidayDto? existing)
    {
        using var dlg = new HolidayEditDialog(existing);
        if (dlg.ShowDialog() != DialogResult.OK) return;
        var r = await _service.SaveAsync(dlg.GetDto());
        if (r.IsSuccess) await LoadAsync();
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private async Task EditSelected()
    {
        if (_grid.SelectedRows.Count == 0) return;
        await OpenDialog((HolidayDto)_grid.SelectedRows[0].DataBoundItem);
    }

    private async Task DeleteSelected()
    {
        if (_grid.SelectedRows.Count == 0) return;
        var h = (HolidayDto)_grid.SelectedRows[0].DataBoundItem;
        if (MessageBox.Show($"Delete '{h.Name}'?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        var r = await _service.DeleteAsync(h.Id, DesktopSession.UserId);
        if (r.IsSuccess) await LoadAsync();
        else MessageBox.Show(r.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

internal class HolidayEditDialog : Form
{
    private readonly LabeledTextBox _txtName;
    private readonly DateTimePicker _dtpDate;
    private readonly ComboBox _cmbType;
    private readonly LabeledTextBox _txtDesc;
    private readonly CheckBox _chkRecurring;
    private readonly HolidayDto? _existing;

    public HolidayEditDialog(HolidayDto? existing)
    {
        _existing = existing;
        Text = existing == null ? "Add Holiday" : "Edit Holiday";
        Size = new Size(400, 360); StartPosition = FormStartPosition.CenterParent;
        BackColor = AppTheme.CardBg; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;

        _txtName = new LabeledTextBox { LabelText = "Holiday Name *", Location = new Point(20, 20), Width = 340 };
        var lblDate = new Label { Text = "Date *", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(20, 100), AutoSize = true };
        _dtpDate = new DateTimePicker { Location = new Point(20, 118), Width = 180, Format = DateTimePickerFormat.Short, Font = AppTheme.BodyFont };

        var lblType = new Label { Text = "Type", Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText, Location = new Point(20, 154), AutoSize = true };
        _cmbType = new ComboBox { Location = new Point(20, 172), Width = 160, Font = AppTheme.BodyFont, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var t in Enum.GetValues<HolidayType>()) _cmbType.Items.Add(t);
        _cmbType.SelectedIndex = 0;

        _txtDesc = new LabeledTextBox { LabelText = "Description", Location = new Point(20, 204), Width = 340 };
        _chkRecurring = new CheckBox { Text = "Recurring (every year)", Location = new Point(20, 282), Font = AppTheme.BodyFont, ForeColor = AppTheme.BodyText };

        var btnSave   = new AppButton { Text = "💾 Save", Width = 100, Location = new Point(120, 310) };
        var btnCancel = new AppButton { Text = "Cancel",  Width = 80,  Location = new Point(230, 310) };
        btnCancel.SetSecondary();
        btnSave.Click   += (s, e) => { if (!string.IsNullOrWhiteSpace(_txtName.Value)) DialogResult = DialogResult.OK; else _txtName.ShowError("Required"); };
        btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;

        if (existing != null)
        {
            _txtName.Value = existing.Name; _dtpDate.Value = existing.HolidayDate;
            _cmbType.SelectedItem = existing.HolidayType; _txtDesc.Value = existing.Description ?? "";
            _chkRecurring.Checked = existing.IsRecurring;
        }
        Controls.AddRange([_txtName, lblDate, _dtpDate, lblType, _cmbType, _txtDesc, _chkRecurring, btnSave, btnCancel]);
    }

    public SaveHolidayDto GetDto() => new()
    {
        Id = _existing?.Id ?? 0, Name = _txtName.Value.Trim(), HolidayDate = _dtpDate.Value,
        HolidayType = (HolidayType)_cmbType.SelectedItem!, Description = _txtDesc.Value.Trim(),
        IsRecurring = _chkRecurring.Checked
    };
}
