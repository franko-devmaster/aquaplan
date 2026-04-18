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
    private readonly Mock<ILogger<OrdersController>> _loggerMock = new();
    private readonly OrdersController _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OrderId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid DistributorId = Guid.Parse("00000000-0000-0000-0000-000000000020");
    private const string UserId = "user-1";

    public OrdersControllerTest()
    {
        _sut = new OrdersController(_orderServiceMock.Object, _orderStatusServiceMock.Object, _permissionServiceMock.Object, _orderAuditServiceMock.Object, _delegationServiceMock.Object, _loggerMock.Object);
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
            TenantId, DateTime.UtcNow, null, null, null);
    }

    private static OrderListDto CreateOrderList(Guid? id = null, string orderNumber = "ORD-001")
    {
        return new OrderListDto(
            id ?? OrderId, orderNumber, OrderStatus.New, false, null,
            UserId, "John Doe", null, null,
            DistributorId, "Distributor A",
            null, null, null, false, DateTime.UtcNow);
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
            .Setup(x => x.GetOrdersFilteredAsync(UserId, TenantId, It.IsAny<OrderFilterDto>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var result = await _sut.GetOrders(null, null, null, null, 1, 20, null, true, null, null, null, null, CancellationToken.None);

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
                false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var result = await _sut.GetOrders(statuses, true, null, "test", 1, 20, null, true, null, null, null, null, CancellationToken.None);

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

    // ─── AssignPreleveur ───────────────────────────────────────
    [Fact]
    public async Task AssignPreleveur_ShouldReturnOk_WhenSuccess()
    {
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
        var assignDto = new OrderAssignDto("preleveur-1");
        _orderServiceMock
            .Setup(x => x.AssignPreleveurAsync(OrderId, assignDto, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderDetailDto?)null);

        var result = await _sut.AssignPreleveur(OrderId, assignDto, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    // ─── Transition ────────────────────────────────────────────
    [Fact]
    public async Task TransitionOrder_ShouldReturnOk_WhenTransitionIsValid()
    {
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
        var dto = new OrderTransitionRequestDto(OrderStatus.Completed);
        _orderStatusServiceMock
            .Setup(x => x.TransitionOrderAsync(OrderId, OrderStatus.Completed, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Invalid transition"));

        var result = await _sut.TransitionOrder(OrderId, dto, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
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
            .Setup(x => x.BulkValidateAsync(UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BulkTransitionResultDto(5));

        var result = await _sut.BulkValidate(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(new BulkTransitionResultDto(5));
    }

    [Fact]
    public async Task BulkTransmit_ShouldReturnOkWithAffected()
    {
        _orderServiceMock
            .Setup(x => x.BulkTransmitAsync(UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BulkTransitionResultDto(2));

        var result = await _sut.BulkTransmit(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(new BulkTransitionResultDto(2));
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
}
