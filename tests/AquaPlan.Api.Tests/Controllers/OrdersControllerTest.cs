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
    private readonly Mock<ILogger<OrdersController>> _loggerMock = new();
    private readonly OrdersController _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OrderId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid DistributorId = Guid.Parse("00000000-0000-0000-0000-000000000020");
    private const string UserId = "user-1";

    public OrdersControllerTest()
    {
        _sut = new OrdersController(_orderServiceMock.Object, _orderStatusServiceMock.Object, _permissionServiceMock.Object, _loggerMock.Object);
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
        Guid? id = null, string orderNumber = "ORD-001", OrderStatus status = OrderStatus.Draft,
        string? preleveurId = null, string? preleveurName = null)
    {
        return new OrderDetailDto(
            id ?? OrderId, orderNumber, status, false,
            null, null,
            UserId, "John Doe", preleveurId, preleveurName,
            DistributorId, "Distributor A",
            null, null, null, null, false, [],
            TenantId, DateTime.UtcNow, null, null);
    }

    private static OrderListDto CreateOrderList(Guid? id = null, string orderNumber = "ORD-001")
    {
        return new OrderListDto(
            id ?? OrderId, orderNumber, OrderStatus.Draft, false, null,
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

        var result = await _sut.GetOrders(null, null, null, 1, 20, null, true, null, null, null, null, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value.Should().BeOfType<OrderPagedResultDto>().Subject;
        value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetOrders_ShouldPassFilterParams()
    {
        var statuses = new List<OrderStatus> { OrderStatus.Draft, OrderStatus.Assigned };
        var pagedResult = new OrderPagedResultDto([], 0, 1, 20);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orderServiceMock
            .Setup(x => x.GetOrdersFilteredAsync(UserId, TenantId,
                It.Is<OrderFilterDto>(f => f.Statuses!.Count == 2 && f.IsUnassigned == true && f.Search == "test"),
                false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var result = await _sut.GetOrders(statuses, true, "test", 1, 20, null, true, null, null, null, null, CancellationToken.None);

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
        _orderServiceMock
            .Setup(x => x.UserHasDistributorAccessAsync(UserId, DistributorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.CreateOrder(createDto, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
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
        var updatedOrder = CreateOrderDetail(status: OrderStatus.Assigned, preleveurId: "preleveur-1", preleveurName: "Preleveur Name");
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
        var dto = new OrderTransitionRequestDto(OrderStatus.Assigned);
        var transition = new OrderStatusTransitionDto(OrderStatus.Draft, OrderStatus.Assigned, DateTime.UtcNow);
        _orderStatusServiceMock
            .Setup(x => x.TransitionOrderAsync(OrderId, OrderStatus.Assigned, UserId, TenantId, It.IsAny<CancellationToken>()))
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
}
