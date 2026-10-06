using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Common.Security;

namespace CodigoActivo.UnitTests.TestSupport;

public static class ActingUsers
{
    public static ActingUserPolicy Policy(ICurrentUser currentUser, FakeReadStore? store = null)
    {
        return new ActingUserPolicy(
            currentUser,
            store ?? new FakeReadStore(),
            new FakeQueryExecutor()
        );
    }

    public static void AddDependent(this FakeReadStore store, Guid dependentId, Guid guardianId)
    {
        store.Users.Add(new UserRow { Id = dependentId, ParentId = guardianId });
    }
}
