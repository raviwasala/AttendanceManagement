namespace AttendanceManagementSystem.UI.Theme;

/// <summary>Centralised colour and font palette for Light / Dark themes.</summary>
public static class AppTheme
{
    public static bool IsDark { get; private set; } = false;

    // ── Colours ───────────────────────────────────────────────────────────────
    public static Color PrimaryColor      => IsDark ? Color.FromArgb(33, 150, 243)  : Color.FromArgb(25, 118, 210);
    public static Color SidebarBg         => IsDark ? Color.FromArgb(30, 30, 30)    : Color.FromArgb(30, 60, 114);
    public static Color SidebarText       => Color.White;
    public static Color SidebarHover      => IsDark ? Color.FromArgb(60, 60, 60)    : Color.FromArgb(21, 101, 192);
    public static Color SidebarActive     => IsDark ? Color.FromArgb(33, 150, 243)  : Color.FromArgb(13, 71, 161);
    public static Color FormBg            => IsDark ? Color.FromArgb(45, 45, 48)    : Color.FromArgb(245, 247, 250);
    public static Color CardBg            => IsDark ? Color.FromArgb(55, 55, 58)    : Color.White;
    public static Color BodyText          => IsDark ? Color.FromArgb(220, 220, 220) : Color.FromArgb(33, 33, 33);
    public static Color SubText           => IsDark ? Color.FromArgb(160, 160, 160) : Color.FromArgb(117, 117, 117);
    public static Color BorderColor       => IsDark ? Color.FromArgb(70, 70, 70)    : Color.FromArgb(220, 220, 220);
    public static Color GridHeaderBg      => IsDark ? Color.FromArgb(50, 50, 53)    : Color.FromArgb(25, 118, 210);
    public static Color GridAltRow        => IsDark ? Color.FromArgb(40, 40, 43)    : Color.FromArgb(232, 240, 254);
    public static Color SuccessColor      => Color.FromArgb(56, 142, 60);
    public static Color DangerColor       => Color.FromArgb(211, 47, 47);
    public static Color WarningColor      => Color.FromArgb(245, 124, 0);
    public static Color InfoColor         => Color.FromArgb(2, 136, 209);
    public static Color StatusBarBg       => IsDark ? Color.FromArgb(37, 37, 38)    : Color.FromArgb(25, 118, 210);

    // ── Fonts ─────────────────────────────────────────────────────────────────
    public static Font TitleFont    => new("Segoe UI", 13f, FontStyle.Bold);
    public static Font HeaderFont   => new("Segoe UI", 11f, FontStyle.Bold);
    public static Font BodyFont     => new("Segoe UI", 9.5f);
    public static Font SmallFont    => new("Segoe UI", 8.5f);
    public static Font ButtonFont   => new("Segoe UI", 9.5f, FontStyle.Bold);
    public static Font SidebarFont  => new("Segoe UI", 10f);

    public static void ToggleTheme() { IsDark = !IsDark; }
    public static void SetDark()     { IsDark = true; }
    public static void SetLight()    { IsDark = false; }
}
