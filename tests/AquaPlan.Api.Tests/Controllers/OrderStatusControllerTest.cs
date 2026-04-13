using AquaPlan.Api.Controllers.Api;
using AquaPlan.Application.DTOs.Orders;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Tests.Controllers;

public class OrderStatusControllerTest
{
    private readonly Mock<IOrderStatusService> _orderStatusServiceMock = new();
    private readonly OrderStatusController _sut;

    public OrderStatusControllerTest()
    {
        _sut = new OrderStatusController(_orderStatusServiceMock.Object);
    }

    [Fact]
    public void GetAllStatuses_ShouldReturnOk()
    {
        var statuses = new List<OrderStatusDto>
        {
            new(OrderStatus.New, "Draft", "Order created", "#9E9E9E", false),
            new(OrderStatus.Done, "Completed", "Order completed", "#4CAF50", true),
        };
        _orderStatusServiceMock.Setup(x => x.GetAllStatuses()).Returns(statuses);

        var result = _sut.GetAllStatuses();

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(statuses);
    }

    [Fact]
    public void GetAllowedTransitions_ShouldReturnOk()
    {
        var transitions = new List<OrderStatusDto>
        {
            new(OrderStatus.InProgress, "Assigned", "Sampler assigned", "#2196F3", false),
            new(OrderStatus.Cancelled, "Cancelled", "Order cancelled", "#F44336", true),
        };
        _orderStatusServiceMock.Setup(x => x.GetAllowedTransitions(OrderStatus.New)).Returns(transitions);

        var result = _sut.GetAllowedTransitions(OrderStatus.New);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(transitions);
    }

    [Fact]
    public void GetAllowedTransitions_ShouldReturnEmptyList_ForTerminalStatus()
    {
        _orderStatusServiceMock.Setup(x => x.GetAllowedTransitions(OrderStatus.Done)).Returns(new List<OrderStatusDto>());

        var result = _sut.GetAllowedTransitions(OrderStatus.Done);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var list = okResult.Value as IList<OrderStatusDto>;
        list.Should().BeEmpty();
    }

    [Fact]
    public void Controller_ShouldHaveAuthorizeAttribute()
    {
        var attributes = typeof(OrderStatusController).GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void GetAllStatuses_ShouldHaveHttpGetAttribute()
    {
        var method = typeof(OrderStatusController).GetMethod(nameof(OrderStatusController.GetAllStatuses));
        method.Should().NotBeNull();
        var attributes = method!.GetCustomAttributes(typeof(HttpGetAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void GetAllowedTransitions_ShouldHaveHttpGetAttribute()
    {
        var method = typeof(OrderStatusController).GetMethod(nameof(OrderStatusController.GetAllowedTransitions));
        method.Should().NotBeNull();
        var attributes = method!.GetCustomAttributes(typeof(HttpGetAttribute), true);
        attributes.Should().NotBeEmpty();
        var httpGet = attributes[0] as HttpGetAttribute;
        httpGet!.Template.Should().Be("{status}/transitions");
    }

    [Fact]
    public void Controller_ShouldHaveRouteAttribute()
    {
        var attributes = typeof(OrderStatusController).GetCustomAttributes(typeof(RouteAttribute), true);
        attributes.Should().NotBeEmpty();
        var route = attributes[0] as RouteAttribute;
        route!.Template.Should().Be("api/[controller]");
    }
}
