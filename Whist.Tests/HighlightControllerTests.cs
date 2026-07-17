using System.Security.Claims;
using Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using ServerAPI.Controllers;
using ServerAPI.Repositories.Highlights;
using ServerAPI.Storage;

namespace Whist.Tests;

public sealed class HighlightControllerTests
{
    [Fact]
    public async Task Create_PreservesRequestedHighlightDate()
    {
        var repository = new FakeHighlightRepository(null);
        var controller = CreateController(repository, userId: 1);
        var requestedDate = new DateTime(2024, 5, 6);

        var result = await controller.Create(new SaveHighlightRequest
        {
            Title = "Historisk highlight",
            Description = "Skal beholde datoen",
            Date = requestedDate
        });

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var created = Assert.IsType<Highlight>(createdResult.Value);
        Assert.Equal(requestedDate, created.Date);
        Assert.Equal(requestedDate, repository.AddedHighlight?.Date);
    }

    [Fact]
    public async Task Update_ForbidsNonOwnerPrivateHighlight()
    {
        var repository = new FakeHighlightRepository(new Highlight
        {
            Id = 10,
            UserId = 2,
            IsPrivate = true,
            Title = "Privat",
            Description = "Privat",
            Date = DateTime.Today
        });
        var controller = CreateController(repository, userId: 1);

        var result = await controller.Update(10, new SaveHighlightRequest
        {
            Title = "Ny titel",
            Description = "Ny beskrivelse",
            Date = DateTime.Today
        }, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.False(repository.UpdateCalled);
    }

    [Fact]
    public async Task Delete_ForbidsNonOwnerPrivateHighlight()
    {
        var repository = new FakeHighlightRepository(new Highlight
        {
            Id = 10,
            UserId = 2,
            IsPrivate = true,
            Title = "Privat",
            Description = "Privat",
            Date = DateTime.Today
        });
        var controller = CreateController(repository, userId: 1);

        var result = await controller.Delete(10, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.False(repository.DeleteCalled);
    }

    private static HighlightController CreateController(FakeHighlightRepository repository, int userId)
    {
        var controller = new HighlightController(
            repository,
            new FakeImageStorageService(),
            NullLogger<HighlightController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim("sub", userId.ToString())],
                    "Test"))
            }
        };

        return controller;
    }

    private sealed class FakeHighlightRepository : IHighlightRepository
    {
        private readonly Highlight? _highlight;

        public FakeHighlightRepository(Highlight? highlight)
        {
            _highlight = highlight;
        }

        public bool UpdateCalled { get; private set; }
        public bool DeleteCalled { get; private set; }
        public Highlight? AddedHighlight { get; private set; }

        public Task<IEnumerable<Highlight>> GetAll() => Task.FromResult<IEnumerable<Highlight>>(_highlight is null ? [] : [_highlight]);
        public Task<Highlight?> GetById(int id) => Task.FromResult(_highlight?.Id == id ? _highlight : null);
        public Task<PagedResult<Highlight>> GetPaged(int page, int pageSize, string? searchTerm = null, DateTime? fromDate = null, DateTime? toDate = null, bool includePrivate = true)
            => Task.FromResult(new PagedResult<Highlight>(_highlight is null ? [] : [_highlight], _highlight is null ? 0 : 1, page, pageSize));

        public Task<Highlight> Add(Highlight highlight)
        {
            AddedHighlight = highlight;
            return Task.FromResult(highlight);
        }

        public Task<bool> Delete(int id)
        {
            DeleteCalled = true;
            return Task.FromResult(true);
        }

        public Task<bool> Update(Highlight highlight)
        {
            UpdateCalled = true;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeImageStorageService : IImageStorageService
    {
        public Task<string> UploadImageAsync(IFormFile file, int? userId, CancellationToken cancellationToken = default)
            => Task.FromResult("https://example.test/image.webp");

        public Task<bool> TryDeleteImageAsync(string? imageUrl, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public bool IsAllowedImageUrl(string? imageUrl) => true;
    }
}
