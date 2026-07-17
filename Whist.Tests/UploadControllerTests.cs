using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ServerAPI.Controllers;
using ServerAPI.Storage;

namespace Whist.Tests;

public sealed class UploadControllerTests
{
    [Fact]
    public async Task UploadImage_RejectsUnsupportedExtensionBeforeStorage()
    {
        var storage = new FakeImageStorageService();
        var controller = CreateController(storage);
        var file = new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "file", "test.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        var result = await controller.UploadImage(new UploadController.UploadImageRequest
        {
            File = file
        }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.False(storage.UploadCalled);
    }

    [Fact]
    public async Task UploadImage_RejectsTooLargeFileBeforeStorage()
    {
        var storage = new FakeImageStorageService();
        var controller = CreateController(storage, maxUploadBytes: 2);
        var file = new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "file", "test.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var result = await controller.UploadImage(new UploadController.UploadImageRequest
        {
            File = file
        }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.False(storage.UploadCalled);
    }

    private static UploadController CreateController(FakeImageStorageService storage, long maxUploadBytes = 1024)
    {
        var options = Options.Create(new BlobStorageOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            ContainerName = "images",
            MaxUploadBytes = maxUploadBytes,
            MaxImageWidth = 1600,
            WebpQuality = 82
        });

        var controller = new UploadController(storage, options, NullLogger<UploadController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim("sub", "1")],
                    "Test"))
            }
        };

        return controller;
    }

    private sealed class FakeImageStorageService : IImageStorageService
    {
        public bool UploadCalled { get; private set; }

        public Task<string> UploadImageAsync(IFormFile file, int? userId, CancellationToken cancellationToken = default)
        {
            UploadCalled = true;
            return Task.FromResult("https://stwhistprod.blob.core.windows.net/images/uploads/test.webp");
        }

        public Task<bool> TryDeleteImageAsync(string? imageUrl, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public bool IsAllowedImageUrl(string? imageUrl) => true;
    }
}
