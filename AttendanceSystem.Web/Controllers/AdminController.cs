using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers;

[Route("Admin")]
[ApiExplorerSettings(IgnoreApi = true)]
public class AdminController : BaseController
{
    private readonly ILogger<AdminController> _logger;

    public AdminController(ILogger<AdminController> logger) => _logger = logger;

    private IActionResult? Auth() => RequireAuth();

    [HttpGet("")] [HttpGet("Index")]
    public IActionResult Index()      { var a = Auth(); if (a != null) return a; return View(); }

    [HttpGet("Departments")]
    public IActionResult Departments(){ var a = Auth(); if (a != null) return a; return View(); }

    [HttpGet("Designations")]
    public IActionResult Designations(){ var a = Auth(); if (a != null) return a; return View(); }

    [HttpGet("Branches")]
    public IActionResult Branches()   { var a = Auth(); if (a != null) return a; return View(); }

    [HttpGet("Shifts")]
    public IActionResult Shifts()     { var a = Auth(); if (a != null) return a; return View(); }

    [HttpGet("Employees")]
    public IActionResult Employees()  { var a = Auth(); if (a != null) return a; return View(); }

    [HttpGet("Attendance")]
    public IActionResult Attendance() { var a = Auth(); if (a != null) return a; return View(); }

    [HttpGet("Leave")]
    public IActionResult Leave()      { var a = Auth(); if (a != null) return a; return View(); }

    [HttpGet("Holidays")]
    public IActionResult Holidays()   { var a = Auth(); if (a != null) return a; return View(); }

    [HttpGet("Users")]
    public IActionResult Users()      { var a = Auth(); if (a != null) return a; return View(); }

    [HttpGet("Roles")]
    public IActionResult Roles()      { var a = Auth(); if (a != null) return a; return View(); }

    [HttpGet("Reports")]
    public IActionResult Reports()    { var a = Auth(); if (a != null) return a; return View(); }

    [HttpGet("Import")]
    public IActionResult Import()     { var a = Auth(); if (a != null) return a; return View(); }

    [HttpGet("Settings")]
    public IActionResult Settings()   { var a = Auth(); if (a != null) return a; return View(); }
}

