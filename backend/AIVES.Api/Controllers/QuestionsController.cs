using AIVES.Api.Features.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIVES.Api.Controllers;

[ApiController, Authorize(Roles = "Lecturer"), Route("api/[controller]")]
public class QuestionsController(QuestionQueryService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? courseId, [FromQuery] string? difficulty,
        [FromQuery] string? status, [FromQuery] string? bloomLevel, [FromQuery] string? q,
        CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 30) =>
        Data(await service.ListAsync(UserId, courseId, difficulty, status, bloomLevel, q, page, pageSize, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) => Data(await service.DetailAsync(id, UserId, ct));
}
