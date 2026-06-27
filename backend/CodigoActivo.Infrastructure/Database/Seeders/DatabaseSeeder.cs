using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Seeders;

public class DatabaseSeeder(CodigoActivoDbContext context)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedUserStatusTypesAsync(ct);
        await SeedUserTypesAsync(ct);
        await SeedActivityRoleTypesAsync(ct);
        await SeedAssignmentStatusTypesAsync(ct);
        await context.SaveChangesAsync(ct);
    }

    private async Task SeedUserStatusTypesAsync(CancellationToken ct)
    {
        var seed = new[]
        {
            new UserStatusType
            {
                Id = SeedIds.UserStatusTypes.Pending,
                Name = "Pending",
                Description = "Account awaiting verification.",
            },
            new UserStatusType
            {
                Id = SeedIds.UserStatusTypes.Active,
                Name = "Active",
                Description = "Active, verified account.",
            },
            new UserStatusType
            {
                Id = SeedIds.UserStatusTypes.Blocked,
                Name = "Blocked",
                Description = "Blocked account.",
            },
            new UserStatusType
            {
                Id = SeedIds.UserStatusTypes.Dependent,
                Name = "Dependent",
                Description = "Account linked to a parent; cannot sign in on its own.",
            },
        };
        foreach (var item in seed)
        {
            if (!await context.UserStatusTypes.AnyAsync(x => x.Id == item.Id, ct))
            {
                context.UserStatusTypes.Add(item);
            }
        }
    }

    private async Task SeedUserTypesAsync(CancellationToken ct)
    {
        var seed = new[]
        {
            new UserType
            {
                Id = SeedIds.UserTypes.Admin,
                Name = "Admin",
                Description = "System administrator.",
            },
            new UserType
            {
                Id = SeedIds.UserTypes.Member,
                Name = "Member",
                Description = "Organization member.",
            },
            new UserType
            {
                Id = SeedIds.UserTypes.Volunteer,
                Name = "Volunteer",
                Description = "Volunteer.",
            },
            new UserType
            {
                Id = SeedIds.UserTypes.Sponsor,
                Name = "Sponsor",
                Description = "Sponsor.",
            },
            new UserType
            {
                Id = SeedIds.UserTypes.Participant,
                Name = "Participant",
                Description = "Participant.",
            },
        };
        foreach (var item in seed)
        {
            if (!await context.UserTypes.AnyAsync(x => x.Id == item.Id, ct))
            {
                context.UserTypes.Add(item);
            }
        }
    }

    private async Task SeedActivityRoleTypesAsync(CancellationToken ct)
    {
        var seed = new[]
        {
            new ActivityRoleType
            {
                Id = SeedIds.ActivityRoleTypes.Leader,
                Name = "Leader",
                Description = "Activity leader.",
            },
            new ActivityRoleType
            {
                Id = SeedIds.ActivityRoleTypes.Helper,
                Name = "Helper",
                Description = "Activity helper.",
            },
            new ActivityRoleType
            {
                Id = SeedIds.ActivityRoleTypes.Participant,
                Name = "Participant",
                Description = "Activity participant.",
            },
        };
        foreach (var item in seed)
        {
            if (!await context.ActivityRoleTypes.AnyAsync(x => x.Id == item.Id, ct))
            {
                context.ActivityRoleTypes.Add(item);
            }
        }
    }

    private async Task SeedAssignmentStatusTypesAsync(CancellationToken ct)
    {
        var seed = new[]
        {
            new AssignmentStatusType
            {
                Id = SeedIds.AssignmentStatusTypes.Requested,
                Name = "Requested",
                Description = "Assignment requested.",
            },
            new AssignmentStatusType
            {
                Id = SeedIds.AssignmentStatusTypes.Confirmed,
                Name = "Confirmed",
                Description = "Assignment confirmed.",
            },
            new AssignmentStatusType
            {
                Id = SeedIds.AssignmentStatusTypes.Denied,
                Name = "Denied",
                Description = "Assignment denied.",
            },
        };
        foreach (var item in seed)
        {
            if (!await context.AssignmentStatusTypes.AnyAsync(x => x.Id == item.Id, ct))
            {
                context.AssignmentStatusTypes.Add(item);
            }
        }
    }
}
