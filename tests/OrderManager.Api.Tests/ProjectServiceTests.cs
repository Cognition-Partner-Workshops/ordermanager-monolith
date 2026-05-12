using Microsoft.EntityFrameworkCore;
using OrderManager.Api.Data;
using OrderManager.Api.Models;
using OrderManager.Api.Services;
using Xunit;

namespace OrderManager.Api.Tests;

public class ProjectServiceTests
{
    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new AppDbContext(options);
        SeedData.Initialize(context);
        return context;
    }

    [Fact]
    public async Task GetAllProjects_ReturnsSeedProjects()
    {
        using var context = CreateContext();
        var service = new ProjectService(context);
        var projects = await service.GetAllProjectsAsync();
        Assert.Equal(3, projects.Count);
    }

    [Fact]
    public async Task GetProjectById_ReturnsProject_WhenExists()
    {
        using var context = CreateContext();
        var service = new ProjectService(context);
        var existing = await context.Projects.FirstAsync();
        var project = await service.GetProjectByIdAsync(existing.Id);
        Assert.NotNull(project);
        Assert.Equal(existing.Name, project.Name);
        Assert.NotNull(project.Customer);
    }

    [Fact]
    public async Task GetProjectById_ReturnsNull_WhenNotExists()
    {
        using var context = CreateContext();
        var service = new ProjectService(context);
        var project = await service.GetProjectByIdAsync(9999);
        Assert.Null(project);
    }

    [Fact]
    public async Task CreateProject_AddsProjectToDatabase()
    {
        using var context = CreateContext();
        var service = new ProjectService(context);
        var customer = await context.Customers.FirstAsync();

        var project = new Project
        {
            Name = "New Project",
            Description = "Test description",
            CustomerId = customer.Id,
            StartDate = new DateTime(2024, 6, 1),
            Status = "Active"
        };

        var created = await service.CreateProjectAsync(project);
        Assert.True(created.Id > 0);
        Assert.Equal("New Project", created.Name);

        var allProjects = await service.GetAllProjectsAsync();
        Assert.Equal(4, allProjects.Count);
    }

    [Fact]
    public async Task UpdateProject_UpdatesFields_WhenExists()
    {
        using var context = CreateContext();
        var service = new ProjectService(context);
        var existing = await context.Projects.FirstAsync();

        var updated = new Project
        {
            Name = "Updated Name",
            Description = "Updated description",
            CustomerId = existing.CustomerId,
            StartDate = existing.StartDate,
            Status = "Completed"
        };

        var result = await service.UpdateProjectAsync(existing.Id, updated);
        Assert.NotNull(result);
        Assert.Equal("Updated Name", result.Name);
        Assert.Equal("Updated description", result.Description);
        Assert.Equal("Completed", result.Status);
    }

    [Fact]
    public async Task UpdateProject_ReturnsNull_WhenNotExists()
    {
        using var context = CreateContext();
        var service = new ProjectService(context);

        var updated = new Project
        {
            Name = "Updated Name",
            Description = "Updated description",
            CustomerId = 1,
            StartDate = DateTime.UtcNow,
            Status = "Active"
        };

        var result = await service.UpdateProjectAsync(9999, updated);
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteProject_RemovesProject_WhenExists()
    {
        using var context = CreateContext();
        var service = new ProjectService(context);
        var existing = await context.Projects.FirstAsync();

        var deleted = await service.DeleteProjectAsync(existing.Id);
        Assert.True(deleted);

        var allProjects = await service.GetAllProjectsAsync();
        Assert.Equal(2, allProjects.Count);
    }

    [Fact]
    public async Task DeleteProject_ReturnsFalse_WhenNotExists()
    {
        using var context = CreateContext();
        var service = new ProjectService(context);

        var deleted = await service.DeleteProjectAsync(9999);
        Assert.False(deleted);
    }
}
