using AttendanceSystem.Application.Services;
using Xunit;

namespace AttendanceSystem.Tests;

/// <summary>
/// DataScope decides who can read whose records — including what every named employee is
/// paid. It is the difference between a manager seeing their own department and a manager
/// seeing the whole company's salaries, and nothing about that failure is visible: the
/// screen simply shows more rows than it should, and looks entirely normal doing it.
///
/// These tests are written around the three shapes the system actually issues:
/// company-wide (administrator), department (manager), and self-only (employee).
/// </summary>
public class DataScopeTests
{
    private static DataScope CompanyWide(int? ownEmployeeId = 1) =>
        new(true, new HashSet<int>(), ownEmployeeId);

    private static DataScope Departments(int ownEmployeeId, params int[] departmentIds) =>
        new(false, new HashSet<int>(departmentIds), ownEmployeeId);

    private static DataScope SelfOnly(int ownEmployeeId) =>
        new(false, new HashSet<int>(), ownEmployeeId);

    // ── Company-wide ──────────────────────────────────────────────────────────

    [Fact]
    public void Company_wide_reads_everybody()
    {
        var scope = CompanyWide();

        Assert.True(scope.Allows(employeeId: 1, departmentId: 1));
        Assert.True(scope.Allows(employeeId: 9999, departmentId: 77));
        Assert.True(scope.IsCompanyWide);
        Assert.False(scope.IsSelfOnly);
    }

    [Fact]
    public void Company_wide_applies_no_filters()
    {
        var scope = CompanyWide();

        // Null means "do not restrict". A filter here would silently narrow an
        // administrator's reports.
        Assert.Null(scope.DepartmentFilter);
        Assert.Null(scope.EmployeeFilter);
    }

    // ── Department ────────────────────────────────────────────────────────────

    [Fact]
    public void A_department_scope_reads_its_own_departments_only()
    {
        var scope = Departments(ownEmployeeId: 50, departmentIds: [3, 7]);

        Assert.True(scope.Allows(employeeId: 100, departmentId: 3));
        Assert.True(scope.Allows(employeeId: 101, departmentId: 7));
        Assert.False(scope.Allows(employeeId: 102, departmentId: 4));
    }

    [Fact]
    public void A_manager_can_always_read_their_own_record()
    {
        // Managers are frequently not in a department they manage. Without this a manager
        // could not open their own payslip.
        var scope = Departments(ownEmployeeId: 50, departmentIds: [3]);

        Assert.True(scope.Allows(employeeId: 50, departmentId: 99));
    }

    [Fact]
    public void A_department_scope_filters_by_department_and_not_by_employee()
    {
        var scope = Departments(ownEmployeeId: 50, departmentIds: [3, 7]);

        Assert.Equal([3, 7], scope.DepartmentFilter!.OrderBy(x => x));
        Assert.Null(scope.EmployeeFilter);
        Assert.False(scope.IsSelfOnly);
    }

    // ── Self only ─────────────────────────────────────────────────────────────

    [Fact]
    public void Self_only_reads_nobody_else()
    {
        var scope = SelfOnly(ownEmployeeId: 42);

        Assert.True(scope.Allows(employeeId: 42, departmentId: 5));
        Assert.False(scope.Allows(employeeId: 43, departmentId: 5));
        // Not even a colleague in the same department.
        Assert.False(scope.Allows(employeeId: 44, departmentId: 5));
        Assert.True(scope.IsSelfOnly);
    }

    [Fact]
    public void Self_only_filters_on_the_employee_id()
    {
        var scope = SelfOnly(ownEmployeeId: 42);

        Assert.Null(scope.DepartmentFilter);
        Assert.Equal(42, scope.EmployeeFilter);
    }

    // ── Failing closed ────────────────────────────────────────────────────────
    //
    // The cases below are the ones that matter. Every other test here says "the right
    // people can see things"; these say "when something is missing, nobody can" — and
    // getting that backwards exposes every salary in the company.

    [Fact]
    public void Nothing_reads_nothing()
    {
        var scope = DataScope.Nothing;

        Assert.False(scope.Allows(employeeId: 1, departmentId: 1));
        Assert.False(scope.Allows(employeeId: 999, departmentId: 999));
        Assert.False(scope.IsCompanyWide);
    }

    [Fact]
    public void Nothing_filters_to_an_id_that_cannot_exist()
    {
        // -1 rather than null. A null employee filter reads as "no restriction" downstream,
        // so a missing session would return every row instead of none — failing open, which
        // is the one thing a scope must never do.
        var scope = DataScope.Nothing;

        Assert.Equal(-1, scope.EmployeeFilter);
    }

    [Fact]
    public void A_scope_with_no_departments_and_no_employee_reads_nothing()
    {
        // A manager whose departments were all removed, or a user with no employee link.
        // This must collapse to self-only-with-nobody, not to company-wide.
        var scope = new DataScope(false, new HashSet<int>(), ownEmployeeId: null);

        Assert.False(scope.Allows(employeeId: 1, departmentId: 1));
        Assert.True(scope.IsSelfOnly);
        Assert.Equal(-1, scope.EmployeeFilter);
    }

    [Fact]
    public void An_empty_department_set_never_becomes_an_unfiltered_query()
    {
        // DepartmentFilter returns null for an empty set, which downstream means "no
        // department restriction". That is only safe because EmployeeFilter takes over —
        // this test holds the two together, since changing either alone opens the gate.
        var scope = new DataScope(false, new HashSet<int>(), ownEmployeeId: 7);

        Assert.Null(scope.DepartmentFilter);
        Assert.NotNull(scope.EmployeeFilter);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-5)]
    public void A_department_scope_never_admits_a_department_it_was_not_given(int departmentId)
    {
        var scope = Departments(ownEmployeeId: 50, departmentIds: [3]);
        Assert.False(scope.Allows(employeeId: 900, departmentId: departmentId));
    }
}
