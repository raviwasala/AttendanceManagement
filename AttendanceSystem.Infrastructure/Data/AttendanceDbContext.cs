using AttendanceSystem.Common.Constants;
using AttendanceSystem.Common.Session;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Infrastructure.Data;

/// <summary>Entity Framework Core database context for the Attendance Management System.</summary>
public class AttendanceDbContext : DbContext
{
    private readonly ICurrentUserContext _currentUser;

    /// <summary>
    /// Single constructor by design: EF Core cannot choose between overloads where one
    /// parameter list is a subset of the other. Callers with no signed-in user (design-time
    /// tooling, background work) pass <see cref="AnonymousUserContext.Instance"/>.
    /// </summary>
    public AttendanceDbContext(DbContextOptions<AttendanceDbContext> options, ICurrentUserContext currentUser)
        : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Role> Roles { get; set; } = null!;
    public DbSet<Permission> Permissions { get; set; } = null!;
    public DbSet<RolePermission> RolePermissions { get; set; } = null!;
    public DbSet<Employee> Employees { get; set; } = null!;
    public DbSet<Department> Departments { get; set; } = null!;
    public DbSet<Designation> Designations { get; set; } = null!;
    public DbSet<Branch> Branches { get; set; } = null!;
    public DbSet<Shift> Shifts { get; set; } = null!;
    public DbSet<EmployeeShift> EmployeeShifts { get; set; } = null!;
    public DbSet<AttendanceLog> AttendanceLogs { get; set; } = null!;
    public DbSet<AttendanceSummary> AttendanceSummaries { get; set; } = null!;
    public DbSet<LeaveType> LeaveTypes { get; set; } = null!;
    public DbSet<LeaveRequest> LeaveRequests { get; set; } = null!;
    public DbSet<Holiday> Holidays { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<CompanySettings> CompanySettings { get; set; } = null!;
    public DbSet<Device> Devices { get; set; } = null!;
    public DbSet<DevicePunch> DevicePunches { get; set; } = null!;
    public DbSet<DeviceUserMapping> DeviceUserMappings { get; set; } = null!;
    public DbSet<DeviceSyncLog> DeviceSyncLogs { get; set; } = null!;
    public DbSet<OvertimeRule> OvertimeRules { get; set; } = null!;
    public DbSet<OvertimeRecord> OvertimeRecords { get; set; } = null!;
    public DbSet<EmployeeHistory> EmployeeHistories { get; set; } = null!;
    public DbSet<EmployeeDocument> EmployeeDocuments { get; set; } = null!;
    public DbSet<AttendancePeriodLock> AttendancePeriodLocks { get; set; } = null!;
    public DbSet<DashboardPreference> DashboardPreferences { get; set; } = null!;
    public DbSet<UserDashboardTile> UserDashboardTiles { get; set; } = null!;

    // ── Auto-timestamp & Soft-Delete interception ─────────────────────────────
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditAndSoftDelete();
        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditAndSoftDelete();
        return base.SaveChanges();
    }

    private void ApplyAuditAndSoftDelete()
    {
        var now = DateTime.Now;

        // Null when nobody is signed in (background jobs, seeding, design-time). Previously this
        // fell back to user 1, which silently attributed system writes to the admin account.
        var currentUserId = _currentUser.UserId;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAt == default)
                    entry.Entity.CreatedAt = now;
                if (!entry.Entity.CreatedBy.HasValue || entry.Entity.CreatedBy == 0)
                    entry.Entity.CreatedBy = currentUserId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.ModifiedAt = now;
                entry.Entity.ModifiedBy = currentUserId;
            }
            else if (entry.State == EntityState.Deleted)
            {
                // Soft-delete interceptor
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.ModifiedAt = now;
                entry.Entity.ModifiedBy = currentUserId;
            }
        }
    }

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
            e.HasQueryFilter(rp => !rp.Role.IsDeleted && !rp.Permission.IsDeleted);
        });

        // ── User ──────────────────────────────────────────────────────────────
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).IsRequired().HasMaxLength(100);
            e.Property(x => x.PasswordHash).IsRequired().HasMaxLength(500);
            e.Property(x => x.Email).IsRequired().HasMaxLength(200);
            e.Property(x => x.FullName).IsRequired().HasMaxLength(200);
            e.Property(x => x.RememberTokenHash).HasMaxLength(100);
            e.Property(x => x.ResetPasswordTokenHash).HasMaxLength(100);
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
            e.Property(x => x.UserCode).HasMaxLength(50);
            e.Property(x => x.NameWithInitials).HasMaxLength(200);
            e.Property(x => x.Nic).HasMaxLength(20);
            e.Property(x => x.ResignationReason).HasMaxLength(500);
            e.HasIndex(x => x.EmployeeCode).IsUnique();

            // UserCode and NIC are indexed for lookup but NOT unique: the supplied data has the
            // same UserCode on many people and repeats an NIC across two records. Searchable,
            // not authoritative.
            e.HasIndex(x => x.UserCode);
            e.HasIndex(x => x.Nic);
            e.HasOne(x => x.Department).WithMany(x => x.Employees).HasForeignKey(x => x.DepartmentId);
            e.HasOne(x => x.Designation).WithMany(x => x.Employees).HasForeignKey(x => x.DesignationId);
            e.HasOne(x => x.Branch).WithMany(x => x.Employees).HasForeignKey(x => x.BranchId);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── EmployeeHistory ───────────────────────────────────────────────────
        modelBuilder.Entity<EmployeeHistory>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.Property(x => x.FromLabel).HasMaxLength(300);
            e.Property(x => x.ToLabel).HasMaxLength(300);

            // Restrict, not Cascade: history is the record of what happened, and deleting an
            // employee must not erase why they left. Employees are soft-deleted anyway.
            e.HasOne(x => x.Employee).WithMany(x => x.History)
             .HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);

            // The profile reads one employee's history newest-first; this is that query.
            e.HasIndex(x => new { x.EmployeeId, x.EffectiveDate });
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── EmployeeDocument ──────────────────────────────────────────────────
        modelBuilder.Entity<EmployeeDocument>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).IsRequired().HasMaxLength(200);
            e.Property(x => x.FileName).IsRequired().HasMaxLength(255);
            e.Property(x => x.ContentType).IsRequired().HasMaxLength(100);
            e.Property(x => x.Notes).HasMaxLength(500);

            e.HasOne(x => x.Employee).WithMany(x => x.Documents)
             .HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.EmployeeId);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── AttendancePeriodLock ──────────────────────────────────────────────
        modelBuilder.Entity<AttendancePeriodLock>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Reason).IsRequired().HasMaxLength(300);
            e.Property(x => x.UnlockReason).HasMaxLength(300);

            // Optional: a null BranchId locks every branch.
            e.HasOne(x => x.Branch).WithMany()
             .HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);

            // Every save and every imported punch asks "is this date locked?", so the range
            // is indexed rather than scanned.
            e.HasIndex(x => new { x.FromDate, x.ToDate });
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── DashboardPreference ───────────────────────────────────────────────
        modelBuilder.Entity<DashboardPreference>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.WidgetKey).IsRequired().HasMaxLength(50);

            // Null UserId is the company default, so the relationship is optional.
            e.HasOne(x => x.User).WithMany()
             .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

            // One row per user per widget. Unique so a double-save cannot leave two
            // contradictory rows and make the dashboard depend on which is read first.
            e.HasIndex(x => new { x.UserId, x.WidgetKey }).IsUnique();
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── UserDashboardTile ─────────────────────────────────────────────────
        modelBuilder.Entity<UserDashboardTile>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).IsRequired().HasMaxLength(60);
            e.Property(x => x.MetricKey).IsRequired().HasMaxLength(40);
            e.Property(x => x.Period).IsRequired().HasMaxLength(20);
            e.Property(x => x.Colour).IsRequired().HasMaxLength(30);

            // Cascade from the user: a personal tile has no meaning once its owner is gone.
            e.HasOne(x => x.User).WithMany()
             .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

            // Restrict on the lookups: deleting a department must not silently delete the
            // tiles that were watching it.
            e.HasOne(x => x.Department).WithMany()
             .HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Branch).WithMany()
             .HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.UserId);
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

        modelBuilder.Entity<Shift>(e =>
        {
            e.Property(x => x.ShiftCode).HasMaxLength(20);
            e.Property(x => x.StandardWorkingHours).HasPrecision(5, 2);
        });

        modelBuilder.Entity<AttendanceLog>(e =>
        {
            e.Property(x => x.GrossHours).HasPrecision(6, 2);
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

        // ── OvertimeRule ──────────────────────────────────────────────────────
        modelBuilder.Entity<OvertimeRule>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
            e.Property(x => x.Description).HasMaxLength(500);
            // decimal(5,2) covers 0.00–999.99: enough for any realistic multiplier and
            // precise enough that 1.5 and 2.25 are stored exactly.
            e.Property(x => x.RateMultiplier).HasColumnType("decimal(5,2)");
            e.HasIndex(x => x.Name).IsUnique();
            // Scope references use NoAction: deleting a department must not silently take
            // the overtime policy with it.
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId)
                .OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId)
                .OnDelete(DeleteBehavior.NoAction);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── OvertimeRecord ────────────────────────────────────────────────────
        modelBuilder.Entity<OvertimeRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.RuleName).HasMaxLength(100);
            e.Property(x => x.Remarks).HasMaxLength(500);
            e.Property(x => x.RejectionReason).HasMaxLength(500);
            e.Property(x => x.RateMultiplier).HasColumnType("decimal(5,2)");
            e.Ignore(x => x.WeightedHours);

            // One claim per employee per day. Regenerating a range then updates the existing
            // pending row instead of stacking duplicates on top of it.
            e.HasIndex(x => new { x.EmployeeId, x.OvertimeDate }).IsUnique()
                .HasFilter("[IsDeleted] = 0");
            e.HasIndex(x => new { x.OvertimeDate, x.Status });

            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.AttendanceLog).WithMany().HasForeignKey(x => x.AttendanceLogId)
                .OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId)
                .OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.OvertimeRule).WithMany().HasForeignKey(x => x.OvertimeRuleId)
                .OnDelete(DeleteBehavior.NoAction);
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

        // ── Device ────────────────────────────────────────────────────────────
        modelBuilder.Entity<Device>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.IpAddress).IsRequired().HasMaxLength(45);   // 45 = max IPv6 literal
            e.Property(x => x.SerialNumber).HasMaxLength(100);
            e.Property(x => x.Model).HasMaxLength(100);
            e.Property(x => x.LastError).HasMaxLength(1000);
            e.Property(x => x.Status).HasConversion<int>();
            // One terminal per endpoint — catches the same device being added twice.
            e.HasIndex(x => new { x.IpAddress, x.Port }).IsUnique();
            e.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── DevicePunch ───────────────────────────────────────────────────────
        modelBuilder.Entity<DevicePunch>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.DeviceUserId).IsRequired().HasMaxLength(50);

            // The duplicate guard. Re-downloading an overlapping window is safe because of
            // this index, which is what allows the watermark to be deliberately conservative.
            e.HasIndex(x => new { x.DeviceId, x.DeviceUserId, x.PunchTime }).IsUnique();

            // Drives the "process what hasn't been processed" query.
            e.HasIndex(x => new { x.IsProcessed, x.PunchTime });

            e.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            // No soft-delete filter: this is raw evidence and must never be hidden.
        });

        // ── DeviceUserMapping ─────────────────────────────────────────────────
        modelBuilder.Entity<DeviceUserMapping>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.DeviceUserId).IsRequired().HasMaxLength(50);
            e.Property(x => x.DeviceUserName).HasMaxLength(200);
            e.HasIndex(x => new { x.DeviceId, x.DeviceUserId }).IsUnique();
            e.HasOne(x => x.Device).WithMany(d => d.UserMappings).HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // ── DeviceSyncLog ─────────────────────────────────────────────────────
        modelBuilder.Entity<DeviceSyncLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ErrorMessage).HasMaxLength(2000);
            e.Property(x => x.Trigger).HasConversion<int>();
            e.Property(x => x.Outcome).HasConversion<int>();
            e.HasIndex(x => new { x.DeviceId, x.StartedAt });
            e.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        // Seed Roles
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = "Administrator", Description = "Full system access",  IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Role { Id = 2, Name = "HR Manager",    Description = "HR module access",    IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Role { Id = 3, Name = "Employee",      Description = "Self-service access", IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );

        // Seed default Admin user (password: Admin@123)
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1, Username = "admin", FullName = "System Administrator",
                Email = "admin@company.com",
                PasswordHash = "$2a$12$nO.92cNvowz6VFNOT/FerO6E0f.7B9VFm3ziDnofYlZi4NWOgJ3nW", // Admin@123
                RoleId = 1, IsActive = true, IsDeleted = false,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        SeedPermissions(modelBuilder);

        // Seed default Branch
        modelBuilder.Entity<Branch>().HasData(
            new Branch { Id = 1, Name = "Head Office", IsActive = true, IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );

        // Seed default Department
        modelBuilder.Entity<Department>().HasData(
            new Department { Id = 1, Name = "Administration",       IsActive = true, IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Department { Id = 2, Name = "Information Technology", IsActive = true, IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Department { Id = 3, Name = "Human Resources",      IsActive = true, IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Department { Id = 4, Name = "Finance",              IsActive = true, IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );

        // Seed default Designation
        modelBuilder.Entity<Designation>().HasData(
            new Designation { Id = 1, Name = "Manager",           IsActive = true, IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Designation { Id = 2, Name = "Software Engineer", IsActive = true, IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Designation { Id = 3, Name = "HR Executive",      IsActive = true, IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Designation { Id = 4, Name = "Accountant",        IsActive = true, IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );

        // Seed default Shift
        modelBuilder.Entity<Shift>().HasData(
            new Shift
            {
                Id = 1, Name = "General Shift",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime   = new TimeSpan(18, 0, 0),
                GraceMinutes = 15,
                WeeklyOffDays = "Saturday,Sunday",
                IsActive = true, IsDeleted = false,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        // Seed Leave Types
        modelBuilder.Entity<LeaveType>().HasData(
            new LeaveType { Id = 1, Name = "Annual Leave",  TotalDays = 14, IsPaid = true,  IsActive = true, IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new LeaveType { Id = 2, Name = "Sick Leave",    TotalDays = 10, IsPaid = true,  IsActive = true, IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new LeaveType { Id = 3, Name = "Casual Leave",  TotalDays =  7, IsPaid = true,  IsActive = true, IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new LeaveType { Id = 4, Name = "Unpaid Leave",  TotalDays = 30, IsPaid = false, IsActive = true, IsDeleted = false, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );

        // Seed Company Settings
        modelBuilder.Entity<CompanySettings>().HasData(
            new CompanySettings
            {
                Id = 1, CompanyName = "My Company Ltd.",
                WorkStartTime = new TimeSpan(9, 0, 0),
                WorkEndTime   = new TimeSpan(18, 0, 0),
                WeekendDays   = "Saturday,Sunday",
                MaxLateMinutes = 15,
                DefaultPageSize = 25,
                ConfirmBeforeDelete = true,
                IsDeleted = false,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }

    /// <summary>
    /// The catalogue of permissions and the default grant for each seeded role.
    ///
    /// Without these rows every permission check denies, because a check can only ever
    /// answer "yes" for a permission that exists and is granted. Ids are assigned by
    /// position in this table, so entries may be appended but must never be reordered
    /// or removed — doing so would silently re-point existing RolePermission rows.
    /// </summary>
    private static readonly (string Module, string[] Actions)[] PermissionCatalogue =
    [
        (AppConstants.Modules.Dashboard,    [AppConstants.Actions.View]),
        (AppConstants.Modules.Employees,    [AppConstants.Actions.View, AppConstants.Actions.Create, AppConstants.Actions.Edit, AppConstants.Actions.Delete, AppConstants.Actions.Export]),
        (AppConstants.Modules.Departments,  [AppConstants.Actions.View, AppConstants.Actions.Create, AppConstants.Actions.Edit, AppConstants.Actions.Delete]),
        (AppConstants.Modules.Designations, [AppConstants.Actions.View, AppConstants.Actions.Create, AppConstants.Actions.Edit, AppConstants.Actions.Delete]),
        (AppConstants.Modules.Branches,     [AppConstants.Actions.View, AppConstants.Actions.Create, AppConstants.Actions.Edit, AppConstants.Actions.Delete]),
        (AppConstants.Modules.Shifts,       [AppConstants.Actions.View, AppConstants.Actions.Create, AppConstants.Actions.Edit, AppConstants.Actions.Delete]),
        (AppConstants.Modules.Attendance,   [AppConstants.Actions.View, AppConstants.Actions.Create, AppConstants.Actions.Edit, AppConstants.Actions.Delete, AppConstants.Actions.Export]),
        (AppConstants.Modules.Leave,        [AppConstants.Actions.View, AppConstants.Actions.Create, AppConstants.Actions.Edit, AppConstants.Actions.Delete, AppConstants.Actions.Approve]),
        (AppConstants.Modules.Holidays,     [AppConstants.Actions.View, AppConstants.Actions.Create, AppConstants.Actions.Edit, AppConstants.Actions.Delete]),
        (AppConstants.Modules.Reports,      [AppConstants.Actions.View, AppConstants.Actions.Export]),
        (AppConstants.Modules.Users,        [AppConstants.Actions.View, AppConstants.Actions.Create, AppConstants.Actions.Edit, AppConstants.Actions.Delete]),
        (AppConstants.Modules.Roles,        [AppConstants.Actions.View, AppConstants.Actions.Create, AppConstants.Actions.Edit, AppConstants.Actions.Delete]),
        (AppConstants.Modules.Settings,     [AppConstants.Actions.View, AppConstants.Actions.Edit]),
        (AppConstants.Modules.Import,       [AppConstants.Actions.View, AppConstants.Actions.Create]),
        (AppConstants.Modules.AuditLogs,    [AppConstants.Actions.View]),
        // Appended, never inserted mid-list: ids are assigned by position and existing
        // RolePermission rows point at them.
        (AppConstants.Modules.Devices,      [AppConstants.Actions.View, AppConstants.Actions.Create, AppConstants.Actions.Edit, AppConstants.Actions.Delete, AppConstants.Actions.Sync]),
        (AppConstants.Modules.Overtime,     [AppConstants.Actions.View, AppConstants.Actions.Create, AppConstants.Actions.Edit, AppConstants.Actions.Delete, AppConstants.Actions.Approve, AppConstants.Actions.Export]),
    ];

    private static void SeedPermissions(ModelBuilder modelBuilder)
    {
        var seedDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var permissions = new List<Permission>();
        var idByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var nextId = 1;

        foreach (var (module, actions) in PermissionCatalogue)
        {
            foreach (var action in actions)
            {
                permissions.Add(new Permission
                {
                    Id = nextId,
                    Module = module,
                    Action = action,
                    DisplayName = $"{action} {module}",
                    IsDeleted = false,
                    CreatedAt = seedDate
                });
                idByKey[$"{module}.{action}"] = nextId;
                nextId++;
            }
        }

        modelBuilder.Entity<Permission>().HasData(permissions);

        // Administrator: everything.
        var grants = permissions.Select(p => new RolePermission { RoleId = 1, PermissionId = p.Id }).ToList();

        // HR Manager: full operational access, but no control over users, roles or settings.
        // Devices is excluded too — hardware configuration is an administrator task. Grant
        // Devices.Sync explicitly through the Roles screen if HR should pull attendance.
        var hrExcludedModules = new[]
        {
            AppConstants.Modules.Users, AppConstants.Modules.Roles,
            AppConstants.Modules.AuditLogs, AppConstants.Modules.Devices
        };
        grants.AddRange(permissions
            .Where(p => !hrExcludedModules.Contains(p.Module, StringComparer.OrdinalIgnoreCase))
            .Where(p => !(p.Module == AppConstants.Modules.Settings && p.Action == AppConstants.Actions.Edit))
            .Select(p => new RolePermission { RoleId = 2, PermissionId = p.Id }));

        // Employee: self-service only — see the dashboard, own attendance, request leave.
        string[] employeeGrants =
        [
            $"{AppConstants.Modules.Dashboard}.{AppConstants.Actions.View}",
            $"{AppConstants.Modules.Attendance}.{AppConstants.Actions.View}",
            $"{AppConstants.Modules.Attendance}.{AppConstants.Actions.Create}",
            $"{AppConstants.Modules.Leave}.{AppConstants.Actions.View}",
            $"{AppConstants.Modules.Leave}.{AppConstants.Actions.Create}",
            $"{AppConstants.Modules.Holidays}.{AppConstants.Actions.View}",
        ];
        grants.AddRange(employeeGrants
            .Where(idByKey.ContainsKey)
            .Select(key => new RolePermission { RoleId = 3, PermissionId = idByKey[key] }));

        modelBuilder.Entity<RolePermission>().HasData(grants);
    }
}
