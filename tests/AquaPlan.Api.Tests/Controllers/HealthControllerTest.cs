using AquaPlan.Api.Controllers.Api;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Tests.Controllers;

public class HealthControllerTest
{
    private readonly HealthController _sut = new();

    [Fact]
    public void Get_ShouldReturnOkWithStatus()
    {
        var result = _sut.Get();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value;
        value.Should().NotBeNull();
    }

    [Fact]
    public void Get_ShouldHaveAllowAnonymousAttribute()
    {
        var controllerType = typeof(HealthController);
        var attribute = controllerType.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), true);
        attribute.Should().NotBeEmpty();
    }
}
