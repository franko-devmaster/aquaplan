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

    private static SamplingLocationDto CreateLocationDto(
        Guid? id = null,
        string name = "Source A",
        string code = "LOC-001",
        bool isActive = true)
    {
        return new SamplingLocationDto(
            id ?? LocationId, name, code, 46.8, 7.15,
            "A description", isActive, DistributorId, "Distributor A", DateTime.UtcNow);
    }

    #region GetForCurrentUser

    [Fact]
    public async Task GetForCurrentUser_ShouldReturnUserLocations_WhenNotAdmin()
    {
        var locations = new List<SamplingLocationDto> { CreateLocationDto() };
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
        var locations = new List<SamplingLocationDto> { CreateLocationDto() };
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

    #endregion

    #region GetFiltered

    [Fact]
    public async Task GetFiltered_ShouldReturnOk()
    {
        var filter = new SamplingLocationFilteringInputDto(DistributorId, "Source", true, 1, 25);
        var listDto = new SamplingLocationListDto(
            new List<SamplingLocationDto> { CreateLocationDto() }, 1, 1, 25);
        _samplingLocationServiceMock
            .Setup(x => x.GetFilteredAsync(filter, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(listDto);

        var result = await _sut.GetFiltered(filter, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(listDto);
    }

    [Fact]
    public void GetFiltered_ShouldHaveHttpGetAttribute()
    {
        var method = typeof(SamplingLocationsController).GetMethod(nameof(SamplingLocationsController.GetFiltered));
        var attribute = method!.GetCustomAttributes(typeof(HttpGetAttribute), true).OfType<HttpGetAttribute>().First();
        attribute.Template.Should().Be("filtered");
    }

    #endregion

    #region GetByDistributor

    [Fact]
    public async Task GetByDistributor_ShouldReturnOk()
    {
        var locations = new List<SamplingLocationDto>
        {
            CreateLocationDto(name: "Source B", code: "LOC-002"),
        };
        _samplingLocationServiceMock
            .Setup(x => x.GetByDistributorAsync(DistributorId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(locations);

        var result = await _sut.GetByDistributor(DistributorId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(locations);
    }

    #endregion

    #region GetById

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
        var location = CreateLocationDto();
        _samplingLocationServiceMock
            .Setup(x => x.GetByIdAsync(LocationId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

        var result = await _sut.GetById(LocationId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(location);
    }

    #endregion

    #region Create

    [Fact]
    public async Task Create_ShouldReturnCreatedAtAction_WhenCodeIsUnique()
    {
        var createDto = new SamplingLocationCreateDto("New Source", "LOC-003", 46.85, 7.1, "New location", DistributorId);
        var created = CreateLocationDto(name: "New Source", code: "LOC-003");
        _samplingLocationServiceMock
            .Setup(x => x.IsLocationCodeUniqueAsync("LOC-003", DistributorId, null, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _samplingLocationServiceMock
            .Setup(x => x.CreateAsync(createDto, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var result = await _sut.Create(createDto, CancellationToken.None);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(SamplingLocationsController.GetById));
        createdResult.Value.Should().Be(created);
    }

    [Fact]
    public async Task Create_ShouldReturnConflict_WhenCodeIsNotUnique()
    {
        var createDto = new SamplingLocationCreateDto("New Source", "LOC-001", 46.85, 7.1, "New location", DistributorId);
        _samplingLocationServiceMock
            .Setup(x => x.IsLocationCodeUniqueAsync("LOC-001", DistributorId, null, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.Create(createDto, CancellationToken.None);

        result.Result.Should().BeOfType<ConflictObjectResult>();
        _samplingLocationServiceMock.Verify(x => x.CreateAsync(It.IsAny<SamplingLocationCreateDto>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
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
    public void Create_ShouldHaveHttpPostAttribute()
    {
        var method = typeof(SamplingLocationsController).GetMethod(nameof(SamplingLocationsController.Create));
        method!.GetCustomAttributes(typeof(HttpPostAttribute), true).Should().NotBeEmpty();
    }

    #endregion

    #region Update

    [Fact]
    public async Task Update_ShouldReturnOk_WhenSuccess()
    {
        var updateDto = new SamplingLocationUpdateDto("Updated Source", "LOC-001", 46.8, 7.15, "Updated description", true);
        var updated = CreateLocationDto(name: "Updated Source");
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
    public void Update_ShouldHaveAdministratorRoleAttribute()
    {
        var method = typeof(SamplingLocationsController).GetMethod(nameof(SamplingLocationsController.Update));
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var authorizeAttr = attributes.OfType<AuthorizeAttribute>().First();
        authorizeAttr.Roles.Should().Be("Administrator");
    }

    #endregion

    #region ToggleStatus

    [Fact]
    public async Task ToggleStatus_ShouldReturnOk_WhenLocationExists()
    {
        var toggleResult = new ToggleStatusResultDto(
            CreateLocationDto(isActive: false), false, null);
        _samplingLocationServiceMock
            .Setup(x => x.ToggleStatusAsync(LocationId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toggleResult);

        var result = await _sut.ToggleStatus(LocationId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(toggleResult);
    }

    [Fact]
    public async Task ToggleStatus_ShouldReturnNotFound_WhenLocationDoesNotExist()
    {
        _samplingLocationServiceMock
            .Setup(x => x.ToggleStatusAsync(LocationId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ToggleStatusResultDto?)null);

        var result = await _sut.ToggleStatus(LocationId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void ToggleStatus_ShouldHaveAdministratorRoleAttribute()
    {
        var method = typeof(SamplingLocationsController).GetMethod(nameof(SamplingLocationsController.ToggleStatus));
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var authorizeAttr = attributes.OfType<AuthorizeAttribute>().First();
        authorizeAttr.Roles.Should().Be("Administrator");
    }

    [Fact]
    public void ToggleStatus_ShouldHaveHttpPutAttribute()
    {
        var method = typeof(SamplingLocationsController).GetMethod(nameof(SamplingLocationsController.ToggleStatus));
        var putAttr = method!.GetCustomAttributes(typeof(HttpPutAttribute), true).OfType<HttpPutAttribute>().First();
        putAttr.Template.Should().Be("{id:guid}/toggle-status");
    }

    #endregion

    #region CheckCodeUnique

    [Fact]
    public async Task CheckCodeUnique_ShouldReturnTrue_WhenUnique()
    {
        _samplingLocationServiceMock
            .Setup(x => x.IsLocationCodeUniqueAsync("LOC-NEW", DistributorId, null, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.CheckCodeUnique("LOC-NEW", DistributorId, null, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(true);
    }

    [Fact]
    public async Task CheckCodeUnique_ShouldReturnFalse_WhenNotUnique()
    {
        _samplingLocationServiceMock
            .Setup(x => x.IsLocationCodeUniqueAsync("LOC-001", DistributorId, null, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.CheckCodeUnique("LOC-001", DistributorId, null, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(false);
    }

    [Fact]
    public void CheckCodeUnique_ShouldHaveAdministratorRoleAttribute()
    {
        var method = typeof(SamplingLocationsController).GetMethod(nameof(SamplingLocationsController.CheckCodeUnique));
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var authorizeAttr = attributes.OfType<AuthorizeAttribute>().First();
        authorizeAttr.Roles.Should().Be("Administrator");
    }

    #endregion

    #region Controller attributes

    [Fact]
    public void Controller_ShouldHaveAuthorizeAttribute()
    {
        var attributes = typeof(SamplingLocationsController).GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Controller_ShouldHaveApiControllerAttribute()
    {
        var attributes = typeof(SamplingLocationsController).GetCustomAttributes(typeof(ApiControllerAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Controller_ShouldHaveRouteAttribute()
    {
        var attributes = typeof(SamplingLocationsController).GetCustomAttributes(typeof(RouteAttribute), true);
        attributes.Should().NotBeEmpty();
        var routeAttr = attributes.OfType<RouteAttribute>().First();
        routeAttr.Template.Should().Be("api/sampling-locations");
    }

    #endregion
}
