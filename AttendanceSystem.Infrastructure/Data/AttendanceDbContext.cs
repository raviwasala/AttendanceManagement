using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Infrastructure.Data;

/// <summary>Entity Framework Core database context for the Attendance Management System.</summary>
public class AttendanceDbContext : DbContext
{
    public AttendanceDbContext(DbContextOptions<AttendanceDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Designation> Designations => Set<Designation>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<EmployeeShift> EmployeeShifts => Set<EmployeeShift>();
    public DbSet<AttendanceLog> AttendanceLogs => Set<AttendanceLog>();
    public DbSet<AttendanceSummary> AttendanceSummaries => Set<AttendanceSummary>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<CompanySettings> CompanySettings => Set<CompanySettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Role ──────────────────────────────────────────────────────────────
        modelBuilder.Entity<Role>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
            e.HasIndex(x => x.Name).IsUnique();
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── Permission ────────────────────────────────────────────────────────
        modelBuilder.Entity<Permission>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Module).IsRequired().HasMaxLength(100);
            e.Property(x => x.Action).IsRequired().HasMaxLength(100);
            e.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
            e.HasIndex(x => new { x.Module, x.Action }).IsUnique();
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── RolePermission ────────────────────────────────────────────────────
        modelBuilder.Entity<RolePermission>(e =>
        {
            e.HasKey(x => new { x.RoleId, x.PermissionId });
            e.HasOne(x => x.Role).WithMany(x => x.RolePermissions).HasForeignKey(x => x.RoleId);
            e.HasOne(x => x.Permission).WithMany(x => x.RolePermissions).HasForeignKey(x => x.PermissionId);
        });

        // ── User ──────────────────────────────────────────────────────────────
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).IsRequired().HasMaxLength(100);
            e.Property(x => x.PasswordHash).IsRequired().HasMaxLength(500);
            e.Property(x => x.Email).IsRequired().HasMaxLength(200);
            e.Property(x => x.FullName).IsRequired().HasMaxLength(200);
            e.HasIndex(x => x.Username).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
            e.HasOne(x => x.Role).WithMany(x => x.Users).HasForeignKey(x => x.RoleId);
            e.HasOne(x => x.Employee).WithOne(x => x.User).HasForeignKey<User>(x => x.EmployeeId);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── Branch ────────────────────────────────────────────────────────────
        modelBuilder.Entity<Branch>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── Department ────────────────────────────────────────────────────────
        modelBuilder.Entity<Department>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.HasIndex(x => x.Name).IsUnique();
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── Designation ───────────────────────────────────────────────────────
        modelBuilder.Entity<Designation>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.HasIndex(x => x.Name).IsUnique();
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── Employee ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Employee>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.EmployeeCode).IsRequired().HasMaxLength(50);
            e.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
            e.Property(x => x.LastName).IsRequired().HasMaxLength(100);
            e.Ignore(x => x.FullName);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Phone).HasMaxLength(20);
            e.Property(x => x.Gender).HasMaxLength(10);
            e.Property(x => x.Address).HasMaxLength(500);
            e.HasIndex(x => x.EmployeeCode).IsUnique();
            e.HasOne(x => x.Department).WithMany(x => x.Employees).HasForeignKey(x => x.DepartmentId);
            e.HasOne(x => x.Designation).WithMany(x => x.Employees).HasForeignKey(x => x.DesignationId);
            e.HasOne(x => x.Branch).WithMany(x => x.Employees).HasForeignKey(x => x.BranchId);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── Shift ─────────────────────────────────────────────────────────────
        modelBuilder.Entity<Shift>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
            e.Property(x => x.WeeklyOffDays).HasMaxLength(200);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── EmployeeShift ─────────────────────────────────────────────────────
        modelBuilder.Entity<EmployeeShift>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Employee).WithMany(x => x.EmployeeShifts).HasForeignKey(x => x.EmployeeId);
            e.HasOne(x => x.Shift).WithMany(x => x.EmployeeShifts).HasForeignKey(x => x.ShiftId);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── AttendanceLog ─────────────────────────────────────────────────────
        modelBuilder.Entity<AttendanceLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Remarks).HasMaxLength(500);
            e.Property(x => x.Status).HasConversion<int>();
            e.HasIndex(x => new { x.EmployeeId, x.AttendanceDate }).IsUnique();
            e.HasOne(x => x.Employee).WithMany(x => x.AttendanceLogs).HasForeignKey(x => x.EmployeeId);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── AttendanceSummary ─────────────────────────────────────────────────
        modelBuilder.Entity<AttendanceSummary>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.EmployeeId, x.Month, x.Year }).IsUnique();
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── LeaveType ─────────────────────────────────────────────────────────
        modelBuilder.Entity<LeaveType>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
            e.HasIndex(x => x.Name).IsUnique();
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── LeaveRequest ──────────────────────────────────────────────────────
        modelBuilder.Entity<LeaveRequest>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Reason).IsRequired().HasMaxLength(1000);
            e.Property(x => x.RejectionReason).HasMaxLength(500);
            e.Property(x => x.Status).HasConversion<int>();
            e.HasOne(x => x.Employee).WithMany(x => x.LeaveRequests).HasForeignKey(x => x.EmployeeId);
            e.HasOne(x => x.LeaveType).WithMany(x => x.LeaveRequests).HasForeignKey(x => x.LeaveTypeId);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── Holiday ───────────────────────────────────────────────────────────
        modelBuilder.Entity<Holiday>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.HolidayType).HasConversion<int>();
            e.HasIndex(x => x.HolidayDate);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── AuditLog ──────────────────────────────────────────────────────────
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Action).IsRequired().HasMaxLength(100);
            e.Property(x => x.Module).IsRequired().HasMaxLength(100);
            e.Property(x => x.EntityName).HasMaxLength(100);
            e.Property(x => x.IpAddress).HasMaxLength(50);
            e.HasOne(x => x.User).WithMany(x => x.AuditLogs).HasForeignKey(x => x.UserId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        // ── CompanySettings ───────────────────────────────────────────────────
        modelBuilder.Entity<CompanySettings>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.CompanyName).IsRequired().HasMaxLength(300);
            e.Property(x => x.WeekendDays).HasMaxLength(200);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        // Seed Roles
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = "Administrator", Description = "Full system access", IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1) },
            new Role { Id = 2, Name = "HR Manager", Description = "HR module access", IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1) },
            new Role { Id = 3, Name = "Employee", Description = "Self-service access", IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1) }
        );

        // Seed default Admin user (password: Admin@123)
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1, Username = "admin", FullName = "System Administrator",
                Email = "admin@company.com",
                PasswordHash = "$2a$12$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi", // Admin@123
                RoleId = 1, IsActive = true, IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1)
            }
        );

        // Seed default Branch
        modelBuilder.Entity<Branch>().HasData(
            new Branch { Id = 1, Name = "Head Office", IsActive = true, IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1) }
        );

        // Seed default Department
        modelBuilder.Entity<Department>().HasData(
            new Department { Id = 1, Name = "Administration", IsActive = true, IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1) },
            new Department { Id = 2, Name = "Information Technology", IsActive = true, IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1) },
            new Department { Id = 3, Name = "Human Resources", IsActive = true, IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1) },
            new Department { Id = 4, Name = "Finance", IsActive = true, IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1) }
        );

        // Seed default Designation
        modelBuilder.Entity<Designation>().HasData(
            new Designation { Id = 1, Name = "Manager", IsActive = true, IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1) },
            new Designation { Id = 2, Name = "Software Engineer", IsActive = true, IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1) },
            new Designation { Id = 3, Name = "HR Executive", IsActive = true, IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1) },
            new Designation { Id = 4, Name = "Accountant", IsActive = true, IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1) }
        );

        // Seed default Shift
        modelBuilder.Entity<Shift>().HasData(
            new Shift
            {
                Id = 1, Name = "General Shift",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(18, 0, 0),
                GraceMinutes = 15,
                WeeklyOffDays = "Saturday,Sunday",
                IsActive = true, IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1)
            }
        );

        // Seed Leave Types
        modelBuilder.Entity<LeaveType>().HasData(
            new LeaveType { Id = 1, Name = "Annual Leave", TotalDays = 14, IsPaid = true, IsActive = true, IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1) },
            new LeaveType { Id = 2, Name = "Sick Leave", TotalDays = 10, IsPaid = true, IsActive = true, IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1) },
            new LeaveType { Id = 3, Name = "Casual Leave", TotalDays = 7, IsPaid = true, IsActive = true, IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1) },
            new LeaveType { Id = 4, Name = "Unpaid Leave", TotalDays = 30, IsPaid = false, IsActive = true, IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1) }
        );

        // Seed Company Settings
        modelBuilder.Entity<CompanySettings>().HasData(
            new CompanySettings
            {
                Id = 1, CompanyName = "My Company Ltd.",
                WorkStartTime = new TimeSpan(9, 0, 0),
                WorkEndTime = new TimeSpan(18, 0, 0),
                WeekendDays = "Saturday,Sunday",
                MaxLateMinutes = 15,
                IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1)
            }
        );
    }
}
