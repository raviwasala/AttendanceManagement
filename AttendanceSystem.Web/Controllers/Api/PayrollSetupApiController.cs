using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Filters;
using Modules = AttendanceSystem.Common.Constants.AppConstants.Modules;
using Actions = AttendanceSystem.Common.Constants.AppConstants.Actions;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Web.Controllers.Api;

/// <summary>
/// Payroll master data.
///
/// Reads need PayrollSetup.View; writes need Edit, and deletes need Delete. The split matters
/// more here than on most screens: a salary structure is company-wide policy, and the people
/// allowed to read it are a wider set than those allowed to change what everyone is paid.
/// </summary>
[Route("api/payroll-setup")]
[SessionAuthorize]
public class PayrollSetupApiController : ApiControllerBase
{
    private readonly IPayrollSetupService _svc;
    public PayrollSetupApiController(IPayrollSetupService svc) => _svc = svc;

    private IActionResult Out<T>(AttendanceSystem.Common.Models.Result<T> r) =>
        r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);

    private IActionResult Out(AttendanceSystem.Common.Models.Result r) =>
        r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);

    // ── Banks ─────────────────────────────────────────────────────────────────

    [HttpGet("banks")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.View)]
    public async Task<IActionResult> Banks() => Out(await _svc.GetBanksAsync());

    [HttpPost("banks")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.Edit)]
    public async Task<IActionResult> SaveBank([FromBody] SaveBankDto dto) =>
        !ModelState.IsValid ? BadRequest(ModelState) : Out(await _svc.SaveBankAsync(dto));

    [HttpDelete("banks/{id:int}")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.Delete)]
    public async Task<IActionResult> DeleteBank(int id) => Out(await _svc.DeleteBankAsync(id));

    // ── Bank branches ─────────────────────────────────────────────────────────

    [HttpGet("bank-branches")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.View)]
    public async Task<IActionResult> BankBranches([FromQuery] int? bankId) =>
        Out(await _svc.GetBankBranchesAsync(bankId));

    [HttpPost("bank-branches")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.Edit)]
    public async Task<IActionResult> SaveBankBranch([FromBody] SaveBankBranchDto dto) =>
        !ModelState.IsValid ? BadRequest(ModelState) : Out(await _svc.SaveBankBranchAsync(dto));

    [HttpDelete("bank-branches/{id:int}")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.Delete)]
    public async Task<IActionResult> DeleteBankBranch(int id) => Out(await _svc.DeleteBankBranchAsync(id));

    // ── Grades ────────────────────────────────────────────────────────────────

    [HttpGet("grades")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.View)]
    public async Task<IActionResult> Grades() => Out(await _svc.GetGradesAsync());

    [HttpPost("grades")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.Edit)]
    public async Task<IActionResult> SaveGrade([FromBody] SaveSalaryGradeDto dto) =>
        !ModelState.IsValid ? BadRequest(ModelState) : Out(await _svc.SaveGradeAsync(dto));

    [HttpDelete("grades/{id:int}")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.Delete)]
    public async Task<IActionResult> DeleteGrade(int id) => Out(await _svc.DeleteGradeAsync(id));

    // ── Groups ────────────────────────────────────────────────────────────────

    [HttpGet("groups")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.View)]
    public async Task<IActionResult> Groups() => Out(await _svc.GetGroupsAsync());

    [HttpPost("groups")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.Edit)]
    public async Task<IActionResult> SaveGroup([FromBody] SaveSalaryGroupDto dto) =>
        !ModelState.IsValid ? BadRequest(ModelState) : Out(await _svc.SaveGroupAsync(dto));

    [HttpDelete("groups/{id:int}")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.Delete)]
    public async Task<IActionResult> DeleteGroup(int id) => Out(await _svc.DeleteGroupAsync(id));

    // ── Sub-departments ───────────────────────────────────────────────────────

    [HttpGet("sub-departments")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.View)]
    public async Task<IActionResult> SubDepartments([FromQuery] int? departmentId) =>
        Out(await _svc.GetSubDepartmentsAsync(departmentId));

    [HttpPost("sub-departments")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.Edit)]
    public async Task<IActionResult> SaveSubDepartment([FromBody] SaveSubDepartmentDto dto) =>
        !ModelState.IsValid ? BadRequest(ModelState) : Out(await _svc.SaveSubDepartmentAsync(dto));

    [HttpDelete("sub-departments/{id:int}")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.Delete)]
    public async Task<IActionResult> DeleteSubDepartment(int id) =>
        Out(await _svc.DeleteSubDepartmentAsync(id));

    // ── Components ────────────────────────────────────────────────────────────

    [HttpGet("components")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.View)]
    public async Task<IActionResult> Components() => Out(await _svc.GetComponentsAsync());

    [HttpPost("components")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.Edit)]
    public async Task<IActionResult> SaveComponent([FromBody] SaveSalaryComponentDto dto) =>
        !ModelState.IsValid ? BadRequest(ModelState) : Out(await _svc.SaveComponentAsync(dto));

    [HttpDelete("components/{id:int}")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.Delete)]
    public async Task<IActionResult> DeleteComponent(int id) => Out(await _svc.DeleteComponentAsync(id));

    // ── Employment categories ─────────────────────────────────────────────────

    [HttpGet("categories")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.View)]
    public async Task<IActionResult> Categories() => Out(await _svc.GetCategoriesAsync());

    [HttpPost("categories")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.Edit)]
    public async Task<IActionResult> SaveCategory([FromBody] SaveEmploymentCategoryDto dto) =>
        !ModelState.IsValid ? BadRequest(ModelState) : Out(await _svc.SaveCategoryAsync(dto));

    [HttpDelete("categories/{id:int}")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.Delete)]
    public async Task<IActionResult> DeleteCategory(int id) => Out(await _svc.DeleteCategoryAsync(id));

    // ── Loan types ────────────────────────────────────────────────────────────

    [HttpGet("loan-types")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.View)]
    public async Task<IActionResult> LoanTypes() => Out(await _svc.GetLoanTypesAsync());

    [HttpPost("loan-types")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.Edit)]
    public async Task<IActionResult> SaveLoanType([FromBody] SaveLoanTypeDto dto) =>
        !ModelState.IsValid ? BadRequest(ModelState) : Out(await _svc.SaveLoanTypeAsync(dto));

    [HttpDelete("loan-types/{id:int}")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.Delete)]
    public async Task<IActionResult> DeleteLoanType(int id) => Out(await _svc.DeleteLoanTypeAsync(id));

    // ── Branch payroll parameters ─────────────────────────────────────────────

    [HttpGet("branch-settings/{branchId:int}")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.View)]
    public async Task<IActionResult> BranchSettings(int branchId) =>
        Out(await _svc.GetBranchSettingsAsync(branchId));

    [HttpPost("branch-settings")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.Edit)]
    public async Task<IActionResult> SaveBranchSettings([FromBody] SaveBranchPayrollSettingsDto dto) =>
        !ModelState.IsValid ? BadRequest(ModelState) : Out(await _svc.SaveBranchSettingsAsync(dto));

    // ── Third-party deductions ────────────────────────────────────────────────

    [HttpGet("third-parties")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.View)]
    public async Task<IActionResult> ThirdParties() => Out(await _svc.GetThirdPartiesAsync());

    [HttpPost("third-parties")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.Edit)]
    public async Task<IActionResult> SaveThirdParty([FromBody] SaveThirdPartyDto dto) =>
        !ModelState.IsValid ? BadRequest(ModelState) : Out(await _svc.SaveThirdPartyAsync(dto));

    [HttpDelete("third-parties/{id:int}")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.Delete)]
    public async Task<IActionResult> DeleteThirdParty(int id) => Out(await _svc.DeleteThirdPartyAsync(id));

    // ── APIT tables ───────────────────────────────────────────────────────────

    [HttpGet("apit-tables")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.View)]
    public async Task<IActionResult> ApitTables() => Out(await _svc.GetApitTablesAsync());

    [HttpPost("apit-tables")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.Edit)]
    public async Task<IActionResult> SaveApitTable([FromBody] SaveApitTaxTableDto dto) =>
        !ModelState.IsValid ? BadRequest(ModelState) : Out(await _svc.SaveApitTableAsync(dto));

    // ── Statutory ─────────────────────────────────────────────────────────────

    [HttpGet("rates")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.View)]
    public async Task<IActionResult> Rates() => Out(await _svc.GetRatesAsync());

    [HttpPost("rates")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.Edit)]
    public async Task<IActionResult> SaveRate([FromBody] SaveEpfEtfRateDto dto) =>
        !ModelState.IsValid ? BadRequest(ModelState) : Out(await _svc.SaveRateAsync(dto));

    [HttpGet("apit")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.View)]
    public async Task<IActionResult> Apit() => Out(await _svc.GetApitBracketsAsync());

    [HttpPost("apit")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.Edit)]
    public async Task<IActionResult> SaveApit([FromBody] SaveApitBracketDto dto) =>
        !ModelState.IsValid ? BadRequest(ModelState) : Out(await _svc.SaveApitBracketAsync(dto));

    [HttpDelete("apit/{id:int}")]
    [SessionAuthorize(Modules.PayrollSetup, Actions.Delete)]
    public async Task<IActionResult> DeleteApit(int id) => Out(await _svc.DeleteApitBracketAsync(id));
}
