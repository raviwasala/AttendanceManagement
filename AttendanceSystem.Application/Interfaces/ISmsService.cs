using AttendanceSystem.Common.Models;

namespace AttendanceSystem.Application.Interfaces;

/// <summary>Outgoing SMS through the configured HTTP gateway.</summary>
public interface ISmsService
{
    /// <summary>
    /// Sends one message, reporting what actually happened.
    ///
    /// Failures are returned rather than swallowed. SMS costs money per message and is used
    /// for things people are waiting on, so silent failure is worse here than an error.
    /// </summary>
    Task<Result> SendAsync(string toNumber, string message);

    /// <summary>Sends a trial message so the gateway can be proven before anyone relies on it.</summary>
    Task<Result> SendTestSmsAsync(string toNumber);
}
