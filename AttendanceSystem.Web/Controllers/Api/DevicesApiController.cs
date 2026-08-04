using AttendanceSystem.Application.DTOs;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Web.Filters;
using Microsoft.AspNetCore.Mvc;
using Modules = AttendanceSystem.Common.Constants.AppConstants.Modules;
using Actions = AttendanceSystem.Common.Constants.AppConstants.Actions;

namespace AttendanceSystem.Web.Controllers.Api;

/// <summary>
/// Fingerprint device registry.
///
/// Test is gated on View rather than Edit: diagnosing a device is what an operator does before
/// escalating, and it changes no configuration. Sync has its own action so pulling attendance
/// can be granted without granting the ability to reconfigure hardware.
/// </summary>
[Route("api/devices")]
[SessionAuthorize]
public class DevicesApiController : ApiControllerBase
{
    private readonly IDeviceService _devices;

    public DevicesApiController(IDeviceService devices) => _devices = devices;

    [HttpGet]
    [SessionAuthorize(Modules.Devices, Actions.View)]
    public async Task<IActionResult> GetAll()
    {
        var r = await _devices.GetAllAsync();
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpGet("{id}")]
    [SessionAuthorize(Modules.Devices, Actions.View)]
    public async Task<IActionResult> GetById(int id)
    {
        var r = await _devices.GetByIdAsync(id);
        return r.IsSuccess ? Ok(r.Data) : NotFound(r.ErrorMessage);
    }

    [HttpGet("branch/{branchId}")]
    [SessionAuthorize(Modules.Devices, Actions.View)]
    public async Task<IActionResult> GetByBranch(int branchId)
    {
        var r = await _devices.GetByBranchAsync(branchId);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPost]
    [SessionAuthorize(Modules.Devices, Actions.Create)]
    public async Task<IActionResult> Create([FromBody] SaveDeviceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // Create and update are separate endpoints so their permissions can differ; the
        // service handles both, keyed on Id.
        dto.Id = 0;
        var r = await _devices.SaveAsync(dto);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpPut("{id}")]
    [SessionAuthorize(Modules.Devices, Actions.Edit)]
    public async Task<IActionResult> Update(int id, [FromBody] SaveDeviceDto dto)
    {
        dto.Id = id;
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var r = await _devices.SaveAsync(dto);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }

    [HttpDelete("{id}")]
    [SessionAuthorize(Modules.Devices, Actions.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var r = await _devices.DeleteAsync(id);
        return r.IsSuccess ? Ok() : BadRequest(r.ErrorMessage);
    }

    [HttpPost("{id}/test")]
    [SessionAuthorize(Modules.Devices, Actions.View)]
    public async Task<IActionResult> TestConnection(int id, CancellationToken ct)
    {
        var r = await _devices.TestConnectionAsync(id, ct);
        return r.IsSuccess ? Ok(r.Data) : BadRequest(r.ErrorMessage);
    }
}
