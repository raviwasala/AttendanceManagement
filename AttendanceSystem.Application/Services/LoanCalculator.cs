using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Application.Services;

/// <summary>
/// The arithmetic of a staff loan — interest and the monthly instalment.
///
/// Pure and static, like <see cref="AttendanceCalculator"/>: no database, no clock. A loan
/// schedule is the kind of thing that has to be checkable against a hand calculation, and a
/// function with no dependencies can be checked without one.
/// </summary>
public static class LoanCalculator
{
    /// <summary>What a loan costs and what is repaid each month.</summary>
    public sealed class Schedule
    {
        /// <summary>Total interest over the life of the loan.</summary>
        public decimal InterestAmount { get; init; }

        /// <summary>Principal plus interest — what the borrower repays in total.</summary>
        public decimal TotalPayable { get; init; }

        /// <summary>The regular monthly instalment.</summary>
        public decimal MonthlyInstallment { get; init; }

        /// <summary>
        /// The final instalment, which absorbs the rounding.
        ///
        /// Without this the instalments would not sum to the total: rounding twelve payments
        /// to the cent leaves a few cents unaccounted for, and a loan that never quite clears
        /// is worse than an uneven last payment.
        /// </summary>
        public decimal FinalInstallment { get; init; }
    }

    /// <summary>
    /// Works out the interest and instalments for a loan.
    /// </summary>
    /// <param name="principal">Amount lent.</param>
    /// <param name="annualRatePercent">Annual interest rate. Zero is normal for a staff loan.</param>
    /// <param name="months">Number of instalments.</param>
    /// <param name="interestType">Flat or reducing balance.</param>
    public static Schedule Calculate(decimal principal, decimal annualRatePercent,
                                     int months, LoanInterestType interestType)
    {
        if (principal <= 0 || months <= 0)
            return new Schedule();

        // Interest-free is the common case for staff loans, and short-circuiting it avoids
        // the reducing-balance formula dividing by a zero rate.
        if (annualRatePercent <= 0)
            return Build(principal, 0m, months);

        if (interestType == LoanInterestType.Fixed)
        {
            // Flat: charged on the whole principal for the full term, regardless of what has
            // been repaid. Simple, and the dearer of the two for the borrower.
            var years = months / 12m;
            var interest = principal * (annualRatePercent / 100m) * years;
            return Build(principal, interest, months);
        }

        // Reducing balance: the standard amortisation formula. Interest each month is charged
        // on what is still outstanding, so the instalment is level but its interest share falls.
        //
        //   instalment = P × r / (1 − (1 + r)^−n)
        //
        // Computed in double because decimal has no Pow; the result is rounded back to money
        // immediately, so the intermediate precision loss cannot reach a stored figure.
        var monthlyRate = (double)(annualRatePercent / 100m / 12m);
        var factor = Math.Pow(1 + monthlyRate, -months);
        var instalment = (double)principal * monthlyRate / (1 - factor);

        var totalPayable = Math.Round((decimal)instalment * months, 2);
        var totalInterest = Math.Round(totalPayable - principal, 2);

        return Build(principal, totalInterest, months);
    }

    /// <summary>
    /// Splits a total into equal instalments, with the last one absorbing the remainder so
    /// the payments always sum exactly to what is owed.
    /// </summary>
    private static Schedule Build(decimal principal, decimal interest, int months)
    {
        var total = Math.Round(principal + interest, 2);
        var monthly = Math.Round(total / months, 2);
        var final = Math.Round(total - (monthly * (months - 1)), 2);

        return new Schedule
        {
            InterestAmount = interest,
            TotalPayable = total,
            MonthlyInstallment = monthly,
            FinalInstallment = final
        };
    }

    /// <summary>
    /// Re-spreads what is still owed over a new number of instalments — used when a loan is
    /// restructured or partly settled early.
    ///
    /// Interest is not recomputed: it was fixed when the loan was granted, and recalculating
    /// it here would silently change the cost of a loan the borrower already agreed to.
    /// </summary>
    public static Schedule Reschedule(decimal outstanding, int remainingMonths)
    {
        if (outstanding <= 0 || remainingMonths <= 0) return new Schedule();

        var monthly = Math.Round(outstanding / remainingMonths, 2);
        var final = Math.Round(outstanding - (monthly * (remainingMonths - 1)), 2);

        return new Schedule
        {
            InterestAmount = 0m,
            TotalPayable = outstanding,
            MonthlyInstallment = monthly,
            FinalInstallment = final
        };
    }
}
