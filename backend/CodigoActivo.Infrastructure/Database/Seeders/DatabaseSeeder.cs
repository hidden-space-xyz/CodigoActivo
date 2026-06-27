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
                Name = "Pendiente",
                Description =
                    "Cuenta registrada que aún no ha completado el proceso de verificación. "
                    + "No puede acceder a las funciones de la plataforma hasta que un administrador la apruebe.",
            },
            new UserStatusType
            {
                Id = SeedIds.UserStatusTypes.Active,
                Name = "Activo",
                Description =
                    "Cuenta verificada y habilitada. Tiene acceso completo a las funcionalidades "
                    + "correspondientes a su tipo de usuario.",
            },
            new UserStatusType
            {
                Id = SeedIds.UserStatusTypes.Blocked,
                Name = "Bloqueado",
                Description =
                    "Cuenta suspendida por un administrador. El acceso queda restringido hasta que "
                    + "se restablezca manualmente.",
            },
            new UserStatusType
            {
                Id = SeedIds.UserStatusTypes.Dependent,
                Name = "Dependiente",
                Description =
                    "Cuenta vinculada a un tutor o cuenta principal. No puede iniciar sesión por sí "
                    + "misma y se gestiona a través de la cuenta responsable.",
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
                Name = "Administrador",
                Description =
                    "Usuario con control total del sistema. Gestiona las cuentas, los eventos, los "
                    + "catálogos y la configuración general de la plataforma.",
            },
            new UserType
            {
                Id = SeedIds.UserTypes.Member,
                Name = "Miembro",
                Description =
                    "Integrante registrado de la organización. Participa de forma habitual en las "
                    + "actividades y tiene acceso a las secciones reservadas para miembros.",
            },
            new UserType
            {
                Id = SeedIds.UserTypes.Volunteer,
                Name = "Voluntario",
                Description =
                    "Persona que colabora de forma desinteresada apoyando en la organización y "
                    + "ejecución de los eventos y actividades.",
            },
            new UserType
            {
                Id = SeedIds.UserTypes.Sponsor,
                Name = "Patrocinador",
                Description =
                    "Entidad o persona que respalda con recursos económicos o materiales los eventos "
                    + "y proyectos de la organización.",
            },
            new UserType
            {
                Id = SeedIds.UserTypes.Participant,
                Name = "Participante",
                Description =
                    "Usuario que se inscribe y asiste a los eventos y actividades sin asumir un rol "
                    + "organizativo permanente.",
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
                Name = "Líder",
                Description =
                    "Responsable de coordinar la actividad. Dirige al equipo, organiza las tareas y "
                    + "vela por el cumplimiento de los objetivos.",
            },
            new ActivityRoleType
            {
                Id = SeedIds.ActivityRoleTypes.Helper,
                Name = "Colaborador",
                Description =
                    "Apoya al líder durante la actividad asumiendo tareas de soporte para su correcto "
                    + "desarrollo.",
            },
            new ActivityRoleType
            {
                Id = SeedIds.ActivityRoleTypes.Participant,
                Name = "Participante",
                Description =
                    "Asiste a la actividad como público o beneficiario, sin responsabilidades de "
                    + "organización.",
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
                Name = "Solicitada",
                Description =
                    "La asignación ha sido solicitada y está pendiente de revisión por parte de un "
                    + "responsable.",
            },
            new AssignmentStatusType
            {
                Id = SeedIds.AssignmentStatusTypes.Confirmed,
                Name = "Confirmada",
                Description =
                    "La asignación ha sido revisada y aprobada. La persona queda oficialmente "
                    + "asignada a la actividad.",
            },
            new AssignmentStatusType
            {
                Id = SeedIds.AssignmentStatusTypes.Denied,
                Name = "Rechazada",
                Description =
                    "La asignación ha sido revisada y rechazada. La persona no participará en la "
                    + "actividad bajo este rol.",
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
