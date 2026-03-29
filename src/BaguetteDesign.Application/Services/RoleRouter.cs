namespace BaguetteDesign.Application.Services;

using BaguetteDesign.Domain.Enums;
using BaguetteDesign.Domain.Interfaces;

public sealed class RoleRouter : IRoleRouter
{
    private readonly long _adminUserId;

    public RoleRouter(long adminUserId)
    {
        _adminUserId = adminUserId;
    }

    public UserRole Resolve(long userId)
        => userId == _adminUserId ? UserRole.Designer : UserRole.Client;
}
