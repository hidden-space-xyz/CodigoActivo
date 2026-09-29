using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Resources;
using CodigoActivo.Domain.Users;
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
            UserStatusType.Create(
                SeedIds.UserStatusTypes.Pending,
                "Pendiente",
                "Cuenta registrada que aún no ha completado el proceso de verificación. "
                    + "No puede acceder a las funciones de la plataforma hasta que un administrador la apruebe.",
                "#6B7280"
            ),
            UserStatusType.Create(
                SeedIds.UserStatusTypes.Active,
                "Activo",
                "Cuenta verificada y habilitada. Tiene acceso completo a las funcionalidades "
                    + "correspondientes a su tipo de usuario.",
                "#22C55E"
            ),
            UserStatusType.Create(
                SeedIds.UserStatusTypes.Blocked,
                "Bloqueado",
                "Cuenta suspendida por un administrador. El acceso queda restringido hasta que "
                    + "se restablezca manualmente.",
                "#EF4444"
            ),
            UserStatusType.Create(
                SeedIds.UserStatusTypes.Dependent,
                "Dependiente",
                "Cuenta vinculada a un tutor o cuenta principal. No puede iniciar sesión por sí "
                    + "misma y se gestiona a través de la cuenta responsable.",
                "#3B82F6"
            ),
        };
        await AddMissingAsync(context.UserStatusTypes, seed, ct);
    }

    private async Task SeedUserTypesAsync(CancellationToken ct)
    {
        var seed = new[]
        {
            UserType.Create(
                SeedIds.UserTypes.Member,
                "Socio",
                "Integrante registrado de la organización. Apoya a la asociación de forma "
                    + "continua, participa en sus actividades y accede a las secciones reservadas a socios.",
                "#EF4444"
            ),
            UserType.Create(
                SeedIds.UserTypes.Sponsor,
                "Patrocinador",
                "Persona o entidad que respalda a la asociación aportando recursos o "
                    + "financiación para que sus actividades sean posibles.",
                "#EAB308"
            ),
            UserType.Create(
                SeedIds.UserTypes.Participant,
                "Participante",
                "Persona que se inscribe y asiste a los eventos y actividades para aprender y "
                    + "disfrutar, sin asumir un rol organizativo.",
                "#3B82F6"
            ),
        };
        await AddMissingAsync(context.UserTypes, seed, ct);
    }

    private async Task SeedActivityRoleTypesAsync(CancellationToken ct)
    {
        var seed = new[]
        {
            ActivityRoleType.Create(
                SeedIds.ActivityRoleTypes.Leader,
                "Líder",
                "Responsable de coordinar la actividad. Dirige al equipo, organiza las tareas "
                    + "y vela por el cumplimiento de los objetivos."
            ),
            ActivityRoleType.Create(
                SeedIds.ActivityRoleTypes.Volunteer,
                "Voluntario",
                "Echa una mano de forma desinteresada durante la actividad, asumiendo tareas de soporte para su correcto desarrollo."
            ),
            ActivityRoleType.Create(
                SeedIds.ActivityRoleTypes.Participant,
                "Participante",
                "Asiste a la actividad como público o beneficiario, sin responsabilidades de organización."
            ),
        };
        await AddMissingAsync(context.ActivityRoleTypes, seed, ct);
    }

    private async Task SeedAssignmentStatusTypesAsync(CancellationToken ct)
    {
        var seed = new[]
        {
            AssignmentStatusType.Create(
                SeedIds.AssignmentStatusTypes.Requested,
                "Solicitada",
                "La asignación ha sido solicitada y está pendiente de revisión por parte de un "
                    + "responsable.",
                "#6B7280"
            ),
            AssignmentStatusType.Create(
                SeedIds.AssignmentStatusTypes.Confirmed,
                "Confirmada",
                "La asignación ha sido revisada y aprobada. La persona queda oficialmente "
                    + "asignada a la actividad.",
                "#22C55E"
            ),
            AssignmentStatusType.Create(
                SeedIds.AssignmentStatusTypes.Denied,
                "Rechazada",
                "La asignación ha sido revisada y rechazada. La persona no participará en la "
                    + "actividad bajo este rol.",
                "#EF4444"
            ),
        };
        await AddMissingAsync(context.AssignmentStatusTypes, seed, ct);
    }

    private async Task SeedActivityModalityTypesAsync(CancellationToken ct)
    {
        var seed = new[]
        {
            ActivityModalityType.Create(SeedIds.ActivityModalityTypes.Presencial, "Presencial"),
            ActivityModalityType.Create(SeedIds.ActivityModalityTypes.Online, "Online"),
        };
        await AddMissingAsync(context.ActivityModalityTypes, seed, ct);
    }

    private async Task SeedResourceTypesAsync(CancellationToken ct)
    {
        var seed = new[]
        {
            ResourceType.Create(
                SeedIds.ResourceTypes.Internal,
                "Interno",
                "Material propio alojado en la plataforma. Incluye una descripción completa "
                    + "que se consulta desde la propia web.",
                "#3B82F6",
                false
            ),
            ResourceType.Create(
                SeedIds.ResourceTypes.External,
                "Externo",
                "Material publicado en otro sitio web. Al abrirlo se redirige directamente "
                    + "al enlace original.",
                "#F97316",
                true
            ),
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
