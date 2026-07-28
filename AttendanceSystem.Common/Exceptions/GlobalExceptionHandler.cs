using AttendanceSystem.Common.Logging;

namespace AttendanceSystem.Common.Exceptions;

/// <summary>Global exception handler for centralised error processing.</summary>
public static class GlobalExceptionHandler
{
    /// <summary>Logs an exception with optional context label.</summary>
    public static void Handle(Exception ex, string context = "") =>
        AppLogger.Error($"[{context}] Unhandled exception: {ex.Message}", ex);

    /// <summary>
    /// Logs a UI-originated exception. Showing a message box is the responsibility
    /// of the presentation layer — call this to get structured logging, then display
    /// the error to the user from your form/control code.
    /// </summary>
    public static void HandleUI(Exception ex, string formName = "") =>
        AppLogger.Error($"[UI:{formName}] {ex.Message}", ex);

    /// <summary>
    /// Wires up domain-level unhandled exception logging.
    /// WinForms ThreadException wiring must be done in the UI project's Program.cs
    /// so that System.Windows.Forms is available.
    /// </summary>
    public static void SetupUnhandledExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                AppLogger.Fatal("Unhandled domain exception", ex);
        };
    }
}
