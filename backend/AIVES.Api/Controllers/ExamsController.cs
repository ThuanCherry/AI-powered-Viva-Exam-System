using AIVES.Api.Features.Exams;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIVES.Api.Controllers;

[ApiController, Authorize(Roles = "Lecturer,Student"), Route("api/[controller]")]
public class ExamsController(ExamQueryService service) : ApiControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) => Data(await service.DetailAsync(id, Caller, ct));

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct, [FromQuery] int? classId = null) =>
        Data(await service.ListAsync(Caller, classId, ct));
}
