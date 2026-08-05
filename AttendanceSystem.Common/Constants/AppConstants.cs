namespace AttendanceSystem.Common.Constants;

public static class AppConstants
{
    public const string AppName = "Attendance Management System";
    public const string AppVersion = "1.0.0";
    public const int MaxLoginAttempts = 5;
    public const int SessionTimeoutMinutes = 60;

    public static class Modules
    {
        public const string Dashboard = "Dashboard";
        public const string Employees = "Employees";
        public const string Departments = "Departments";
        public const string Designations = "Designations";
        public const string Branches = "Branches";
        public const string Shifts = "Shifts";
        public const string Attendance = "Attendance";
        public const string Leave = "Leave";
        public const string Holidays = "Holidays";
        public const string Reports = "Reports";
        public const string Users = "Users";
        public const string Roles = "Roles";
        public const string Settings = "Settings";
        public const string Import = "Import";
        public const string Devices = "Devices";
        public const string AuditLogs = "AuditLogs";
        public const string Overtime = "Overtime";
    }

    public static class Actions
    {
        public const string View = "View";
        public const string Create = "Create";
        public const string Edit = "Edit";
        public const string Delete = "Delete";
        public const string Export = "Export";
        public const string Approve = "Approve";

        /// <summary>Triggering a device synchronisation — separate from Edit so an operator
        /// can be allowed to pull attendance without being able to reconfigure hardware.</summary>
        public const string Sync = "Sync";
    }

    public static class DateFormats
    {
        public const string Display = "dd-MMM-yyyy";
        public const string TimeDisplay = "hh:mm tt";
        public const string DateTimeDisplay = "dd-MMM-yyyy hh:mm tt";
        public const string MonthYear = "MMM-yyyy";
    }
}
