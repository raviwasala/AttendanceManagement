namespace AttendanceSystem.Common.Helpers;

/// <summary>String utility extensions.</summary>
public static class StringHelper
{
    public static bool IsNullOrEmpty(this string? s) => string.IsNullOrWhiteSpace(s);

    public static string Truncate(this string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "…";

    public static string ToTitleCase(this string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.ToLower());
    }

    public static string GenerateEmployeeCode(int number) =>
        $"EMP-{number:D5}";
}
