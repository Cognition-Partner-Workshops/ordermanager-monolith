using Microsoft.AspNetCore.Mvc;
using OrderManager.Api.Models;
using OrderManager.Api.Services;

namespace OrderManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly ProjectService _projectService;

    public ProjectsController(ProjectService projectService)
    {
        _projectService = projectService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _projectService.GetAllProjectsAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var project = await _projectService.GetProjectByIdAsync(id);
        return project is null ? NotFound() : Ok(project);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Project project)
    {
        var created = await _projectService.CreateProjectAsync(project);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Project project)
    {
        var updated = await _projectService.UpdateProjectAsync(id, project);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _projectService.DeleteProjectAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
