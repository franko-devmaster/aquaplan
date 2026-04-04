using AquaPlan.Api.Controllers.Api;
using AquaPlan.Application.DTOs.ChangeRequests;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace AquaPlan.Api.Tests.Controllers;

public class SamplingLocationChangeRequestsControllerTest
{
    private readonly Mock<ISamplingLocationChangeRequestService> _changeRequestServiceMock = new();
    private readonly Mock<ILogger<SamplingLocationChangeRequestsController>> _loggerMock = new();
    private readonly SamplingLocationChangeRequestsController _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid RequestId = Guid.Parse("00000000-0000-0000-0000-000000000050");
    private static readonly Guid SamplingLocationId = Guid.Parse("00000000-0000-0000-0000-000000000060");
    private static readonly Guid DistributorId = Guid.Parse("00000000-0000-0000-0000-000000000020");
    private const string UserId = "user-1";

    public SamplingLocationChangeRequestsControllerTest()
    {
        _sut = new SamplingLocationChangeRequestsController(
            _changeRequestServiceMock.Object,
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
            new Claim(ClaimTypes.NameIdentifier, UserId),
            new Claim("tenant_id", tenantId),
        ], "test"));
    }

    private static ChangeRequestDto CreateChangeRequestDto(
        ChangeRequestType requestType = ChangeRequestType.Create,
        ChangeRequestStatus status = ChangeRequestStatus.Pending)
    {
        return new ChangeRequestDto(
            RequestId,
            requestType,
            status,
            SamplingLocationId,
            "Source des Mosses",
            DistributorId,
            "Eau de Fribourg",
            "Source des Mosses",
            "SRC-001",
            46.8,
            7.15,
            "Description",
            UserId,
            "Jean Dupont",
            DateTime.UtcNow,
            null,
            null,
            null,
            null);
    }

    // --- SubmitCreateRequest ---

    [Fact]
    public async Task SubmitCreateRequest_ShouldReturnCreatedAtAction()
    {
        var createDto = new ChangeRequestCreateDto("Source des Mosses", "SRC-001", 46.8, 7.15, "Description", DistributorId);
        var created = CreateChangeRequestDto();
        _changeRequestServiceMock
            .Setup(x => x.SubmitCreateRequestAsync(createDto, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var result = await _sut.SubmitCreateRequest(createDto, CancellationToken.None);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(SamplingLocationChangeRequestsController.GetById));
        createdResult.Value.Should().Be(created);
    }

    [Fact]
    public async Task SubmitCreateRequest_WhenUnauthorized_ShouldReturnForbid()
    {
        var createDto = new ChangeRequestCreateDto("Source des Mosses", "SRC-001", 46.8, 7.15, "Description", DistributorId);
        _changeRequestServiceMock
            .Setup(x => x.SubmitCreateRequestAsync(createDto, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException());

        var result = await _sut.SubmitCreateRequest(createDto, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    // --- SubmitUpdateRequest ---

    [Fact]
    public async Task SubmitUpdateRequest_ShouldReturnCreatedAtAction()
    {
        var updateDto = new ChangeRequestUpdateDto("Source modifiée", "SRC-002", 46.9, 7.16, "Nouvelle description");
        var created = CreateChangeRequestDto(ChangeRequestType.Update);
        _changeRequestServiceMock
            .Setup(x => x.SubmitUpdateRequestAsync(SamplingLocationId, updateDto, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var result = await _sut.SubmitUpdateRequest(SamplingLocationId, updateDto, CancellationToken.None);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(SamplingLocationChangeRequestsController.GetById));
        createdResult.Value.Should().Be(created);
    }

    [Fact]
    public async Task SubmitUpdateRequest_WhenNotFound_ShouldReturnNotFound()
    {
        var updateDto = new ChangeRequestUpdateDto("Source modifiée", "SRC-002", 46.9, 7.16, "Nouvelle description");
        _changeRequestServiceMock
            .Setup(x => x.SubmitUpdateRequestAsync(SamplingLocationId, updateDto, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        var result = await _sut.SubmitUpdateRequest(SamplingLocationId, updateDto, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task SubmitUpdateRequest_WhenUnauthorized_ShouldReturnForbid()
    {
        var updateDto = new ChangeRequestUpdateDto("Source modifiée", "SRC-002", 46.9, 7.16, "Nouvelle description");
        _changeRequestServiceMock
            .Setup(x => x.SubmitUpdateRequestAsync(SamplingLocationId, updateDto, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException());

        var result = await _sut.SubmitUpdateRequest(SamplingLocationId, updateDto, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    // --- SubmitDeactivateRequest ---

    [Fact]
    public async Task SubmitDeactivateRequest_ShouldReturnCreatedAtAction()
    {
        var created = CreateChangeRequestDto(ChangeRequestType.Deactivate);
        _changeRequestServiceMock
            .Setup(x => x.SubmitDeactivateRequestAsync(SamplingLocationId, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var result = await _sut.SubmitDeactivateRequest(SamplingLocationId, CancellationToken.None);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(SamplingLocationChangeRequestsController.GetById));
        createdResult.Value.Should().Be(created);
    }

    [Fact]
    public async Task SubmitDeactivateRequest_WhenNotFound_ShouldReturnNotFound()
    {
        _changeRequestServiceMock
            .Setup(x => x.SubmitDeactivateRequestAsync(SamplingLocationId, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        var result = await _sut.SubmitDeactivateRequest(SamplingLocationId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task SubmitDeactivateRequest_WhenUnauthorized_ShouldReturnForbid()
    {
        _changeRequestServiceMock
            .Setup(x => x.SubmitDeactivateRequestAsync(SamplingLocationId, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException());

        var result = await _sut.SubmitDeactivateRequest(SamplingLocationId, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    // --- GetMyRequests ---

    [Fact]
    public async Task GetMyRequests_ShouldReturnOkWithRequests()
    {
        var requests = new List<ChangeRequestDto>
        {
            CreateChangeRequestDto(),
            CreateChangeRequestDto(ChangeRequestType.Update),
        };
        _changeRequestServiceMock
            .Setup(x => x.GetMyRequestsAsync(UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(requests);

        var result = await _sut.GetMyRequests(CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(requests);
    }

    // --- GetPendingRequests ---

    [Fact]
    public async Task GetPendingRequests_ShouldReturnOkWithRequests()
    {
        var requests = new List<ChangeRequestDto>
        {
            CreateChangeRequestDto(),
        };
        _changeRequestServiceMock
            .Setup(x => x.GetPendingRequestsAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(requests);

        var result = await _sut.GetPendingRequests(CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(requests);
    }

    [Fact]
    public void GetPendingRequests_ShouldHaveAdministratorRoleAttribute()
    {
        var method = typeof(SamplingLocationChangeRequestsController).GetMethod(nameof(SamplingLocationChangeRequestsController.GetPendingRequests));
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var authorizeAttr = attributes.OfType<AuthorizeAttribute>().First();
        authorizeAttr.Roles.Should().Be("Administrator");
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_ShouldReturnOkWithRequest()
    {
        var request = CreateChangeRequestDto();
        _changeRequestServiceMock
            .Setup(x => x.GetByIdAsync(RequestId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var result = await _sut.GetById(RequestId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(request);
    }

    [Fact]
    public async Task GetById_WhenNotFound_ShouldReturnNotFound()
    {
        _changeRequestServiceMock
            .Setup(x => x.GetByIdAsync(RequestId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChangeRequestDto?)null);

        var result = await _sut.GetById(RequestId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    // --- Approve ---

    [Fact]
    public async Task Approve_ShouldReturnOkWithApprovedRequest()
    {
        var reviewDto = new ChangeRequestReviewDto("Approuvé");
        var approved = CreateChangeRequestDto(status: ChangeRequestStatus.Approved);
        _changeRequestServiceMock
            .Setup(x => x.ApproveAsync(RequestId, "Approuvé", UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(approved);

        var result = await _sut.Approve(RequestId, reviewDto, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(approved);
    }

    [Fact]
    public async Task Approve_WhenNotFound_ShouldReturnNotFound()
    {
        var reviewDto = new ChangeRequestReviewDto("Approuvé");
        _changeRequestServiceMock
            .Setup(x => x.ApproveAsync(RequestId, "Approuvé", UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChangeRequestDto?)null);

        var result = await _sut.Approve(RequestId, reviewDto, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void Approve_ShouldHaveAdministratorRoleAttribute()
    {
        var method = typeof(SamplingLocationChangeRequestsController).GetMethod(nameof(SamplingLocationChangeRequestsController.Approve));
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var authorizeAttr = attributes.OfType<AuthorizeAttribute>().First();
        authorizeAttr.Roles.Should().Be("Administrator");
    }

    // --- Reject ---

    [Fact]
    public async Task Reject_ShouldReturnOkWithRejectedRequest()
    {
        var reviewDto = new ChangeRequestReviewDto("Données incomplètes");
        var rejected = CreateChangeRequestDto(status: ChangeRequestStatus.Rejected);
        _changeRequestServiceMock
            .Setup(x => x.RejectAsync(RequestId, "Données incomplètes", UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rejected);

        var result = await _sut.Reject(RequestId, reviewDto, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(rejected);
    }

    [Fact]
    public async Task Reject_WhenNotFound_ShouldReturnNotFound()
    {
        var reviewDto = new ChangeRequestReviewDto("Données incomplètes");
        _changeRequestServiceMock
            .Setup(x => x.RejectAsync(RequestId, "Données incomplètes", UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChangeRequestDto?)null);

        var result = await _sut.Reject(RequestId, reviewDto, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Reject_WhenCommentIsEmpty_ShouldReturnBadRequest(string? comment)
    {
        var reviewDto = new ChangeRequestReviewDto(comment);

        var result = await _sut.Reject(RequestId, reviewDto, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void Reject_ShouldHaveAdministratorRoleAttribute()
    {
        var method = typeof(SamplingLocationChangeRequestsController).GetMethod(nameof(SamplingLocationChangeRequestsController.Reject));
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var authorizeAttr = attributes.OfType<AuthorizeAttribute>().First();
        authorizeAttr.Roles.Should().Be("Administrator");
    }

    // --- Controller Attributes ---

    [Fact]
    public void Controller_ShouldHaveAuthorizeAttribute()
    {
        var attributes = typeof(SamplingLocationChangeRequestsController).GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Controller_ShouldHaveApiControllerAttribute()
    {
        var attributes = typeof(SamplingLocationChangeRequestsController).GetCustomAttributes(typeof(ApiControllerAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Controller_ShouldHaveRouteAttribute()
    {
        var attributes = typeof(SamplingLocationChangeRequestsController).GetCustomAttributes(typeof(RouteAttribute), true);
        attributes.Should().NotBeEmpty();
        var routeAttr = attributes.OfType<RouteAttribute>().First();
        routeAttr.Template.Should().Be("api/sampling-location-requests");
    }

    // --- HTTP Method Attributes ---

    [Fact]
    public void SubmitCreateRequest_ShouldHaveHttpPostAttribute()
    {
        var method = typeof(SamplingLocationChangeRequestsController).GetMethod(nameof(SamplingLocationChangeRequestsController.SubmitCreateRequest));
        var attributes = method!.GetCustomAttributes(typeof(HttpPostAttribute), true);
        attributes.Should().NotBeEmpty();
        var httpPostAttr = attributes.OfType<HttpPostAttribute>().First();
        httpPostAttr.Template.Should().Be("create");
    }

    [Fact]
    public void SubmitUpdateRequest_ShouldHaveHttpPostAttribute()
    {
        var method = typeof(SamplingLocationChangeRequestsController).GetMethod(nameof(SamplingLocationChangeRequestsController.SubmitUpdateRequest));
        var attributes = method!.GetCustomAttributes(typeof(HttpPostAttribute), true);
        attributes.Should().NotBeEmpty();
        var httpPostAttr = attributes.OfType<HttpPostAttribute>().First();
        httpPostAttr.Template.Should().Be("{slId:guid}/update");
    }

    [Fact]
    public void SubmitDeactivateRequest_ShouldHaveHttpPostAttribute()
    {
        var method = typeof(SamplingLocationChangeRequestsController).GetMethod(nameof(SamplingLocationChangeRequestsController.SubmitDeactivateRequest));
        var attributes = method!.GetCustomAttributes(typeof(HttpPostAttribute), true);
        attributes.Should().NotBeEmpty();
        var httpPostAttr = attributes.OfType<HttpPostAttribute>().First();
        httpPostAttr.Template.Should().Be("{slId:guid}/deactivate");
    }

    [Fact]
    public void GetMyRequests_ShouldHaveHttpGetAttribute()
    {
        var method = typeof(SamplingLocationChangeRequestsController).GetMethod(nameof(SamplingLocationChangeRequestsController.GetMyRequests));
        var attributes = method!.GetCustomAttributes(typeof(HttpGetAttribute), true);
        attributes.Should().NotBeEmpty();
        var httpGetAttr = attributes.OfType<HttpGetAttribute>().First();
        httpGetAttr.Template.Should().Be("my");
    }

    [Fact]
    public void GetPendingRequests_ShouldHaveHttpGetAttribute()
    {
        var method = typeof(SamplingLocationChangeRequestsController).GetMethod(nameof(SamplingLocationChangeRequestsController.GetPendingRequests));
        var attributes = method!.GetCustomAttributes(typeof(HttpGetAttribute), true);
        attributes.Should().NotBeEmpty();
        var httpGetAttr = attributes.OfType<HttpGetAttribute>().First();
        httpGetAttr.Template.Should().Be("pending");
    }

    [Fact]
    public void GetById_ShouldHaveHttpGetAttribute()
    {
        var method = typeof(SamplingLocationChangeRequestsController).GetMethod(nameof(SamplingLocationChangeRequestsController.GetById));
        var attributes = method!.GetCustomAttributes(typeof(HttpGetAttribute), true);
        attributes.Should().NotBeEmpty();
        var httpGetAttr = attributes.OfType<HttpGetAttribute>().First();
        httpGetAttr.Template.Should().Be("{id:guid}");
    }

    [Fact]
    public void Approve_ShouldHaveHttpPostAttribute()
    {
        var method = typeof(SamplingLocationChangeRequestsController).GetMethod(nameof(SamplingLocationChangeRequestsController.Approve));
        var attributes = method!.GetCustomAttributes(typeof(HttpPostAttribute), true);
        attributes.Should().NotBeEmpty();
        var httpPostAttr = attributes.OfType<HttpPostAttribute>().First();
        httpPostAttr.Template.Should().Be("{id:guid}/approve");
    }

    [Fact]
    public void Reject_ShouldHaveHttpPostAttribute()
    {
        var method = typeof(SamplingLocationChangeRequestsController).GetMethod(nameof(SamplingLocationChangeRequestsController.Reject));
        var attributes = method!.GetCustomAttributes(typeof(HttpPostAttribute), true);
        attributes.Should().NotBeEmpty();
        var httpPostAttr = attributes.OfType<HttpPostAttribute>().First();
        httpPostAttr.Template.Should().Be("{id:guid}/reject");
    }
}
