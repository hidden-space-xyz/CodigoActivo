using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.UnitTests.TestSupport;

public sealed class TestCurrentUser : ICurrentUser
{
    public TestCurrentUser(UserId? id = null, bool isAdmin = false)
    {
        Id = id ?? UserId.New();
        IsAdmin = isAdmin;
    }

    public UserId? Id { get; set; }

    public bool IsAdmin { get; set; }

    public static TestCurrentUser Anonymous()
    {
        return new TestCurrentUser { Id = null };
    }
}
