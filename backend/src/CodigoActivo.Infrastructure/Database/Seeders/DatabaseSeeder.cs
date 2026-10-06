using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Resources;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Catalogs;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Seeders;

/// <summary>
/// Creates the initial database records when they are missing.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public class DatabaseSeeder(CodigoActivoDbContext context)
{
    /// <summary>
    /// Creates the required database records when they do not exist.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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
            new UserStatusEntry
            {
                Id = UserStatus.Pending,
                Name = "Pendiente",
                Description =
                    "Cuenta registrada que aún no ha completado el proceso de verificación. "
                    + "No puede acceder a las funciones de la plataforma hasta que un administrador la apruebe.",
                Color = "#6B7280",
            },
            new UserStatusEntry
            {
                Id = UserStatus.Active,
                Name = "Activo",
                Description =
                    "Cuenta verificada y habilitada. Tiene acceso completo a las funcionalidades "
                    + "correspondientes a su tipo de usuario.",
                Color = "#22C55E",
            },
            new UserStatusEntry
            {
                Id = UserStatus.Blocked,
                Name = "Bloqueado",
                Description =
                    "Cuenta suspendida por un administrador. El acceso queda restringido hasta que "
                    + "se restablezca manualmente.",
                Color = "#EF4444",
            },
            new UserStatusEntry
            {
                Id = UserStatus.Dependent,
                Name = "Dependiente",
                Description =
                    "Cuenta vinculada a un tutor o cuenta principal. No puede iniciar sesión por sí "
                    + "misma y se gestiona a través de la cuenta responsable.",
                Color = "#3B82F6",
            },
        };
        await AddMissingAsync<UserStatusEntry, UserStatus>(context.UserStatusTypes, seed, ct);
    }

    private async Task SeedUserTypesAsync(CancellationToken ct)
    {
        var seed = new[]
        {
            new UserTypeEntry
            {
                Id = UserType.Member,
                Name = "Socio",
                Description =
                    "Integrante registrado de la organización. Apoya a la asociación de forma "
                    + "continua, participa en sus actividades y accede a las secciones reservadas a socios.",
                Color = "#EF4444",
            },
            new UserTypeEntry
            {
                Id = UserType.Sponsor,
                Name = "Patrocinador",
                Description =
                    "Persona o entidad que respalda a la asociación aportando recursos o "
                    + "financiación para que sus actividades sean posibles.",
                Color = "#EAB308",
            },
            new UserTypeEntry
            {
                Id = UserType.Participant,
                Name = "Participante",
                Description =
                    "Persona que se inscribe y asiste a los eventos y actividades para aprender y "
                    + "disfrutar, sin asumir un rol organizativo.",
                Color = "#3B82F6",
            },
        };
        await AddMissingAsync<UserTypeEntry, UserType>(context.UserTypes, seed, ct);
    }

    private async Task SeedActivityRoleTypesAsync(CancellationToken ct)
    {
        var seed = new[]
        {
            new ActivityRoleEntry
            {
                Id = ActivityRole.Leader,
                Name = "Líder",
                Description =
                    "Responsable de coordinar la actividad. Dirige al equipo, organiza las tareas "
                    + "y vela por el cumplimiento de los objetivos.",
            },
            new ActivityRoleEntry
            {
                Id = ActivityRole.Volunteer,
                Name = "Voluntario",
                Description =
                    "Echa una mano de forma desinteresada durante la actividad, asumiendo tareas de soporte para su correcto desarrollo.",
            },
            new ActivityRoleEntry
            {
                Id = ActivityRole.Participant,
                Name = "Participante",
                Description =
                    "Asiste a la actividad como público o beneficiario, sin responsabilidades de organización.",
            },
        };
        await AddMissingAsync<ActivityRoleEntry, ActivityRole>(context.ActivityRoleTypes, seed, ct);
    }

    private async Task SeedAssignmentStatusTypesAsync(CancellationToken ct)
    {
        var seed = new[]
        {
            new AssignmentStatusEntry
            {
                Id = AssignmentStatus.Requested,
                Name = "Solicitada",
                Description =
                    "La asignación ha sido solicitada y está pendiente de revisión por parte de un "
                    + "responsable.",
                Color = "#6B7280",
            },
            new AssignmentStatusEntry
            {
                Id = AssignmentStatus.Confirmed,
                Name = "Confirmada",
                Description =
                    "La asignación ha sido revisada y aprobada. La persona queda oficialmente "
                    + "asignada a la actividad.",
                Color = "#22C55E",
            },
            new AssignmentStatusEntry
            {
                Id = AssignmentStatus.Denied,
                Name = "Rechazada",
                Description =
                    "La asignación ha sido revisada y rechazada. La persona no participará en la "
                    + "actividad bajo este rol.",
                Color = "#EF4444",
            },
        };
        await AddMissingAsync<AssignmentStatusEntry, AssignmentStatus>(
            context.AssignmentStatusTypes,
            seed,
            ct
        );
    }

    private async Task SeedActivityModalityTypesAsync(CancellationToken ct)
    {
        var seed = new[]
        {
            new ActivityModalityEntry { Id = ActivityModality.Presencial, Name = "Presencial" },
            new ActivityModalityEntry { Id = ActivityModality.Online, Name = "Online" },
        };
        await AddMissingAsync<ActivityModalityEntry, ActivityModality>(
            context.ActivityModalityTypes,
            seed,
            ct
        );
    }

    private async Task SeedResourceTypesAsync(CancellationToken ct)
    {
        var seed = new[]
        {
            new ResourceTypeEntry
            {
                Id = ResourceType.Internal,
                Name = "Interno",
                Description =
                    "Material propio alojado en la plataforma. Incluye una descripción completa "
                    + "que se consulta desde la propia web.",
                Color = "#3B82F6",
                IsExternal = false,
            },
            new ResourceTypeEntry
            {
                Id = ResourceType.External,
                Name = "Externo",
                Description =
                    "Material publicado en otro sitio web. Al abrirlo se redirige directamente "
                    + "al enlace original.",
                Color = "#F97316",
                IsExternal = true,
            },
        };
        await AddMissingAsync<ResourceTypeEntry, ResourceType>(context.ResourceTypes, seed, ct);
    }

    private static async Task AddMissingAsync<TEntity, TValue>(
        DbSet<TEntity> set,
        IReadOnlyList<TEntity> seed,
        CancellationToken ct
    )
        where TEntity : CatalogEntry<TValue>
        where TValue : struct, Enum
    {
        var ids = seed.Select(x => x.Id).ToList();
        var existing = await set.Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct);
        set.AddRange(seed.Where(x => !existing.Contains(x.Id)));
    }
}
