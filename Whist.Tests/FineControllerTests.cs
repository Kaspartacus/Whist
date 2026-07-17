using Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using ServerAPI.Controllers;
using ServerAPI.Repositories.Fines;

namespace Whist.Tests;

public sealed class FineControllerTests
{
    [Fact]
    public async Task Add_ReturnsBadRequestWhenTargetUserDoesNotExist()
    {
        var repository = new FakeFineRepository { AddResult = false };
        var controller = CreateController(repository);

        var result = await controller.Add(new SaveFineRequest
        {
            UserId = 99999,
            Amount = 25
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Update_ReturnsNotFoundWhenFineDoesNotExist()
    {
        var repository = new FakeFineRepository { UpdateResult = false };
        var controller = CreateController(repository);

        var result = await controller.Update(new SaveFineRequest
        {
            Id = 10,
            UserId = 1,
            Amount = 25,
            Date = DateTime.Today
        });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsNotFoundWhenFineDoesNotExist()
    {
        var repository = new FakeFineRepository { DeleteResult = false };
        var controller = CreateController(repository);

        var result = await controller.Delete(userId: 1, id: 10);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Add_StillReturnsOkWhenFineIsCreated()
    {
        var repository = new FakeFineRepository { AddResult = true };
        var controller = CreateController(repository);

        var result = await controller.Add(new SaveFineRequest
        {
            UserId = 1,
            Amount = 25
        });

        Assert.IsType<OkResult>(result);
    }

    private static FineController CreateController(FakeFineRepository repository)
    {
        var controller = new FineController(repository, NullLogger<FineController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controller;
    }

    private sealed class FakeFineRepository : IFineRepository
    {
        public bool AddResult { get; init; } = true;
        public bool UpdateResult { get; init; } = true;
        public bool DeleteResult { get; init; } = true;

        public Task<Fine[]> GetAll(bool includeArchived = false)
            => Task.FromResult(Array.Empty<Fine>());

        public Task<Fine[]> GetByUserId(int userId, bool includeArchived = false)
            => Task.FromResult(Array.Empty<Fine>());

        public Task<bool> AddFine(Fine fine)
            => Task.FromResult(AddResult);

        public Task<bool> Update(Fine fine)
            => Task.FromResult(UpdateResult);

        public Task<bool> Delete(int userId, int id)
            => Task.FromResult(DeleteResult);

        public Task<PagedResult<Fine>> GetPaged(
            int page,
            int pageSize,
            int? userId = null,
            string? searchTerm = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            decimal? minAmount = null,
            decimal? maxAmount = null,
            bool? isPaid = null,
            bool? isArchived = null)
            => Task.FromResult(new PagedResult<Fine>([], 0, page, pageSize));
    }
}
