using AquaPlan.Api.Controllers.Api;
using AquaPlan.Application.DTOs.AnalysisProfiles;
using AquaPlan.Application.DTOs.AnalysisPrograms;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace AquaPlan.Api.Tests.Controllers;

public class AnalysisProgramsControllerTest
{
    private readonly Mock<IAnalysisProgramService> _analysisProgramServiceMock = new();
    private readonly Mock<ILogger<AnalysisProgramsController>> _loggerMock = new();
    private readonly AnalysisProgramsController _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid ProgramId = Guid.Parse("00000000-0000-0000-0000-000000000040");
    private static readonly Guid ProfileId = Guid.Parse("00000000-0000-0000-0000-000000000030");

    public AnalysisProgramsControllerTest()
    {
        _sut = new AnalysisProgramsController(
            _analysisProgramServiceMock.Object,
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

    private static AnalysisProgramDto CreateProgramDto(
        string code = "PRG-01",
        string name = "Programme standard",
        bool isActive = true)
    {
        return new AnalysisProgramDto(
            ProgramId,
            code,
            name,
            null,
            isActive,
            DateTime.UtcNow,
            new List<AnalysisProfileListDto>
            {
                new(ProfileId, "BAC-01", "Bactériologie de base", AnalysisCategory.Bacteriology, true),
            });
    }

    [Fact]
    public async Task GetAll_ShouldReturnOkWithPrograms()
    {
        var programs = new List<AnalysisProgramListDto>
        {
            new(ProgramId, "PRG-01", "Programme standard", true, 3),
        };
        _analysisProgramServiceMock
            .Setup(x => x.GetAllAsync(TenantId, It.IsAny<AnalysisProgramFilteringInputDto?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(programs);

        var result = await _sut.GetAll(null, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(programs);
    }

    [Fact]
    public async Task GetById_ShouldReturnOkWithProgram()
    {
        var program = CreateProgramDto();
        _analysisProgramServiceMock
            .Setup(x => x.GetByIdAsync(ProgramId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(program);

        var result = await _sut.GetById(ProgramId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(program);
    }

    [Fact]
    public async Task GetById_WhenNotFound_ShouldReturnNotFound()
    {
        _analysisProgramServiceMock
            .Setup(x => x.GetByIdAsync(ProgramId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AnalysisProgramDto?)null);

        var result = await _sut.GetById(ProgramId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Create_ShouldReturnCreatedAtAction()
    {
        var addDto = new AnalysisProgramAddDto("PRG-01", "Programme standard", null);
        var created = CreateProgramDto();
        _analysisProgramServiceMock
            .Setup(x => x.CreateAsync(addDto, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var result = await _sut.Create(addDto, CancellationToken.None);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(AnalysisProgramsController.GetById));
        createdResult.Value.Should().Be(created);
    }

    [Fact]
    public async Task Update_ShouldReturnOkWithUpdatedProgram()
    {
        var updateDto = new AnalysisProgramUpdateDto("PRG-01", "Programme avancé", null, true);
        var updated = CreateProgramDto(name: "Programme avancé");
        _analysisProgramServiceMock
            .Setup(x => x.UpdateAsync(ProgramId, updateDto, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updated);

        var result = await _sut.Update(ProgramId, updateDto, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(updated);
    }

    [Fact]
    public async Task Update_WhenNotFound_ShouldReturnNotFound()
    {
        var updateDto = new AnalysisProgramUpdateDto("PRG-01", "Programme avancé", null, true);
        _analysisProgramServiceMock
            .Setup(x => x.UpdateAsync(ProgramId, updateDto, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AnalysisProgramDto?)null);

        var result = await _sut.Update(ProgramId, updateDto, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ToggleStatus_ShouldReturnOkWithUpdatedProgram()
    {
        var toggled = CreateProgramDto(isActive: false);
        _analysisProgramServiceMock
            .Setup(x => x.ToggleStatusAsync(ProgramId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toggled);

        var result = await _sut.ToggleStatus(ProgramId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(toggled);
    }

    [Fact]
    public async Task ToggleStatus_WhenNotFound_ShouldReturnNotFound()
    {
        _analysisProgramServiceMock
            .Setup(x => x.ToggleStatusAsync(ProgramId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AnalysisProgramDto?)null);

        var result = await _sut.ToggleStatus(ProgramId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task AddProfiles_ShouldReturnOkWithUpdatedProgram()
    {
        var profileIds = new List<Guid> { ProfileId };
        var addProfilesDto = new AnalysisProgramAddProfilesDto(profileIds);
        var updated = CreateProgramDto();
        _analysisProgramServiceMock
            .Setup(x => x.AddProfilesAsync(ProgramId, profileIds, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updated);

        var result = await _sut.AddProfiles(ProgramId, addProfilesDto, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(updated);
    }

    [Fact]
    public async Task AddProfiles_WhenNotFound_ShouldReturnNotFound()
    {
        var profileIds = new List<Guid> { ProfileId };
        var addProfilesDto = new AnalysisProgramAddProfilesDto(profileIds);
        _analysisProgramServiceMock
            .Setup(x => x.AddProfilesAsync(ProgramId, profileIds, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AnalysisProgramDto?)null);

        var result = await _sut.AddProfiles(ProgramId, addProfilesDto, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task RemoveProfile_ShouldReturnNoContent()
    {
        _analysisProgramServiceMock
            .Setup(x => x.RemoveProfileAsync(ProgramId, ProfileId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.RemoveProfile(ProgramId, ProfileId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task RemoveProfile_WhenNotFound_ShouldReturnNotFound()
    {
        _analysisProgramServiceMock
            .Setup(x => x.RemoveProfileAsync(ProgramId, ProfileId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.RemoveProfile(ProgramId, ProfileId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void Controller_ShouldHaveAuthorizeAttribute()
    {
        var attributes = typeof(AnalysisProgramsController).GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Controller_ShouldHaveApiControllerAttribute()
    {
        var attributes = typeof(AnalysisProgramsController).GetCustomAttributes(typeof(ApiControllerAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Controller_ShouldHaveRouteAttribute()
    {
        var attributes = typeof(AnalysisProgramsController).GetCustomAttributes(typeof(RouteAttribute), true);
        attributes.Should().NotBeEmpty();
        var routeAttr = attributes.OfType<RouteAttribute>().First();
        routeAttr.Template.Should().Be("api/analysis-programs");
    }

    [Fact]
    public void GetAll_ShouldHaveHttpGetAttribute()
    {
        var method = typeof(AnalysisProgramsController).GetMethod(nameof(AnalysisProgramsController.GetAll));
        var attributes = method!.GetCustomAttributes(typeof(HttpGetAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void GetById_ShouldHaveHttpGetAttribute()
    {
        var method = typeof(AnalysisProgramsController).GetMethod(nameof(AnalysisProgramsController.GetById));
        var attributes = method!.GetCustomAttributes(typeof(HttpGetAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_ShouldHaveHttpPostAttribute()
    {
        var method = typeof(AnalysisProgramsController).GetMethod(nameof(AnalysisProgramsController.Create));
        var attributes = method!.GetCustomAttributes(typeof(HttpPostAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_ShouldHaveAdministratorRoleAttribute()
    {
        var method = typeof(AnalysisProgramsController).GetMethod(nameof(AnalysisProgramsController.Create));
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var authorizeAttr = attributes.OfType<AuthorizeAttribute>().First();
        authorizeAttr.Roles.Should().Be("Administrator");
    }

    [Fact]
    public void Update_ShouldHaveHttpPutAttribute()
    {
        var method = typeof(AnalysisProgramsController).GetMethod(nameof(AnalysisProgramsController.Update));
        var attributes = method!.GetCustomAttributes(typeof(HttpPutAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Update_ShouldHaveAdministratorRoleAttribute()
    {
        var method = typeof(AnalysisProgramsController).GetMethod(nameof(AnalysisProgramsController.Update));
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var authorizeAttr = attributes.OfType<AuthorizeAttribute>().First();
        authorizeAttr.Roles.Should().Be("Administrator");
    }

    [Fact]
    public void ToggleStatus_ShouldHaveHttpPatchAttribute()
    {
        var method = typeof(AnalysisProgramsController).GetMethod(nameof(AnalysisProgramsController.ToggleStatus));
        var attributes = method!.GetCustomAttributes(typeof(HttpPatchAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void ToggleStatus_ShouldHaveAdministratorRoleAttribute()
    {
        var method = typeof(AnalysisProgramsController).GetMethod(nameof(AnalysisProgramsController.ToggleStatus));
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var authorizeAttr = attributes.OfType<AuthorizeAttribute>().First();
        authorizeAttr.Roles.Should().Be("Administrator");
    }

    [Fact]
    public void AddProfiles_ShouldHaveHttpPostAttribute()
    {
        var method = typeof(AnalysisProgramsController).GetMethod(nameof(AnalysisProgramsController.AddProfiles));
        var attributes = method!.GetCustomAttributes(typeof(HttpPostAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void AddProfiles_ShouldHaveAdministratorRoleAttribute()
    {
        var method = typeof(AnalysisProgramsController).GetMethod(nameof(AnalysisProgramsController.AddProfiles));
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var authorizeAttr = attributes.OfType<AuthorizeAttribute>().First();
        authorizeAttr.Roles.Should().Be("Administrator");
    }

    [Fact]
    public void RemoveProfile_ShouldHaveHttpDeleteAttribute()
    {
        var method = typeof(AnalysisProgramsController).GetMethod(nameof(AnalysisProgramsController.RemoveProfile));
        var attributes = method!.GetCustomAttributes(typeof(HttpDeleteAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void RemoveProfile_ShouldHaveAdministratorRoleAttribute()
    {
        var method = typeof(AnalysisProgramsController).GetMethod(nameof(AnalysisProgramsController.RemoveProfile));
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var authorizeAttr = attributes.OfType<AuthorizeAttribute>().First();
        authorizeAttr.Roles.Should().Be("Administrator");
    }
}
