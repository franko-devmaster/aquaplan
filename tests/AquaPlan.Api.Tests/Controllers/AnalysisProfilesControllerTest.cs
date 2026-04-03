using AquaPlan.Api.Controllers.Api;
using AquaPlan.Application.DTOs.AnalysisProfiles;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace AquaPlan.Api.Tests.Controllers;

public class AnalysisProfilesControllerTest
{
    private readonly Mock<IAnalysisProfileService> _analysisProfileServiceMock = new();
    private readonly Mock<ILogger<AnalysisProfilesController>> _loggerMock = new();
    private readonly AnalysisProfilesController _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid ProfileId = Guid.Parse("00000000-0000-0000-0000-000000000030");

    public AnalysisProfilesControllerTest()
    {
        _sut = new AnalysisProfilesController(
            _analysisProfileServiceMock.Object,
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
    public async Task GetAll_ShouldReturnOkWithProfiles()
    {
        var profiles = new List<AnalysisProfileListDto>
        {
            new(ProfileId, "BAC-01", "Bactériologie de base", AnalysisCategory.Bacteriology, true),
        };
        _analysisProfileServiceMock
            .Setup(x => x.GetAllAsync(TenantId, It.IsAny<AnalysisProfileFilteringInputDto?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(profiles);

        var result = await _sut.GetAll(null, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(profiles);
    }

    [Fact]
    public async Task GetById_ShouldReturnOkWithProfile()
    {
        var profile = new AnalysisProfileDto(ProfileId, "BAC-01", "Bactériologie de base", null, AnalysisCategory.Bacteriology, true, DateTime.UtcNow);
        _analysisProfileServiceMock
            .Setup(x => x.GetByIdAsync(ProfileId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var result = await _sut.GetById(ProfileId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(profile);
    }

    [Fact]
    public async Task GetById_WhenNotFound_ShouldReturnNotFound()
    {
        _analysisProfileServiceMock
            .Setup(x => x.GetByIdAsync(ProfileId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AnalysisProfileDto?)null);

        var result = await _sut.GetById(ProfileId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Create_ShouldReturnCreatedAtAction()
    {
        var addDto = new AnalysisProfileAddDto("BAC-01", "Bactériologie de base", null, AnalysisCategory.Bacteriology);
        var created = new AnalysisProfileDto(ProfileId, "BAC-01", "Bactériologie de base", null, AnalysisCategory.Bacteriology, true, DateTime.UtcNow);
        _analysisProfileServiceMock
            .Setup(x => x.CreateAsync(addDto, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var result = await _sut.Create(addDto, CancellationToken.None);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(AnalysisProfilesController.GetById));
        createdResult.Value.Should().Be(created);
    }

    [Fact]
    public async Task Update_ShouldReturnOkWithUpdatedProfile()
    {
        var updateDto = new AnalysisProfileUpdateDto("BAC-01", "Bactériologie avancée", null, AnalysisCategory.Bacteriology, true);
        var updated = new AnalysisProfileDto(ProfileId, "BAC-01", "Bactériologie avancée", null, AnalysisCategory.Bacteriology, true, DateTime.UtcNow);
        _analysisProfileServiceMock
            .Setup(x => x.UpdateAsync(ProfileId, updateDto, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updated);

        var result = await _sut.Update(ProfileId, updateDto, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(updated);
    }

    [Fact]
    public async Task Update_WhenNotFound_ShouldReturnNotFound()
    {
        var updateDto = new AnalysisProfileUpdateDto("BAC-01", "Bactériologie avancée", null, AnalysisCategory.Bacteriology, true);
        _analysisProfileServiceMock
            .Setup(x => x.UpdateAsync(ProfileId, updateDto, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AnalysisProfileDto?)null);

        var result = await _sut.Update(ProfileId, updateDto, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ToggleStatus_ShouldReturnOkWithUpdatedProfile()
    {
        var toggled = new AnalysisProfileDto(ProfileId, "BAC-01", "Bactériologie de base", null, AnalysisCategory.Bacteriology, false, DateTime.UtcNow);
        _analysisProfileServiceMock
            .Setup(x => x.ToggleStatusAsync(ProfileId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toggled);

        var result = await _sut.ToggleStatus(ProfileId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(toggled);
    }

    [Fact]
    public async Task ToggleStatus_WhenNotFound_ShouldReturnNotFound()
    {
        _analysisProfileServiceMock
            .Setup(x => x.ToggleStatusAsync(ProfileId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AnalysisProfileDto?)null);

        var result = await _sut.ToggleStatus(ProfileId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void Controller_ShouldHaveAuthorizeAttribute()
    {
        var attributes = typeof(AnalysisProfilesController).GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Controller_ShouldHaveApiControllerAttribute()
    {
        var attributes = typeof(AnalysisProfilesController).GetCustomAttributes(typeof(ApiControllerAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Controller_ShouldHaveRouteAttribute()
    {
        var attributes = typeof(AnalysisProfilesController).GetCustomAttributes(typeof(RouteAttribute), true);
        attributes.Should().NotBeEmpty();
        var routeAttr = attributes.OfType<RouteAttribute>().First();
        routeAttr.Template.Should().Be("api/analysis-profiles");
    }

    [Fact]
    public void GetAll_ShouldHaveHttpGetAttribute()
    {
        var method = typeof(AnalysisProfilesController).GetMethod(nameof(AnalysisProfilesController.GetAll));
        var attributes = method!.GetCustomAttributes(typeof(HttpGetAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void GetById_ShouldHaveHttpGetAttribute()
    {
        var method = typeof(AnalysisProfilesController).GetMethod(nameof(AnalysisProfilesController.GetById));
        var attributes = method!.GetCustomAttributes(typeof(HttpGetAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_ShouldHaveHttpPostAttribute()
    {
        var method = typeof(AnalysisProfilesController).GetMethod(nameof(AnalysisProfilesController.Create));
        var attributes = method!.GetCustomAttributes(typeof(HttpPostAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_ShouldHaveAdministratorRoleAttribute()
    {
        var method = typeof(AnalysisProfilesController).GetMethod(nameof(AnalysisProfilesController.Create));
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var authorizeAttr = attributes.OfType<AuthorizeAttribute>().First();
        authorizeAttr.Roles.Should().Be("Administrator");
    }

    [Fact]
    public void Update_ShouldHaveHttpPutAttribute()
    {
        var method = typeof(AnalysisProfilesController).GetMethod(nameof(AnalysisProfilesController.Update));
        var attributes = method!.GetCustomAttributes(typeof(HttpPutAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Update_ShouldHaveAdministratorRoleAttribute()
    {
        var method = typeof(AnalysisProfilesController).GetMethod(nameof(AnalysisProfilesController.Update));
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var authorizeAttr = attributes.OfType<AuthorizeAttribute>().First();
        authorizeAttr.Roles.Should().Be("Administrator");
    }

    [Fact]
    public void ToggleStatus_ShouldHaveHttpPatchAttribute()
    {
        var method = typeof(AnalysisProfilesController).GetMethod(nameof(AnalysisProfilesController.ToggleStatus));
        var attributes = method!.GetCustomAttributes(typeof(HttpPatchAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void ToggleStatus_ShouldHaveAdministratorRoleAttribute()
    {
        var method = typeof(AnalysisProfilesController).GetMethod(nameof(AnalysisProfilesController.ToggleStatus));
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var authorizeAttr = attributes.OfType<AuthorizeAttribute>().First();
        authorizeAttr.Roles.Should().Be("Administrator");
    }
}
