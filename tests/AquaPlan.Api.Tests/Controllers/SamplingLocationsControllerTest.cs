using AquaPlan.Api.Controllers.Api;
using AquaPlan.Application.DTOs.SamplingLocations;
using AquaPlan.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace AquaPlan.Api.Tests.Controllers;

public class SamplingLocationsControllerTest
{
    private readonly Mock<ISamplingLocationService> _samplingLocationServiceMock = new();
    private readonly Mock<IPermissionService> _permissionServiceMock = new();
    private readonly Mock<ILogger<SamplingLocationsController>> _loggerMock = new();
    private readonly SamplingLocationsController _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid LocationId = Guid.Parse("00000000-0000-0000-0000-000000000030");
    private static readonly Guid DistributorId = Guid.Parse("00000000-0000-0000-0000-000000000020");
    private const string UserId = "user-1";

    public SamplingLocationsControllerTest()
    {
        _sut = new SamplingLocationsController(
            _samplingLocationServiceMock.Object,
            _permissionServiceMock.Object,
            _loggerMock.Object);
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = CreateUser() }
        };
    }

    private static ClaimsPrincipal CreateUser(string userId = UserId, string tenantId = "00000000-0000-0000-0000-000000000001")
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim("tenant_id", tenantId),
        ], "test"));
    }

    [Fact]
    public async Task GetForCurrentUser_ShouldReturnUserLocations_WhenNotAdmin()
    {
        var locations = new List<SamplingLocationDto>
        {
            new(LocationId, "Source A", "LOC-001", 46.8, 7.15, "A description", true, DistributorId, "Distributor A", DateTime.UtcNow),
        };
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "AdministerSystem", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _samplingLocationServiceMock
            .Setup(x => x.GetForUserAsync(UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(locations);

        var result = await _sut.GetForCurrentUser(CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(locations);
        _samplingLocationServiceMock.Verify(x => x.GetForUserAsync(UserId, TenantId, It.IsAny<CancellationToken>()), Times.Once);
        _samplingLocationServiceMock.Verify(x => x.GetAllAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetForCurrentUser_ShouldReturnAllLocations_WhenAdmin()
    {
        var locations = new List<SamplingLocationDto>
        {
            new(LocationId, "Source A", "LOC-001", 46.8, 7.15, "A description", true, DistributorId, "Distributor A", DateTime.UtcNow),
        };
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "AdministerSystem", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _samplingLocationServiceMock
            .Setup(x => x.GetAllAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(locations);

        var result = await _sut.GetForCurrentUser(CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(locations);
        _samplingLocationServiceMock.Verify(x => x.GetAllAsync(TenantId, It.IsAny<CancellationToken>()), Times.Once);
        _samplingLocationServiceMock.Verify(x => x.GetForUserAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByDistributor_ShouldReturnOk()
    {
        var locations = new List<SamplingLocationDto>
        {
            new(LocationId, "Source B", "LOC-002", 46.9, 7.2, null, true, DistributorId, "Distributor A", DateTime.UtcNow),
        };
        _samplingLocationServiceMock
            .Setup(x => x.GetByDistributorAsync(DistributorId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(locations);

        var result = await _sut.GetByDistributor(DistributorId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(locations);
    }

    [Fact]
    public async Task GetById_ShouldReturnNotFound_WhenLocationDoesNotExist()
    {
        _samplingLocationServiceMock
            .Setup(x => x.GetByIdAsync(LocationId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SamplingLocationDto?)null);

        var result = await _sut.GetById(LocationId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetById_ShouldReturnOk_WhenLocationExists()
    {
        var location = new SamplingLocationDto(LocationId, "Source A", "LOC-001", 46.8, 7.15, "A description", true, DistributorId, "Distributor A", DateTime.UtcNow);
        _samplingLocationServiceMock
            .Setup(x => x.GetByIdAsync(LocationId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

        var result = await _sut.GetById(LocationId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(location);
    }

    [Fact]
    public async Task Create_ShouldReturnCreatedAtAction()
    {
        var createDto = new SamplingLocationCreateDto("New Source", "LOC-003", 46.85, 7.1, "New location", DistributorId);
        var created = new SamplingLocationDto(LocationId, "New Source", "LOC-003", 46.85, 7.1, "New location", true, DistributorId, "Distributor A", DateTime.UtcNow);
        _samplingLocationServiceMock
            .Setup(x => x.CreateAsync(createDto, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var result = await _sut.Create(createDto, CancellationToken.None);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(SamplingLocationsController.GetById));
        createdResult.Value.Should().Be(created);
    }

    [Fact]
    public void Create_ShouldHaveAdministratorRoleAttribute()
    {
        var method = typeof(SamplingLocationsController).GetMethod(nameof(SamplingLocationsController.Create));
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var authorizeAttr = attributes.OfType<AuthorizeAttribute>().First();
        authorizeAttr.Roles.Should().Be("Administrator");
    }

    [Fact]
    public async Task Update_ShouldReturnOk_WhenSuccess()
    {
        var updateDto = new SamplingLocationUpdateDto("Updated Source", "LOC-001", 46.8, 7.15, "Updated description", true);
        var updated = new SamplingLocationDto(LocationId, "Updated Source", "LOC-001", 46.8, 7.15, "Updated description", true, DistributorId, "Distributor A", DateTime.UtcNow);
        _samplingLocationServiceMock
            .Setup(x => x.UpdateAsync(LocationId, updateDto, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updated);

        var result = await _sut.Update(LocationId, updateDto, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(updated);
    }

    [Fact]
    public async Task Update_ShouldReturnNotFound_WhenLocationDoesNotExist()
    {
        var updateDto = new SamplingLocationUpdateDto("Updated Source", "LOC-001", 46.8, 7.15, "Updated description", true);
        _samplingLocationServiceMock
            .Setup(x => x.UpdateAsync(LocationId, updateDto, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SamplingLocationDto?)null);

        var result = await _sut.Update(LocationId, updateDto, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void Controller_ShouldHaveAuthorizeAttribute()
    {
        var attributes = typeof(SamplingLocationsController).GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
    }
}
