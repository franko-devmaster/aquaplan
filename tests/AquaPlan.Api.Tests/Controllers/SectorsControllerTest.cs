using AquaPlan.Api.Controllers.Api;
using AquaPlan.Application.DTOs.Sectors;
using AquaPlan.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace AquaPlan.Api.Tests.Controllers;

public class SectorsControllerTest
{
    private readonly Mock<ISectorService> _sectorServiceMock = new();
    private readonly Mock<IPermissionService> _permissionServiceMock = new();
    private readonly Mock<ILogger<SectorsController>> _loggerMock = new();
    private readonly SectorsController _sut;

    private const string UserId = "user-1";
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid SectorId = Guid.Parse("00000000-0000-0000-0000-000000000030");
    private static readonly Guid DistributorId = Guid.Parse("00000000-0000-0000-0000-000000000050");

    public SectorsControllerTest()
    {
        _sut = new SectorsController(
            _sectorServiceMock.Object,
            _permissionServiceMock.Object,
            _loggerMock.Object);
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = CreateUser() }
        };
    }

    private static ClaimsPrincipal CreateUser(string tenantId = "00000000-0000-0000-0000-000000000001")
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim("tenant_id", tenantId),
        ], "test"));
    }

    [Fact]
    public async Task GetAll_ShouldReturnOk_WithSectors()
    {
        var sectors = new List<SectorListDto>
        {
            new(SectorId, "Secteur Nord", "SN", "Description", true, DistributorId, "Test Distributor", DateTime.UtcNow),
        };
        _sectorServiceMock
            .Setup(x => x.GetAllAsync(It.IsAny<SectorFilteringInputDto>(), TenantId, UserId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sectors);

        var result = await _sut.GetAll(null, null, null, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(sectors);
    }

    [Fact]
    public async Task GetAll_ShouldForwardIsAdminTrue_WhenUserHasViewAllOrdersPermission()
    {
        // AQ-418 — controller must derive isAdmin from the permission service and
        // forward it to the service so admins keep their tenant-wide view.
        _permissionServiceMock
            .Setup(p => p.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _sectorServiceMock
            .Setup(x => x.GetAllAsync(It.IsAny<SectorFilteringInputDto>(), TenantId, UserId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SectorListDto>());

        await _sut.GetAll(null, null, null, CancellationToken.None);

        _sectorServiceMock.Verify(x => x.GetAllAsync(
            It.IsAny<SectorFilteringInputDto>(), TenantId, UserId, true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAll_ShouldForwardIsAdminFalse_WhenUserLacksPermission()
    {
        // AQ-418 — non-admin users must be scoped to their authorized distributors.
        _permissionServiceMock
            .Setup(p => p.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _sectorServiceMock
            .Setup(x => x.GetAllAsync(It.IsAny<SectorFilteringInputDto>(), TenantId, UserId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SectorListDto>());

        await _sut.GetAll(null, null, null, CancellationToken.None);

        _sectorServiceMock.Verify(x => x.GetAllAsync(
            It.IsAny<SectorFilteringInputDto>(), TenantId, UserId, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetById_ShouldReturnOk_WhenFound()
    {
        var sector = new SectorDto(SectorId, "Secteur Nord", "SN", "Description", true, DistributorId, "Test Distributor", DateTime.UtcNow);
        _sectorServiceMock
            .Setup(x => x.GetByIdAsync(SectorId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sector);

        var result = await _sut.GetById(SectorId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(sector);
    }

    [Fact]
    public async Task GetById_ShouldReturnNotFound_WhenNull()
    {
        _sectorServiceMock
            .Setup(x => x.GetByIdAsync(SectorId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SectorDto?)null);

        var result = await _sut.GetById(SectorId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Create_ShouldReturnCreated()
    {
        var addDto = new SectorAddDto("Nouveau Secteur", "NS", "Description", DistributorId);
        var created = new SectorDto(SectorId, "Nouveau Secteur", "NS", "Description", true, DistributorId, "Test Distributor", DateTime.UtcNow);
        _sectorServiceMock
            .Setup(x => x.CreateAsync(addDto, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var result = await _sut.Create(addDto, CancellationToken.None);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(SectorsController.GetById));
        createdResult.Value.Should().Be(created);
    }

    [Fact]
    public async Task Update_ShouldReturnOk_WhenFound()
    {
        var updateDto = new SectorUpdateDto("Secteur Modifie", "SM", "Description", DistributorId);
        var updated = new SectorDto(SectorId, "Secteur Modifie", "SM", "Description", true, DistributorId, "Test Distributor", DateTime.UtcNow);
        _sectorServiceMock
            .Setup(x => x.UpdateAsync(SectorId, updateDto, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updated);

        var result = await _sut.Update(SectorId, updateDto, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(updated);
    }

    [Fact]
    public async Task Update_ShouldReturnNotFound_WhenNull()
    {
        var updateDto = new SectorUpdateDto("Secteur Modifie", "SM", null, DistributorId);
        _sectorServiceMock
            .Setup(x => x.UpdateAsync(SectorId, updateDto, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SectorDto?)null);

        var result = await _sut.Update(SectorId, updateDto, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ToggleStatus_ShouldReturnOk_WhenFound()
    {
        var toggled = new SectorDto(SectorId, "Secteur Nord", "SN", "Description", false, DistributorId, "Test Distributor", DateTime.UtcNow);
        _sectorServiceMock
            .Setup(x => x.ToggleStatusAsync(SectorId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toggled);

        var result = await _sut.ToggleStatus(SectorId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(toggled);
    }

    [Fact]
    public async Task ToggleStatus_ShouldReturnNotFound_WhenNull()
    {
        _sectorServiceMock
            .Setup(x => x.ToggleStatusAsync(SectorId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SectorDto?)null);

        var result = await _sut.ToggleStatus(SectorId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    // Attribute tests

    [Fact]
    public void Controller_ShouldHaveAuthorizeAttribute()
    {
        var attributes = typeof(SectorsController).GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Controller_ShouldHaveApiControllerAttribute()
    {
        var attributes = typeof(SectorsController).GetCustomAttributes(typeof(ApiControllerAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Controller_ShouldHaveRouteAttribute()
    {
        var attributes = typeof(SectorsController).GetCustomAttributes(typeof(RouteAttribute), true);
        attributes.Should().NotBeEmpty();
        var routeAttr = attributes.OfType<RouteAttribute>().First();
        routeAttr.Template.Should().Be("api/[controller]");
    }

    [Fact]
    public void GetAll_ShouldHaveHttpGetAttribute()
    {
        var method = typeof(SectorsController).GetMethod(nameof(SectorsController.GetAll));
        var attributes = method!.GetCustomAttributes(typeof(HttpGetAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void GetById_ShouldHaveHttpGetAttribute()
    {
        var method = typeof(SectorsController).GetMethod(nameof(SectorsController.GetById));
        var attributes = method!.GetCustomAttributes(typeof(HttpGetAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_ShouldHaveHttpPostAttribute()
    {
        var method = typeof(SectorsController).GetMethod(nameof(SectorsController.Create));
        var attributes = method!.GetCustomAttributes(typeof(HttpPostAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_ShouldHaveAdministratorRoleAttribute()
    {
        var method = typeof(SectorsController).GetMethod(nameof(SectorsController.Create));
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var authorizeAttr = attributes.OfType<AuthorizeAttribute>().First();
        authorizeAttr.Roles.Should().Be("Administrator");
    }

    [Fact]
    public void Update_ShouldHaveHttpPutAttribute()
    {
        var method = typeof(SectorsController).GetMethod(nameof(SectorsController.Update));
        var attributes = method!.GetCustomAttributes(typeof(HttpPutAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Update_ShouldHaveAdministratorRoleAttribute()
    {
        var method = typeof(SectorsController).GetMethod(nameof(SectorsController.Update));
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var authorizeAttr = attributes.OfType<AuthorizeAttribute>().First();
        authorizeAttr.Roles.Should().Be("Administrator");
    }

    [Fact]
    public void ToggleStatus_ShouldHaveHttpPutAttribute()
    {
        var method = typeof(SectorsController).GetMethod(nameof(SectorsController.ToggleStatus));
        var attributes = method!.GetCustomAttributes(typeof(HttpPutAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void ToggleStatus_ShouldHaveAdministratorRoleAttribute()
    {
        var method = typeof(SectorsController).GetMethod(nameof(SectorsController.ToggleStatus));
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var authorizeAttr = attributes.OfType<AuthorizeAttribute>().First();
        authorizeAttr.Roles.Should().Be("Administrator");
    }
}
