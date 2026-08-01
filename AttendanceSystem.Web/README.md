# Attendance Management System - Web Portal

## 📋 Overview

The **Attendance Management System Web Portal** is a modern ASP.NET Core MVC application that provides a comprehensive admin dashboard for managing attendance records, users, and biometric data. Built on .NET 10 with a responsive Bootstrap-based UI and a complete admin template.

## ✨ Features

### Admin Dashboard
- **Real-time Statistics**: Present/Absent/Late arrivals overview
- **Quick Actions**: Fast access to common operations
- **Recent Activity Feed**: Track system events and changes

### User Management
- Add, edit, and delete user accounts
- Assign departments and roles
- Bulk import user data
- User status tracking (Active/Inactive/Pending)

### Attendance Records
- View attendance records with detailed filtering
- Filter by date range, department, and employee
- Export attendance data to Excel/PDF
- Statistics dashboard (Present, Absent, Late, On Leave)

### Biometric Data Import
- Upload biometric data from Excel/CSV files
- Support for multiple import types:
  - Attendance Records
  - User Data
  - Biometric Templates
- Import history tracking
- Template download for standardized data format
- Real-time import progress tracking

### Reports & Analytics
- Multiple report types:
  - Attendance Summary
  - Individual Reports
  - Department Reports
  - Late Arrivals Report
  - Absent Report
- Interactive charts (Chart.js)
- Export to PDF and Excel
- Monthly statistics summaries

### System Settings
- **General Settings**: Timezone, working hours, system preferences
- **Database Settings**: Connection configuration and testing
- **Email Settings**: SMTP configuration for notifications
- **Biometric Settings**: Device configuration and sync intervals
- **Security Settings**: Password policies, session management, 2FA
- **Backup Settings**: Automated backup configuration and scheduling

## 🏗️ Project Structure

```
AttendanceSystem.Web/
├── Controllers/
│   ├── HomeController.cs
│   └── AdminController.cs
├── Views/
│   ├── Admin/
│   │   ├── Index.cshtml (Dashboard)
│   │   ├── Users.cshtml (User Management)
│   │   ├── Attendance.cshtml (Attendance Records)
│   │   ├── Import.cshtml (Data Import)
│   │   ├── Reports.cshtml (Reports & Analytics)
│   │   └── Settings.cshtml (System Settings)
│   ├── Home/
│   │   ├── Index.cshtml
│   │   └── Privacy.cshtml
│   └── Shared/
│       ├── _Layout.cshtml (Main Layout)
│       └── _ValidationScriptsPartial.cshtml
├── wwwroot/
│   ├── css/
│   │   └── site.css
│   ├── js/
│   │   └── site.js
│   └── lib/ (Bootstrap, jQuery)
├── Program.cs
├── appsettings.json
└── AttendanceSystem.Web.csproj
```

## 🛠️ Technology Stack

- **Framework**: ASP.NET Core (.NET 10)
- **View Engine**: Razor Pages with MVC Controllers
- **UI Framework**: Bootstrap 5
- **Icons**: Font Awesome 6.4.0
- **Charts**: Chart.js
- **Logging**: Serilog
- **Mapping**: AutoMapper
- **ORM**: Entity Framework Core 10
- **Database**: SQL Server

## 📦 NuGet Packages

- `AutoMapper` v16.2.0 - Object mapping
- `AutoMapper.Extensions.Microsoft.DependencyInjection` v12.0.1 - DI integration
- `Microsoft.EntityFrameworkCore` v10.0.10 - ORM
- `Microsoft.EntityFrameworkCore.SqlServer` v10.0.10 - SQL Server provider
- `Microsoft.EntityFrameworkCore.Design` v10.0.10 - EF tools
- `Serilog.AspNetCore` v10.0.0 - Structured logging for ASP.NET Core
- `Serilog.Sinks.File` v7.0.0 - File logging sink

## 🚀 Getting Started

### Prerequisites
- .NET 10 SDK installed
- SQL Server (local or remote)
- Visual Studio 2026 or later (or VS Code)

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/raviwasala/AttendanceManagement
   cd AttendanceManagementSystem
   ```

2. **Open the solution**
   ```bash
   Explorer AttendanceManagementSystem.slnx
   ```

3. **Restore NuGet packages**
   ```bash
   dotnet restore
   ```

4. **Update configuration**
   Edit `appsettings.json`:
   ```json
   {
	 "ConnectionStrings": {
	   "DefaultConnection": "Server=localhost;Database=AttendanceDB;Trusted_Connection=true;"
	 },
	 "Logging": {
	   "LogLevel": {
		 "Default": "Information"
	   }
	 }
   }
   ```

5. **Run migrations** (when DB context is configured)
   ```bash
   dotnet ef database update
   ```

6. **Start the application**
   - Press **F5** in Visual Studio, or
   - Run `dotnet run` from the command line

7. **Access the application**
   - Open browser to: `https://localhost:5001`
   - Navigate to `/Admin` for the admin dashboard

## 🔧 Configuration

### appsettings.json
```json
{
  "ConnectionStrings": {
	"DefaultConnection": "Server=.;Database=AttendanceDB;Trusted_Connection=true;"
  },
  "Logging": {
	"LogLevel": {
	  "Default": "Information",
	  "Microsoft.AspNetCore": "Warning"
	}
  }
}
```

### Serilog Logging
Logs are written to:
- **Console**: Real-time output
- **File**: `logs/attendance-web-YYYY-MM-DD.txt` (daily rolling)

## 📡 API Integration Points

The following services need to be integrated from your infrastructure layer:

```csharp
// Register in Program.cs when ready:
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IBiometricService, BiometricService>();
builder.Services.AddScoped<IReportService, ReportService>();
```

## 🎨 UI Components

### Admin Dashboard Components
- **Stat Cards**: KPI displays with gradient backgrounds
- **Quick Action Buttons**: Fast navigation to common tasks
- **Recent Activity Table**: System event log
- **Responsive Sidebar**: Navigation menu with icon indicators
- **Modal Dialogs**: Add/Edit forms

### Responsive Design
- Mobile-first approach
- Breakpoints: xs, sm, md, lg, xl
- Touch-friendly controls
- Collapsible navigation on small screens

## 🔐 Security Considerations

### Currently Configured
- HTTPS redirect
- HSTS headers
- CSRF protection (auto in MVC)

### To Be Implemented
- Role-based Authorization (Admin, Manager, Employee)
- User authentication
- Two-factor authentication
- SQL injection prevention (use EF Core parameterized queries)
- XSS protection
- Data encryption for sensitive fields

## 📊 Admin Pages Breakdown

| Page | Purpose | Key Features |
|------|---------|--------------|
| Dashboard | Overview of system state | KPIs, Quick actions, Activity log |
| Users | User account management | Add/Edit/Delete users, Status tracking |
| Attendance | View attendance records | Filter, Export, Statistics |
| Import | Biometric data import | File upload, Type selection, Progress |
| Reports | Analytics and reporting | Multiple report types, Charts, Export |
| Settings | System configuration | Database, Email, Security, Backup |

## 🧪 Development Tips

### Adding a New Admin Page

1. **Create Controller Action** in `AdminController.cs`
   ```csharp
   [HttpGet("NewPage")]
   public IActionResult NewPage()
   {
	   _logger.LogInformation("New page accessed");
	   return View();
   }
   ```

2. **Create View** in `Views/Admin/NewPage.cshtml`
   ```html
   @{
	   ViewData["Title"] = "New Page";
   }
   <div class="container-fluid mt-4">
	   <!-- Your content here -->
   </div>
   ```

3. **Add Sidebar Link** in `Views/Shared/_Layout.cshtml`
   ```html
   <li><a href="/Admin/NewPage"><i class="fas fa-icon"></i> New Page</a></li>
   ```

### Using AutoMapper

Configure mappings in a new `AutoMapper/MappingProfile.cs`:
```csharp
public class MappingProfile : Profile
{
	public MappingProfile()
	{
		CreateMap<UserEntity, UserDto>();
	}
}
```

### Logging

```csharp
_logger.LogInformation("User {UserId} accessed dashboard", userId);
_logger.LogWarning("Failed login attempt for user {Username}", username);
_logger.LogError(ex, "Error processing import for file {FileName}", fileName);
```

## 📝 Next Steps

1. **Implement Authentication**
   - Add ASP.NET Core Identity
   - Create login/logout flows

2. **Wire Infrastructure Services**
   - Implement `IAttendanceService`
   - Implement `IUserService`
   - Implement `IBiometricService`

3. **Database Integration**
   - Configure DbContext
   - Create migrations
   - Seed initial data

4. **API Endpoints**
   - Create API controllers for AJAX calls
   - Implement CRUD operations

5. **Testing**
   - Add unit tests
   - Add integration tests
   - Add E2E tests

## 📚 Resources

- [ASP.NET Core Docs](https://docs.microsoft.com/aspnet/core)
- [Bootstrap 5 Docs](https://getbootstrap.com/docs/5.0)
- [Font Awesome Icons](https://fontawesome.com/icons)
- [Chart.js Documentation](https://www.chartjs.org)
- [Entity Framework Core](https://docs.microsoft.com/ef/core)
- [Serilog Documentation](https://serilog.net)

## 🐛 Troubleshooting

### Build Issues
```bash
# Clean and rebuild
dotnet clean
dotnet build
```

### Package Issues
```bash
# Clear NuGet cache
nuget locals all -clear
dotnet restore
```

### Database Connection
Check `appsettings.json` for:
- Correct server name
- Database exists
- User has permissions
- Network connectivity

## 👨‍💼 Support

For issues, questions, or feature requests, please visit:
- [GitHub Issues](https://github.com/raviwasala/AttendanceManagement/issues)

## 📄 License

This project is part of the Attendance Management System.

---

**Version**: 1.0.0  
**Last Updated**: 2026  
**Target Framework**: .NET 10
