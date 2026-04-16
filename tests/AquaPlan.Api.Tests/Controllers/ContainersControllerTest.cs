using AquaPlan.Api.Controllers.Api;
using AquaPlan.Application.DTOs.Containers;
using AquaPlan.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace AquaPlan.Api.Tests.Controllers;

public class ContainersControllerTest
{
    private readonly Mock<IContainerService> _containerServiceMock = new();
    private readonly Mock<ILogger<ContainersController>> _loggerMock = new();
    private readonly ContainersController _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid ContainerId = Guid.Parse("00000000-0000-0000-0000-000000000100");

    public ContainersControllerTest()
    {
        _sut = new ContainersController(_containerServiceMock.Object, _loggerMock.Object);
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
    public async Task GetAll_ShouldReturnOkWithContainers()
    {
        var containers = new List<ContainerListDto>
        {
            new(ContainerId, "BACT-V250", "Bouteille verre stérile microbiologie", "Verre borosilicaté", 250, "Transparent", true),
        };
        _containerServiceMock
            .Setup(x => x.GetAllAsync(TenantId, It.IsAny<ContainerFilteringInputDto?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);

        var result = await _sut.GetAll(null, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(containers);
    }

    [Fact]
    public async Task GetById_ShouldReturnOkWithContainer()
    {
        var container = new ContainerDto(ContainerId, "BACT-V250", "Bouteille verre stérile microbiologie", "Verre borosilicaté", 250, "Transparent", true, DateTime.UtcNow, null);
        _containerServiceMock
            .Setup(x => x.GetByIdAsync(ContainerId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(container);

        var result = await _sut.GetById(ContainerId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(container);
    }

    [Fact]
    public async Task GetById_WhenNotFound_ShouldReturnNotFound()
    {
        _containerServiceMock
            .Setup(x => x.GetByIdAsync(ContainerId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContainerDto?)null);

        var result = await _sut.GetById(ContainerId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Create_ShouldReturnCreatedAtAction()
    {
        var addDto = new ContainerAddDto("BACT-V250", "Bouteille", "Verre borosilicaté", 250, "Transparent");
        var created = new ContainerDto(ContainerId, "BACT-V250", "Bouteille", "Verre borosilicaté", 250, "Transparent", true, DateTime.UtcNow, null);
        _containerServiceMock
            .Setup(x => x.CreateAsync(addDto, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var result = await _sut.Create(addDto, CancellationToken.None);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(ContainersController.GetById));
        createdResult.Value.Should().Be(created);
    }

    [Fact]
    public async Task Update_ShouldReturnOkWithUpdated()
    {
        var updateDto = new ContainerUpdateDto("BACT-V250", "Nouveau nom", "Verre", 300, "Ambré", true);
        var updated = new ContainerDto(ContainerId, "BACT-V250", "Nouveau nom", "Verre", 300, "Ambré", true, DateTime.UtcNow, DateTime.UtcNow);
        _containerServiceMock
            .Setup(x => x.UpdateAsync(ContainerId, updateDto, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updated);

        var result = await _sut.Update(ContainerId, updateDto, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(updated);
    }

    [Fact]
    public async Task Update_WhenNotFound_ShouldReturnNotFound()
    {
        var updateDto = new ContainerUpdateDto("BACT-V250", "X", "Verre", 250, "Transparent", true);
        _containerServiceMock
            .Setup(x => x.UpdateAsync(ContainerId, updateDto, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContainerDto?)null);

        var result = await _sut.Update(ContainerId, updateDto, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ToggleStatus_ShouldReturnOkWithToggled()
    {
        var toggled = new ContainerDto(ContainerId, "BACT-V250", "Bouteille", "Verre", 250, "Transparent", false, DateTime.UtcNow, DateTime.UtcNow);
        _containerServiceMock
            .Setup(x => x.ToggleStatusAsync(ContainerId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toggled);

        var result = await _sut.ToggleStatus(ContainerId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(toggled);
    }

    [Fact]
    public async Task ToggleStatus_WhenNotFound_ShouldReturnNotFound()
    {
        _containerServiceMock
            .Setup(x => x.ToggleStatusAsync(ContainerId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContainerDto?)null);

        var result = await _sut.ToggleStatus(ContainerId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void Controller_ShouldHaveAuthorizeAttribute()
    {
        var attributes = typeof(ContainersController).GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Controller_ShouldHaveApiControllerAttribute()
    {
        var attributes = typeof(ContainersController).GetCustomAttributes(typeof(ApiControllerAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Controller_ShouldHaveRouteAttribute()
    {
        var attributes = typeof(ContainersController).GetCustomAttributes(typeof(RouteAttribute), true);
        attributes.Should().NotBeEmpty();
        var routeAttr = attributes.OfType<RouteAttribute>().First();
        routeAttr.Template.Should().Be("api/containers");
    }

    [Fact]
    public void GetAll_ShouldHaveHttpGetAttribute()
    {
        var method = typeof(ContainersController).GetMethod(nameof(ContainersController.GetAll));
        var attributes = method!.GetCustomAttributes(typeof(HttpGetAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void GetById_ShouldHaveHttpGetAttribute()
    {
        var method = typeof(ContainersController).GetMethod(nameof(ContainersController.GetById));
        var attributes = method!.GetCustomAttributes(typeof(HttpGetAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_ShouldHaveHttpPostAttribute()
    {
        var method = typeof(ContainersController).GetMethod(nameof(ContainersController.Create));
        var attributes = method!.GetCustomAttributes(typeof(HttpPostAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_ShouldHaveAdministratorRoleAttribute()
    {
        var method = typeof(ContainersController).GetMethod(nameof(ContainersController.Create));
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var authorizeAttr = attributes.OfType<AuthorizeAttribute>().First();
        authorizeAttr.Roles.Should().Be("Administrator");
    }

    [Fact]
    public void Update_ShouldHaveHttpPutAttribute()
    {
        var method = typeof(ContainersController).GetMethod(nameof(ContainersController.Update));
        var attributes = method!.GetCustomAttributes(typeof(HttpPutAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Update_ShouldHaveAdministratorRoleAttribute()
    {
        var method = typeof(ContainersController).GetMethod(nameof(ContainersController.Update));
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var authorizeAttr = attributes.OfType<AuthorizeAttribute>().First();
        authorizeAttr.Roles.Should().Be("Administrator");
    }

    [Fact]
    public void ToggleStatus_ShouldHaveHttpPatchAttribute()
    {
        var method = typeof(ContainersController).GetMethod(nameof(ContainersController.ToggleStatus));
        var attributes = method!.GetCustomAttributes(typeof(HttpPatchAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void ToggleStatus_ShouldHaveAdministratorRoleAttribute()
    {
        var method = typeof(ContainersController).GetMethod(nameof(ContainersController.ToggleStatus));
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var authorizeAttr = attributes.OfType<AuthorizeAttribute>().First();
        authorizeAttr.Roles.Should().Be("Administrator");
    }
}
