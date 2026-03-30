namespace BaguetteDesign.Domain.Interfaces;

using BaguetteDesign.Domain.Enums;

public interface IRoleRouter
{
    UserRole Resolve(long userId);
}
