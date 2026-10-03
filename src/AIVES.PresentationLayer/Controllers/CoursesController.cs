using AIVES.BusinessLogicLayer.Interfaces;
using AIVES.DataAccessLayer.Models;
using Microsoft.AspNetCore.Mvc;

namespace AIVES.PresentationLayer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CoursesController : ControllerBase
{
    private readonly ICourseService _courseService;

    public CoursesController(ICourseService courseService)
    {
        _courseService = courseService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _courseService.GetAllAsync();
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _courseService.GetByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Course course)
    {
        var result = await _courseService.CreateAsync(course);
        return result.Success
            ? CreatedAtAction(nameof(GetById), new { id = result.Data!.CourseId }, result)
            : BadRequest(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _courseService.DeleteAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
