using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;
using AgyAccountSwarm.Services;
using Xunit;

namespace AgyAccountSwarm.Tests;

public class SwarmProjectCrudTests : IDisposable
{
    private readonly string _tempTestDir;

    public SwarmProjectCrudTests()
    {
        _tempTestDir = Path.Combine(Path.GetTempPath(), "SwarmProjectCrudTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempTestDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempTestDir))
            {
                Directory.Delete(_tempTestDir, true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Theory]
    [InlineData("My First Swarm Project", "my-first-swarm-project")]
    [InlineData("Rust:API*v1/backend?", "rustapiv1backend")]
    [InlineData("  Project   With   Spaces  ", "project---with---spaces")]
    [InlineData("", "default-project")]
    [InlineData("---", "project")]
    public void SanitizeProjectFolderName_ProducesSafeDirectoryNames(string input, string expected)
    {
        var result = SwarmProject.SanitizeProjectFolderName(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SwarmProject_Initialization_HasValidDefaults()
    {
        var project = new SwarmProject();

        Assert.False(string.IsNullOrWhiteSpace(project.Id));
        Assert.Equal("Rust", project.TechStack);
        Assert.Equal("main", project.DefaultBranch);
        Assert.True(project.UseGitWorktrees);
        Assert.NotNull(project.AssignedWorkerIds);
        Assert.Empty(project.AssignedWorkerIds);
        Assert.True(project.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void ProjectWorkerSelectionItem_DefaultsAndMapping()
    {
        var item = new ProjectWorkerSelectionItem
        {
            ProfileId = "prof-123",
            ProfileName = "Alice",
            AccountEmail = "alice@example.com",
            ColorTag = "#10B981",
            AvatarInitial = "A",
            Tier = "Pro",
            IsSelected = true
        };

        Assert.Equal("prof-123", item.ProfileId);
        Assert.Equal("Alice", item.ProfileName);
        Assert.Equal("alice@example.com", item.AccountEmail);
        Assert.True(item.IsSelected);
        Assert.Equal("Pro", item.Tier);
    }

    [Fact]
    public async Task StorageService_SaveAndLoadProjects_PreservesAllFields()
    {
        // Custom storage service targeting the temp directory
        var storage = new ProfileStorageService(_tempTestDir);

        var projects = new List<SwarmProject>
        {
            new()
            {
                Id = "proj-1",
                Name = "E-Commerce Core",
                Description = "Microservice architecture backend",
                TechStack = "C# / .NET 9",
                RootDirectory = Path.Combine(_tempTestDir, "ecommerce"),
                UseGitWorktrees = true,
                DefaultBranch = "develop",
                DefaultObjective = "Build high-throughput checkout pipeline",
                AssignedWorkerIds = new List<string> { "worker-a", "worker-b" },
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                LastActiveAt = DateTime.UtcNow
            },
            new()
            {
                Id = "proj-2",
                Name = "Crypto Vault",
                Description = "Zero-knowledge proofs module",
                TechStack = "Rust",
                RootDirectory = Path.Combine(_tempTestDir, "crypto-vault"),
                UseGitWorktrees = false,
                DefaultBranch = "main",
                DefaultObjective = "Audit cryptographic primitives",
                AssignedWorkerIds = new List<string> { "worker-c" },
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                LastActiveAt = null
            }
        };

        await storage.SaveProjectsAsync(projects);

        var loaded = (await storage.LoadProjectsAsync()).ToList();

        Assert.Equal(2, loaded.Count);

        var p1 = loaded.First(p => p.Id == "proj-1");
        Assert.Equal("E-Commerce Core", p1.Name);
        Assert.Equal("Microservice architecture backend", p1.Description);
        Assert.Equal("C# / .NET 9", p1.TechStack);
        Assert.True(p1.UseGitWorktrees);
        Assert.Equal("develop", p1.DefaultBranch);
        Assert.Equal(2, p1.AssignedWorkerIds.Count);
        Assert.Contains("worker-a", p1.AssignedWorkerIds);
        Assert.Contains("worker-b", p1.AssignedWorkerIds);

        var p2 = loaded.First(p => p.Id == "proj-2");
        Assert.Equal("Crypto Vault", p2.Name);
        Assert.Equal("Rust", p2.TechStack);
        Assert.False(p2.UseGitWorktrees);
        Assert.Single(p2.AssignedWorkerIds);
        Assert.Contains("worker-c", p2.AssignedWorkerIds);
    }

    [Fact]
    public async Task StorageService_LoadProjects_WhenNonExistent_ReturnsEmpty()
    {
        var storage = new ProfileStorageService(Path.Combine(_tempTestDir, "empty_dir"));
        var loaded = await storage.LoadProjectsAsync();

        Assert.NotNull(loaded);
        Assert.Empty(loaded);
    }
}
