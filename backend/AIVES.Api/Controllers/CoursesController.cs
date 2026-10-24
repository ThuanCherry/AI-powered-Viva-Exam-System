using AIVES.Api.Features.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIVES.Api.Controllers;

[ApiController, Authorize(Roles = "Admin,Lecturer,Student"), Route("api/[controller]")]
public class CoursesController(CourseCatalogService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct, [FromQuery] int? lecturerId = null) =>
        Data(await service.ListAsync(Caller, lecturerId, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) => Data(await service.DetailAsync(id, Caller, ct));

    [HttpPut("{id:int}/assign-lecturer"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignLecturer(int id, [FromBody] AssignLecturerRequest request, CancellationToken ct) =>
        Data(await service.AssignLecturerAsync(id, request.LecturerId, Caller, ct));
}

public record AssignLecturerRequest(int? LecturerId);
