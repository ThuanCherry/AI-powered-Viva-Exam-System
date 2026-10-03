using AIVES.BusinessLogicLayer.Interfaces;
using AIVES.DataAccessLayer.Models;
using AIVES.PresentationLayer.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AIVES.PresentationLayer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Get all users.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _userService.GetAllAsync();
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Get user by ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _userService.GetByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Register a new user.
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterViewModel model)
    {
        var user = new User
        {
            FullName = model.FullName,
            Email = model.Email,
            Role = model.Role
        };

        var result = await _userService.CreateAsync(user, model.Password);
        return result.Success
            ? CreatedAtAction(nameof(GetById), new { id = result.Data!.UserId }, result)
            : BadRequest(result);
    }

    /// <summary>
    /// Login user.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginViewModel model)
    {
        var result = await _userService.LoginAsync(model.Email, model.Password);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    /// <summary>
    /// Deactivate a user.
    /// </summary>
    [HttpPut("{id}/deactivate")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var result = await _userService.DeactivateAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
