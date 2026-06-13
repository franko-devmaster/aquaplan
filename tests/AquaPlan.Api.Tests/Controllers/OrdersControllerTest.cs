using AquaPlan.Api.Controllers.Api;
using AquaPlan.Application.DTOs.Orders;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace AquaPlan.Api.Tests.Controllers;

public class OrdersControllerTest
{
    private readonly Mock<IOrderService> _orderServiceMock = new();
    private readonly Mock<IOrderStatusService> _orderStatusServiceMock = new();
    private readonly Mock<IPermissionService> _permissionServiceMock = new();
    private readonly Mock<IOrderAuditService> _orderAuditServiceMock = new();
    private readonly Mock<IDelegationService> _delegationServiceMock = new();
    private readonly Mock<ILimsResultService> _limsResultServiceMock = new();
    private readonly Mock<ILogger<OrdersController>> _loggerMock = new();
    private readonly OrdersController _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OrderId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid DistributorId = Guid.Parse("00000000-0000-0000-0000-000000000020");
    private const string UserId = "user-1";

    public OrdersControllerTest()
    {
        _sut = new OrdersController(_orderServiceMock.Object, _orderStatusServiceMock.Object, _permissionServiceMock.Object, _orderAuditServiceMock.Object, _delegationServiceMock.Object, _limsResultServiceMock.Object, _loggerMock.Object);
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

    private static OrderDetailDto CreateOrderDetail(
        Guid? id = null, string orderNumber = "ORD-001", OrderStatus status = OrderStatus.New,
        string? preleveurId = null, string? preleveurName = null)
    {
        return new OrderDetailDto(
            id ?? OrderId, orderNumber, status, false,
            null, null,
            UserId, "John Doe", preleveurId, preleveurName,
            DistributorId, "Distributor A",
            null, null, null, null, null, false, [],
            TenantId, DateTime.UtcNow, null, null, null,
            false, null, null, ResultsStatus.NotReceived);
    }

    private static OrderListDto CreateOrderList(Guid? id = null, string orderNumber = "ORD-001")
    {
        return new OrderListDto(
            id ?? OrderId, orderNumber, OrderStatus.New, false, null,
            UserId, "John Doe", null, null,
            DistributorId, "Distributor A",
            null, null, null, false, DateTime.UtcNow,
            ResultsStatus.NotReceived);
    }

    // ─── GetOrders ─────────────────────────────────────────────
    [Fact]
    public async Task GetOrders_ShouldReturnPagedResult_WhenAdminUser()
    {
        var pagedResult = new OrderPagedResultDto([CreateOrderList()], 1, 1, 20);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _orderServiceMock
            .Setup(x => x.GetOrdersFilteredAsync(UserId, TenantId, It.IsAny<OrderFilterDto>(), true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var result = await _sut.GetOrders(null, null, null, null, 1, 20, null, true, null, null, null, null, null, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value.Should().BeOfType<OrderPagedResultDto>().Subject;
        value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetOrders_ShouldPassFilterParams()
    {
        var statuses = new List<OrderStatus> { OrderStatus.New, OrderStatus.InProgress };
        var pagedResult = new OrderPagedResultDto([], 0, 1, 20);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orderServiceMock
            .Setup(x => x.GetOrdersFilteredAsync(UserId, TenantId,
                It.Is<OrderFilterDto>(f => f.Statuses!.Count == 2 && f.IsUnassigned == true && f.Search == "test"),
                false, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var result = await _sut.GetOrders(statuses, true, null, "test", 1, 20, null, true, null, null, null, null, null, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeOfType<OrderPagedResultDto>();
    }

    // ─── GetOrder ──────────────────────────────────────────────
    [Fact]
    public async Task GetOrder_ShouldReturnNotFound_WhenOrderDoesNotExist()
    {
        _orderServiceMock
            .Setup(x => x.GetOrderByIdAsync(OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderDetailDto?)null);

        var result = await _sut.GetOrder(OrderId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetOrder_ShouldReturnOk_WhenAdminUser()
    {
        var order = CreateOrderDetail();
        _orderServiceMock
            .Setup(x => x.GetOrderByIdAsync(OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.GetOrder(OrderId, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetOrder_ShouldReturnForbid_WhenUserHasNoAccess()
    {
        var order = CreateOrderDetail();
        _orderServiceMock
            .Setup(x => x.GetOrderByIdAsync(OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orderServiceMock
            .Setup(x => x.UserCanAccessOrderAsync(UserId, OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.GetOrder(OrderId, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    // ─── CreateOrder ───────────────────────────────────────────
    [Fact]
    public async Task CreateOrder_ShouldReturnCreatedAtAction_WhenAdmin()
    {
        var createDto = new OrderCreateDto(DistributorId, null, null, null, null, null, false);
        var createdOrder = CreateOrderDetail(orderNumber: "ORD-002");
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _orderServiceMock
            .Setup(x => x.CreateOrderAsync(createDto, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdOrder);

        var result = await _sut.CreateOrder(createDto, CancellationToken.None);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(OrdersController.GetOrder));
    }

    [Fact]
    public async Task CreateOrder_ShouldReturnForbid_WhenUserHasNoDistributorAccess()
    {
        var createDto = new OrderCreateDto(DistributorId, null, null, null, null, null, false);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        // AQ-369 — user's authorized distributors do not include DistributorId
        _delegationServiceMock
            .Setup(x => x.GetAuthorizedDistributorIdsForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());

        var result = await _sut.CreateOrder(createDto, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task CreateOrder_ShouldReturnCreated_WhenUserIsAuthorizedOnDelegatingDistributor()
    {
        var createDto = new OrderCreateDto(DistributorId, null, null, null, null, null, false);
        var createdOrder = CreateOrderDetail(orderNumber: "ORD-003");
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _delegationServiceMock
            .Setup(x => x.GetAuthorizedDistributorIdsForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([DistributorId]);
        _orderServiceMock
            .Setup(x => x.CreateOrderAsync(createDto, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdOrder);

        var result = await _sut.CreateOrder(createDto, CancellationToken.None);

        result.Result.Should().BeOfType<CreatedAtActionResult>();
    }

    // ─── UpdateOrder ───────────────────────────────────────────
    [Fact]
    public async Task UpdateOrder_ShouldReturnOk_WhenSuccessful()
    {
        var updateDto = new OrderUpdateDto(null, null, DateTime.UtcNow.AddDays(7), null, "Updated notes");
        var updatedOrder = CreateOrderDetail();
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _orderServiceMock
            .Setup(x => x.UpdateOrderAsync(OrderId, updateDto, UserId, TenantId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedOrder);

        var result = await _sut.UpdateOrder(OrderId, updateDto, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateOrder_ShouldReturnNotFound_WhenOrderDoesNotExist()
    {
        var updateDto = new OrderUpdateDto(null, null, null, null, null);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _orderServiceMock
            .Setup(x => x.UpdateOrderAsync(OrderId, updateDto, UserId, TenantId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderDetailDto?)null);

        var result = await _sut.UpdateOrder(OrderId, updateDto, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task UpdateOrder_ShouldReturnBadRequest_WhenStatusNotEditable()
    {
        var updateDto = new OrderUpdateDto(null, null, null, null, null);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _orderServiceMock
            .Setup(x => x.UpdateOrderAsync(OrderId, updateDto, UserId, TenantId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cannot modify order in status InProgress."));

        var result = await _sut.UpdateOrder(OrderId, updateDto, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ─── DeleteOrder ───────────────────────────────────────────
    [Fact]
    public async Task DeleteOrder_ShouldReturnNoContent_WhenSuccessful()
    {
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _orderServiceMock
            .Setup(x => x.DeleteOrderAsync(OrderId, UserId, TenantId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.DeleteOrder(OrderId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteOrder_ShouldReturnNotFound_WhenOrderDoesNotExist()
    {
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _orderServiceMock
            .Setup(x => x.DeleteOrderAsync(OrderId, UserId, TenantId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.DeleteOrder(OrderId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteOrder_ShouldReturnBadRequest_WhenStatusNotDeletable()
    {
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _orderServiceMock
            .Setup(x => x.DeleteOrderAsync(OrderId, UserId, TenantId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cannot delete order in status Completed."));

        var result = await _sut.DeleteOrder(OrderId, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // F-104 / F-105 — grant ViewAllOrders so the per-order access checks added to
    // AssignPreleveur and TransitionOrder pass for the happy-path tests.
    private void GrantViewAllOrders()
    {
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    // ─── AssignPreleveur ───────────────────────────────────────
    [Fact]
    public async Task AssignPreleveur_ShouldReturnOk_WhenSuccess()
    {
        GrantViewAllOrders();
        var assignDto = new OrderAssignDto("preleveur-1");
        var updatedOrder = CreateOrderDetail(status: OrderStatus.New, preleveurId: "preleveur-1", preleveurName: "Preleveur Name");
        _orderServiceMock
            .Setup(x => x.AssignPreleveurAsync(OrderId, assignDto, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedOrder);

        var result = await _sut.AssignPreleveur(OrderId, assignDto, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(updatedOrder);
    }

    [Fact]
    public async Task AssignPreleveur_ShouldReturnNotFound_WhenOrderDoesNotExist()
    {
        GrantViewAllOrders();
        var assignDto = new OrderAssignDto("preleveur-1");
        _orderServiceMock
            .Setup(x => x.AssignPreleveurAsync(OrderId, assignDto, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderDetailDto?)null);

        var result = await _sut.AssignPreleveur(OrderId, assignDto, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task AssignPreleveur_ShouldReturnForbid_WhenNonAdminCannotAccessOrder()
    {
        // F-104 — a non-admin without access to the order cannot assign a préleveur.
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orderServiceMock
            .Setup(x => x.UserCanAccessOrderAsync(UserId, OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orderServiceMock
            .Setup(x => x.GetOrderByIdAsync(OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderDetail());

        var result = await _sut.AssignPreleveur(OrderId, new OrderAssignDto("preleveur-1"), CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
        _orderServiceMock.Verify(
            x => x.AssignPreleveurAsync(It.IsAny<Guid>(), It.IsAny<OrderAssignDto>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void AssignPreleveur_ShouldHaveAuthorizeAttributeRestrictingPreleveurOnly()
    {
        // F-104 — restricted to admin + mandataire roles.
        var method = typeof(OrdersController).GetMethod(nameof(OrdersController.AssignPreleveur));
        var auth = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true).OfType<AuthorizeAttribute>().FirstOrDefault();
        auth.Should().NotBeNull();
        var roles = (auth!.Roles ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        roles.Should().Contain("Administrator").And.Contain("Requérant").And.Contain("Requérant-Préleveur");
        roles.Should().NotContain("Préleveur");
    }

    // ─── Transition ────────────────────────────────────────────
    [Fact]
    public async Task TransitionOrder_ShouldReturnOk_WhenTransitionIsValid()
    {
        GrantViewAllOrders();
        var dto = new OrderTransitionRequestDto(OrderStatus.InProgress);
        var transition = new OrderStatusTransitionDto(OrderStatus.New, OrderStatus.InProgress, DateTime.UtcNow);
        _orderStatusServiceMock
            .Setup(x => x.TransitionOrderAsync(OrderId, OrderStatus.InProgress, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transition);

        var result = await _sut.TransitionOrder(OrderId, dto, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task TransitionOrder_ShouldReturnBadRequest_WhenTransitionIsInvalid()
    {
        GrantViewAllOrders();
        var dto = new OrderTransitionRequestDto(OrderStatus.Completed);
        _orderStatusServiceMock
            .Setup(x => x.TransitionOrderAsync(OrderId, OrderStatus.Completed, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Invalid transition"));

        var result = await _sut.TransitionOrder(OrderId, dto, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task TransitionOrder_ShouldReturnForbid_WhenNonAdminCannotAccessOrder()
    {
        // F-105 — a transition is a write, so a non-admin without access to the order is refused.
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orderServiceMock
            .Setup(x => x.UserCanAccessOrderAsync(UserId, OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orderServiceMock
            .Setup(x => x.GetOrderByIdAsync(OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderDetail());

        var result = await _sut.TransitionOrder(OrderId, new OrderTransitionRequestDto(OrderStatus.Cancelled), CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
        _orderStatusServiceMock.Verify(
            x => x.TransitionOrderAsync(It.IsAny<Guid>(), It.IsAny<OrderStatus>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ─── GetRequiredContainers ─────────────────────────────────
    [Fact]
    public async Task GetRequiredContainers_ShouldReturnNotFound_WhenOrderNotFound()
    {
        _orderServiceMock
            .Setup(x => x.GetRequiredContainersAsync(OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IList<RequiredContainerDto>?)null);

        var result = await _sut.GetRequiredContainers(OrderId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetRequiredContainers_ShouldReturnForbid_WhenNonAdminCannotAccessOrder()
    {
        var containers = new List<RequiredContainerDto>
        {
            new(Guid.NewGuid(), "BACT-V250", "Flacon", "Verre", 250, null),
        };
        _orderServiceMock
            .Setup(x => x.GetRequiredContainersAsync(OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orderServiceMock
            .Setup(x => x.UserCanAccessOrderAsync(UserId, OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.GetRequiredContainers(OrderId, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetRequiredContainers_ShouldReturnOk_WhenAdminUser()
    {
        var containers = new List<RequiredContainerDto>
        {
            new(Guid.NewGuid(), "BACT-V250", "Flacon", "Verre", 250, null),
        };
        _orderServiceMock
            .Setup(x => x.GetRequiredContainersAsync(OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.GetRequiredContainers(OrderId, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(containers);
    }

    [Fact]
    public void GetRequiredContainers_ShouldHaveHttpGetAttribute()
    {
        var method = typeof(OrdersController).GetMethod(nameof(OrdersController.GetRequiredContainers));
        method.Should().NotBeNull();
        var attributes = method!.GetCustomAttributes(typeof(HttpGetAttribute), true);
        attributes.Should().NotBeEmpty();
        attributes.OfType<HttpGetAttribute>().First().Template.Should().Be("{id:guid}/required-containers");
    }

    // ─── Attribute tests ───────────────────────────────────────
    [Fact]
    public void Controller_ShouldHaveAuthorizeAttribute()
    {
        var attributes = typeof(OrdersController).GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void UpdateOrder_ShouldHaveHttpPutAttribute()
    {
        var method = typeof(OrdersController).GetMethod(nameof(OrdersController.UpdateOrder));
        method.Should().NotBeNull();
        var attributes = method!.GetCustomAttributes(typeof(HttpPutAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void DeleteOrder_ShouldHaveHttpDeleteAttribute()
    {
        var method = typeof(OrdersController).GetMethod(nameof(OrdersController.DeleteOrder));
        method.Should().NotBeNull();
        var attributes = method!.GetCustomAttributes(typeof(HttpDeleteAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    // ─── Bulk endpoints ────────────────────────────────────────
    [Fact]
    public async Task BulkValidate_ShouldReturnOkWithAffected()
    {
        _orderServiceMock
            .Setup(x => x.BulkValidateAsync(UserId, TenantId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BulkTransitionResultDto(5));

        var result = await _sut.BulkValidate(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(new BulkTransitionResultDto(5));
    }

    [Fact]
    public async Task BulkValidate_WhenUserHasViewAllPermission_ShouldPassIsAdminTrue()
    {
        // Sprint Sec F-005 — admin scoping is derived from ViewAllOrders, like GetOrders.
        _permissionServiceMock
            .Setup(p => p.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _orderServiceMock
            .Setup(x => x.BulkValidateAsync(UserId, TenantId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BulkTransitionResultDto(7));

        var result = await _sut.BulkValidate(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(new BulkTransitionResultDto(7));
        _orderServiceMock.Verify(
            x => x.BulkValidateAsync(UserId, TenantId, true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BulkTransmit_ShouldReturnOkWithAffected()
    {
        _orderServiceMock
            .Setup(x => x.BulkTransmitAsync(UserId, TenantId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BulkTransitionResultDto(2));

        var result = await _sut.BulkTransmit(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(new BulkTransitionResultDto(2));
    }

    [Fact]
    public async Task BulkTransmit_WhenUserIsNotAdmin_ShouldPassIsAdminFalse()
    {
        // Sprint Sec F-005 — a requérant must not transmit the whole tenant.
        _permissionServiceMock
            .Setup(p => p.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orderServiceMock
            .Setup(x => x.BulkTransmitAsync(UserId, TenantId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BulkTransitionResultDto(1));

        await _sut.BulkTransmit(CancellationToken.None);

        _orderServiceMock.Verify(
            x => x.BulkTransmitAsync(UserId, TenantId, false, It.IsAny<CancellationToken>()),
            Times.Once);
        _orderServiceMock.Verify(
            x => x.BulkTransmitAsync(It.IsAny<string>(), It.IsAny<Guid>(), true, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void BulkValidate_ShouldHaveHttpPostAttributeWithRoute()
    {
        var method = typeof(OrdersController).GetMethod(nameof(OrdersController.BulkValidate));
        method.Should().NotBeNull();
        var attributes = method!.GetCustomAttributes(typeof(HttpPostAttribute), true);
        attributes.Should().NotBeEmpty();
        attributes.OfType<HttpPostAttribute>().First().Template.Should().Be("bulk-validate");
    }

    [Fact]
    public void BulkTransmit_ShouldHaveHttpPostAttributeWithRoute()
    {
        var method = typeof(OrdersController).GetMethod(nameof(OrdersController.BulkTransmit));
        method.Should().NotBeNull();
        var attributes = method!.GetCustomAttributes(typeof(HttpPostAttribute), true);
        attributes.Should().NotBeEmpty();
        attributes.OfType<HttpPostAttribute>().First().Template.Should().Be("bulk-transmit");
    }

    [Fact]
    public void BulkValidate_ShouldHaveAuthorizeAttributeWithRoles()
    {
        var method = typeof(OrdersController).GetMethod(nameof(OrdersController.BulkValidate));
        method.Should().NotBeNull();
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var auth = attributes.OfType<AuthorizeAttribute>().First();
        auth.Roles.Should().Contain("Administrator").And.Contain("Requérant");
    }

    [Fact]
    public void BulkTransmit_ShouldHaveAuthorizeAttributeWithRoles()
    {
        var method = typeof(OrdersController).GetMethod(nameof(OrdersController.BulkTransmit));
        method.Should().NotBeNull();
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var auth = attributes.OfType<AuthorizeAttribute>().First();
        auth.Roles.Should().Contain("Administrator").And.Contain("Requérant");
    }

    // ─── BulkFinalize (AQ-406) ─────────────────────────────────
    [Fact]
    public async Task BulkFinalize_ShouldReturnOkWithValidatedAndTransmittedCounts()
    {
        _orderServiceMock
            .Setup(x => x.BulkFinalizeAsync(UserId, TenantId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BulkFinalizeResultDto(3, 5));

        var result = await _sut.BulkFinalize(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(new BulkFinalizeResultDto(3, 5));
    }

    [Fact]
    public void BulkFinalize_ShouldHaveHttpPostAttributeWithRoute()
    {
        var method = typeof(OrdersController).GetMethod(nameof(OrdersController.BulkFinalize));
        method.Should().NotBeNull();
        var attributes = method!.GetCustomAttributes(typeof(HttpPostAttribute), true);
        attributes.Should().NotBeEmpty();
        attributes.OfType<HttpPostAttribute>().First().Template.Should().Be("bulk-finalize");
    }

    [Fact]
    public void BulkFinalize_ShouldHaveAuthorizeAttributeWithRoles()
    {
        var method = typeof(OrdersController).GetMethod(nameof(OrdersController.BulkFinalize));
        method.Should().NotBeNull();
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var auth = attributes.OfType<AuthorizeAttribute>().First();
        auth.Roles.Should().Contain("Administrator").And.Contain("Requérant");
    }

    [Fact]
    public void CreateOrder_ShouldHaveAuthorizeAttributeRestrictingPreleveur()
    {
        var method = typeof(OrdersController).GetMethod(nameof(OrdersController.CreateOrder));
        method.Should().NotBeNull();
        var auth = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .OfType<AuthorizeAttribute>()
            .FirstOrDefault();
        auth.Should().NotBeNull();
        auth!.Roles.Should()
            .Contain(RoleName.Administrator)
            .And.Contain(RoleName.Requerant)
            .And.Contain(RoleName.RequerantPreleveur);

        // Preleveur alone must NOT be in the allowed roles list (stand-alone, not as part of Requérant-Préleveur)
        var roleList = (auth.Roles ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        roleList.Should().NotContain(RoleName.Preleveur);
    }

    [Fact]
    public void CreateOrder_ShouldHaveHttpPostAttribute()
    {
        var method = typeof(OrdersController).GetMethod(nameof(OrdersController.CreateOrder));
        method.Should().NotBeNull();
        var attributes = method!.GetCustomAttributes(typeof(HttpPostAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    // ─── AQ-34 / AQ-400 — Pull results + GET results ───────────

    [Fact]
    public async Task PullResults_WhenOrderMissing_ShouldReturnNotFound()
    {
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _limsResultServiceMock
            .Setup(x => x.PullAsync(OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AquaPlan.Application.Services.Interfaces.PullResultsOutcome?)null);

        var result = await _sut.PullResults(OrderId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task PullResults_WhenOk_ShouldReturnSamplingResultListDto()
    {
        var list = new AquaPlan.Application.DTOs.SamplingResults.SamplingResultListDto([], 0, 0, 0);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _limsResultServiceMock
            .Setup(x => x.PullAsync(OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AquaPlan.Application.Services.Interfaces.PullResultsOutcome(list, 0, false));

        var result = await _sut.PullResults(OrderId, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(list);
    }

    [Fact]
    public async Task PullResults_AsNonAdminWithoutAccess_ShouldReturnForbidWhenOrderExists()
    {
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orderServiceMock
            .Setup(x => x.UserCanAccessOrderAsync(UserId, OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orderServiceMock
            .Setup(x => x.GetOrderByIdAsync(OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderDetail());

        var result = await _sut.PullResults(OrderId, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public void PullResults_ShouldBeRestrictedToAdminAndRequerants()
    {
        var method = typeof(OrdersController).GetMethod(nameof(OrdersController.PullResults))!;
        var auth = method.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .OfType<AuthorizeAttribute>().Single();
        auth.Roles.Should().Be($"{RoleName.Administrator},{RoleName.Requerant},{RoleName.RequerantPreleveur}");
        var post = method.GetCustomAttributes(typeof(HttpPostAttribute), true)
            .OfType<HttpPostAttribute>().Single();
        post.Template.Should().Be("{id:guid}/pull-results");
    }

    [Fact]
    public async Task GetResults_AsAdmin_ShouldReturnOk()
    {
        var list = new AquaPlan.Application.DTOs.SamplingResults.SamplingResultListDto([], 0, 0, 0);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _limsResultServiceMock
            .Setup(x => x.GetByOrderAsync(OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);

        var result = await _sut.GetResults(OrderId, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(list);
    }

    [Fact]
    public async Task GetResults_AsNonAdminWithoutAccess_ShouldReturnForbidWhenOrderExists()
    {
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orderServiceMock
            .Setup(x => x.UserCanAccessOrderAsync(UserId, OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orderServiceMock
            .Setup(x => x.GetOrderByIdAsync(OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderDetail());

        var result = await _sut.GetResults(OrderId, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetResults_WhenOrderMissing_ShouldReturnNotFound()
    {
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orderServiceMock
            .Setup(x => x.UserCanAccessOrderAsync(UserId, OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orderServiceMock
            .Setup(x => x.GetOrderByIdAsync(OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderDetailDto?)null);

        var result = await _sut.GetResults(OrderId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void GetResults_ShouldHaveHttpGetAttribute()
    {
        var method = typeof(OrdersController).GetMethod(nameof(OrdersController.GetResults))!;
        var get = method.GetCustomAttributes(typeof(HttpGetAttribute), true)
            .OfType<HttpGetAttribute>().Single();
        get.Template.Should().Be("{id:guid}/results");
    }

    // ─── AQ-31 — Dashboard summary ─────────────────────────────

    [Fact]
    public async Task GetDashboardSummary_ShouldReturnOk()
    {
        var summary = new OrderDashboardSummaryDto(4, 1, 7, 12);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _orderServiceMock
            .Setup(x => x.GetDashboardSummaryAsync(UserId, TenantId, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);

        var result = await _sut.GetDashboardSummary(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(summary);
    }

    [Fact]
    public void GetDashboardSummary_ShouldHaveHttpGetWithRoute()
    {
        var method = typeof(OrdersController).GetMethod(nameof(OrdersController.GetDashboardSummary))!;
        var get = method.GetCustomAttributes(typeof(HttpGetAttribute), true)
            .OfType<HttpGetAttribute>().Single();
        get.Template.Should().Be("dashboard-summary");
    }
}
