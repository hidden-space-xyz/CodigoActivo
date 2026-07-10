using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Entities.Abstractions;
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
        await SeedActivityModalityTypesAsync(ct);
        await SeedResourceTypesAsync(ct);
        await context.SaveChangesAsync(ct);
    }

    private async Task SeedUserStatusTypesAsync(CancellationToken ct)
    {
        var seed = new[]
        {
            new UserStatusType
            {
                Id = SeedIds.UserStatusTypes.Pending,
                Name = "Pendiente",
                Color = "#6B7280",
                Description =
                    "Cuenta registrada que aún no ha completado el proceso de verificación. "
                    + "No puede acceder a las funciones de la plataforma hasta que un administrador la apruebe.",
            },
            new UserStatusType
            {
                Id = SeedIds.UserStatusTypes.Active,
                Name = "Activo",
                Color = "#22C55E",
                Description =
                    "Cuenta verificada y habilitada. Tiene acceso completo a las funcionalidades "
                    + "correspondientes a su tipo de usuario.",
            },
            new UserStatusType
            {
                Id = SeedIds.UserStatusTypes.Blocked,
                Name = "Bloqueado",
                Color = "#EF4444",
                Description =
                    "Cuenta suspendida por un administrador. El acceso queda restringido hasta que "
                    + "se restablezca manualmente.",
            },
            new UserStatusType
            {
                Id = SeedIds.UserStatusTypes.Dependent,
                Name = "Dependiente",
                Color = "#3B82F6",
                Description =
                    "Cuenta vinculada a un tutor o cuenta principal. No puede iniciar sesión por sí "
                    + "misma y se gestiona a través de la cuenta responsable.",
            },
        };
        await AddMissingAsync(context.UserStatusTypes, seed, ct);
    }

    private async Task SeedUserTypesAsync(CancellationToken ct)
    {
        var seed = new[]
        {
            new UserType
            {
                Id = SeedIds.UserTypes.Member,
                Name = "Socio",
                Color = "#EF4444",
                Description =
                    "Integrante registrado de la organización. Apoya a la asociación de forma "
                    + "continua, participa en sus actividades y accede a las secciones reservadas a socios.",
                Hidden = false,
                IsAllowedForAdults = true,
                IsAllowedForMinors = true,
            },
            new UserType
            {
                Id = SeedIds.UserTypes.Volunteer,
                Name = "Voluntario puntual",
                Color = "#3B82F6",
                Description =
                    "Persona que echa una mano de forma desinteresada en eventos y talleres "
                    + "concretos, colaborando en su organización y ejecución cuando puede.",
                Hidden = false,
                IsAllowedForAdults = true,
                IsAllowedForMinors = true,
            },
            new UserType
            {
                Id = SeedIds.UserTypes.Participant,
                Name = "Participante",
                Color = "#FFFFFF",
                Description =
                    "Persona que se inscribe y asiste a los eventos y actividades para aprender y "
                    + "disfrutar, sin asumir un rol organizativo.",
                Hidden = false,
                IsAllowedForAdults = false,
                IsAllowedForMinors = true,
            },
        };
        await AddMissingAsync(context.UserTypes, seed, ct);
    }

    private async Task SeedActivityRoleTypesAsync(CancellationToken ct)
    {
        var seed = new[]
        {
            new ActivityRoleType
            {
                Id = SeedIds.ActivityRoleTypes.Leader,
                Name = "Líder",
                Description =
                    "Responsable de coordinar la actividad. Dirige al equipo, organiza las tareas y vela por el cumplimiento de los objetivos.",
            },
            new ActivityRoleType
            {
                Id = SeedIds.ActivityRoleTypes.Helper,
                Name = "Colaborador",
                Description =
                    "Apoya al líder durante la actividad asumiendo tareas de soporte para su correcto desarrollo.",
            },
            new ActivityRoleType
            {
                Id = SeedIds.ActivityRoleTypes.Participant,
                Name = "Participante",
                Description =
                    "Asiste a la actividad como público o beneficiario, sin responsabilidades de organización.",
            },
        };
        await AddMissingAsync(context.ActivityRoleTypes, seed, ct);
    }

    private async Task SeedAssignmentStatusTypesAsync(CancellationToken ct)
    {
        var seed = new[]
        {
            new AssignmentStatusType
            {
                Id = SeedIds.AssignmentStatusTypes.Requested,
                Name = "Solicitada",
                Color = "#6B7280",
                Description =
                    "La asignación ha sido solicitada y está pendiente de revisión por parte de un "
                    + "responsable.",
            },
            new AssignmentStatusType
            {
                Id = SeedIds.AssignmentStatusTypes.Confirmed,
                Name = "Confirmada",
                Color = "#22C55E",
                Description =
                    "La asignación ha sido revisada y aprobada. La persona queda oficialmente "
                    + "asignada a la actividad.",
            },
            new AssignmentStatusType
            {
                Id = SeedIds.AssignmentStatusTypes.Denied,
                Name = "Rechazada",
                Color = "#EF4444",
                Description =
                    "La asignación ha sido revisada y rechazada. La persona no participará en la "
                    + "actividad bajo este rol.",
            },
        };
        await AddMissingAsync(context.AssignmentStatusTypes, seed, ct);
    }

    private async Task SeedActivityModalityTypesAsync(CancellationToken ct)
    {
        var seed = new[]
        {
            new ActivityModalityType
            {
                Id = SeedIds.ActivityModalityTypes.Presencial,
                Name = "Presencial",
            },
            new ActivityModalityType { Id = SeedIds.ActivityModalityTypes.Online, Name = "Online" },
        };
        await AddMissingAsync(context.ActivityModalityTypes, seed, ct);
    }

    private async Task SeedResourceTypesAsync(CancellationToken ct)
    {
        var seed = new[]
        {
            new ResourceType
            {
                Id = SeedIds.ResourceTypes.Internal,
                Name = "Interno",
                Color = "#3B82F6",
                IsExternal = false,
                Description =
                    "Material propio alojado en la plataforma. Incluye una descripción completa "
                    + "que se consulta desde la propia web.",
            },
            new ResourceType
            {
                Id = SeedIds.ResourceTypes.External,
                Name = "Externo",
                Color = "#F97316",
                IsExternal = true,
                Description =
                    "Material publicado en otro sitio web. Al abrirlo se redirige directamente "
                    + "al enlace original.",
            },
        };
        await AddMissingAsync(context.ResourceTypes, seed, ct);
    }

    private static async Task AddMissingAsync<TEntity>(
        DbSet<TEntity> set,
        IReadOnlyList<TEntity> seed,
        CancellationToken ct
    )
        where TEntity : IdentifiableEntity
    {
        var ids = seed.Select(x => x.Id).ToList();
        var existing = await set.Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct);
        set.AddRange(seed.Where(x => !existing.Contains(x.Id)));
    }
}
