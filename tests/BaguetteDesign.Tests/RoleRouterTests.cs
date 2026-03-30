namespace BaguetteDesign.Tests;

using BaguetteDesign.Application.Services;
using BaguetteDesign.Domain.Enums;
using Xunit;

public sealed class RoleRouterTests
{
    private const long AdminId = 12345L;
    private readonly RoleRouter _sut = new(AdminId);

    [Fact]
    public void Resolve_GivenAdminId_ReturnsDesigner()
    {
        var role = _sut.Resolve(AdminId);

        Assert.Equal(UserRole.Designer, role);
    }

    [Fact]
    public void Resolve_GivenOtherId_ReturnsClient()
    {
        var role = _sut.Resolve(99999L);

        Assert.Equal(UserRole.Client, role);
    }

    [Fact]
    public void Resolve_GivenZeroId_ReturnsClient()
    {
        var role = _sut.Resolve(0L);

        Assert.Equal(UserRole.Client, role);
    }

    [Fact]
    public void Resolve_GivenNegativeId_ReturnsClient()
    {
        var role = _sut.Resolve(-1L);

        Assert.Equal(UserRole.Client, role);
    }
}
