using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace AttendanceSystem.Application.Services;

/// <summary>
/// Captures what a record looked like, and what changed about it, for the audit trail.
///
/// Two decisions shape this:
///
/// It records the <em>difference</em>, not the whole row. "admin updated Employee #7" is
/// useless, but so is a 30-field dump where the reader has to spot which one moved. An update
/// stores only the fields that actually changed, so the audit screen can say
/// "BasicSalary: 45000 → 52000" and nothing else.
///
/// It refuses to record secrets. Password hashes and the remember-me and reset tokens are the
/// kind of thing that must never end up in a table people are encouraged to read. The denylist
/// matches on name so a field added later called "…Token" or "…Hash" is excluded by default —
/// safer to omit something harmless than to leak something that is not.
/// </summary>
public static class AuditSnapshot
{
    /// <summary>
    /// Never recorded. Matched case-insensitively as substrings, so PasswordHash,
    /// RememberTokenHash and PasswordResetTokenHash are all covered by two entries.
    /// </summary>
    private static readonly string[] SecretMarkers =
    [
        "password", "token", "hash", "secret", "apikey", "commkey"
    ];

    /// <summary>
    /// Bookkeeping columns. They change on every write by definition, so including them would
    /// mean every diff lists ModifiedAt and buries the field somebody actually cares about.
    /// </summary>
    private static readonly HashSet<string> Bookkeeping = new(StringComparer.OrdinalIgnoreCase)
    {
        "CreatedAt", "CreatedBy", "ModifiedAt", "ModifiedBy"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,

        // The default encoder escapes +, &, < and non-ASCII to \uXXXX, which is right for HTML
        // but turns a phone number into "+94 77...". These values are read straight out of
        // the table with SQL as often as through the screen, and the screen escapes on output
        // anyway, so store them legibly.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// A flat snapshot of an entity's scalar properties.
    ///
    /// Navigation properties and collections are skipped: they are usually not loaded, and where
    /// they are, serialising them drags half the object graph into the audit table.
    /// </summary>
    public static Dictionary<string, object?> Capture(object? entity)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (entity == null) return result;

        foreach (var prop in entity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead || prop.GetIndexParameters().Length > 0) continue;
            if (Bookkeeping.Contains(prop.Name)) continue;
            if (IsSecret(prop.Name)) continue;
            if (!IsScalar(prop.PropertyType)) continue;

            object? value;
            try { value = prop.GetValue(entity); }
            catch { continue; } // a computed property that throws must not break the write

            result[prop.Name] = Normalise(value);
        }

        return result;
    }

    /// <summary>
    /// The fields that differ, as two aligned JSON objects — same keys, before and after values.
    /// Returns nulls when nothing changed, so an update that altered nothing records no diff
    /// rather than an empty pair of braces.
    /// </summary>
    public static (string? OldValues, string? NewValues) Diff(
        Dictionary<string, object?> before, Dictionary<string, object?> after)
    {
        var oldChanged = new Dictionary<string, object?>();
        var newChanged = new Dictionary<string, object?>();

        foreach (var key in before.Keys.Union(after.Keys, StringComparer.OrdinalIgnoreCase))
        {
            before.TryGetValue(key, out var oldValue);
            after.TryGetValue(key, out var newValue);

            if (Equals(oldValue, newValue)) continue;

            oldChanged[key] = oldValue;
            newChanged[key] = newValue;
        }

        return oldChanged.Count == 0
            ? (null, null)
            : (Serialise(oldChanged), Serialise(newChanged));
    }

    /// <summary>Convenience for the common case: snapshot taken before, entity after.</summary>
    public static (string? OldValues, string? NewValues) DiffAgainst(
        Dictionary<string, object?> before, object? after) =>
        Diff(before, Capture(after));

    /// <summary>
    /// The whole (filtered) record as a single JSON object — for creates and deletes, where
    /// there is no "other side" to compare against.
    /// </summary>
    public static string? Snapshot(object? entity)
    {
        var captured = Capture(entity);
        return captured.Count == 0 ? null : Serialise(captured);
    }

    private static string Serialise(Dictionary<string, object?> values) =>
        JsonSerializer.Serialize(values, JsonOptions);

    private static bool IsSecret(string name) =>
        SecretMarkers.Any(m => name.Contains(m, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Scalars only. Strings are IEnumerable, so they are allowed explicitly before the
    /// collection test rejects everything else that enumerates.
    /// </summary>
    private static bool IsScalar(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;

        if (t == typeof(string)) return true;
        if (t.IsEnum) return true;
        if (t.IsPrimitive) return true;

        return t == typeof(decimal) || t == typeof(DateTime) || t == typeof(DateTimeOffset)
            || t == typeof(TimeSpan) || t == typeof(Guid)
            || (!typeof(IEnumerable).IsAssignableFrom(t) && t.IsValueType);
    }

    /// <summary>
    /// Values are normalised so the comparison is on meaning rather than representation —
    /// otherwise a TimeSpan and its string form, or two equal DateTimes with different Kind,
    /// would read as a change.
    /// </summary>
    private static object? Normalise(object? value) => value switch
    {
        null => null,
        DateTime d => d.ToString("yyyy-MM-dd HH:mm:ss"),
        DateTimeOffset d => d.ToString("yyyy-MM-dd HH:mm:ss"),
        TimeSpan t => t.ToString(@"hh\:mm\:ss"),
        Enum e => e.ToString(),
        decimal m => m.ToString("0.##"),
        double d => d.ToString("0.##"),
        float f => f.ToString("0.##"),
        _ => value
    };
}
