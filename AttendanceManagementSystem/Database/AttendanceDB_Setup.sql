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
	Id             INT IDENTITY(1,1) PRIMARY KEY,
	EmployeeCode   NVARCHAR(50) NOT NULL,
	FirstName      NVARCHAR(100) NOT NULL,
	LastName       NVARCHAR(100) NOT NULL,
	Email          NVARCHAR(200) NULL,
	Phone          NVARCHAR(20) NULL,
	DateOfBirth    DATE NULL,
	JoiningDate    DATE NOT NULL,
	Gender         NVARCHAR(10) NULL,
	Address        NVARCHAR(500) NULL,
	Photo          VARBINARY(MAX) NULL,
	DepartmentId   INT NOT NULL,
	DesignationId  INT NOT NULL,
	BranchId       INT NOT NULL,
	IsActive       BIT NOT NULL DEFAULT 1,
	IsDeleted      BIT NOT NULL DEFAULT 0,
	CreatedAt      DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
	CreatedBy      INT NULL,
	ModifiedAt     DATETIME2 NULL,
	ModifiedBy     INT NULL,
	CONSTRAINT UQ_Employees_Code UNIQUE (EmployeeCode),
	CONSTRAINT FK_Employees_Department  FOREIGN KEY (DepartmentId)  REFERENCES Departments(Id),
	CONSTRAINT FK_Employees_Designation FOREIGN KEY (DesignationId) REFERENCES Designations(Id),
	CONSTRAINT FK_Employees_Branch      FOREIGN KEY (BranchId)      REFERENCES Branches(Id)
);
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
	RememberToken       NVARCHAR(200) NULL,
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

-- ── Seed Data ─────────────────────────────────────────────────────────────────
SET IDENTITY_INSERT Roles ON;
IF NOT EXISTS (SELECT 1 FROM Roles WHERE Id = 1)
INSERT INTO Roles (Id,Name,Description,IsDeleted,CreatedAt) VALUES
  (1,'Administrator','Full system access',0,GETUTCDATE()),
  (2,'HR Manager','HR module access',0,GETUTCDATE()),
  (3,'Employee','Self-service access',0,GETUTCDATE());
SET IDENTITY_INSERT Roles OFF;
GO

-- Admin user  (Password: Admin@123)
IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'admin')
INSERT INTO Users (Username,PasswordHash,Email,FullName,RoleId,IsActive,IsDeleted,CreatedAt)
VALUES ('admin','$2a$12$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi',
		'admin@company.com','System Administrator',1,1,0,GETUTCDATE());
GO

IF NOT EXISTS (SELECT 1 FROM Branches WHERE Id = 1)
BEGIN
	SET IDENTITY_INSERT Branches ON;
	INSERT INTO Branches (Id,Name,IsActive,IsDeleted,CreatedAt) VALUES (1,'Head Office',1,0,GETUTCDATE());
	SET IDENTITY_INSERT Branches OFF;
END
GO

IF NOT EXISTS (SELECT 1 FROM Departments WHERE Id = 1)
BEGIN
	SET IDENTITY_INSERT Departments ON;
	INSERT INTO Departments (Id,Name,IsActive,IsDeleted,CreatedAt) VALUES
	  (1,'Administration',1,0,GETUTCDATE()),
	  (2,'Information Technology',1,0,GETUTCDATE()),
	  (3,'Human Resources',1,0,GETUTCDATE()),
	  (4,'Finance',1,0,GETUTCDATE());
	SET IDENTITY_INSERT Departments OFF;
END
GO

IF NOT EXISTS (SELECT 1 FROM Designations WHERE Id = 1)
BEGIN
	SET IDENTITY_INSERT Designations ON;
	INSERT INTO Designations (Id,Name,IsActive,IsDeleted,CreatedAt) VALUES
	  (1,'Manager',1,0,GETUTCDATE()),
	  (2,'Software Engineer',1,0,GETUTCDATE()),
	  (3,'HR Executive',1,0,GETUTCDATE()),
	  (4,'Accountant',1,0,GETUTCDATE());
	SET IDENTITY_INSERT Designations OFF;
END
GO

IF NOT EXISTS (SELECT 1 FROM Shifts WHERE Id = 1)
BEGIN
	SET IDENTITY_INSERT Shifts ON;
	INSERT INTO Shifts (Id,Name,StartTime,EndTime,GraceMinutes,WeeklyOffDays,IsActive,IsDeleted,CreatedAt)
	VALUES (1,'General Shift','09:00:00','18:00:00',15,'Saturday,Sunday',1,0,GETUTCDATE());
	SET IDENTITY_INSERT Shifts OFF;
END
GO

IF NOT EXISTS (SELECT 1 FROM LeaveTypes WHERE Id = 1)
BEGIN
	SET IDENTITY_INSERT LeaveTypes ON;
	INSERT INTO LeaveTypes (Id,Name,TotalDays,IsPaid,IsActive,IsDeleted,CreatedAt) VALUES
	  (1,'Annual Leave',14,1,1,0,GETUTCDATE()),
	  (2,'Sick Leave',10,1,1,0,GETUTCDATE()),
	  (3,'Casual Leave',7,1,1,0,GETUTCDATE()),
	  (4,'Unpaid Leave',30,0,1,0,GETUTCDATE());
	SET IDENTITY_INSERT LeaveTypes OFF;
END
GO

IF NOT EXISTS (SELECT 1 FROM CompanySettings WHERE Id = 1)
BEGIN
	SET IDENTITY_INSERT CompanySettings ON;
	INSERT INTO CompanySettings (Id,CompanyName,WorkStartTime,WorkEndTime,WeekendDays,MaxLateMinutes,IsDeleted,CreatedAt)
	VALUES (1,'My Company Ltd.','09:00:00','18:00:00','Saturday,Sunday',15,0,GETUTCDATE());
	SET IDENTITY_INSERT CompanySettings OFF;
END
GO

-- Permissions seed
IF NOT EXISTS (SELECT 1 FROM Permissions)
BEGIN
	INSERT INTO Permissions (Module,Action,DisplayName,IsDeleted,CreatedAt) VALUES
	  ('Dashboard','View','View Dashboard',0,GETUTCDATE()),
	  ('Employees','View','View Employees',0,GETUTCDATE()),
	  ('Employees','Create','Create Employee',0,GETUTCDATE()),
	  ('Employees','Edit','Edit Employee',0,GETUTCDATE()),
	  ('Employees','Delete','Delete Employee',0,GETUTCDATE()),
	  ('Departments','View','View Departments',0,GETUTCDATE()),
	  ('Departments','Create','Create Department',0,GETUTCDATE()),
	  ('Departments','Edit','Edit Department',0,GETUTCDATE()),
	  ('Departments','Delete','Delete Department',0,GETUTCDATE()),
	  ('Attendance','View','View Attendance',0,GETUTCDATE()),
	  ('Attendance','Create','Mark Attendance',0,GETUTCDATE()),
	  ('Attendance','Edit','Edit Attendance',0,GETUTCDATE()),
	  ('Attendance','Delete','Delete Attendance',0,GETUTCDATE()),
	  ('Leave','View','View Leave',0,GETUTCDATE()),
	  ('Leave','Create','Apply Leave',0,GETUTCDATE()),
	  ('Leave','Approve','Approve/Reject Leave',0,GETUTCDATE()),
	  ('Holidays','View','View Holidays',0,GETUTCDATE()),
	  ('Holidays','Create','Create Holiday',0,GETUTCDATE()),
	  ('Reports','View','View Reports',0,GETUTCDATE()),
	  ('Reports','Export','Export Reports',0,GETUTCDATE()),
	  ('Users','View','View Users',0,GETUTCDATE()),
	  ('Users','Create','Create User',0,GETUTCDATE()),
	  ('Users','Edit','Edit User',0,GETUTCDATE()),
	  ('Users','Delete','Delete User',0,GETUTCDATE()),
	  ('Settings','View','View Settings',0,GETUTCDATE()),
	  ('Settings','Edit','Edit Settings',0,GETUTCDATE()),
	  ('AuditLogs','View','View Audit Logs',0,GETUTCDATE());

	-- Grant all permissions to Administrator role
	INSERT INTO RolePermissions (RoleId,PermissionId)
	SELECT 1, Id FROM Permissions;
END
GO

PRINT 'Database setup complete.';
