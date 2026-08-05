-- ============================================================
-- Attendance Management System — Database Setup Script
-- SQL Server 2022
-- ============================================================

USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'AttendanceDB')
	CREATE DATABASE AttendanceDB;
GO

USE AttendanceDB;
GO

-- ── Roles ─────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Roles')
CREATE TABLE Roles (
	Id          INT IDENTITY(1,1) PRIMARY KEY,
	Name        NVARCHAR(100) NOT NULL,
	Description NVARCHAR(500) NULL,
	IsDeleted   BIT NOT NULL DEFAULT 0,
	CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
	CreatedBy   INT NULL,
	ModifiedAt  DATETIME2 NULL,
	ModifiedBy  INT NULL,
	CONSTRAINT UQ_Roles_Name UNIQUE (Name)
);
GO

-- ── Permissions ───────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Permissions')
CREATE TABLE Permissions (
	Id          INT IDENTITY(1,1) PRIMARY KEY,
	Module      NVARCHAR(100) NOT NULL,
	Action      NVARCHAR(100) NOT NULL,
	DisplayName NVARCHAR(200) NOT NULL,
	IsDeleted   BIT NOT NULL DEFAULT 0,
	CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
	CreatedBy   INT NULL,
	ModifiedAt  DATETIME2 NULL,
	ModifiedBy  INT NULL,
	CONSTRAINT UQ_Permissions_ModuleAction UNIQUE (Module, Action)
);
GO

-- ── RolePermissions ───────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RolePermissions')
CREATE TABLE RolePermissions (
	RoleId       INT NOT NULL,
	PermissionId INT NOT NULL,
	CONSTRAINT PK_RolePermissions PRIMARY KEY (RoleId, PermissionId),
	CONSTRAINT FK_RolePermissions_Role       FOREIGN KEY (RoleId)       REFERENCES Roles(Id),
	CONSTRAINT FK_RolePermissions_Permission FOREIGN KEY (PermissionId) REFERENCES Permissions(Id)
);
GO

-- ── Branches ──────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Branches')
CREATE TABLE Branches (
	Id        INT IDENTITY(1,1) PRIMARY KEY,
	Name      NVARCHAR(200) NOT NULL,
	Address   NVARCHAR(500) NULL,
	Phone     NVARCHAR(20) NULL,
	IsActive  BIT NOT NULL DEFAULT 1,
	IsDeleted BIT NOT NULL DEFAULT 0,
	CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
	CreatedBy INT NULL,
	ModifiedAt DATETIME2 NULL,
	ModifiedBy INT NULL
);
GO

-- ── Departments ───────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Departments')
CREATE TABLE Departments (
	Id          INT IDENTITY(1,1) PRIMARY KEY,
	Name        NVARCHAR(200) NOT NULL,
	Description NVARCHAR(500) NULL,
	IsActive    BIT NOT NULL DEFAULT 1,
	IsDeleted   BIT NOT NULL DEFAULT 0,
	CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
	CreatedBy   INT NULL,
	ModifiedAt  DATETIME2 NULL,
	ModifiedBy  INT NULL,
	CONSTRAINT UQ_Departments_Name UNIQUE (Name)
);
GO

-- ── Designations ──────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Designations')
CREATE TABLE Designations (
	Id          INT IDENTITY(1,1) PRIMARY KEY,
	Name        NVARCHAR(200) NOT NULL,
	Description NVARCHAR(500) NULL,
	IsActive    BIT NOT NULL DEFAULT 1,
	IsDeleted   BIT NOT NULL DEFAULT 0,
	CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
	CreatedBy   INT NULL,
	ModifiedAt  DATETIME2 NULL,
	ModifiedBy  INT NULL,
	CONSTRAINT UQ_Designations_Name UNIQUE (Name)
);
GO

-- ── Employees ─────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Employees')
CREATE TABLE Employees (
	Id                INT IDENTITY(1,1) PRIMARY KEY,
	EmployeeCode      NVARCHAR(50) NOT NULL,
	FirstName         NVARCHAR(100) NOT NULL,
	LastName          NVARCHAR(100) NOT NULL,
	Email             NVARCHAR(200) NULL,
	Phone             NVARCHAR(20) NULL,
	DateOfBirth       DATE NULL,
	JoiningDate       DATE NOT NULL,
	Gender            NVARCHAR(10) NULL,
	Address           NVARCHAR(500) NULL,
	Photo             VARBINARY(MAX) NULL,
	DepartmentId      INT NOT NULL,
	DesignationId     INT NOT NULL,
	BranchId          INT NOT NULL,
	BiometricEnrollId INT NULL,
	IsActive          BIT NOT NULL DEFAULT 1,
	IsDeleted         BIT NOT NULL DEFAULT 0,
	CreatedAt         DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
	CreatedBy         INT NULL,
	ModifiedAt        DATETIME2 NULL,
	ModifiedBy        INT NULL,
	CONSTRAINT UQ_Employees_Code UNIQUE (EmployeeCode),
	CONSTRAINT FK_Employees_Department  FOREIGN KEY (DepartmentId)  REFERENCES Departments(Id),
	CONSTRAINT FK_Employees_Designation FOREIGN KEY (DesignationId) REFERENCES Designations(Id),
	CONSTRAINT FK_Employees_Branch      FOREIGN KEY (BranchId)      REFERENCES Branches(Id)
);
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Employees') AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Employees') AND name = 'BiometricEnrollId')
BEGIN
	ALTER TABLE Employees ADD BiometricEnrollId INT NULL;
END
CREATE INDEX IX_Employees_Department  ON Employees (DepartmentId);
CREATE INDEX IX_Employees_Designation ON Employees (DesignationId);
CREATE INDEX IX_Employees_Branch      ON Employees (BranchId);
GO

-- ── Users ─────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Users')
CREATE TABLE Users (
	Id                  INT IDENTITY(1,1) PRIMARY KEY,
	Username            NVARCHAR(100) NOT NULL,
	PasswordHash        NVARCHAR(500) NOT NULL,
	Email               NVARCHAR(200) NOT NULL,
	FullName            NVARCHAR(200) NOT NULL,
	RoleId              INT NOT NULL,
	EmployeeId          INT NULL,
	IsActive            BIT NOT NULL DEFAULT 1,
	IsLocked            BIT NOT NULL DEFAULT 0,
	FailedLoginAttempts INT NOT NULL DEFAULT 0,
	LastLoginAt         DATETIME2 NULL,
	PasswordChangedAt   DATETIME2 NULL,
	RememberToken            NVARCHAR(200) NULL,
	ResetPasswordToken       NVARCHAR(MAX) NULL,
	ResetPasswordTokenExpiry DATETIME2 NULL,
	IsDeleted           BIT NOT NULL DEFAULT 0,
	CreatedAt           DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
	CreatedBy           INT NULL,
	ModifiedAt          DATETIME2 NULL,
	ModifiedBy          INT NULL,
	CONSTRAINT UQ_Users_Username UNIQUE (Username),
	CONSTRAINT UQ_Users_Email    UNIQUE (Email),
	CONSTRAINT FK_Users_Role     FOREIGN KEY (RoleId)     REFERENCES Roles(Id),
	CONSTRAINT FK_Users_Employee FOREIGN KEY (EmployeeId) REFERENCES Employees(Id)
);
GO

-- ── Shifts ────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Shifts')
CREATE TABLE Shifts (
	Id           INT IDENTITY(1,1) PRIMARY KEY,
	Name         NVARCHAR(100) NOT NULL,
	StartTime    TIME NOT NULL,
	EndTime      TIME NOT NULL,
	GraceMinutes INT NOT NULL DEFAULT 0,
	WeeklyOffDays NVARCHAR(200) NOT NULL DEFAULT 'Saturday,Sunday',
	IsActive     BIT NOT NULL DEFAULT 1,
	IsDeleted    BIT NOT NULL DEFAULT 0,
	CreatedAt    DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
	CreatedBy    INT NULL,
	ModifiedAt   DATETIME2 NULL,
	ModifiedBy   INT NULL
);
GO

-- ── EmployeeShifts ────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'EmployeeShifts')
CREATE TABLE EmployeeShifts (
	Id            INT IDENTITY(1,1) PRIMARY KEY,
	EmployeeId    INT NOT NULL,
	ShiftId       INT NOT NULL,
	EffectiveFrom DATE NOT NULL,
	EffectiveTo   DATE NULL,
	IsDeleted     BIT NOT NULL DEFAULT 0,
	CreatedAt     DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
	CreatedBy     INT NULL,
	ModifiedAt    DATETIME2 NULL,
	ModifiedBy    INT NULL,
	CONSTRAINT FK_EmployeeShifts_Employee FOREIGN KEY (EmployeeId) REFERENCES Employees(Id),
	CONSTRAINT FK_EmployeeShifts_Shift    FOREIGN KEY (ShiftId)    REFERENCES Shifts(Id)
);
CREATE INDEX IX_EmployeeShifts_Employee ON EmployeeShifts (EmployeeId);
GO

-- ── AttendanceLogs ────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AttendanceLogs')
CREATE TABLE AttendanceLogs (
	Id                 INT IDENTITY(1,1) PRIMARY KEY,
	EmployeeId         INT NOT NULL,
	AttendanceDate     DATE NOT NULL,
	CheckIn            DATETIME2 NULL,
	CheckOut           DATETIME2 NULL,
	Status             INT NOT NULL DEFAULT 1,
	IsLate             BIT NOT NULL DEFAULT 0,
	IsEarlyLeave       BIT NOT NULL DEFAULT 0,
	LateMinutes        INT NULL,
	EarlyLeaveMinutes  INT NULL,
	WorkingHours       FLOAT NULL,
	Remarks            NVARCHAR(500) NULL,
	IsManual           BIT NOT NULL DEFAULT 0,
	IsDeleted          BIT NOT NULL DEFAULT 0,
	CreatedAt          DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
	CreatedBy          INT NULL,
	ModifiedAt         DATETIME2 NULL,
	ModifiedBy         INT NULL,
	CONSTRAINT UQ_AttendanceLogs_EmployeeDate UNIQUE (EmployeeId, AttendanceDate),
	CONSTRAINT FK_AttendanceLogs_Employee     FOREIGN KEY (EmployeeId) REFERENCES Employees(Id)
);
CREATE INDEX IX_AttendanceLogs_Date     ON AttendanceLogs (AttendanceDate);
CREATE INDEX IX_AttendanceLogs_Employee ON AttendanceLogs (EmployeeId);
GO

-- ── AttendanceSummaries ───────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AttendanceSummaries')
CREATE TABLE AttendanceSummaries (
	Id                INT IDENTITY(1,1) PRIMARY KEY,
	EmployeeId        INT NOT NULL,
	Month             INT NOT NULL,
	Year              INT NOT NULL,
	TotalDays         INT NOT NULL DEFAULT 0,
	PresentDays       INT NOT NULL DEFAULT 0,
	AbsentDays        INT NOT NULL DEFAULT 0,
	LateDays          INT NOT NULL DEFAULT 0,
	LeaveDays         INT NOT NULL DEFAULT 0,
	HolidayDays       INT NOT NULL DEFAULT 0,
	TotalWorkingHours FLOAT NOT NULL DEFAULT 0,
	IsDeleted         BIT NOT NULL DEFAULT 0,
	CreatedAt         DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
	CreatedBy         INT NULL,
	ModifiedAt        DATETIME2 NULL,
	ModifiedBy        INT NULL,
	CONSTRAINT UQ_AttendanceSummaries_EmpMonthYear UNIQUE (EmployeeId, Month, Year),
	CONSTRAINT FK_AttendanceSummaries_Employee     FOREIGN KEY (EmployeeId) REFERENCES Employees(Id)
);
GO

-- ── LeaveTypes ────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LeaveTypes')
CREATE TABLE LeaveTypes (
	Id        INT IDENTITY(1,1) PRIMARY KEY,
	Name      NVARCHAR(100) NOT NULL,
	TotalDays INT NOT NULL,
	IsPaid    BIT NOT NULL DEFAULT 1,
	IsActive  BIT NOT NULL DEFAULT 1,
	IsDeleted BIT NOT NULL DEFAULT 0,
	CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
	CreatedBy INT NULL,
	ModifiedAt DATETIME2 NULL,
	ModifiedBy INT NULL,
	CONSTRAINT UQ_LeaveTypes_Name UNIQUE (Name)
);
GO

-- ── LeaveRequests ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LeaveRequests')
CREATE TABLE LeaveRequests (
	Id              INT IDENTITY(1,1) PRIMARY KEY,
	EmployeeId      INT NOT NULL,
	LeaveTypeId     INT NOT NULL,
	FromDate        DATE NOT NULL,
	ToDate          DATE NOT NULL,
	TotalDays       INT NOT NULL,
	Reason          NVARCHAR(1000) NOT NULL,
	Status          INT NOT NULL DEFAULT 1,
	ApprovedBy      INT NULL,
	ApprovedAt      DATETIME2 NULL,
	RejectionReason NVARCHAR(500) NULL,
	IsDeleted       BIT NOT NULL DEFAULT 0,
	CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
	CreatedBy       INT NULL,
	ModifiedAt      DATETIME2 NULL,
	ModifiedBy      INT NULL,
	CONSTRAINT FK_LeaveRequests_Employee  FOREIGN KEY (EmployeeId)  REFERENCES Employees(Id),
	CONSTRAINT FK_LeaveRequests_LeaveType FOREIGN KEY (LeaveTypeId) REFERENCES LeaveTypes(Id)
);
CREATE INDEX IX_LeaveRequests_Employee  ON LeaveRequests (EmployeeId);
CREATE INDEX IX_LeaveRequests_Status    ON LeaveRequests (Status);
GO

-- ── Holidays ──────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Holidays')
CREATE TABLE Holidays (
	Id          INT IDENTITY(1,1) PRIMARY KEY,
	Name        NVARCHAR(200) NOT NULL,
	HolidayDate DATE NOT NULL,
	HolidayType INT NOT NULL DEFAULT 1,
	Description NVARCHAR(500) NULL,
	IsRecurring BIT NOT NULL DEFAULT 0,
	IsDeleted   BIT NOT NULL DEFAULT 0,
	CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
	CreatedBy   INT NULL,
	ModifiedAt  DATETIME2 NULL,
	ModifiedBy  INT NULL
);
CREATE INDEX IX_Holidays_Date ON Holidays (HolidayDate);
GO

-- ── AuditLogs ─────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AuditLogs')
CREATE TABLE AuditLogs (
	Id         INT IDENTITY(1,1) PRIMARY KEY,
	UserId     INT NULL,
	Action     NVARCHAR(100) NOT NULL,
	Module     NVARCHAR(100) NOT NULL,
	EntityName NVARCHAR(100) NULL,
	EntityId   INT NULL,
	OldValues  NVARCHAR(MAX) NULL,
	NewValues  NVARCHAR(MAX) NULL,
	IpAddress  NVARCHAR(50) NULL,
	CreatedAt  DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
	CONSTRAINT FK_AuditLogs_User FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE SET NULL
);
CREATE INDEX IX_AuditLogs_UserId ON AuditLogs (UserId);
CREATE INDEX IX_AuditLogs_Module ON AuditLogs (Module);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AttendanceLogs_Date_Emp' AND object_id = OBJECT_ID('AttendanceLogs'))
    CREATE INDEX IX_AttendanceLogs_Date_Emp ON AttendanceLogs (AttendanceDate, EmployeeId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LeaveRequests_Emp_Status' AND object_id = OBJECT_ID('LeaveRequests'))
    CREATE INDEX IX_LeaveRequests_Emp_Status ON LeaveRequests (EmployeeId, Status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLogs_Timestamp_User' AND object_id = OBJECT_ID('AuditLogs'))
    CREATE INDEX IX_AuditLogs_Timestamp_User ON AuditLogs (Timestamp, UserId);
GO

-- ── CompanySettings ───────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CompanySettings')
CREATE TABLE CompanySettings (
	Id             INT IDENTITY(1,1) PRIMARY KEY,
	CompanyName    NVARCHAR(300) NOT NULL,
	Address        NVARCHAR(500) NULL,
	Phone          NVARCHAR(50) NULL,
	Email          NVARCHAR(200) NULL,
	Website        NVARCHAR(300) NULL,
	LogoPath       NVARCHAR(500) NULL,
	WorkStartTime  TIME NOT NULL,
	WorkEndTime    TIME NOT NULL,
	WeekendDays    NVARCHAR(200) NOT NULL DEFAULT 'Saturday,Sunday',
	MaxLateMinutes INT NOT NULL DEFAULT 15,
	IsDeleted      BIT NOT NULL DEFAULT 0,
	CreatedAt      DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
	CreatedBy      INT NULL,
	ModifiedAt     DATETIME2 NULL,
	ModifiedBy     INT NULL
);
GO

-- ============================================================
-- ── Master & Seed Data Population Script ────────────────────
-- ============================================================

-- 1. System Roles
IF NOT EXISTS (SELECT 1 FROM Roles WHERE Id = 1)
BEGIN
	SET IDENTITY_INSERT Roles ON;
	INSERT INTO Roles (Id, Name, Description, IsDeleted, CreatedAt, CreatedBy) VALUES
	  (1, 'Administrator', 'Full system access with complete administrative & configuration control', 0, GETUTCDATE(), 1),
	  (2, 'HR Manager',    'HR module access, employee management, and leave approval rights', 0, GETUTCDATE(), 1),
	  (3, 'Manager',       'Departmental view, team attendance oversight, and leave recommendation', 0, GETUTCDATE(), 1),
	  (4, 'Supervisor',    'Shift management, daily attendance monitoring, and team supervision', 0, GETUTCDATE(), 1),
	  (5, 'Employee',      'Self-service access for checking personal attendance and submitting leave requests', 0, GETUTCDATE(), 1);
	SET IDENTITY_INSERT Roles OFF;
END
GO

-- 2. System Permissions
IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Module = 'Dashboard' AND Action = 'View')
BEGIN
	INSERT INTO Permissions (Module, Action, DisplayName, IsDeleted, CreatedAt, CreatedBy) VALUES
	  -- Dashboard Module
	  ('Dashboard', 'View', 'View Main Dashboard', 0, GETUTCDATE(), 1),
	  
	  -- Employees Module
	  ('Employees', 'View',   'View Employee Directory', 0, GETUTCDATE(), 1),
	  ('Employees', 'Create', 'Add New Employee', 0, GETUTCDATE(), 1),
	  ('Employees', 'Edit',   'Edit Employee Profile', 0, GETUTCDATE(), 1),
	  ('Employees', 'Delete', 'Delete/Deactivate Employee', 0, GETUTCDATE(), 1),
	  
	  -- Departments Module
	  ('Departments', 'View',   'View Departments', 0, GETUTCDATE(), 1),
	  ('Departments', 'Create', 'Create Department', 0, GETUTCDATE(), 1),
	  ('Departments', 'Edit',   'Edit Department', 0, GETUTCDATE(), 1),
	  ('Departments', 'Delete', 'Delete Department', 0, GETUTCDATE(), 1),
	  
	  -- Designations Module
	  ('Designations', 'View',   'View Designations', 0, GETUTCDATE(), 1),
	  ('Designations', 'Create', 'Create Designation', 0, GETUTCDATE(), 1),
	  ('Designations', 'Edit',   'Edit Designation', 0, GETUTCDATE(), 1),
	  ('Designations', 'Delete', 'Delete Designation', 0, GETUTCDATE(), 1),

	  -- Branches Module
	  ('Branches', 'View',   'View Branches', 0, GETUTCDATE(), 1),
	  ('Branches', 'Create', 'Create Branch', 0, GETUTCDATE(), 1),
	  ('Branches', 'Edit',   'Edit Branch', 0, GETUTCDATE(), 1),
	  ('Branches', 'Delete', 'Delete Branch', 0, GETUTCDATE(), 1),
	  
	  -- Shifts Module
	  ('Shifts', 'View',   'View Shift Schedules', 0, GETUTCDATE(), 1),
	  ('Shifts', 'Create', 'Create Shift Schedule', 0, GETUTCDATE(), 1),
	  ('Shifts', 'Edit',   'Edit Shift Schedule', 0, GETUTCDATE(), 1),
	  ('Shifts', 'Delete', 'Delete Shift Schedule', 0, GETUTCDATE(), 1),
	  ('Shifts', 'Assign', 'Assign Employee Shifts', 0, GETUTCDATE(), 1),

	  -- Attendance Module
	  ('Attendance', 'View',       'View Attendance Logs', 0, GETUTCDATE(), 1),
	  ('Attendance', 'Create',     'Mark Daily Attendance', 0, GETUTCDATE(), 1),
	  ('Attendance', 'Edit',       'Edit Attendance Record', 0, GETUTCDATE(), 1),
	  ('Attendance', 'Delete',     'Delete Attendance Record', 0, GETUTCDATE(), 1),
	  ('Attendance', 'BulkImport', 'Bulk Import Attendance Logs', 0, GETUTCDATE(), 1),
	  
	  -- Leave Module
	  ('Leave', 'View',    'View Leave Requests', 0, GETUTCDATE(), 1),
	  ('Leave', 'Create',  'Submit Leave Request', 0, GETUTCDATE(), 1),
	  ('Leave', 'Edit',    'Edit Leave Request', 0, GETUTCDATE(), 1),
	  ('Leave', 'Approve', 'Approve/Reject Leave Requests', 0, GETUTCDATE(), 1),
	  ('Leave', 'Delete',  'Delete Leave Request', 0, GETUTCDATE(), 1),
	  
	  -- Holidays Module
	  ('Holidays', 'View',   'View Holiday List', 0, GETUTCDATE(), 1),
	  ('Holidays', 'Create', 'Create Holiday', 0, GETUTCDATE(), 1),
	  ('Holidays', 'Edit',   'Edit Holiday', 0, GETUTCDATE(), 1),
	  ('Holidays', 'Delete', 'Delete Holiday', 0, GETUTCDATE(), 1),
	  
	  -- Reports Module
	  ('Reports', 'View',   'View System Reports', 0, GETUTCDATE(), 1),
	  ('Reports', 'Export', 'Export Reports (Excel/PDF)', 0, GETUTCDATE(), 1),
	  
	  -- Users Module
	  ('Users', 'View',   'View System Users', 0, GETUTCDATE(), 1),
	  ('Users', 'Create', 'Create System User', 0, GETUTCDATE(), 1),
	  ('Users', 'Edit',   'Edit System User', 0, GETUTCDATE(), 1),
	  ('Users', 'Delete', 'Delete/Lock User Account', 0, GETUTCDATE(), 1),
	  
	  -- Roles & Access Control
	  ('Roles', 'View',              'View System Roles', 0, GETUTCDATE(), 1),
	  ('Roles', 'Create',            'Create Role', 0, GETUTCDATE(), 1),
	  ('Roles', 'Edit',              'Edit Role', 0, GETUTCDATE(), 1),
	  ('Roles', 'Delete',            'Delete Role', 0, GETUTCDATE(), 1),
	  ('Roles', 'AssignPermissions', 'Manage Role Permission Matrix', 0, GETUTCDATE(), 1),
	  
	  -- Settings Module
	  ('Settings', 'View', 'View System Settings', 0, GETUTCDATE(), 1),
	  ('Settings', 'Edit', 'Modify System Settings', 0, GETUTCDATE(), 1),
	  
	  -- Audit Logs
	  ('AuditLogs', 'View', 'View Audit Logs', 0, GETUTCDATE(), 1);
END
GO

-- 3. Role Permission Bindings
IF NOT EXISTS (SELECT 1 FROM RolePermissions WHERE RoleId = 1)
BEGIN
	-- Administrator: Full Access
	INSERT INTO RolePermissions (RoleId, PermissionId)
	SELECT 1, Id FROM Permissions;

	-- HR Manager (RoleId = 2)
	INSERT INTO RolePermissions (RoleId, PermissionId)
	SELECT 2, Id FROM Permissions 
	WHERE Module IN ('Dashboard', 'Employees', 'Departments', 'Designations', 'Branches', 'Shifts', 'Attendance', 'Leave', 'Holidays', 'Reports')
	   OR (Module = 'Users' AND Action IN ('View', 'Edit'));

	-- Manager (RoleId = 3)
	INSERT INTO RolePermissions (RoleId, PermissionId)
	SELECT 3, Id FROM Permissions 
	WHERE (Module = 'Dashboard' AND Action = 'View')
	   OR (Module = 'Employees' AND Action = 'View')
	   OR (Module = 'Departments' AND Action = 'View')
	   OR (Module = 'Attendance' AND Action IN ('View', 'Create', 'Edit'))
	   OR (Module = 'Leave' AND Action IN ('View', 'Create', 'Approve'))
	   OR (Module = 'Holidays' AND Action = 'View')
	   OR (Module = 'Reports' AND Action IN ('View', 'Export'));

	-- Supervisor (RoleId = 4)
	INSERT INTO RolePermissions (RoleId, PermissionId)
	SELECT 4, Id FROM Permissions 
	WHERE (Module = 'Dashboard' AND Action = 'View')
	   OR (Module = 'Employees' AND Action = 'View')
	   OR (Module = 'Shifts' AND Action IN ('View', 'Assign'))
	   OR (Module = 'Attendance' AND Action IN ('View', 'Create'))
	   OR (Module = 'Leave' AND Action = 'View')
	   OR (Module = 'Holidays' AND Action = 'View');

	-- Employee (RoleId = 5)
	INSERT INTO RolePermissions (RoleId, PermissionId)
	SELECT 5, Id FROM Permissions 
	WHERE (Module = 'Dashboard' AND Action = 'View')
	   OR (Module = 'Attendance' AND Action = 'View')
	   OR (Module = 'Leave' AND Action IN ('View', 'Create'))
	   OR (Module = 'Holidays' AND Action = 'View');
END
GO

-- 4. Branches Master Data
IF NOT EXISTS (SELECT 1 FROM Branches WHERE Id = 1)
BEGIN
	SET IDENTITY_INSERT Branches ON;
	INSERT INTO Branches (Id, Name, Address, Phone, IsActive, IsDeleted, CreatedAt, CreatedBy) VALUES
	  (1, 'Head Office - Colombo',   'No. 100, Galle Road, Colombo 03, Sri Lanka', '+94 11 234 5678', 1, 0, GETUTCDATE(), 1),
	  (2, 'Kandy Regional Branch',  'No. 45, Peradeniya Road, Kandy, Sri Lanka',   '+94 81 223 4567', 1, 0, GETUTCDATE(), 1),
	  (3, 'Galle Coastal Branch',   'No. 12, Main Street, Fort, Galle, Sri Lanka', '+94 91 224 5678', 1, 0, GETUTCDATE(), 1),
	  (4, 'Jaffna Northern Office', 'No. 88, Stanley Road, Jaffna, Sri Lanka',     '+94 21 222 3456', 1, 0, GETUTCDATE(), 1);
	SET IDENTITY_INSERT Branches OFF;
END
GO

-- 5. Departments Master Data
IF NOT EXISTS (SELECT 1 FROM Departments WHERE Id = 1)
BEGIN
	SET IDENTITY_INSERT Departments ON;
	INSERT INTO Departments (Id, Name, Description, IsActive, IsDeleted, CreatedAt, CreatedBy) VALUES
	  (1, 'Administration',         'Executive management and administrative services',      1, 0, GETUTCDATE(), 1),
	  (2, 'Information Technology', 'Software development, infrastructure, and IT support',  1, 0, GETUTCDATE(), 1),
	  (3, 'Human Resources',        'Talent acquisition, employee relations, and payroll',   1, 0, GETUTCDATE(), 1),
	  (4, 'Finance & Accounting',   'Financial planning, budgeting, and accounting',         1, 0, GETUTCDATE(), 1),
	  (5, 'Operations',             'Business operational operations and logistics',          1, 0, GETUTCDATE(), 1),
	  (6, 'Sales & Marketing',      'Client acquisition, branding, and business expansion', 1, 0, GETUTCDATE(), 1),
	  (7, 'Customer Support',       'Client support services and helpdesk management',       1, 0, GETUTCDATE(), 1);
	SET IDENTITY_INSERT Departments OFF;
END
GO

-- 6. Designations Master Data
IF NOT EXISTS (SELECT 1 FROM Designations WHERE Id = 1)
BEGIN
	SET IDENTITY_INSERT Designations ON;
	INSERT INTO Designations (Id, Name, Description, IsActive, IsDeleted, CreatedAt, CreatedBy) VALUES
	  (1,  'Chief Executive Officer',     'Executive Leadership',                 1, 0, GETUTCDATE(), 1),
	  (2,  'Software Development Manager','Engineering Management',               1, 0, GETUTCDATE(), 1),
	  (3,  'Senior Software Engineer',    'Senior Software Development',          1, 0, GETUTCDATE(), 1),
	  (4,  'Software Engineer',           'Software Development & Maintenance',   1, 0, GETUTCDATE(), 1),
	  (5,  'Quality Assurance Engineer',  'Software Quality Testing',             1, 0, GETUTCDATE(), 1),
	  (6,  'HR Manager',                  'Human Resources Leadership',           1, 0, GETUTCDATE(), 1),
	  (7,  'HR Executive',                'HR Operations & Personnel Support',    1, 0, GETUTCDATE(), 1),
	  (8,  'Finance Manager',             'Financial Operations Management',      1, 0, GETUTCDATE(), 1),
	  (9,  'Senior Accountant',           'Accounting & Financial Reporting',     1, 0, GETUTCDATE(), 1),
	  (10, 'Operations Lead',             'Field Operations Supervision',         1, 0, GETUTCDATE(), 1),
	  (11, 'Support Specialist',          'Customer Service & Technical Support', 1, 0, GETUTCDATE(), 1),
	  (12, 'Administrative Officer',      'Office Management & Administration',   1, 0, GETUTCDATE(), 1);
	SET IDENTITY_INSERT Designations OFF;
END
GO

-- 7. Shifts Master Data
IF NOT EXISTS (SELECT 1 FROM Shifts WHERE Id = 1)
BEGIN
	SET IDENTITY_INSERT Shifts ON;
	INSERT INTO Shifts (Id, Name, StartTime, EndTime, GraceMinutes, WeeklyOffDays, IsActive, IsDeleted, CreatedAt, CreatedBy) VALUES
	  (1, 'General Shift', '08:30:00', '17:00:00', 15, 'Saturday,Sunday', 1, 0, GETUTCDATE(), 1),
	  (2, 'Morning Shift', '07:00:00', '15:30:00', 15, 'Saturday,Sunday', 1, 0, GETUTCDATE(), 1),
	  (3, 'Evening Shift', '14:00:00', '22:30:00', 15, 'Saturday,Sunday', 1, 0, GETUTCDATE(), 1),
	  (4, 'Night Shift',   '22:00:00', '06:30:00', 15, 'Saturday,Sunday', 1, 0, GETUTCDATE(), 1),
	  (5, 'Flexible Shift','08:30:00', '17:30:00', 30, 'Sunday',          1, 0, GETUTCDATE(), 1);
	SET IDENTITY_INSERT Shifts OFF;
END
GO

-- 8. Leave Types Master Data
IF NOT EXISTS (SELECT 1 FROM LeaveTypes WHERE Id = 1)
BEGIN
	SET IDENTITY_INSERT LeaveTypes ON;
	INSERT INTO LeaveTypes (Id, Name, TotalDays, IsPaid, IsActive, IsDeleted, CreatedAt, CreatedBy) VALUES
	  (1, 'Annual Leave',       14, 1, 1, 0, GETUTCDATE(), 1),
	  (2, 'Casual Leave',        7, 1, 1, 0, GETUTCDATE(), 1),
	  (3, 'Medical Leave',      14, 1, 1, 0, GETUTCDATE(), 1),
	  (4, 'Maternity Leave',    84, 1, 1, 0, GETUTCDATE(), 1),
	  (5, 'Paternity Leave',     5, 1, 1, 0, GETUTCDATE(), 1),
	  (6, 'Duty Leave',         10, 1, 1, 0, GETUTCDATE(), 1),
	  (7, 'Unpaid Leave',       30, 0, 1, 0, GETUTCDATE(), 1);
	SET IDENTITY_INSERT LeaveTypes OFF;
END
GO

-- 9. Company Settings Master Data
IF NOT EXISTS (SELECT 1 FROM CompanySettings WHERE Id = 1)
BEGIN
	SET IDENTITY_INSERT CompanySettings ON;
	INSERT INTO CompanySettings (Id, CompanyName, Address, Phone, Email, Website, LogoPath, WorkStartTime, WorkEndTime, WeekendDays, MaxLateMinutes, IsDeleted, CreatedAt, CreatedBy) VALUES
	  (1, 'Lanka Enterprise Solutions (Pvt) Ltd.', 'No. 100, Galle Road, Colombo 03, Sri Lanka', '+94 11 234 5678', 'info@globalenterprise.lk', 'https://www.globalenterprise.lk', '/assets/images/logo.png', '08:30:00', '17:00:00', 'Saturday,Sunday', 15, 0, GETUTCDATE(), 1);
	SET IDENTITY_INSERT CompanySettings OFF;
END
GO

-- 10. Public & Mercantile Holidays Seed Data (Sri Lanka 2026)
IF NOT EXISTS (SELECT 1 FROM Holidays WHERE Id = 1)
BEGIN
	SET IDENTITY_INSERT Holidays ON;
	INSERT INTO Holidays (Id, Name, HolidayDate, HolidayType, Description, IsRecurring, IsDeleted, CreatedAt, CreatedBy) VALUES
	  (1,  'Duruthu Full Moon Poya Day',           '2026-01-03', 1, 'Public, Bank & Mercantile Holiday', 1, 0, GETUTCDATE(), 1),
	  (2,  'Tamil Thai Pongal Day',                '2026-01-14', 1, 'Public & Bank Holiday',           1, 0, GETUTCDATE(), 1),
	  (3,  'Navam Full Moon Poya Day',             '2026-02-01', 1, 'Public, Bank & Mercantile Holiday', 1, 0, GETUTCDATE(), 1),
	  (4,  'National Independence Day',            '2026-02-04', 1, '78th National Independence Day of Sri Lanka', 1, 0, GETUTCDATE(), 1),
	  (5,  'Mahasivarathri Day',                   '2026-02-15', 1, 'Public & Bank Holiday',           1, 0, GETUTCDATE(), 1),
	  (6,  'Medin Full Moon Poya Day',             '2026-03-03', 1, 'Public, Bank & Mercantile Holiday', 1, 0, GETUTCDATE(), 1),
	  (7,  'Bak Full Moon Poya Day',               '2026-04-01', 1, 'Public, Bank & Mercantile Holiday', 1, 0, GETUTCDATE(), 1),
	  (8,  'Good Friday',                          '2026-04-03', 1, 'Public & Bank Holiday',           0, 0, GETUTCDATE(), 1),
	  (9,  'Sinhala & Tamil New Year Eve',         '2026-04-13', 1, 'Public, Bank & Mercantile Holiday', 1, 0, GETUTCDATE(), 1),
	  (10, 'Sinhala & Tamil New Year Day',         '2026-04-14', 1, 'Public, Bank & Mercantile Holiday', 1, 0, GETUTCDATE(), 1),
	  (11, 'May Day / International Workers'' Day', '2026-05-01', 1, 'Public, Bank & Mercantile Holiday', 1, 0, GETUTCDATE(), 1),
	  (12, 'Vesak Full Moon Poya Day',             '2026-05-31', 1, 'Public, Bank & Mercantile Holiday', 1, 0, GETUTCDATE(), 1),
	  (13, 'Day following Vesak Full Moon Poya Day','2026-06-01', 1, 'Public, Bank & Mercantile Holiday', 1, 0, GETUTCDATE(), 1),
	  (14, 'Poson Full Moon Poya Day',             '2026-06-29', 1, 'Public, Bank & Mercantile Holiday', 1, 0, GETUTCDATE(), 1),
	  (15, 'Esala Full Moon Poya Day',             '2026-07-29', 1, 'Public, Bank & Mercantile Holiday', 1, 0, GETUTCDATE(), 1),
	  (16, 'Milad-Un-Nabi (Holy Prophet''s Birthday)','2026-08-26', 1, 'Public & Bank Holiday',          1, 0, GETUTCDATE(), 1),
	  (17, 'Nikini Full Moon Poya Day',            '2026-08-27', 1, 'Public, Bank & Mercantile Holiday', 1, 0, GETUTCDATE(), 1),
	  (18, 'Binara Full Moon Poya Day',            '2026-09-25', 1, 'Public, Bank & Mercantile Holiday', 1, 0, GETUTCDATE(), 1),
	  (19, 'Vap Full Moon Poya Day',               '2026-10-25', 1, 'Public, Bank & Mercantile Holiday', 1, 0, GETUTCDATE(), 1),
	  (20, 'Deepavali Festival Day',               '2026-11-08', 1, 'Public & Bank Holiday',           1, 0, GETUTCDATE(), 1),
	  (21, 'Ill Full Moon Poya Day',               '2026-11-24', 1, 'Public, Bank & Mercantile Holiday', 1, 0, GETUTCDATE(), 1),
	  (22, 'Unduvap Full Moon Poya Day',           '2026-12-23', 1, 'Public, Bank & Mercantile Holiday', 1, 0, GETUTCDATE(), 1),
	  (23, 'Christmas Day',                        '2026-12-25', 1, 'Public, Bank & Mercantile Holiday', 1, 0, GETUTCDATE(), 1);
	SET IDENTITY_INSERT Holidays OFF;
END
GO

-- 11. Employees Seed Data (Sri Lankan Employees)
IF NOT EXISTS (SELECT 1 FROM Employees WHERE Id = 1)
BEGIN
	SET IDENTITY_INSERT Employees ON;
	INSERT INTO Employees (Id, EmployeeCode, FirstName, LastName, Email, Phone, DateOfBirth, JoiningDate, Gender, Address, DepartmentId, DesignationId, BranchId, IsActive, IsDeleted, CreatedAt, CreatedBy) VALUES
	  (1,  'EMP001', 'Kasun',     'Perera',         'kasun.perera@company.lk',      '+94771234567', '1985-04-12', '2020-01-15', 'Male',   '123 Galle Road, Colombo 03',       1, 1,  1, 1, 0, GETUTCDATE(), 1),
	  (2,  'EMP002', 'Dilhani',   'Fernando',       'dilhani.fernando@company.lk',  '+94712345678', '1990-08-22', '2021-03-01', 'Female', '45 Kandy Road, Kiribathgoda',      3, 6,  1, 1, 0, GETUTCDATE(), 1),
	  (3,  'EMP003', 'Nuwan',     'Silva',          'nuwan.silva@company.lk',       '+94753456789', '1978-03-15', '2019-06-10', 'Male',   '78 High Level Road, Maharagama',   5, 10, 1, 1, 0, GETUTCDATE(), 1),
	  (4,  'EMP004', 'Ruwan',     'Jayasinghe',     'ruwan.jayasinghe@company.lk',  '+94784567890', '1988-11-05', '2021-09-15', 'Male',   '101 Peradeniya Road, Kandy',      2, 2,  2, 1, 0, GETUTCDATE(), 1),
	  (5,  'EMP005', 'Kaveesha',  'De Silva',       'kaveesha.desilva@company.lk',  '+94705678901', '1993-02-18', '2022-02-01', 'Female', '202 Temple Road, Kandy',          2, 3,  2, 1, 0, GETUTCDATE(), 1),
	  (6,  'EMP006', 'Pathum',    'Wickramasinghe', 'pathum.w@company.lk',          '+94766789012', '1995-07-30', '2022-08-15', 'Male',   '303 Main Street, Galle Fort',     2, 4,  3, 1, 0, GETUTCDATE(), 1),
	  (7,  'EMP007', 'Tharushi',  'Rajapaksha',     'tharushi.r@company.lk',        '+94727890123', '1992-09-14', '2023-01-10', 'Female', '404 Matara Road, Galle',          2, 5,  3, 1, 0, GETUTCDATE(), 1),
	  (8,  'EMP008', 'Malith',    'Gunawardena',    'malith.g@company.lk',          '+94778901234', '1986-12-01', '2020-11-01', 'Male',   '505 Beach Road, Negombo',         4, 8,  1, 1, 0, GETUTCDATE(), 1),
	  (9,  'EMP009', 'Sanduni',   'Herath',         'sanduni.h@company.lk',         '+94719012345', '1991-05-25', '2022-04-15', 'Female', '606 Kurunegala Road, Kurunegala',  4, 9,  1, 1, 0, GETUTCDATE(), 1),
	  (10, 'EMP010', 'Dinesh',    'Ratnayake',      'dinesh.r@company.lk',          '+94750123456', '1994-10-10', '2023-05-01', 'Male',   '707 Hospital Road, Jaffna',       7, 11, 4, 1, 0, GETUTCDATE(), 1);
	SET IDENTITY_INSERT Employees OFF;
END
GO

-- 12. Users Seed Data (Default Password for all seeded users: Admin@123)
-- Hash: $2a$12$nO.92cNvowz6VFNOT/FerO6E0f.7B9VFm3ziDnofYlZi4NWOgJ3nW
IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'admin')
BEGIN
	INSERT INTO Users (Username, PasswordHash, Email, FullName, RoleId, EmployeeId, IsActive, IsLocked, FailedLoginAttempts, IsDeleted, CreatedAt, CreatedBy) VALUES
	  ('admin',        '$2a$12$nO.92cNvowz6VFNOT/FerO6E0f.7B9VFm3ziDnofYlZi4NWOgJ3nW', 'admin@company.lk',             'System Administrator', 1, 1, 1, 0, 0, 0, GETUTCDATE(), 1),
	  ('hr.manager',   '$2a$12$nO.92cNvowz6VFNOT/FerO6E0f.7B9VFm3ziDnofYlZi4NWOgJ3nW', 'dilhani.fernando@company.lk', 'Dilhani Fernando',     2, 2, 1, 0, 0, 0, GETUTCDATE(), 1),
	  ('manager',      '$2a$12$nO.92cNvowz6VFNOT/FerO6E0f.7B9VFm3ziDnofYlZi4NWOgJ3nW', 'nuwan.silva@company.lk',       'Nuwan Silva',          3, 3, 1, 0, 0, 0, GETUTCDATE(), 1),
	  ('supervisor',   '$2a$12$nO.92cNvowz6VFNOT/FerO6E0f.7B9VFm3ziDnofYlZi4NWOgJ3nW', 'dinesh.r@company.lk',          'Dinesh Ratnayake',     4, 10, 1, 0, 0, 0, GETUTCDATE(), 1),
	  ('employee',     '$2a$12$nO.92cNvowz6VFNOT/FerO6E0f.7B9VFm3ziDnofYlZi4NWOgJ3nW', 'pathum.w@company.lk',          'Pathum Wickramasinghe', 5, 6, 1, 0, 0, 0, GETUTCDATE(), 1),
	  ('r.jayasinghe', '$2a$12$nO.92cNvowz6VFNOT/FerO6E0f.7B9VFm3ziDnofYlZi4NWOgJ3nW', 'ruwan.jayasinghe@company.lk',  'Ruwan Jayasinghe',     3, 4, 1, 0, 0, 0, GETUTCDATE(), 1),
	  ('k.desilva',    '$2a$12$nO.92cNvowz6VFNOT/FerO6E0f.7B9VFm3ziDnofYlZi4NWOgJ3nW', 'kaveesha.desilva@company.lk',  'Kaveesha De Silva',    5, 5, 1, 0, 0, 0, GETUTCDATE(), 1),
	  ('t.rajapaksha', '$2a$12$nO.92cNvowz6VFNOT/FerO6E0f.7B9VFm3ziDnofYlZi4NWOgJ3nW', 'tharushi.r@company.lk',        'Tharushi Rajapaksha',  5, 7, 1, 0, 0, 0, GETUTCDATE(), 1);
END
GO

-- 13. Employee Shifts Assignments
IF NOT EXISTS (SELECT 1 FROM EmployeeShifts WHERE Id = 1)
BEGIN
	INSERT INTO EmployeeShifts (EmployeeId, ShiftId, EffectiveFrom, EffectiveTo, IsDeleted, CreatedAt, CreatedBy) VALUES
	  (1,  1, '2026-01-01', NULL, 0, GETUTCDATE(), 1),
	  (2,  1, '2026-01-01', NULL, 0, GETUTCDATE(), 1),
	  (3,  1, '2026-01-01', NULL, 0, GETUTCDATE(), 1),
	  (4,  1, '2026-01-01', NULL, 0, GETUTCDATE(), 1),
	  (5,  1, '2026-01-01', NULL, 0, GETUTCDATE(), 1),
	  (6,  1, '2026-01-01', NULL, 0, GETUTCDATE(), 1),
	  (7,  1, '2026-01-01', NULL, 0, GETUTCDATE(), 1),
	  (8,  1, '2026-01-01', NULL, 0, GETUTCDATE(), 1),
	  (9,  1, '2026-01-01', NULL, 0, GETUTCDATE(), 1),
	  (10, 2, '2026-01-01', NULL, 0, GETUTCDATE(), 1);
END
GO

-- 14. Sample Leave Requests
IF NOT EXISTS (SELECT 1 FROM LeaveRequests WHERE Id = 1)
BEGIN
	INSERT INTO LeaveRequests (EmployeeId, LeaveTypeId, FromDate, ToDate, TotalDays, Reason, Status, ApprovedBy, ApprovedAt, IsDeleted, CreatedAt, CreatedBy) VALUES
	  (5, 3, '2026-07-10', '2026-07-12', 3, 'Family emergency and personal leave', 2, 2, GETUTCDATE(), 0, GETUTCDATE(), 1),
	  (6, 1, '2026-08-10', '2026-08-15', 6, 'Annual summer vacation',              1, 2, NULL,          0, GETUTCDATE(), 1),
	  (7, 2, '2026-07-20', '2026-07-21', 2, 'Medical checkup and recovery',       2, 2, GETUTCDATE(), 0, GETUTCDATE(), 1);
END
GO

-- 15. Sample Attendance Logs (Demo Data for Dashboard Visuals)
IF NOT EXISTS (SELECT 1 FROM AttendanceLogs WHERE Id = 1)
BEGIN
	INSERT INTO AttendanceLogs (EmployeeId, AttendanceDate, CheckIn, CheckOut, Status, IsLate, IsEarlyLeave, LateMinutes, EarlyLeaveMinutes, WorkingHours, Remarks, IsManual, IsDeleted, CreatedAt, CreatedBy) VALUES
	  -- Employee 1 (Kasun Perera - Present)
	  (1, '2026-08-01', '2026-08-01 08:25:00', '2026-08-01 17:05:00', 1, 0, 0, 0, 0, 8.67, 'On time', 0, 0, GETUTCDATE(), 1),
	  (1, '2026-08-02', '2026-08-02 08:28:00', '2026-08-02 17:00:00', 1, 0, 0, 0, 0, 8.53, 'On time', 0, 0, GETUTCDATE(), 1),
	  
	  -- Employee 4 (Ruwan Jayasinghe - Present / Late)
	  (4, '2026-08-01', '2026-08-01 08:30:00', '2026-08-01 17:00:00', 1, 0, 0, 0, 0, 8.50, 'On time', 0, 0, GETUTCDATE(), 1),
	  (4, '2026-08-02', '2026-08-02 08:55:00', '2026-08-02 17:15:00', 3, 1, 0, 25, 0, 8.33, 'Traffic delay', 0, 0, GETUTCDATE(), 1),
	  
	  -- Employee 5 (Kaveesha De Silva - Present)
	  (5, '2026-08-01', '2026-08-01 08:20:00', '2026-08-01 17:10:00', 1, 0, 0, 0, 0, 8.83, 'On time', 0, 0, GETUTCDATE(), 1),
	  (5, '2026-08-02', '2026-08-02 08:22:00', '2026-08-02 17:00:00', 1, 0, 0, 0, 0, 8.63, 'On time', 0, 0, GETUTCDATE(), 1),
	  
	  -- Employee 6 (Pathum Wickramasinghe - Present / On Leave)
	  (6, '2026-08-01', '2026-08-01 08:35:00', '2026-08-01 17:00:00', 1, 0, 0, 0, 0, 8.42, 'On time', 0, 0, GETUTCDATE(), 1),
	  (6, '2026-08-02', NULL,                  NULL,                  5, 0, 0, 0, 0, 0.00, 'On Approved Casual Leave', 0, 0, GETUTCDATE(), 1);
END
GO

-- 16. Sample Attendance Monthly Summaries
IF NOT EXISTS (SELECT 1 FROM AttendanceSummaries WHERE Id = 1)
BEGIN
	INSERT INTO AttendanceSummaries (EmployeeId, Month, Year, TotalDays, PresentDays, AbsentDays, LateDays, LeaveDays, HolidayDays, TotalWorkingHours, IsDeleted, CreatedAt, CreatedBy) VALUES
	  (1, 7, 2026, 22, 22, 0, 0, 0, 1, 187.0, 0, GETUTCDATE(), 1),
	  (2, 7, 2026, 22, 21, 0, 1, 1, 1, 178.5, 0, GETUTCDATE(), 1),
	  (3, 7, 2026, 22, 22, 0, 0, 0, 1, 187.0, 0, GETUTCDATE(), 1),
	  (4, 7, 2026, 22, 20, 1, 2, 1, 1, 170.0, 0, GETUTCDATE(), 1),
	  (5, 7, 2026, 22, 19, 0, 0, 3, 1, 161.5, 0, GETUTCDATE(), 1),
	  (6, 7, 2026, 22, 21, 1, 1, 0, 1, 178.5, 0, GETUTCDATE(), 1),
	  (7, 7, 2026, 22, 20, 0, 0, 2, 1, 170.0, 0, GETUTCDATE(), 1);
END
GO

PRINT 'Master data seeding with Sri Lanka context completed successfully.';

