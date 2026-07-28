using System.ComponentModel;
using AttendanceManagementSystem.UI.Theme;

namespace AttendanceManagementSystem.UI.Controls;

/// <summary>Styled flat button used throughout the application.</summary>
public class AppButton : Button
{
    private bool _hovered;
    private Color _baseColor;
    private Color _hoverColor;

    public AppButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        Font = AppTheme.ButtonFont;
        ForeColor = Color.White;
        SetColors(AppTheme.PrimaryColor);
        Height = 36;
        Padding = new Padding(8, 0, 8, 0);
    }

    public void SetColors(Color baseColor)
    {
        _baseColor = baseColor;
        _hoverColor = ControlPaint.Dark(baseColor, 0.1f);
        BackColor = baseColor;
    }

    public void SetDanger()   => SetColors(AppTheme.DangerColor);
    public void SetSuccess()  => SetColors(AppTheme.SuccessColor);
    public void SetWarning()  => SetColors(AppTheme.WarningColor);
    public void SetSecondary() { SetColors(Color.FromArgb(97, 97, 97)); }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true; BackColor = _hoverColor; base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; BackColor = _baseColor; base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // Draw rounded corners
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var pen = new Pen(BackColor);
        e.Graphics.DrawRectangle(pen, rect);
    }
}

/// <summary>Card panel with shadow effect for dashboard tiles.</summary>
public class CardPanel : Panel
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string CardTitle { get; set; } = string.Empty;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string CardValue { get; set; } = string.Empty;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color AccentColor { get; set; } = AppTheme.PrimaryColor;

    public CardPanel()
    {
        DoubleBuffered = true;
        BackColor = AppTheme.CardBg;
        Size = new Size(200, 110);
        Padding = new Padding(12);
        Cursor = Cursors.Default;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // Accent bar left
        using var accentBrush = new SolidBrush(AccentColor);
        g.FillRectangle(accentBrush, 0, 0, 5, Height);

        // Title
        using var titleFont = new Font("Segoe UI", 9f);
        using var titleBrush = new SolidBrush(AppTheme.SubText);
        g.DrawString(CardTitle, titleFont, titleBrush, new PointF(14, 14));

        // Value
        using var valueFont = new Font("Segoe UI", 22f, FontStyle.Bold);
        using var valueBrush = new SolidBrush(AppTheme.BodyText);
        g.DrawString(CardValue, valueFont, valueBrush, new PointF(14, 36));

        // Border
        using var borderPen = new Pen(AppTheme.BorderColor);
        g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
    }
}

/// <summary>Styled DataGridView with alternating rows and themed header.</summary>
public class AppDataGrid : DataGridView
{
    public AppDataGrid()
    {
        DoubleBuffered = true;
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        MultiSelect = false;
        ReadOnly = true;
        AllowUserToAddRows = false;
        AllowUserToDeleteRows = false;
        BorderStyle = BorderStyle.None;
        CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        GridColor = AppTheme.BorderColor;
        RowHeadersVisible = false;
        BackgroundColor = AppTheme.CardBg;
        Font = AppTheme.BodyFont;

        ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = AppTheme.GridHeaderBg,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            SelectionBackColor = AppTheme.GridHeaderBg,
            Padding = new Padding(4)
        };

        DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = AppTheme.CardBg,
            ForeColor = AppTheme.BodyText,
            SelectionBackColor = AppTheme.PrimaryColor,
            SelectionForeColor = Color.White,
            Padding = new Padding(2, 4, 2, 4)
        };

        AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = AppTheme.GridAltRow,
            ForeColor = AppTheme.BodyText,
            SelectionBackColor = AppTheme.PrimaryColor,
            SelectionForeColor = Color.White
        };

        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        ColumnHeadersHeight = 38;
        RowTemplate.Height = 34;
    }
}

/// <summary>Styled TextBox with label and validation error support.</summary>
public class LabeledTextBox : UserControl
{
    private readonly Label _label;
    private readonly TextBox _textBox;
    private readonly Label _errorLabel;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string LabelText { get => _label.Text; set => _label.Text = value; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Value { get => _textBox.Text; set => _textBox.Text = value; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsPassword { set => _textBox.UseSystemPasswordChar = value; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Placeholder { get; set; } = string.Empty;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new string Text { get => _textBox.Text; set => _textBox.Text = value; }
    public TextBox InnerTextBox => _textBox;

    public LabeledTextBox()
    {
        _label = new Label { AutoSize = true, Font = AppTheme.SmallFont, ForeColor = AppTheme.SubText };
        _textBox = new TextBox
        {
            Font = AppTheme.BodyFont, BorderStyle = BorderStyle.FixedSingle,
            BackColor = AppTheme.CardBg, ForeColor = AppTheme.BodyText, Height = 30
        };
        _errorLabel = new Label
        {
            AutoSize = true, Font = AppTheme.SmallFont,
            ForeColor = AppTheme.DangerColor, Visible = false
        };

        Height = 72;
        AutoSize = false;
        BackColor = Color.Transparent;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, BackColor = Color.Transparent };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(_label, 0, 0);
        layout.Controls.Add(_textBox, 0, 1);
        layout.Controls.Add(_errorLabel, 0, 2);
        _textBox.Dock = DockStyle.Fill;
        Controls.Add(layout);
    }

    public void ShowError(string message) { _errorLabel.Text = message; _errorLabel.Visible = true; _textBox.BackColor = Color.FromArgb(255, 235, 238); }
    public void ClearError() { _errorLabel.Visible = false; _textBox.BackColor = AppTheme.CardBg; }
}

/// <summary>Search bar panel with TextBox and search icon label.</summary>
public class SearchBar : Panel
{
    private readonly TextBox _txtSearch;
    public event EventHandler? SearchChanged;
    public string SearchText => _txtSearch.Text;

    public SearchBar()
    {
        Height = 38; BackColor = AppTheme.CardBg;
        BorderStyle = BorderStyle.FixedSingle;

        var icon = new Label { Text = "🔍", Width = 30, Dock = DockStyle.Left, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 11) };
        _txtSearch = new TextBox
        {
            Dock = DockStyle.Fill, BorderStyle = BorderStyle.None,
            Font = AppTheme.BodyFont, BackColor = AppTheme.CardBg,
            ForeColor = AppTheme.BodyText, PlaceholderText = "Search..."
        };
        _txtSearch.TextChanged += (s, e) => SearchChanged?.Invoke(this, e);
        Controls.Add(_txtSearch);
        Controls.Add(icon);
    }
}
