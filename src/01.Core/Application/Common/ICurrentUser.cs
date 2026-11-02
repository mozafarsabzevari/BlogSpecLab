namespace BlogSpecLab.Core.Application.Common;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }
}
