using AquaPlan.Api.Controllers.Api;
using AquaPlan.Application.DTOs.Distributors;
using AquaPlan.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace AquaPlan.Api.Tests.Controllers;

public class DistributorsControllerTest
{
    private readonly Mock<IDistributorService> _distributorServiceMock = new();
    private readonly Mock<ILogger<DistributorsController>> _loggerMock = new();
    private readonly DistributorsController _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid DistributorId = Guid.Parse("00000000-0000-0000-0000-000000000020");

    public DistributorsControllerTest()
    {
        _sut = new DistributorsController(
            _distributorServiceMock.Object,
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
    public async Task GetAll_ShouldReturnOk_WithDistributors()
    {
        var distributors = new List<DistributorListDto>
        {
            new(DistributorId, "Eau de Fribourg", null, "Sarine", "Réseau A", true, DateTime.UtcNow),
        };
        _distributorServiceMock
            .Setup(x => x.GetAllAsync(It.IsAny<DistributorFilteringInputDto>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(distributors);

        var result = await _sut.GetAll(null, null, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(distributors);
    }

    [Fact]
    public async Task GetAll_ShouldPassFilterParameters()
    {
        _distributorServiceMock
            .Setup(x => x.GetAllAsync(
                It.Is<DistributorFilteringInputDto>(f => f.Name == "Eau" && f.IsActive == true),
                TenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DistributorListDto>());

        await _sut.GetAll("Eau", true, CancellationToken.None);

        _distributorServiceMock.Verify(x => x.GetAllAsync(
            It.Is<DistributorFilteringInputDto>(f => f.Name == "Eau" && f.IsActive == true),
            TenantId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetById_ShouldReturnOk_WhenDistributorExists()
    {
        var distributor = new DistributorDto(DistributorId, "Eau de Fribourg", null, "Sarine", "Réseau A", true, DateTime.UtcNow);
        _distributorServiceMock
            .Setup(x => x.GetByIdAsync(DistributorId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(distributor);

        var result = await _sut.GetById(DistributorId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(distributor);
    }

    [Fact]
    public async Task GetById_ShouldReturnNotFound_WhenDistributorDoesNotExist()
    {
        _distributorServiceMock
            .Setup(x => x.GetByIdAsync(DistributorId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DistributorDto?)null);

        var result = await _sut.GetById(DistributorId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Create_ShouldReturnCreatedAtAction()
    {
        var addDto = new DistributorAddDto("Nouveau Distributeur", null, "Gruyère", "Réseau B");
        var created = new DistributorDto(DistributorId, "Nouveau Distributeur", null, "Gruyère", "Réseau B", true, DateTime.UtcNow);
        _distributorServiceMock
            .Setup(x => x.CreateAsync(addDto, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var result = await _sut.Create(addDto, CancellationToken.None);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(DistributorsController.GetById));
        createdResult.Value.Should().Be(created);
    }

    [Fact]
    public void Create_ShouldHaveAdministratorRoleAttribute()
    {
        var method = typeof(DistributorsController).GetMethod(nameof(DistributorsController.Create));
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var authorizeAttr = attributes.OfType<AuthorizeAttribute>().First();
        authorizeAttr.Roles.Should().Be("Administrator");
    }

    [Fact]
    public async Task Update_ShouldReturnOk_WhenSuccess()
    {
        var updateDto = new DistributorUpdateDto("Distributeur Modifié", null, "Sarine", "Réseau A");
        var updated = new DistributorDto(DistributorId, "Distributeur Modifié", null, "Sarine", "Réseau A", true, DateTime.UtcNow);
        _distributorServiceMock
            .Setup(x => x.UpdateAsync(DistributorId, updateDto, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updated);

        var result = await _sut.Update(DistributorId, updateDto, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(updated);
    }

    [Fact]
    public async Task Update_ShouldReturnNotFound_WhenDistributorDoesNotExist()
    {
        var updateDto = new DistributorUpdateDto("Distributeur Modifié", null, "Sarine", "Réseau A");
        _distributorServiceMock
            .Setup(x => x.UpdateAsync(DistributorId, updateDto, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DistributorDto?)null);

        var result = await _sut.Update(DistributorId, updateDto, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void Update_ShouldHaveAdministratorRoleAttribute()
    {
        var method = typeof(DistributorsController).GetMethod(nameof(DistributorsController.Update));
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var authorizeAttr = attributes.OfType<AuthorizeAttribute>().First();
        authorizeAttr.Roles.Should().Be("Administrator");
    }

    [Fact]
    public async Task ToggleStatus_ShouldReturnOk_WhenSuccess()
    {
        var toggled = new DistributorDto(DistributorId, "Eau de Fribourg", null, "Sarine", "Réseau A", false, DateTime.UtcNow);
        _distributorServiceMock
            .Setup(x => x.ToggleStatusAsync(DistributorId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toggled);

        var result = await _sut.ToggleStatus(DistributorId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(toggled);
    }

    [Fact]
    public async Task ToggleStatus_ShouldReturnNotFound_WhenDistributorDoesNotExist()
    {
        _distributorServiceMock
            .Setup(x => x.ToggleStatusAsync(DistributorId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DistributorDto?)null);

        var result = await _sut.ToggleStatus(DistributorId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void ToggleStatus_ShouldHaveAdministratorRoleAttribute()
    {
        var method = typeof(DistributorsController).GetMethod(nameof(DistributorsController.ToggleStatus));
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var authorizeAttr = attributes.OfType<AuthorizeAttribute>().First();
        authorizeAttr.Roles.Should().Be("Administrator");
    }

    [Fact]
    public void Controller_ShouldHaveAuthorizeAttribute()
    {
        var attributes = typeof(DistributorsController).GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Controller_ShouldHaveApiControllerAttribute()
    {
        var attributes = typeof(DistributorsController).GetCustomAttributes(typeof(ApiControllerAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Controller_ShouldHaveRouteAttribute()
    {
        var attributes = typeof(DistributorsController).GetCustomAttributes(typeof(RouteAttribute), true);
        attributes.Should().NotBeEmpty();
        var routeAttr = attributes.OfType<RouteAttribute>().First();
        routeAttr.Template.Should().Be("api/[controller]");
    }

    [Fact]
    public void GetAll_ShouldHaveHttpGetAttribute()
    {
        var method = typeof(DistributorsController).GetMethod(nameof(DistributorsController.GetAll));
        var attributes = method!.GetCustomAttributes(typeof(HttpGetAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void GetById_ShouldHaveHttpGetAttribute()
    {
        var method = typeof(DistributorsController).GetMethod(nameof(DistributorsController.GetById));
        var attributes = method!.GetCustomAttributes(typeof(HttpGetAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_ShouldHaveHttpPostAttribute()
    {
        var method = typeof(DistributorsController).GetMethod(nameof(DistributorsController.Create));
        var attributes = method!.GetCustomAttributes(typeof(HttpPostAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Update_ShouldHaveHttpPutAttribute()
    {
        var method = typeof(DistributorsController).GetMethod(nameof(DistributorsController.Update));
        var attributes = method!.GetCustomAttributes(typeof(HttpPutAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void ToggleStatus_ShouldHaveHttpPutAttribute()
    {
        var method = typeof(DistributorsController).GetMethod(nameof(DistributorsController.ToggleStatus));
        var attributes = method!.GetCustomAttributes(typeof(HttpPutAttribute), true);
        attributes.Should().NotBeEmpty();
    }
}
