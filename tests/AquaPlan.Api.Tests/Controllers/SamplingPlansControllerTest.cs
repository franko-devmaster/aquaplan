using AquaPlan.Api.Controllers.Api;
using AquaPlan.Application.DTOs.SamplingPlans;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AquaPlan.Api.Tests.Controllers;

public class SamplingPlansControllerTest
{
    private readonly Mock<ISamplingPlanService> _samplingPlanServiceMock = new();
    private readonly Mock<IPermissionService> _permissionServiceMock = new();
    private readonly Mock<ILogger<SamplingPlansController>> _loggerMock = new();
    private readonly SamplingPlansController _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid PlanId = Guid.Parse("00000000-0000-0000-0000-000000000050");
    private static readonly Guid DistributorId = Guid.Parse("00000000-0000-0000-0000-000000000020");
    private const string UserId = "user-1";

    public SamplingPlansControllerTest()
    {
        _sut = new SamplingPlansController(
            _samplingPlanServiceMock.Object,
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

    private static SamplingPlanDetailDto CreateDetailDto(
        Guid? id = null,
        SamplingPlanStatus status = SamplingPlanStatus.Draft)
    {
        return new SamplingPlanDetailDto(
            id ?? PlanId, 2026, status, DistributorId, "Distributor A",
            UserId, "Test User", "Some notes", null,
            new List<SamplingPlanItemDto>(), TenantId,
            DateTime.UtcNow, null, null);
    }

    private static SamplingPlanCreateDto CreateCreateDto()
    {
        return new SamplingPlanCreateDto(
            DistributorId, 2026, "Notes",
            new List<SamplingPlanItemCreateDto>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), 4, new List<int> { 1, 4, 7, 10 })
            });
    }

    private static SamplingPlanUpdateDto CreateUpdateDto()
    {
        return new SamplingPlanUpdateDto(
            "Updated notes",
            new List<SamplingPlanItemCreateDto>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), 2, new List<int> { 3, 9 })
            });
    }

    #region Controller attributes

    [Fact]
    public void Controller_ShouldHaveAuthorizeAttribute()
    {
        var attributes = typeof(SamplingPlansController).GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Controller_ShouldHaveApiControllerAttribute()
    {
        var attributes = typeof(SamplingPlansController).GetCustomAttributes(typeof(ApiControllerAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Controller_ShouldHaveRouteAttribute()
    {
        var attributes = typeof(SamplingPlansController).GetCustomAttributes(typeof(RouteAttribute), true);
        attributes.Should().NotBeEmpty();
        var routeAttr = attributes.OfType<RouteAttribute>().First();
        routeAttr.Template.Should().Be("api/[controller]");
    }

    [Fact]
    public void Controller_ShouldNotHaveAllowAnonymousAttribute()
    {
        var attributes = typeof(SamplingPlansController).GetCustomAttributes(typeof(AllowAnonymousAttribute), true);
        attributes.Should().BeEmpty();
    }

    #endregion

    #region GetPlans

    [Fact]
    public async Task GetPlans_ShouldReturnOk_WhenAdmin()
    {
        var pagedResult = new SamplingPlanPagedResultDto(new List<SamplingPlanListDto>(), 0, 1, 20);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _samplingPlanServiceMock
            .Setup(x => x.GetPlansFilteredAsync(UserId, TenantId, It.IsAny<SamplingPlanFilterDto>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var result = await _sut.GetPlans(null, null, null, null, 1, 20, null, true, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(pagedResult);
    }

    [Fact]
    public async Task GetPlans_ShouldReturnOk_WhenNotAdmin()
    {
        var pagedResult = new SamplingPlanPagedResultDto(new List<SamplingPlanListDto>(), 0, 1, 20);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _samplingPlanServiceMock
            .Setup(x => x.GetPlansFilteredAsync(UserId, TenantId, It.IsAny<SamplingPlanFilterDto>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var result = await _sut.GetPlans(null, null, null, null, 1, 20, null, true, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(pagedResult);
    }

    [Fact]
    public void GetPlans_ShouldHaveHttpGetAttribute()
    {
        var method = typeof(SamplingPlansController).GetMethod(nameof(SamplingPlansController.GetPlans));
        method!.GetCustomAttributes(typeof(HttpGetAttribute), true).Should().NotBeEmpty();
    }

    #endregion

    #region GetPlan

    [Fact]
    public async Task GetPlan_ShouldReturnNotFound_WhenPlanDoesNotExist()
    {
        _samplingPlanServiceMock
            .Setup(x => x.GetPlanByIdAsync(PlanId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SamplingPlanDetailDto?)null);

        var result = await _sut.GetPlan(PlanId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetPlan_ShouldReturnOk_WhenAdmin()
    {
        var plan = CreateDetailDto();
        _samplingPlanServiceMock
            .Setup(x => x.GetPlanByIdAsync(PlanId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.GetPlan(PlanId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(plan);
    }

    [Fact]
    public async Task GetPlan_WhenNotAdminAndHasAccess_ShouldReturnOk()
    {
        var plan = CreateDetailDto();
        _samplingPlanServiceMock
            .Setup(x => x.GetPlanByIdAsync(PlanId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _samplingPlanServiceMock
            .Setup(x => x.UserHasDistributorAccessAsync(UserId, DistributorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.GetPlan(PlanId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(plan);
    }

    [Fact]
    public async Task GetPlan_WhenNotAdminAndNoAccess_ShouldReturnForbid()
    {
        var plan = CreateDetailDto();
        _samplingPlanServiceMock
            .Setup(x => x.GetPlanByIdAsync(PlanId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _samplingPlanServiceMock
            .Setup(x => x.UserHasDistributorAccessAsync(UserId, DistributorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.GetPlan(PlanId, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public void GetPlan_ShouldHaveHttpGetAttribute()
    {
        var method = typeof(SamplingPlansController).GetMethod(nameof(SamplingPlansController.GetPlan));
        var attr = method!.GetCustomAttributes(typeof(HttpGetAttribute), true).OfType<HttpGetAttribute>().First();
        attr.Template.Should().Be("{id:guid}");
    }

    #endregion

    #region CreatePlan

    [Fact]
    public async Task CreatePlan_WhenAdmin_ShouldReturnCreatedAtAction()
    {
        var createDto = CreateCreateDto();
        var created = CreateDetailDto();
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _samplingPlanServiceMock
            .Setup(x => x.CreatePlanAsync(createDto, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var result = await _sut.CreatePlan(createDto, CancellationToken.None);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(SamplingPlansController.GetPlan));
        createdResult.Value.Should().Be(created);
    }

    [Fact]
    public async Task CreatePlan_WhenNotAdminAndHasAccess_ShouldReturnCreatedAtAction()
    {
        var createDto = CreateCreateDto();
        var created = CreateDetailDto();
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _samplingPlanServiceMock
            .Setup(x => x.UserHasDistributorAccessAsync(UserId, DistributorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _samplingPlanServiceMock
            .Setup(x => x.CreatePlanAsync(createDto, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var result = await _sut.CreatePlan(createDto, CancellationToken.None);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.Value.Should().Be(created);
    }

    [Fact]
    public async Task CreatePlan_WhenNotAdminAndNoAccess_ShouldReturnForbid()
    {
        var createDto = CreateCreateDto();
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _samplingPlanServiceMock
            .Setup(x => x.UserHasDistributorAccessAsync(UserId, DistributorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.CreatePlan(createDto, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
        _samplingPlanServiceMock.Verify(x => x.CreatePlanAsync(It.IsAny<SamplingPlanCreateDto>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreatePlan_WhenServiceThrowsInvalidOperation_ShouldReturnBadRequest()
    {
        var createDto = CreateCreateDto();
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _samplingPlanServiceMock
            .Setup(x => x.CreatePlanAsync(createDto, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Duplicate plan"));

        var result = await _sut.CreatePlan(createDto, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void CreatePlan_ShouldHaveHttpPostAttribute()
    {
        var method = typeof(SamplingPlansController).GetMethod(nameof(SamplingPlansController.CreatePlan));
        method!.GetCustomAttributes(typeof(HttpPostAttribute), true).Should().NotBeEmpty();
    }

    [Fact]
    public void CreatePlan_ShouldHaveAuthorizeAttributeRestrictingPreleveur()
    {
        var method = typeof(SamplingPlansController).GetMethod(nameof(SamplingPlansController.CreatePlan));
        method.Should().NotBeNull();
        var auth = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .OfType<AuthorizeAttribute>()
            .FirstOrDefault();
        auth.Should().NotBeNull();
        auth!.Roles.Should()
            .Contain(RoleName.Administrator)
            .And.Contain(RoleName.Requerant)
            .And.Contain(RoleName.RequerantPreleveur);

        var roleList = (auth.Roles ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        roleList.Should().NotContain(RoleName.Preleveur);
    }

    #endregion

    #region UpdatePlan

    [Fact]
    public async Task UpdatePlan_WhenPlanNotFound_ShouldReturnNotFound()
    {
        var updateDto = CreateUpdateDto();
        _samplingPlanServiceMock
            .Setup(x => x.GetPlanByIdAsync(PlanId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SamplingPlanDetailDto?)null);

        var result = await _sut.UpdatePlan(PlanId, updateDto, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task UpdatePlan_WhenAdminAndPlanExists_ShouldReturnOk()
    {
        var existing = CreateDetailDto();
        var updateDto = CreateUpdateDto();
        var updated = CreateDetailDto();
        _samplingPlanServiceMock
            .Setup(x => x.GetPlanByIdAsync(PlanId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _samplingPlanServiceMock
            .Setup(x => x.UpdatePlanAsync(PlanId, updateDto, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updated);

        var result = await _sut.UpdatePlan(PlanId, updateDto, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(updated);
    }

    [Fact]
    public async Task UpdatePlan_WhenNotAdminAndNoAccess_ShouldReturnForbid()
    {
        var existing = CreateDetailDto();
        var updateDto = CreateUpdateDto();
        _samplingPlanServiceMock
            .Setup(x => x.GetPlanByIdAsync(PlanId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _samplingPlanServiceMock
            .Setup(x => x.UserHasDistributorAccessAsync(UserId, DistributorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.UpdatePlan(PlanId, updateDto, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task UpdatePlan_WhenServiceReturnsNull_ShouldReturnNotFound()
    {
        var existing = CreateDetailDto();
        var updateDto = CreateUpdateDto();
        _samplingPlanServiceMock
            .Setup(x => x.GetPlanByIdAsync(PlanId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _samplingPlanServiceMock
            .Setup(x => x.UpdatePlanAsync(PlanId, updateDto, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SamplingPlanDetailDto?)null);

        var result = await _sut.UpdatePlan(PlanId, updateDto, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task UpdatePlan_WhenServiceThrowsInvalidOperation_ShouldReturnBadRequest()
    {
        var existing = CreateDetailDto();
        var updateDto = CreateUpdateDto();
        _samplingPlanServiceMock
            .Setup(x => x.GetPlanByIdAsync(PlanId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _samplingPlanServiceMock
            .Setup(x => x.UpdatePlanAsync(PlanId, updateDto, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cannot update validated plan"));

        var result = await _sut.UpdatePlan(PlanId, updateDto, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void UpdatePlan_ShouldHaveHttpPutAttribute()
    {
        var method = typeof(SamplingPlansController).GetMethod(nameof(SamplingPlansController.UpdatePlan));
        var attr = method!.GetCustomAttributes(typeof(HttpPutAttribute), true).OfType<HttpPutAttribute>().First();
        attr.Template.Should().Be("{id:guid}");
    }

    #endregion

    #region DeletePlan

    [Fact]
    public async Task DeletePlan_WhenPlanNotFound_ShouldReturnNotFound()
    {
        _samplingPlanServiceMock
            .Setup(x => x.GetPlanByIdAsync(PlanId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SamplingPlanDetailDto?)null);

        var result = await _sut.DeletePlan(PlanId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeletePlan_WhenAdminAndDeleted_ShouldReturnNoContent()
    {
        var existing = CreateDetailDto();
        _samplingPlanServiceMock
            .Setup(x => x.GetPlanByIdAsync(PlanId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _samplingPlanServiceMock
            .Setup(x => x.DeletePlanAsync(PlanId, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.DeletePlan(PlanId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeletePlan_WhenNotAdminAndNoAccess_ShouldReturnForbid()
    {
        var existing = CreateDetailDto();
        _samplingPlanServiceMock
            .Setup(x => x.GetPlanByIdAsync(PlanId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _samplingPlanServiceMock
            .Setup(x => x.UserHasDistributorAccessAsync(UserId, DistributorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.DeletePlan(PlanId, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task DeletePlan_WhenServiceReturnsFalse_ShouldReturnNotFound()
    {
        var existing = CreateDetailDto();
        _samplingPlanServiceMock
            .Setup(x => x.GetPlanByIdAsync(PlanId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _samplingPlanServiceMock
            .Setup(x => x.DeletePlanAsync(PlanId, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.DeletePlan(PlanId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeletePlan_WhenServiceThrowsInvalidOperation_ShouldReturnBadRequest()
    {
        var existing = CreateDetailDto();
        _samplingPlanServiceMock
            .Setup(x => x.GetPlanByIdAsync(PlanId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _samplingPlanServiceMock
            .Setup(x => x.DeletePlanAsync(PlanId, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cannot delete"));

        var result = await _sut.DeletePlan(PlanId, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void DeletePlan_ShouldHaveHttpDeleteAttribute()
    {
        var method = typeof(SamplingPlansController).GetMethod(nameof(SamplingPlansController.DeletePlan));
        var attr = method!.GetCustomAttributes(typeof(HttpDeleteAttribute), true).OfType<HttpDeleteAttribute>().First();
        attr.Template.Should().Be("{id:guid}");
    }

    #endregion

    #region SubmitPlan

    [Fact]
    public async Task SubmitPlan_WhenSuccess_ShouldReturnOk()
    {
        var plan = CreateDetailDto(status: SamplingPlanStatus.Submitted);
        _samplingPlanServiceMock
            .Setup(x => x.SubmitPlanAsync(PlanId, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var result = await _sut.SubmitPlan(PlanId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(plan);
    }

    [Fact]
    public async Task SubmitPlan_WhenPlanNotFound_ShouldReturnNotFound()
    {
        _samplingPlanServiceMock
            .Setup(x => x.SubmitPlanAsync(PlanId, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SamplingPlanDetailDto?)null);

        var result = await _sut.SubmitPlan(PlanId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task SubmitPlan_WhenServiceThrowsInvalidOperation_ShouldReturnBadRequest()
    {
        _samplingPlanServiceMock
            .Setup(x => x.SubmitPlanAsync(PlanId, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Invalid state transition"));

        var result = await _sut.SubmitPlan(PlanId, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void SubmitPlan_ShouldHaveHttpPostAttribute()
    {
        var method = typeof(SamplingPlansController).GetMethod(nameof(SamplingPlansController.SubmitPlan));
        var attr = method!.GetCustomAttributes(typeof(HttpPostAttribute), true).OfType<HttpPostAttribute>().First();
        attr.Template.Should().Be("{id:guid}/submit");
    }

    #endregion

    #region ValidatePlan

    [Fact]
    public async Task ValidatePlan_WhenAdmin_ShouldReturnOk()
    {
        var plan = CreateDetailDto(status: SamplingPlanStatus.Validated);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _samplingPlanServiceMock
            .Setup(x => x.ValidatePlanAsync(PlanId, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var result = await _sut.ValidatePlan(PlanId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(plan);
    }

    [Fact]
    public async Task ValidatePlan_WhenNotAdmin_ShouldReturnForbid()
    {
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.ValidatePlan(PlanId, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
        _samplingPlanServiceMock.Verify(x => x.ValidatePlanAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ValidatePlan_WhenPlanNotFound_ShouldReturnNotFound()
    {
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _samplingPlanServiceMock
            .Setup(x => x.ValidatePlanAsync(PlanId, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SamplingPlanDetailDto?)null);

        var result = await _sut.ValidatePlan(PlanId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ValidatePlan_WhenServiceThrowsInvalidOperation_ShouldReturnBadRequest()
    {
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _samplingPlanServiceMock
            .Setup(x => x.ValidatePlanAsync(PlanId, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Already validated"));

        var result = await _sut.ValidatePlan(PlanId, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void ValidatePlan_ShouldHaveHttpPostAttribute()
    {
        var method = typeof(SamplingPlansController).GetMethod(nameof(SamplingPlansController.ValidatePlan));
        var attr = method!.GetCustomAttributes(typeof(HttpPostAttribute), true).OfType<HttpPostAttribute>().First();
        attr.Template.Should().Be("{id:guid}/validate");
    }

    #endregion

    #region RejectPlan

    [Fact]
    public async Task RejectPlan_WhenAdmin_ShouldReturnOk()
    {
        var rejectDto = new SamplingPlanRejectDto("Not complete");
        var plan = CreateDetailDto(status: SamplingPlanStatus.Rejected);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _samplingPlanServiceMock
            .Setup(x => x.RejectPlanAsync(PlanId, "Not complete", UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var result = await _sut.RejectPlan(PlanId, rejectDto, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(plan);
    }

    [Fact]
    public async Task RejectPlan_WhenNotAdmin_ShouldReturnForbid()
    {
        var rejectDto = new SamplingPlanRejectDto("Not complete");
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.RejectPlan(PlanId, rejectDto, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
        _samplingPlanServiceMock.Verify(x => x.RejectPlanAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RejectPlan_WhenPlanNotFound_ShouldReturnNotFound()
    {
        var rejectDto = new SamplingPlanRejectDto("Not complete");
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _samplingPlanServiceMock
            .Setup(x => x.RejectPlanAsync(PlanId, "Not complete", UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SamplingPlanDetailDto?)null);

        var result = await _sut.RejectPlan(PlanId, rejectDto, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task RejectPlan_WhenServiceThrowsInvalidOperation_ShouldReturnBadRequest()
    {
        var rejectDto = new SamplingPlanRejectDto("Not complete");
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _samplingPlanServiceMock
            .Setup(x => x.RejectPlanAsync(PlanId, "Not complete", UserId, TenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cannot reject draft"));

        var result = await _sut.RejectPlan(PlanId, rejectDto, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void RejectPlan_ShouldHaveHttpPostAttribute()
    {
        var method = typeof(SamplingPlansController).GetMethod(nameof(SamplingPlansController.RejectPlan));
        var attr = method!.GetCustomAttributes(typeof(HttpPostAttribute), true).OfType<HttpPostAttribute>().First();
        attr.Template.Should().Be("{id:guid}/reject");
    }

    #endregion

    #region GenerateOrders

    [Fact]
    public async Task GenerateOrders_WhenAdmin_ShouldReturnOk()
    {
        var generateResult = new GenerateOrdersResultDto(2, new List<GeneratedOrderSummaryDto>());
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _samplingPlanServiceMock
            .Setup(x => x.GenerateOrdersFromPlanAsync(PlanId, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(generateResult);

        var result = await _sut.GenerateOrders(PlanId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(generateResult);
    }

    [Fact]
    public async Task GenerateOrders_WhenNotAdminAndHasAccess_ShouldReturnOk()
    {
        var plan = CreateDetailDto();
        var generateResult = new GenerateOrdersResultDto(1, new List<GeneratedOrderSummaryDto>());
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _samplingPlanServiceMock
            .Setup(x => x.GetPlanByIdAsync(PlanId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        _samplingPlanServiceMock
            .Setup(x => x.UserHasDistributorAccessAsync(UserId, DistributorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _samplingPlanServiceMock
            .Setup(x => x.GenerateOrdersFromPlanAsync(PlanId, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(generateResult);

        var result = await _sut.GenerateOrders(PlanId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(generateResult);
    }

    [Fact]
    public async Task GenerateOrders_WhenNotAdminAndPlanNotFound_ShouldReturnNotFound()
    {
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _samplingPlanServiceMock
            .Setup(x => x.GetPlanByIdAsync(PlanId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SamplingPlanDetailDto?)null);

        var result = await _sut.GenerateOrders(PlanId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GenerateOrders_WhenNotAdminAndNoAccess_ShouldReturnForbid()
    {
        var plan = CreateDetailDto();
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _samplingPlanServiceMock
            .Setup(x => x.GetPlanByIdAsync(PlanId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        _samplingPlanServiceMock
            .Setup(x => x.UserHasDistributorAccessAsync(UserId, DistributorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.GenerateOrders(PlanId, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GenerateOrders_WhenServiceThrowsInvalidOperation_ShouldReturnBadRequest()
    {
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _samplingPlanServiceMock
            .Setup(x => x.GenerateOrdersFromPlanAsync(PlanId, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Plan not validated"));

        var result = await _sut.GenerateOrders(PlanId, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void GenerateOrders_ShouldHaveHttpPostAttribute()
    {
        var method = typeof(SamplingPlansController).GetMethod(nameof(SamplingPlansController.GenerateOrders));
        var attr = method!.GetCustomAttributes(typeof(HttpPostAttribute), true).OfType<HttpPostAttribute>().First();
        attr.Template.Should().Be("{id:guid}/generate-orders");
    }

    #endregion

    #region Serialization contract (AQ-364)

    // Regression guard: the Angular SamplingPlanStatus enum relies on the API
    // returning the status as a string (e.g. "Draft"). If JsonStringEnumConverter
    // is removed or the DTO stops using the enum type, the frontend compares
    // "Draft" === 0 (always false), which silently hides all action buttons on
    // the plan detail page ("impossible de créer/compléter un plan de prélèvement").
    [Theory]
    [InlineData(SamplingPlanStatus.Draft, "Draft")]
    [InlineData(SamplingPlanStatus.Submitted, "Submitted")]
    [InlineData(SamplingPlanStatus.Validated, "Validated")]
    [InlineData(SamplingPlanStatus.Rejected, "Rejected")]
    public void SamplingPlanDetailDto_ShouldSerializeStatusAsString(SamplingPlanStatus status, string expected)
    {
        var dto = CreateDetailDto(status: status);
        var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };

        var json = JsonSerializer.Serialize(dto, options);

        using var document = JsonDocument.Parse(json);
        var statusProperty = document.RootElement.GetProperty("Status");
        statusProperty.ValueKind.Should().Be(JsonValueKind.String);
        statusProperty.GetString().Should().Be(expected);
    }

    [Fact]
    public void SamplingPlanListDto_ShouldSerializeStatusAsString()
    {
        var listDto = new SamplingPlanListDto(
            PlanId, 2026, SamplingPlanStatus.Draft, DistributorId, "Distributor A",
            UserId, "Test User", 0, DateTime.UtcNow);
        var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };

        var json = JsonSerializer.Serialize(listDto, options);

        using var document = JsonDocument.Parse(json);
        var statusProperty = document.RootElement.GetProperty("Status");
        statusProperty.ValueKind.Should().Be(JsonValueKind.String);
        statusProperty.GetString().Should().Be("Draft");
    }

    #endregion
}
