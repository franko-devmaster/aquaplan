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

    [Fact]
    public async Task GetOrders_ShouldReturnAllOrders_WhenUserHasViewAllPermission()
    {
        var orders = new List<OrderListDto>
        {
            new(OrderId, "ORD-001", OrderStatus.Draft, false, UserId, "John Doe", null, null, DistributorId, "Distributor A", DateTime.UtcNow),
        };
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _orderServiceMock
            .Setup(x => x.GetAllOrdersAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orders);

        var result = await _sut.GetOrders(CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(orders);
        _orderServiceMock.Verify(x => x.GetAllOrdersAsync(TenantId, It.IsAny<CancellationToken>()), Times.Once);
        _orderServiceMock.Verify(x => x.GetOrdersForUserAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetOrders_ShouldReturnUserOrders_WhenUserDoesNotHaveViewAllPermission()
    {
        var orders = new List<OrderListDto>
        {
            new(OrderId, "ORD-001", OrderStatus.Draft, false, UserId, "John Doe", null, null, DistributorId, "Distributor A", DateTime.UtcNow),
        };
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orderServiceMock
            .Setup(x => x.GetOrdersForUserAsync(UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orders);

        var result = await _sut.GetOrders(CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(orders);
        _orderServiceMock.Verify(x => x.GetOrdersForUserAsync(UserId, TenantId, It.IsAny<CancellationToken>()), Times.Once);
        _orderServiceMock.Verify(x => x.GetAllOrdersAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

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
        var order = new OrderDetailDto(OrderId, "ORD-001", OrderStatus.Draft, false, UserId, "John Doe",
            null, null, DistributorId, "Distributor A", TenantId, DateTime.UtcNow, null, null);
        _orderServiceMock
            .Setup(x => x.GetOrderByIdAsync(OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.GetOrder(OrderId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(order);
    }

    [Fact]
    public async Task GetOrder_ShouldReturnOk_WhenUserHasAccess()
    {
        var order = new OrderDetailDto(OrderId, "ORD-001", OrderStatus.Draft, false, UserId, "John Doe",
            null, null, DistributorId, "Distributor A", TenantId, DateTime.UtcNow, null, null);
        _orderServiceMock
            .Setup(x => x.GetOrderByIdAsync(OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orderServiceMock
            .Setup(x => x.UserCanAccessOrderAsync(UserId, OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.GetOrder(OrderId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(order);
    }

    [Fact]
    public async Task GetOrder_ShouldReturnForbid_WhenUserHasNoAccess()
    {
        var order = new OrderDetailDto(OrderId, "ORD-001", OrderStatus.Draft, false, "other-user", "Other",
            null, null, DistributorId, "Distributor A", TenantId, DateTime.UtcNow, null, null);
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

    [Fact]
    public async Task CreateOrder_ShouldReturnCreatedAtAction_WhenAdmin()
    {
        var createDto = new OrderCreateDto(DistributorId, null, false);
        var createdOrder = new OrderDetailDto(OrderId, "ORD-002", OrderStatus.Draft, false, UserId, "John Doe",
            null, null, DistributorId, "Distributor A", TenantId, DateTime.UtcNow, null, null);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _orderServiceMock
            .Setup(x => x.CreateOrderAsync(createDto, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdOrder);

        var result = await _sut.CreateOrder(createDto, CancellationToken.None);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(OrdersController.GetOrder));
        createdResult.Value.Should().Be(createdOrder);
    }

    [Fact]
    public async Task CreateOrder_ShouldReturnCreatedAtAction_WhenUserHasDistributorAccess()
    {
        var createDto = new OrderCreateDto(DistributorId, null, false);
        var createdOrder = new OrderDetailDto(OrderId, "ORD-002", OrderStatus.Draft, false, UserId, "John Doe",
            null, null, DistributorId, "Distributor A", TenantId, DateTime.UtcNow, null, null);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orderServiceMock
            .Setup(x => x.UserHasDistributorAccessAsync(UserId, DistributorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _orderServiceMock
            .Setup(x => x.CreateOrderAsync(createDto, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdOrder);

        var result = await _sut.CreateOrder(createDto, CancellationToken.None);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.Value.Should().Be(createdOrder);
    }

    [Fact]
    public async Task CreateOrder_ShouldReturnForbid_WhenUserHasNoDistributorAccess()
    {
        var createDto = new OrderCreateDto(DistributorId, null, false);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orderServiceMock
            .Setup(x => x.UserHasDistributorAccessAsync(UserId, DistributorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.CreateOrder(createDto, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task AssignPreleveur_ShouldReturnOk_WhenSuccess()
    {
        var assignDto = new OrderAssignDto("preleveur-1");
        var updatedOrder = new OrderDetailDto(OrderId, "ORD-001", OrderStatus.Assigned, false, UserId, "John Doe",
            "preleveur-1", "Preleveur Name", DistributorId, "Distributor A", TenantId, DateTime.UtcNow, DateTime.UtcNow, null);
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

    [Fact]
    public async Task TransitionOrder_ShouldReturnOk_WhenTransitionIsValid()
    {
        var dto = new OrderTransitionRequestDto(OrderStatus.Assigned);
        var transition = new OrderStatusTransitionDto(OrderStatus.Draft, OrderStatus.Assigned, DateTime.UtcNow);
        _orderStatusServiceMock
            .Setup(x => x.TransitionOrderAsync(OrderId, OrderStatus.Assigned, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transition);

        var result = await _sut.TransitionOrder(OrderId, dto, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(transition);
    }

    [Fact]
    public async Task TransitionOrder_ShouldReturnNotFound_WhenOrderDoesNotExist()
    {
        var dto = new OrderTransitionRequestDto(OrderStatus.Assigned);
        _orderStatusServiceMock
            .Setup(x => x.TransitionOrderAsync(OrderId, OrderStatus.Assigned, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        var result = await _sut.TransitionOrder(OrderId, dto, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task TransitionOrder_ShouldReturnBadRequest_WhenTransitionIsInvalid()
    {
        var dto = new OrderTransitionRequestDto(OrderStatus.Completed);
        _orderStatusServiceMock
            .Setup(x => x.TransitionOrderAsync(OrderId, OrderStatus.Completed, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Transition from Draft to Completed is not allowed."));

        var result = await _sut.TransitionOrder(OrderId, dto, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void TransitionOrder_ShouldHaveHttpPostAttribute()
    {
        var method = typeof(OrdersController).GetMethod(nameof(OrdersController.TransitionOrder));
        method.Should().NotBeNull();
        var attributes = method!.GetCustomAttributes(typeof(HttpPostAttribute), true);
        attributes.Should().NotBeEmpty();
        var httpPost = attributes[0] as HttpPostAttribute;
        httpPost!.Template.Should().Be("{id:guid}/transition");
    }

    [Fact]
    public void Controller_ShouldHaveAuthorizeAttribute()
    {
        var attributes = typeof(OrdersController).GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
    }
}
