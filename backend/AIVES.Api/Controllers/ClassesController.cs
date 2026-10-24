using AIVES.Api.Features.Catalog;
using AIVES.Api.Features.Lecturer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIVES.Api.Controllers;

[ApiController, Authorize(Roles = "Lecturer,Student"), Route("api/classes")]
public class ClassesController(ClassQueryService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct, [FromQuery] int? courseId = null) =>
        Data(await service.ListAsync(Caller, courseId, ct));
}

[ApiController, Authorize(Roles = "Lecturer,Student"), Route("api/documents")]
public class DocumentsController(DocumentStore service) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int courseId, CancellationToken ct) =>
        Data(await service.ListAsync(courseId, UserId, Caller.IsLecturer, ct));

    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        var file = await service.DownloadAsync(id, UserId, Caller.IsLecturer, ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return PhysicalFile(file.Path, file.ContentType, file.FileName, enableRangeProcessing: true);
    }
}
