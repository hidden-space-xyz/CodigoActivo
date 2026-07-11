using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Security;
using CodigoActivo.Domain.Storage;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Database.Seeders;

public sealed class DemoDataSeeder(
    CodigoActivoDbContext context,
    ILocalFileSystemRepository storage,
    IPasswordHasher passwordHasher,
    IClock clock,
    ILogger<DemoDataSeeder> logger
)
{
    public const string DemoPassword = "Demo1234!";

    private const int AdultCount = 20;
    private const int ChildCount = 5;

    private static Guid AdminId => UserId(0);

    private static readonly Guid[] AllRoleTypeIds =
    [
        SeedIds.ActivityRoleTypes.Leader,
        SeedIds.ActivityRoleTypes.Helper,
        SeedIds.ActivityRoleTypes.Participant,
    ];

    private static readonly string[] CategoryColors =
    [
        "#F97316",
        "#84CC16",
        "#0EA5E9",
        "#A855F7",
        "#EF4444",
        "#14B8A6",
        "#EAB308",
        "#EC4899",
    ];

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (!context.Database.IsRelational())
            return;

        if (await context.Users.AnyAsync(u => u.Id == AdminId, ct))
        {
            logger.LogInformation("Demo data already present, skipping demo seed");
            return;
        }

        var graph = BuildGraph(clock, passwordHasher);
        var fileIds = graph.Files.ConvertAll(f => f.Id);

        try
        {
            logger.LogInformation(
                "Downloading {Count} demo images from picsum.photos",
                fileIds.Count
            );
            await DownloadImagesAsync(fileIds, ct);

            await using var transaction = await context.Database.BeginTransactionAsync(ct);

            context.Users.AddRange(graph.Users);
            await context.SaveChangesAsync(ct);

            context.Files.AddRange(graph.Files);
            context.EventCategoryTypes.AddRange(graph.CategoryTypes);
            await context.SaveChangesAsync(ct);

            context.Events.AddRange(graph.Events);
            await context.SaveChangesAsync(ct);

            context.EventCategories.AddRange(graph.EventCategories);
            context.Activities.AddRange(graph.Activities);
            await context.SaveChangesAsync(ct);

            context.ActivityAllowedRoleTypes.AddRange(graph.AllowedRoles);
            context.ActivityUserRoleAssignments.AddRange(graph.Assignments);
            await context.SaveChangesAsync(ct);

            context.Announcements.AddRange(graph.Announcements);
            context.Resources.AddRange(graph.Resources);
            context.Partners.AddRange(graph.Partners);
            await context.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);
        }
        catch
        {
            foreach (var id in fileIds)
                storage.Delete(StoredName(id));
            throw;
        }

        logger.LogInformation(
            "Demo data seeded: {Users} users, {Events} events, {Activities} activities, {Files} images",
            graph.Users.Count,
            graph.Events.Count,
            graph.Activities.Count,
            graph.Files.Count
        );
    }

    public async Task RemoveAsync(CancellationToken ct = default)
    {
        if (!context.Database.IsRelational())
            return;

        if (!await context.Users.AnyAsync(u => u.Id == AdminId, ct))
            return;

        logger.LogInformation("Removing demo data");

        var demoUserIds = Enumerable.Range(0, UserSeeds.Length).Select(UserId).ToList();
        var demoCategoryIds = Enumerable
            .Range(0, DemoCategories.Length)
            .Select(CategoryId)
            .ToList();
        var ownedEventIds = await context
            .Events.Where(e => e.CreatedBy == AdminId)
            .Select(e => e.Id)
            .ToListAsync(ct);
        var ownedActivityIds = await context
            .Activities.Where(a => a.CreatedBy == AdminId)
            .Select(a => a.Id)
            .ToListAsync(ct);

        await context
            .ActivityUserRoleAssignments.Where(x =>
                ownedActivityIds.Contains(x.ActivityId) || demoUserIds.Contains(x.UserId)
            )
            .ExecuteDeleteAsync(ct);
        await context
            .ActivityAllowedRoleTypes.Where(x => ownedActivityIds.Contains(x.ActivityId))
            .ExecuteDeleteAsync(ct);
        await context
            .EventCategories.Where(x => ownedEventIds.Contains(x.EventId))
            .ExecuteDeleteAsync(ct);
        await context.Activities.Where(a => a.CreatedBy == AdminId).ExecuteDeleteAsync(ct);
        await context.Events.Where(e => e.CreatedBy == AdminId).ExecuteDeleteAsync(ct);
        await context.Announcements.Where(a => a.CreatedBy == AdminId).ExecuteDeleteAsync(ct);
        await context.Resources.Where(r => r.CreatedBy == AdminId).ExecuteDeleteAsync(ct);
        await context.Partners.Where(p => p.CreatedBy == AdminId).ExecuteDeleteAsync(ct);

        await context
            .EventCategoryTypes.Where(c =>
                demoCategoryIds.Contains(c.Id)
                && !context.EventCategories.Any(ec => ec.EventCategoryTypeId == c.Id)
            )
            .ExecuteDeleteAsync(ct);

        var reclaimableFiles = await context
            .Files.Where(f =>
                f.UploadedBy == AdminId
                && !context.Events.Any(e => e.ThumbnailId == f.Id)
                && !context.Activities.Any(a => a.ThumbnailId == f.Id)
                && !context.Announcements.Any(a => a.ThumbnailId == f.Id)
                && !context.Resources.Any(r => r.ThumbnailId == f.Id)
                && !context.Partners.Any(p => p.ThumbnailId == f.Id)
            )
            .Select(f => new { f.Id, f.Extension })
            .ToListAsync(ct);
        var reclaimableFileIds = reclaimableFiles.ConvertAll(f => f.Id);
        await context.Files.Where(f => reclaimableFileIds.Contains(f.Id)).ExecuteDeleteAsync(ct);

        await context
            .Users.Where(u => demoUserIds.Contains(u.Id) && u.ParentId != null)
            .ExecuteDeleteAsync(ct);
        await context
            .Users.Where(u => demoUserIds.Contains(u.Id) && u.Id != AdminId)
            .ExecuteDeleteAsync(ct);
        await RemoveOrNeutralizeAdminAsync(ct);

        foreach (var file in reclaimableFiles)
            storage.Delete(
                string.Create(CultureInfo.InvariantCulture, $"{file.Id}.{file.Extension}")
            );

        logger.LogInformation("Demo data removed");
    }

    private async Task RemoveOrNeutralizeAdminAsync(CancellationToken ct)
    {
        try
        {
            await context.Users.Where(u => u.Id == AdminId).ExecuteDeleteAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Demo admin still referenced by non-demo data; disabling the account instead of deleting it"
            );
            await context
                .Users.Where(u => u.Id == AdminId)
                .ExecuteUpdateAsync(
                    setters =>
                        setters
                            .SetProperty(u => u.UserStatusTypeId, SeedIds.UserStatusTypes.Blocked)
                            .SetProperty(u => u.PasswordHash, (string?)null)
                            .SetProperty(u => u.OtpCodeHash, (string?)null)
                            .SetProperty(u => u.OtpExpiresAt, (DateTimeOffset?)null),
                    ct
                );
        }
    }

    internal static DemoGraph BuildGraph(IClock clock, IPasswordHasher passwordHasher)
    {
        var now = clock.UtcNow;
        var passwordHash = passwordHasher.Hash(DemoPassword);

        var users = new List<User>(UserSeeds.Length);
        for (var i = 0; i < UserSeeds.Length; i++)
        {
            var seed = UserSeeds[i];
            var isChild = seed.Kind == UserKind.Child;
            users.Add(
                new User
                {
                    Id = UserId(i),
                    FirstName = seed.FirstName,
                    LastName = seed.LastName,
                    Email = isChild ? null : BuildEmail(seed),
                    Phone = isChild ? null : BuildPhone(i),
                    PasswordHash = isChild ? null : passwordHash,
                    BirthDate = BuildBirthDate(i, seed.BirthYear),
                    ParentId = seed.ParentIndex is { } parent ? UserId(parent) : null,
                    UserStatusTypeId = isChild
                        ? SeedIds.UserStatusTypes.Dependent
                        : SeedIds.UserStatusTypes.Active,
                    UserTypeId = ResolveUserTypeId(seed.Kind),
                    IsAdmin = seed.Kind == UserKind.Admin,
                    LastLoginAt = isChild ? null : now.AddDays(-(i % 9)),
                    CreatedAt = now.AddDays(-120),
                }
            );
        }

        var categoryTypes = new List<EventCategoryType>(DemoCategories.Length);
        var categoryIdByName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < DemoCategories.Length; i++)
        {
            categoryTypes.Add(
                new EventCategoryType
                {
                    Id = CategoryId(i),
                    Name = DemoCategories[i],
                    Color = CategoryColors[i % CategoryColors.Length],
                }
            );
            categoryIdByName[DemoCategories[i]] = CategoryId(i);
        }

        var files = new List<FileEntity>();
        var events = new List<Event>(DemoEvents.Length);
        var eventCategories = new List<EventCategory>();
        var activities = new List<Activity>();
        var allowedRoles = new List<ActivityAllowedRoleType>();
        var assignments = new List<ActivityUserRoleAssignment>();

        for (var e = 0; e < DemoEvents.Length; e++)
        {
            var seed = DemoEvents[e];
            var eventId = Guid.NewGuid();
            var duration = 1 + (e % 3);
            var start = clock.Today.AddDays(-180 + (e * 22));
            var end = start.AddDays(duration - 1);
            var label = (e + 1).ToString("D2", CultureInfo.InvariantCulture);
            var descriptionImageId = NewFile(files, $"evento-{label}-galeria.jpg", now);

            events.Add(
                new Event
                {
                    Id = eventId,
                    Title = seed.Title,
                    Subtitle = seed.Subtitle,
                    Description = BuildRichText(seed.Description, descriptionImageId, seed.Title),
                    EventStartsAt = start,
                    EventEndsAt = end,
                    SignupStartsAt = ToUtc(clock.TimeZone, start.AddDays(-30), 9, 0),
                    SignupEndsAt = ToUtc(clock.TimeZone, start.AddDays(-1), 23, 59),
                    Featured = e is 2 or 7 or 12 or 17,
                    ThumbnailId = NewFile(files, $"evento-{label}-portada.jpg", now),
                    CreatedAt = now.AddDays(-90),
                    CreatedBy = AdminId,
                }
            );

            foreach (var categoryId in ResolveCategoryIds(seed.Categories, categoryIdByName))
                eventCategories.Add(
                    new EventCategory { EventId = eventId, EventCategoryTypeId = categoryId }
                );

            for (var a = 0; a < seed.Activities.Length; a++)
            {
                var activity = seed.Activities[a];
                var activityId = Guid.NewGuid();
                var activityStart = ToUtc(
                    clock.TimeZone,
                    start.AddDays(a % duration),
                    10 + (a * 2),
                    0
                );

                activities.Add(
                    new Activity
                    {
                        Id = activityId,
                        Title = activity.Title,
                        Description = activity.Description,
                        Location = activity.Location,
                        ActivityStartsAt = activityStart,
                        ActivityEndsAt = activityStart.AddMinutes(90),
                        EventId = eventId,
                        ActivityModalityTypeId = ResolveModalityId(activity.Modality),
                        ThumbnailId = NewFile(files, $"evento-{label}-actividad-{a + 1}.jpg", now),
                        CreatedAt = now.AddDays(-85),
                        CreatedBy = AdminId,
                    }
                );

                foreach (var roleTypeId in AllRoleTypeIds)
                    allowedRoles.Add(
                        new ActivityAllowedRoleType
                        {
                            ActivityId = activityId,
                            ActivityRoleTypeId = roleTypeId,
                        }
                    );

                assignments.AddRange(BuildAssignments((e * 5) + a, activityId));
            }
        }

        var news = new List<Announcement>(DemoNews.Length);
        for (var i = 0; i < DemoNews.Length; i++)
        {
            var seed = DemoNews[i];
            var label = (i + 1).ToString("D2", CultureInfo.InvariantCulture);
            news.Add(
                new Announcement
                {
                    Id = Guid.NewGuid(),
                    Title = seed.Title,
                    Subtitle = seed.Subtitle,
                    Description = BuildRichText(seed.Description, null, null),
                    Featured = i is 0 or 4,
                    ThumbnailId = NewFile(files, $"noticia-{label}-portada.jpg", now),
                    CreatedAt = now.AddDays(-(i * 6) - 3),
                    CreatedBy = AdminId,
                }
            );
        }

        var resources = new List<Resource>(DemoResources.Length + DemoExternalResources.Length);
        for (var i = 0; i < DemoResources.Length; i++)
        {
            var seed = DemoResources[i];
            var label = (i + 1).ToString("D2", CultureInfo.InvariantCulture);
            resources.Add(
                new Resource
                {
                    Id = Guid.NewGuid(),
                    Title = seed.Title,
                    Subtitle = seed.Subtitle,
                    Description = BuildRichText(seed.Description, null, null),
                    ResourceTypeId = SeedIds.ResourceTypes.Internal,
                    ThumbnailId = NewFile(files, $"recurso-{label}-portada.jpg", now),
                    CreatedAt = now.AddDays(-(i * 8) - 5),
                    CreatedBy = AdminId,
                }
            );
        }

        for (var i = 0; i < DemoExternalResources.Length; i++)
        {
            var seed = DemoExternalResources[i];
            var label = (DemoResources.Length + i + 1).ToString("D2", CultureInfo.InvariantCulture);
            resources.Add(
                new Resource
                {
                    Id = Guid.NewGuid(),
                    Title = seed.Title,
                    Subtitle = seed.Subtitle,
                    Url = seed.Url,
                    ResourceTypeId = SeedIds.ResourceTypes.External,
                    ThumbnailId = NewFile(files, $"recurso-{label}-portada.jpg", now),
                    CreatedAt = now.AddDays(-((DemoResources.Length + i) * 8) - 5),
                    CreatedBy = AdminId,
                }
            );
        }

        var partners = new List<Partner>(DemoPartners.Length);
        for (var i = 0; i < DemoPartners.Length; i++)
        {
            var seed = DemoPartners[i];
            var label = (i + 1).ToString("D2", CultureInfo.InvariantCulture);
            partners.Add(
                new Partner
                {
                    Id = Guid.NewGuid(),
                    Name = seed.Name,
                    Tier = seed.Tier,
                    Web = seed.Web,
                    FromDate = clock.Today.AddMonths(-(6 + (i * 4))),
                    ThumbnailId = NewFile(files, $"partner-{label}-logo.jpg", now),
                    CreatedAt = now.AddDays(-200),
                    CreatedBy = AdminId,
                }
            );
        }

        return new DemoGraph(
            users,
            files,
            categoryTypes,
            events,
            eventCategories,
            activities,
            allowedRoles,
            assignments,
            news,
            resources,
            partners
        );
    }

    private static Guid NewFile(List<FileEntity> files, string name, DateTimeOffset now)
    {
        var id = Guid.NewGuid();
        files.Add(
            new FileEntity
            {
                Id = id,
                Name = name,
                Extension = "jpg",
                UploadedAt = now,
                UploadedBy = AdminId,
            }
        );
        return id;
    }

    private static IEnumerable<ActivityUserRoleAssignment> BuildAssignments(
        int globalIndex,
        Guid activityId
    )
    {
        var baseAdult = (globalIndex * 3) % AdultCount;
        (int UserIndex, Guid RoleTypeId)[] picks =
        [
            (baseAdult, SeedIds.ActivityRoleTypes.Leader),
            ((baseAdult + 1) % AdultCount, SeedIds.ActivityRoleTypes.Helper),
            ((baseAdult + 2) % AdultCount, SeedIds.ActivityRoleTypes.Participant),
            (AdultCount + (globalIndex % ChildCount), SeedIds.ActivityRoleTypes.Participant),
            ((baseAdult + 3) % AdultCount, SeedIds.ActivityRoleTypes.Participant),
        ];

        for (var slot = 0; slot < picks.Length; slot++)
        {
            yield return new ActivityUserRoleAssignment
            {
                UserId = UserId(picks[slot].UserIndex),
                ActivityId = activityId,
                ActivityRoleTypeId = picks[slot].RoleTypeId,
                AssignmentStatusId = ResolveAssignmentStatus(globalIndex, slot),
            };
        }
    }

    private static IEnumerable<Guid> ResolveCategoryIds(
        string[] names,
        Dictionary<string, Guid> categoryIdByName
    )
    {
        var ids = names
            .Where(categoryIdByName.ContainsKey)
            .Select(name => categoryIdByName[name])
            .Distinct()
            .ToList();
        return ids.Count > 0 ? ids : [CategoryId(0)];
    }

    private async Task DownloadImagesAsync(IReadOnlyCollection<Guid> fileIds, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        using var throttle = new SemaphoreSlim(8, 8);
        await Task.WhenAll(fileIds.Select(id => DownloadImageAsync(http, throttle, id, ct)));
    }

    private async Task DownloadImageAsync(
        HttpClient http,
        SemaphoreSlim throttle,
        Guid fileId,
        CancellationToken ct
    )
    {
        await throttle.WaitAsync(ct);
        try
        {
            var url = string.Create(
                CultureInfo.InvariantCulture,
                $"https://picsum.photos/seed/{fileId:N}/1080/720"
            );
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    var bytes = await http.GetByteArrayAsync(url, ct);
                    using var stream = new MemoryStream(bytes, writable: false);
                    await storage.SaveAsync(StoredName(fileId), stream, ct);
                    return;
                }
                catch (Exception ex) when (!ct.IsCancellationRequested && attempt < 3)
                {
                    logger.LogWarning(
                        ex,
                        "Retrying demo image {FileId} (attempt {Attempt})",
                        fileId,
                        attempt
                    );
                    await Task.Delay(TimeSpan.FromMilliseconds(400 * attempt), ct);
                }
            }
        }
        finally
        {
            throttle.Release();
        }
    }

    private static string BuildRichText(
        IReadOnlyList<string> paragraphs,
        Guid? imageId,
        string? imageAlt
    )
    {
        var content = new JsonArray();
        for (var i = 0; i < paragraphs.Count; i++)
        {
            content.Add(
                new JsonObject
                {
                    ["type"] = "paragraph",
                    ["content"] = new JsonArray(
                        new JsonObject { ["type"] = "text", ["text"] = paragraphs[i] }
                    ),
                }
            );

            if (imageId is { } id && i == 0)
            {
                content.Add(
                    new JsonObject
                    {
                        ["type"] = "image",
                        ["attrs"] = new JsonObject
                        {
                            ["src"] = RichTextFileReferences.ContentUrlMarker(id),
                            ["alt"] = imageAlt,
                            ["title"] = null,
                        },
                    }
                );
            }
        }

        return new JsonObject { ["type"] = "doc", ["content"] = content }.ToJsonString();
    }

    private static DateTimeOffset ToUtc(TimeZoneInfo timeZone, DateOnly date, int hour, int minute)
    {
        var local = date.ToDateTime(new TimeOnly(hour, minute));
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timeZone), TimeSpan.Zero);
    }

    private static string StoredName(Guid fileId) =>
        string.Create(CultureInfo.InvariantCulture, $"{fileId}.jpg");

    private static Guid MakeId(int category, int index) =>
        new(
            string.Create(
                CultureInfo.InvariantCulture,
                $"dede{category:x4}-0000-0000-0000-{index:x12}"
            )
        );

    private static Guid UserId(int index) => MakeId(1, index);

    private static Guid CategoryId(int index) => MakeId(3, index);

    private static Guid ResolveUserTypeId(UserKind kind) =>
        kind switch
        {
            UserKind.Sponsor => SeedIds.UserTypes.Sponsor,
            UserKind.Child => SeedIds.UserTypes.Participant,
            _ => SeedIds.UserTypes.Member,
        };

    private static Guid ResolveModalityId(string modality) =>
        string.Equals(modality, "Online", StringComparison.OrdinalIgnoreCase)
            ? SeedIds.ActivityModalityTypes.Online
            : SeedIds.ActivityModalityTypes.Presencial;

    private static Guid ResolveAssignmentStatus(int globalIndex, int slot)
    {
        if (slot == 0)
            return SeedIds.AssignmentStatusTypes.Confirmed;
        if ((globalIndex + slot) % 7 == 0)
            return SeedIds.AssignmentStatusTypes.Requested;
        return (globalIndex + slot) % 11 == 0
            ? SeedIds.AssignmentStatusTypes.Denied
            : SeedIds.AssignmentStatusTypes.Confirmed;
    }

    private static string BuildEmail(UserSeed seed)
    {
        var local = Ascii($"{seed.FirstName}.{seed.LastName}").ToLowerInvariant().Replace(' ', '.');
        return $"{local}@demo.codigoactivo.es";
    }

    private static string BuildPhone(int index) =>
        string.Create(CultureInfo.InvariantCulture, $"+3466{index:D7}");

    private static DateOnly BuildBirthDate(int index, int year) =>
        new(year, ((index * 7) % 12) + 1, ((index * 5) % 28) + 1);

    private static string Ascii(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private enum UserKind
    {
        Admin,
        Member,
        Sponsor,
        Child,
    }

    private sealed record UserSeed(
        string FirstName,
        string LastName,
        UserKind Kind,
        int BirthYear,
        int? ParentIndex
    );

    private sealed record EventSeed(
        string Title,
        string Subtitle,
        string[] Categories,
        string[] Description,
        ActivitySeed[] Activities
    );

    private sealed record ActivitySeed(
        string Title,
        string Description,
        string Location,
        string Modality
    );

    private sealed record ContentSeed(string Title, string Subtitle, string[] Description);

    private sealed record ExternalResourceSeed(string Title, string Subtitle, string Url);

    private sealed record PartnerSeed(string Name, int Tier, string? Web);

    private static readonly string[] DemoCategories =
    [
        "Formación",
        "Robótica",
        "Ciberseguridad",
        "Inteligencia Artificial",
        "Videojuegos",
        "Impresión 3D",
        "Infantil",
        "Comunidad",
    ];

    private static readonly EventSeed[] DemoEvents =
    [
        new(
            "Introducción a la programación con Python",
            "Un itinerario práctico para dar tus primeros pasos en el desarrollo de software, sin conocimientos previos y con proyectos reales desde la primera sesión.",
            ["Formación"],
            [
                "Este curso está pensado para personas que nunca han programado y quieren empezar con buen pie. Trabajamos con Python por ser un lenguaje claro, muy demandado y con una comunidad enorme, ideal para entender los conceptos básicos sin perderse en detalles innecesarios.",
                "A lo largo de las sesiones combinamos explicaciones cortas con mucha práctica guiada. Cada participante escribe su propio código desde el primer día y va construyendo pequeños programas que consolidan lo aprendido, siempre acompañado por el equipo de mentoras y mentores voluntarios de la asociación.",
                "Al terminar habrás creado tu primer proyecto completo, entenderás la lógica detrás de cualquier lenguaje de programación y sabrás cómo seguir aprendiendo por tu cuenta con recursos gratuitos y de calidad. No necesitas más que un ordenador y ganas de aprender.",
            ],
            [
                new(
                    "Primeros pasos con Python y el entorno de trabajo",
                    "Instalamos Python y el editor Visual Studio Code, configuramos el entorno y ejecutamos nuestro primer programa. Repasamos cómo funciona un intérprete y por qué es tan importante practicar poco a poco.",
                    "Centro Cívico El Pozo del Tío Raimundo, Madrid",
                    "Presencial"
                ),
                new(
                    "Variables, tipos de datos y estructuras de control",
                    "Aprendemos a guardar información en variables, a trabajar con números y textos, y a tomar decisiones en el código con condicionales y bucles. Practicamos con ejercicios cortos y resolvemos dudas en directo.",
                    "Centro Cívico El Pozo del Tío Raimundo, Madrid",
                    "Presencial"
                ),
                new(
                    "Funciones y organización del código",
                    "Vemos cómo agrupar instrucciones en funciones para reutilizar código y mantenerlo ordenado. Esta sesión se imparte a distancia para facilitar la conciliación de quienes no pueden asistir presencialmente.",
                    "Sesión online por videollamada (plataforma Jitsi)",
                    "Online"
                ),
                new(
                    "Tu primer proyecto: una agenda de contactos",
                    "Ponemos en práctica todo lo aprendido construyendo una pequeña agenda que permite añadir, buscar y borrar contactos. Un proyecto real que demuestra que ya sabes programar cosas útiles.",
                    "Centro Cívico El Pozo del Tío Raimundo, Madrid",
                    "Presencial"
                ),
                new(
                    "Buenas prácticas y siguientes pasos",
                    "Cerramos el curso con consejos sobre estilo de código, control de versiones básico y una hoja de ruta con recursos gratuitos para seguir avanzando. Resolvemos las últimas dudas del grupo.",
                    "Sesión online por videollamada (plataforma Jitsi)",
                    "Online"
                ),
            ]
        ),
        new(
            "Robótica educativa con Arduino",
            "Construye y programa tus propios circuitos desde cero. Un taller para descubrir la electrónica y el pensamiento lógico montando robots sencillos que cobran vida.",
            ["Robótica", "Formación"],
            [
                "La placa Arduino es una de las mejores puertas de entrada al mundo de la electrónica y la programación física. En este taller aprenderás a conectar componentes, leer sensores y controlar motores para crear tus primeros proyectos con las manos.",
                "Trabajamos por parejas con kits que la asociación pone a disposición de cada participante, de modo que no hace falta comprar nada. Partimos de montajes muy sencillos y vamos aumentando la dificultad a medida que ganamos confianza con el hardware y el código.",
                "El taller está abierto a jóvenes y personas adultas curiosas, sin necesidad de experiencia previa. Al final montaremos un pequeño robot que reacciona a su entorno, y te llevarás las bases para seguir experimentando en casa o en el aula.",
            ],
            [
                new(
                    "La placa Arduino y sus componentes",
                    "Conocemos las partes de la placa, la protoboard y los componentes básicos como resistencias, cables y LEDs. Montamos nuestro primer circuito que enciende una luz de forma segura.",
                    "Espacio Maker de la Biblioteca Pública de Valencia",
                    "Presencial"
                ),
                new(
                    "Programar luces: parpadeos y semáforos",
                    "Damos el salto al código para controlar cuándo se encienden y apagan las luces. Construimos un semáforo con tres LEDs y ajustamos sus tiempos, entendiendo cómo el software gobierna el hardware.",
                    "Espacio Maker de la Biblioteca Pública de Valencia",
                    "Presencial"
                ),
                new(
                    "Sensores: que el robot perciba el mundo",
                    "Conectamos sensores de luz, temperatura y distancia para que el circuito reaccione a lo que ocurre a su alrededor. Aprendemos a leer sus valores y a tomar decisiones con ellos.",
                    "Espacio Maker de la Biblioteca Pública de Valencia",
                    "Presencial"
                ),
                new(
                    "Movimiento: motores y servos",
                    "Incorporamos motores y servomotores para dar movimiento a nuestros montajes. Controlamos velocidad y giro, y preparamos las piezas que darán vida al robot final.",
                    "Espacio Maker de la Biblioteca Pública de Valencia",
                    "Presencial"
                ),
                new(
                    "Proyecto final: robot que esquiva obstáculos",
                    "Unimos sensores y motores para montar un robot capaz de detectar obstáculos y cambiar de dirección. Cada equipo presenta su creación y comparte los retos que ha superado.",
                    "Espacio Maker de la Biblioteca Pública de Valencia",
                    "Presencial"
                ),
            ]
        ),
        new(
            "Ciberseguridad práctica para pymes y autónomos",
            "Protege tu negocio sin ser experto en tecnología. Medidas sencillas y asequibles para reducir riesgos, evitar fraudes y cumplir con tus obligaciones digitales.",
            ["Ciberseguridad", "Formación"],
            [
                "Los pequeños negocios son un objetivo habitual de estafas y ataques informáticos, precisamente porque suelen carecer de un departamento técnico. Este programa está diseñado para dueñas y dueños de pymes, autónomos y personal administrativo que quieren proteger su actividad.",
                "Nos centramos en medidas realistas y de bajo coste que cualquier negocio puede aplicar de inmediato: gestión de contraseñas, copias de seguridad, detección de correos fraudulentos y protección de los datos de la clientela. Nada de tecnicismos innecesarios.",
                "Cada sesión incluye ejemplos de casos reales ocurridos en el tejido empresarial español y una lista de comprobación para llevarte a casa. Saldrás con un plan de acción concreto para mejorar la seguridad de tu negocio desde el día siguiente.",
            ],
            [
                new(
                    "Mapa de riesgos de tu negocio",
                    "Identificamos qué información y qué sistemas son críticos para tu actividad y dónde están tus puntos débiles. Elaboramos un inventario sencillo que sirve de base para todo lo demás.",
                    "Cámara de Comercio de Bilbao",
                    "Presencial"
                ),
                new(
                    "Contraseñas y doble factor sin dolores de cabeza",
                    "Aprendemos a usar un gestor de contraseñas y a activar la verificación en dos pasos en las herramientas del día a día. Descartamos hábitos peligrosos como reutilizar la misma clave en todo.",
                    "Cámara de Comercio de Bilbao",
                    "Presencial"
                ),
                new(
                    "Detectar el phishing y el fraude del CEO",
                    "Analizamos correos y mensajes fraudulentos reales para aprender a reconocer las señales de alarma. Ensayamos qué hacer ante una factura falsa o una petición urgente de transferencia.",
                    "Sesión online por videollamada (plataforma Google Meet)",
                    "Online"
                ),
                new(
                    "Copias de seguridad que de verdad funcionan",
                    "Diseñamos una estrategia de copias sencilla siguiendo la regla de las tres copias. Comprobamos que se puedan restaurar, porque una copia que no se puede recuperar no sirve de nada.",
                    "Cámara de Comercio de Bilbao",
                    "Presencial"
                ),
                new(
                    "Plan de respuesta ante incidentes",
                    "Preparamos un protocolo claro de a quién avisar y qué pasos seguir si algo sale mal. Repasamos los recursos públicos gratuitos, como la línea de ayuda del INCIBE, para pedir apoyo.",
                    "Sesión online por videollamada (plataforma Google Meet)",
                    "Online"
                ),
            ]
        ),
        new(
            "Inteligencia artificial generativa aplicada",
            "Aprende a sacar partido a las herramientas de IA en tu trabajo y tu día a día, con criterio, sentido crítico y respeto a la privacidad.",
            ["Inteligencia Artificial", "Formación"],
            [
                "La inteligencia artificial generativa ha pasado de los laboratorios a nuestras herramientas cotidianas en muy poco tiempo. Este curso te ayuda a entender qué puede y qué no puede hacer, para que la uses con cabeza y no te dejes llevar por el ruido.",
                "Combinamos una parte de fundamentos, para saber cómo funcionan por dentro estos modelos, con mucha práctica: redacción de textos, generación de imágenes, resumen de documentos y automatización de tareas repetitivas. Todo con ejemplos cercanos y aplicables.",
                "Prestamos especial atención a los límites de la tecnología: sesgos, errores, verificación de la información y protección de datos personales. Nuestro objetivo es formar a personas usuarias responsables, no a fanáticas ni a detractoras de la IA.",
            ],
            [
                new(
                    "Cómo piensan los modelos de lenguaje",
                    "Explicamos de forma sencilla qué es un modelo de lenguaje, cómo se entrena y por qué a veces se inventa respuestas. Entender esto es clave para usarlo con criterio.",
                    "Ateneu de Innovación, Barcelona",
                    "Presencial"
                ),
                new(
                    "El arte de escribir buenas instrucciones",
                    "Practicamos cómo formular peticiones claras para obtener mejores resultados. Comparamos respuestas según cómo planteamos la instrucción y creamos nuestra propia biblioteca de prompts útiles.",
                    "Ateneu de Innovación, Barcelona",
                    "Presencial"
                ),
                new(
                    "Generación de imágenes y contenido visual",
                    "Exploramos herramientas de creación de imágenes a partir de texto y hablamos de sus usos, sus derechos de autor y sus dilemas. Cada participante genera y ajusta sus propias composiciones.",
                    "Sesión online por videollamada (plataforma Zoom)",
                    "Online"
                ),
                new(
                    "Automatizar tareas repetitivas con IA",
                    "Vemos cómo encadenar la IA con otras herramientas para resumir correos, clasificar documentos o redactar borradores. Diseñamos un pequeño flujo de trabajo pensado para ahorrar tiempo real.",
                    "Ateneu de Innovación, Barcelona",
                    "Presencial"
                ),
                new(
                    "Límites, sesgos y uso responsable",
                    "Analizamos casos de errores y sesgos, y establecemos buenas prácticas para verificar la información y proteger datos personales. Debatimos cuándo conviene y cuándo no conviene usar estas herramientas.",
                    "Sesión online por videollamada (plataforma Zoom)",
                    "Online"
                ),
            ]
        ),
        new(
            "Crea tu primer videojuego con Godot",
            "Del papel a la pantalla: diseña, programa y publica un videojuego 2D completo usando un motor libre y gratuito, sin necesidad de experiencia previa.",
            ["Videojuegos", "Formación"],
            [
                "Hacer videojuegos es una forma divertidísima de aprender a programar, diseñar y contar historias. En este taller usamos Godot, un motor de código abierto, gratuito y cada vez más popular, perfecto para empezar sin barreras económicas.",
                "Partimos de una idea sencilla y la vamos convirtiendo en un juego jugable a lo largo de las sesiones. Aprenderás a mover personajes, gestionar colisiones, añadir puntuaciones y sonidos, y a pulir la experiencia hasta que sea agradable de jugar.",
                "El taller mezcla creatividad y lógica a partes iguales, y está pensado para adolescentes y personas adultas. Al terminar tendrás tu propio juego exportado y listo para compartir con amistades y familia, además de las bases para seguir creando por tu cuenta.",
            ],
            [
                new(
                    "Descubre el motor Godot",
                    "Recorremos la interfaz del motor, creamos nuestro primer proyecto y entendemos cómo se organizan las escenas y los nodos. Ponemos un personaje en pantalla en la primera sesión.",
                    "Fábrica de Innovación, Polígono Sur, Sevilla",
                    "Presencial"
                ),
                new(
                    "Movimiento y físicas del personaje",
                    "Programamos los controles para mover al protagonista y hacer que salte. Trabajamos con la gravedad y las colisiones para que el mundo del juego se comporte de forma creíble.",
                    "Fábrica de Innovación, Polígono Sur, Sevilla",
                    "Presencial"
                ),
                new(
                    "Enemigos, objetos y reglas del juego",
                    "Añadimos enemigos, monedas y obstáculos, y definimos las reglas que hacen ganar o perder. Aprendemos a llevar la cuenta de vidas y puntos con variables sencillas.",
                    "Sesión online por videollamada (plataforma Discord con pantalla compartida)",
                    "Online"
                ),
                new(
                    "Sonido, música y detalles visuales",
                    "Incorporamos efectos de sonido y música, y damos vida a las pantallas con animaciones y menús. Estos detalles marcan la diferencia entre un prototipo y un juego que apetece jugar.",
                    "Fábrica de Innovación, Polígono Sur, Sevilla",
                    "Presencial"
                ),
                new(
                    "Exporta y comparte tu juego",
                    "Aprendemos a exportar el juego para que otras personas puedan jugarlo y lo mostramos al resto del grupo. Cerramos con ideas para seguir mejorándolo y ampliándolo en casa.",
                    "Fábrica de Innovación, Polígono Sur, Sevilla",
                    "Presencial"
                ),
            ]
        ),
        new(
            "Impresión 3D desde cero",
            "Descubre cómo pasar de una idea a un objeto físico. Modelado sencillo, preparación de la impresora y tus primeras piezas reales en un taller totalmente práctico.",
            ["Impresión 3D", "Formación"],
            [
                "La impresión 3D ha dejado de ser cosa de laboratorios para convertirse en una herramienta accesible para reparar, crear y aprender. En este taller descubrirás todo el proceso, desde el diseño digital hasta la pieza que sostienes en la mano.",
                "Trabajamos con impresoras de filamento y con herramientas de modelado gratuitas y fáciles de usar, de modo que cualquiera pueda empezar en casa después del curso. Explicamos también los materiales más comunes y cuándo conviene cada uno.",
                "Aprenderás a evitar los errores más habituales que frustran a quien empieza, como piezas despegadas o mal calibradas. Al terminar tendrás varias impresiones propias y la confianza para seguir experimentando y compartir en la comunidad maker.",
            ],
            [
                new(
                    "Cómo funciona una impresora 3D",
                    "Conocemos las partes de la máquina, los tipos de impresión y los materiales más habituales como el PLA. Entendemos el recorrido completo desde el modelo digital hasta el objeto físico.",
                    "Laboratorio Ciudadano de Zaragoza (La Remolacha)",
                    "Presencial"
                ),
                new(
                    "Tu primer modelo con Tinkercad",
                    "Diseñamos una pieza sencilla combinando formas básicas en una herramienta gratuita que funciona en el navegador. No hace falta instalar nada ni tener experiencia en diseño.",
                    "Sesión online por videollamada (plataforma Jitsi)",
                    "Online"
                ),
                new(
                    "Del diseño a la impresora: el laminado",
                    "Aprendemos a preparar el modelo con un programa laminador, ajustando relleno, altura de capa y soportes. Descubrimos cómo estas decisiones afectan al tiempo y a la calidad final.",
                    "Laboratorio Ciudadano de Zaragoza (La Remolacha)",
                    "Presencial"
                ),
                new(
                    "Calibración y primeras impresiones",
                    "Nivelamos la base, cargamos el filamento y lanzamos nuestras primeras piezas. Aprendemos a diagnosticar y corregir los problemas más frecuentes cuando algo no sale bien.",
                    "Laboratorio Ciudadano de Zaragoza (La Remolacha)",
                    "Presencial"
                ),
                new(
                    "Acabados, usos y comunidad maker",
                    "Vemos técnicas de postprocesado para mejorar el aspecto de las piezas y repositorios donde descargar y compartir diseños. Cada participante se lleva a casa sus propias impresiones.",
                    "Laboratorio Ciudadano de Zaragoza (La Remolacha)",
                    "Presencial"
                ),
            ]
        ),
        new(
            "Datos abiertos para una ciudad transparente",
            "Aprende a encontrar, entender y aprovechar los datos públicos para conocer mejor tu municipio y participar en la vida ciudadana con información en la mano.",
            ["Comunidad", "Formación"],
            [
                "Las administraciones publican cada vez más información en portales de datos abiertos: presupuestos, contratos, calidad del aire, transporte o padrón. Saber acceder a esos datos es una forma poderosa de ejercer una ciudadanía activa e informada.",
                "En este itinerario aprenderemos a localizar conjuntos de datos, a interpretarlos sin miedo a las cifras y a construir visualizaciones sencillas que cuenten historias. No hace falta ser programador ni analista, solo tener curiosidad por lo que ocurre a nuestro alrededor.",
                "El taller termina con un pequeño proyecto colectivo en el que analizamos un tema de interés local a partir de datos reales del municipio. Una manera práctica de descubrir cómo la transparencia y la tecnología pueden mejorar la vida de la comunidad.",
            ],
            [
                new(
                    "Qué son los datos abiertos y por qué importan",
                    "Explicamos el concepto de datos abiertos, sus licencias y su valor para la transparencia y la participación. Vemos ejemplos de proyectos ciudadanos nacidos de datos públicos.",
                    "Centro de Cultura Antiguo Instituto, Gijón",
                    "Presencial"
                ),
                new(
                    "Explorando los portales públicos",
                    "Navegamos por portales de datos abiertos autonómicos y municipales y aprendemos a buscar y descargar conjuntos de datos. Comentamos qué formatos existen y cuáles son más útiles.",
                    "Centro de Cultura Antiguo Instituto, Gijón",
                    "Presencial"
                ),
                new(
                    "Limpiar y ordenar datos con hoja de cálculo",
                    "Con una simple hoja de cálculo aprendemos a filtrar, ordenar y depurar datos para poder trabajar con ellos. Descubrimos que buena parte del trabajo consiste en preparar bien la información.",
                    "Sesión online por videollamada (plataforma BigBlueButton)",
                    "Online"
                ),
                new(
                    "Contar historias con gráficos",
                    "Convertimos las tablas en gráficos claros que ayudan a entender la realidad de un vistazo. Reflexionamos sobre cómo una visualización puede aclarar o, mal usada, confundir.",
                    "Centro de Cultura Antiguo Instituto, Gijón",
                    "Presencial"
                ),
                new(
                    "Proyecto ciudadano con datos locales",
                    "En pequeños grupos analizamos un tema de interés del municipio y presentamos nuestras conclusiones al resto. Cerramos hablando de cómo trasladar estas ideas a la vida asociativa.",
                    "Centro de Cultura Antiguo Instituto, Gijón",
                    "Presencial"
                ),
            ]
        ),
        new(
            "Competencias digitales para personas mayores",
            "Pierde el miedo al móvil y al ordenador. Un espacio tranquilo y sin prisas para manejar con soltura las herramientas digitales del día a día.",
            ["Formación", "Comunidad"],
            [
                "La vida cotidiana es cada vez más digital: la cita médica, el banco, los trámites o mantener el contacto con la familia. Este programa acompaña a las personas mayores para que ganen autonomía y confianza con la tecnología, a su ritmo y sin sentirse juzgadas.",
                "Las sesiones son pausadas, con grupos reducidos y con voluntariado que atiende las dudas de forma individual. Repetimos lo que haga falta y practicamos con los propios dispositivos de cada participante, porque cada móvil y cada ordenador son un mundo.",
                "Más allá de aprender a usar aplicaciones, damos mucha importancia a la seguridad: reconocer estafas, proteger los datos y no caer en fraudes habituales. El objetivo es que cada persona use la tecnología con tranquilidad y sin depender siempre de otros.",
            ],
            [
                new(
                    "Manejar el móvil con confianza",
                    "Repasamos lo básico del teléfono: ajustar el tamaño de la letra, el volumen, la conexión a internet y organizar las aplicaciones. Practicamos con calma hasta que cada gesto resulte natural.",
                    "Centro de Mayores San Juan, Salamanca",
                    "Presencial"
                ),
                new(
                    "Mensajería y videollamadas con la familia",
                    "Aprendemos a enviar mensajes, fotos y notas de voz, y a hacer videollamadas para ver a los nietos aunque estén lejos. Un taller que conecta la tecnología con lo que de verdad importa.",
                    "Centro de Mayores San Juan, Salamanca",
                    "Presencial"
                ),
                new(
                    "Trámites y salud sin colas",
                    "Vemos cómo pedir cita médica, consultar recetas y realizar gestiones sencillas desde casa. Explicamos qué es la identificación digital y cómo usarla paso a paso.",
                    "Centro de Mayores San Juan, Salamanca",
                    "Presencial"
                ),
                new(
                    "Banca online con seguridad",
                    "Aprendemos a consultar el saldo y los movimientos de la cuenta con tranquilidad, entendiendo qué es seguro y qué no. Insistimos en no compartir nunca claves ni códigos con desconocidos.",
                    "Centro de Mayores San Juan, Salamanca",
                    "Presencial"
                ),
                new(
                    "Cómo reconocer estafas y bulos",
                    "Analizamos ejemplos reales de mensajes fraudulentos, llamadas sospechosas y noticias falsas. Aprendemos a desconfiar de la urgencia y a consultar antes de hacer clic o dar datos.",
                    "Sesión online por videollamada (plataforma Zoom), con apoyo telefónico",
                    "Online"
                ),
            ]
        ),
        new(
            "Scratch para peques: mis primeros programas",
            "Cuentos, juegos y animaciones que se programan arrastrando bloques. Un primer contacto con la lógica y la creatividad digital para niñas y niños.",
            ["Infantil", "Formación"],
            [
                "Programar puede ser tan natural como jugar. Con Scratch, un entorno visual de bloques de colores, las niñas y los niños crean sus propias historias y videojuegos mientras aprenden a pensar de forma ordenada y a resolver problemas paso a paso.",
                "El taller está diseñado para edades tempranas, con actividades cortas, dinámicas y llenas de imaginación. En lugar de escribir código, encajan piezas como si fuera un puzle, lo que les permite centrarse en las ideas y ver los resultados al instante.",
                "Más allá de la tecnología, trabajamos la creatividad, la paciencia y el trabajo en equipo. Cada peque termina con sus propias creaciones que puede enseñar en casa, y muchas familias descubren que aprender a programar también puede ser cosa de juego.",
            ],
            [
                new(
                    "Conocemos a los personajes de Scratch",
                    "Descubrimos el entorno, elegimos personajes y escenarios y hacemos que se muevan por primera vez. Todo mediante bloques de colores que encajamos como piezas de un juego.",
                    "Biblioteca Regional Manuel Altolaguirre, Málaga",
                    "Presencial"
                ),
                new(
                    "Que se muevan y hablen",
                    "Aprendemos a que los personajes caminen, salten y digan cosas en bocadillos. Los peques inventan pequeñas escenas y ven cómo sus ideas cobran vida en la pantalla.",
                    "Biblioteca Regional Manuel Altolaguirre, Málaga",
                    "Presencial"
                ),
                new(
                    "Bucles y repeticiones divertidas",
                    "Introducimos la idea de repetir acciones para crear bailes, dibujos y patrones. Una forma sencilla de descubrir uno de los conceptos más importantes de la programación.",
                    "Biblioteca Regional Manuel Altolaguirre, Málaga",
                    "Presencial"
                ),
                new(
                    "Creamos una historia interactiva",
                    "Cada peque construye un pequeño cuento en el que se puede elegir qué pasa a continuación. Trabajamos la narración, la secuencia y la creatividad en un mismo proyecto.",
                    "Biblioteca Regional Manuel Altolaguirre, Málaga",
                    "Presencial"
                ),
                new(
                    "Nuestro primer minijuego",
                    "Programamos un juego sencillo de atrapar objetos con puntuación y lo mostramos a las familias. Celebramos juntos todo lo aprendido durante el taller.",
                    "Biblioteca Regional Manuel Altolaguirre, Málaga",
                    "Presencial"
                ),
            ]
        ),
        new(
            "Hackathon comunitario de software libre",
            "Un fin de semana para crear en equipo soluciones digitales con impacto social, aprendiendo, colaborando y conociendo a otras personas apasionadas por la tecnología.",
            ["Comunidad", "Formación"],
            [
                "Un hackathon es un encuentro intensivo en el que equipos multidisciplinares desarrollan proyectos en pocas horas. El nuestro tiene un enfoque social y de código abierto: buscamos ideas que sirvan a asociaciones, colectivos y a la propia comunidad.",
                "No hace falta ser un programador experto para participar. Necesitamos también personas que diseñen, que comuniquen, que organicen y que aporten conocimiento del problema real a resolver. La mezcla de perfiles es precisamente lo que hace grande a un hackathon.",
                "Durante todo el evento contamos con mentoras y mentores voluntarios que acompañan a los equipos, además de comida, buen ambiente y espacio para descansar. Al final, cada grupo presenta su prototipo y todo el código queda publicado con licencia libre para quien quiera continuarlo.",
            ],
            [
                new(
                    "Presentación de retos sociales",
                    "Diversas entidades locales exponen problemas reales que necesitan soluciones tecnológicas. Los retos abarcan desde la accesibilidad hasta la gestión de voluntariado o el medio ambiente.",
                    "Escuela Técnica Superior de Ingeniería Informática, Granada",
                    "Presencial"
                ),
                new(
                    "Formación de equipos e ideación",
                    "Nos organizamos en equipos equilibrados según los perfiles y elegimos el reto que más nos motiva. Dedicamos las primeras horas a definir bien la idea antes de empezar a construir.",
                    "Escuela Técnica Superior de Ingeniería Informática, Granada",
                    "Presencial"
                ),
                new(
                    "Mentorías técnicas y de producto",
                    "Personas expertas voluntarias rotan por las mesas para resolver dudas de programación, diseño y enfoque. Un apoyo clave para desatascar problemas y afinar el proyecto.",
                    "Escuela Técnica Superior de Ingeniería Informática, Granada",
                    "Presencial"
                ),
                new(
                    "Desarrollo intensivo del prototipo",
                    "El grueso del evento: cada equipo construye una versión funcional mínima de su idea. Se trabaja en abierto y se comparte el código en un repositorio común desde el primer momento.",
                    "Escuela Técnica Superior de Ingeniería Informática, Granada",
                    "Presencial"
                ),
                new(
                    "Presentaciones finales y celebración",
                    "Cada equipo dispone de unos minutos para mostrar lo conseguido ante el resto y las entidades. Más que competir, celebramos lo aprendido y buscamos cómo dar continuidad a los proyectos.",
                    "Escuela Técnica Superior de Ingeniería Informática, Granada",
                    "Presencial"
                ),
            ]
        ),
        new(
            "Ciberseguridad para adolescentes y familias",
            "Navegar, jugar y usar redes sociales con cabeza. Un espacio de encuentro entre jóvenes y personas adultas para cuidar la vida digital en casa.",
            ["Ciberseguridad", "Infantil", "Comunidad"],
            [
                "La vida digital de la adolescencia está llena de oportunidades, pero también de riesgos que conviene conocer. Este programa reúne a jóvenes y a sus familias para hablar de privacidad, redes sociales, videojuegos y convivencia digital sin dramatismos ni prohibiciones.",
                "En lugar de dar sermones, proponemos el diálogo y ejemplos cercanos. Trabajamos en actividades que hacen pensar tanto a los más jóvenes como a las personas adultas, porque la seguridad en internet es una responsabilidad compartida en el hogar.",
                "El programa se imparte en formato online para facilitar la participación de familias de distintos puntos, con sesiones en horario de tarde. Al terminar, cada familia dispone de acuerdos y hábitos concretos para una relación más sana y segura con la tecnología.",
            ],
            [
                new(
                    "Tu huella digital: lo que dejas en internet",
                    "Descubrimos que todo lo que publicamos deja rastro y aprendemos a pensar antes de compartir. Reflexionamos sobre la reputación digital y sobre qué información conviene mantener en privado.",
                    "Sesión online por videollamada (plataforma Zoom)",
                    "Online"
                ),
                new(
                    "Privacidad en redes sociales",
                    "Revisamos juntos la configuración de privacidad de las redes más usadas y decidimos quién puede ver qué. Un taller práctico en el que cada persona ajusta sus propias cuentas.",
                    "Sesión online por videollamada (plataforma Zoom)",
                    "Online"
                ),
                new(
                    "Frente al ciberacoso",
                    "Hablamos con naturalidad del ciberacoso, cómo detectarlo y a quién acudir. Aprendemos a bloquear, denunciar y, sobre todo, a apoyar a quien lo está sufriendo.",
                    "Sesión online por videollamada (plataforma Zoom)",
                    "Online"
                ),
                new(
                    "Contraseñas y cuentas seguras en familia",
                    "Enseñamos a crear contraseñas fuertes y a activar la verificación en dos pasos en juegos y redes. Explicamos por qué no se comparten las claves ni siquiera entre amistades.",
                    "Sesión online por videollamada (plataforma Zoom)",
                    "Online"
                ),
                new(
                    "Acuerdos digitales en casa",
                    "Cerramos construyendo, entre jóvenes y familias, unos acuerdos realistas sobre tiempos, usos y confianza. La idea es pactar juntos, no imponer, para que de verdad funcionen.",
                    "Sesión online por videollamada (plataforma Zoom)",
                    "Online"
                ),
            ]
        ),
        new(
            "Inteligencia artificial y ética: debate ciudadano",
            "Un ciclo abierto de charlas y coloquios para pensar entre todas y todos qué sociedad queremos construir con la inteligencia artificial.",
            ["Inteligencia Artificial", "Comunidad"],
            [
                "La inteligencia artificial ya influye en decisiones que nos afectan: qué contenidos vemos, cómo se conceden ayudas o cómo se seleccionan candidaturas en un proceso. Este ciclo abre un espacio de reflexión ciudadana sobre sus implicaciones, más allá de la propaganda y del miedo.",
                "Cada sesión combina una intervención divulgativa breve con un coloquio abierto en el que toda la audiencia puede participar. No buscamos respuestas cerradas, sino plantear las preguntas adecuadas y escuchar puntos de vista diversos.",
                "El ciclo se emite en directo por internet y queda grabado para su consulta posterior, con el fin de llegar al mayor número de personas posible. Está pensado para cualquier persona interesada en el futuro de la tecnología y de la convivencia, sin requisitos técnicos.",
            ],
            [
                new(
                    "Qué es y qué no es la inteligencia artificial",
                    "Una charla introductoria que desmonta mitos y aclara conceptos básicos para poder debatir con conocimiento. Ponemos las bases comunes para las sesiones siguientes.",
                    "Emisión online en directo (plataforma YouTube y web de la asociación)",
                    "Online"
                ),
                new(
                    "Algoritmos y sesgos: ¿decisiones justas?",
                    "Analizamos cómo los sistemas automáticos pueden reproducir discriminaciones existentes. Debatimos sobre transparencia y responsabilidad cuando una máquina toma decisiones sobre personas.",
                    "Emisión online en directo (plataforma YouTube y web de la asociación)",
                    "Online"
                ),
                new(
                    "IA y empleo: miedos y oportunidades",
                    "Conversamos sobre cómo la automatización transforma el mundo del trabajo, con luces y sombras. Buscamos una mirada realista, lejos del catastrofismo y del optimismo ingenuo.",
                    "Emisión online en directo (plataforma YouTube y web de la asociación)",
                    "Online"
                ),
                new(
                    "Privacidad, datos y vigilancia",
                    "Reflexionamos sobre el valor de nuestros datos y sobre los límites entre la comodidad y la vigilancia. Repasamos derechos y herramientas para proteger nuestra intimidad.",
                    "Emisión online en directo (plataforma YouTube y web de la asociación)",
                    "Online"
                ),
                new(
                    "Mesa redonda: la IA que queremos",
                    "Cerramos el ciclo con una mesa que reúne distintas voces y recoge las aportaciones de la audiencia. Un espacio para imaginar juntos un uso de la tecnología al servicio de las personas.",
                    "Emisión online en directo (plataforma YouTube y web de la asociación)",
                    "Online"
                ),
            ]
        ),
        new(
            "Desarrollo web full stack para principiantes",
            "Construye tu primera aplicación web completa, desde la página que ve el usuario hasta la base de datos que guarda la información, con herramientas actuales y gratuitas.",
            ["Formación"],
            [
                "Detrás de cualquier página o aplicación web hay dos mundos que trabajan juntos: lo que vemos en el navegador y lo que ocurre en el servidor. Este curso te lleva de la mano por ambos para que entiendas cómo se construye una aplicación de principio a fin.",
                "Empezamos por las bases de la web con HTML, CSS y JavaScript, y avanzamos hacia el lado del servidor y las bases de datos. En lugar de acumular teoría, vamos construyendo un proyecto real que crece sesión a sesión.",
                "Es un curso exigente pero accesible, ideal para quien quiere orientar su carrera hacia el desarrollo o entender de verdad cómo funciona internet. Al terminar habrás desplegado tu propia aplicación en la red y tendrás una base sólida para seguir especializándote.",
            ],
            [
                new(
                    "Maquetación con HTML y CSS",
                    "Aprendemos a estructurar el contenido de una página y a darle estilo para que se vea bien en cualquier pantalla. Construimos la interfaz de nuestro proyecto desde cero.",
                    "Espacio de Coworking A Xanela, A Coruña",
                    "Presencial"
                ),
                new(
                    "Interactividad con JavaScript",
                    "Añadimos comportamiento a la página para que responda a las acciones de la persona usuaria. Trabajamos con eventos, formularios y actualización dinámica del contenido.",
                    "Espacio de Coworking A Xanela, A Coruña",
                    "Presencial"
                ),
                new(
                    "El servidor con Node.js",
                    "Damos el salto al lado del servidor para crear una interfaz de programación que responda a las peticiones. Entendemos cómo se comunican el navegador y el servidor.",
                    "Sesión online por videollamada (plataforma Jitsi)",
                    "Online"
                ),
                new(
                    "Guardar datos en una base de datos",
                    "Conectamos nuestra aplicación a una base de datos para almacenar y recuperar información de forma permanente. Aprendemos las operaciones básicas de crear, leer, actualizar y borrar.",
                    "Espacio de Coworking A Xanela, A Coruña",
                    "Presencial"
                ),
                new(
                    "Publicar tu aplicación en internet",
                    "Desplegamos el proyecto en un servicio gratuito para que sea accesible desde cualquier lugar. Repasamos conceptos de dominios, seguridad básica y mantenimiento.",
                    "Sesión online por videollamada (plataforma Jitsi)",
                    "Online"
                ),
            ]
        ),
        new(
            "Liga de robótica educativa para jóvenes",
            "Diseña, construye y programa un robot para superar retos por misiones. Trabajo en equipo, ingenio y mucha diversión en una competición amistosa entre centros.",
            ["Robótica", "Infantil"],
            [
                "Las competiciones de robótica son una forma extraordinaria de aprender ciencia, tecnología e ingeniería mientras se trabaja en equipo. En esta liga, grupos de jóvenes preparan un robot capaz de resolver una serie de misiones sobre un tablero temático.",
                "El reto no es solo técnico. Cada equipo debe organizarse, repartir tareas, documentar su trabajo y desarrollar un pequeño proyecto de investigación relacionado con la temática de la temporada. Se aprende tanto de programar como de colaborar y comunicar.",
                "Acompañamos a los equipos durante toda la preparación con sesiones de formación y mentorías, y culminamos con un torneo amistoso entre centros de la región. Lo importante no es ganar, sino todo lo que se aprende por el camino y el espíritu de comunidad que se genera.",
            ],
            [
                new(
                    "Presentación del reto de la temporada",
                    "Conocemos el tablero, las misiones y las reglas de la temporada. Cada equipo empieza a planificar su estrategia y a repartir responsabilidades entre sus integrantes.",
                    "Centro Tecnológico Juvenil, Murcia",
                    "Presencial"
                ),
                new(
                    "Diseño y construcción del robot",
                    "Montamos la estructura del robot y experimentamos con distintos accesorios y ruedas. Aprendemos que un buen diseño mecánico es la mitad del éxito en cada misión.",
                    "Centro Tecnológico Juvenil, Murcia",
                    "Presencial"
                ),
                new(
                    "Programación de las misiones",
                    "Traducimos la estrategia en código para que el robot recorra el tablero y complete los objetivos. Probamos, medimos y ajustamos una y otra vez hasta afinar cada movimiento.",
                    "Centro Tecnológico Juvenil, Murcia",
                    "Presencial"
                ),
                new(
                    "Proyecto de investigación en equipo",
                    "Cada grupo investiga un problema real relacionado con la temática y propone una solución. Preparamos una breve exposición para presentarla ante el jurado y el público.",
                    "Sesión online por videollamada (plataforma Google Meet) para tutorías",
                    "Online"
                ),
                new(
                    "Torneo amistoso entre centros",
                    "Llega el gran día: los equipos ponen a prueba sus robots en varias rondas ante otros centros. Celebramos el esfuerzo, el trabajo en equipo y el juego limpio por encima del resultado.",
                    "Pabellón Municipal de Deportes, Murcia",
                    "Presencial"
                ),
            ]
        ),
        new(
            "Diseño 3D e impresión para prototipado",
            "Da el salto del diseño básico al modelado profesional. Aprende a crear piezas funcionales, con medidas precisas, listas para fabricar y ensamblar.",
            ["Impresión 3D", "Formación"],
            [
                "Cuando se quiere fabricar una pieza que encaje, resista o cumpla una función concreta, hace falta ir más allá del modelado sencillo. Este curso intermedio te introduce en el diseño paramétrico, la disciplina que usan ingenieras e ingenieros para prototipar de verdad.",
                "Trabajamos con herramientas de modelado profesional pero accesibles, aprendiendo a definir medidas exactas, tolerancias y ensamblajes. Veremos cómo pensar una pieza no solo por su forma, sino por cómo se va a imprimir y para qué se va a usar.",
                "El taller está orientado a personas makers, emprendedoras y estudiantes que ya conocen lo básico de la impresión 3D y quieren dar un paso más. Al finalizar habrás diseñado e impreso un prototipo funcional propio, con criterio técnico y buenos acabados.",
            ],
            [
                new(
                    "Introducción al modelado paramétrico",
                    "Descubrimos la lógica del diseño por parámetros, donde cada medida se puede modificar y todo se actualiza. Creamos nuestras primeras piezas controlando dimensiones con precisión.",
                    "Fab Lab de Donostia / San Sebastián",
                    "Presencial"
                ),
                new(
                    "Tolerancias y ensamblajes",
                    "Aprendemos a diseñar piezas que encajan entre sí dejando los huecos adecuados. Practicamos con uniones, encajes a presión y mecanismos sencillos que deben moverse.",
                    "Fab Lab de Donostia / San Sebastián",
                    "Presencial"
                ),
                new(
                    "Elegir bien el material",
                    "Comparamos las propiedades de los materiales más comunes y cuándo conviene cada uno según la resistencia, la flexibilidad o la temperatura. Elegimos el filamento adecuado para nuestro prototipo.",
                    "Sesión online por videollamada (plataforma Jitsi)",
                    "Online"
                ),
                new(
                    "Optimizar el diseño para imprimir",
                    "Adaptamos las piezas para que se impriman mejor: orientación, soportes, grosores mínimos y ahorro de material. Un buen diseño evita fallos y reduce el tiempo de impresión.",
                    "Fab Lab de Donostia / San Sebastián",
                    "Presencial"
                ),
                new(
                    "Impresión y postprocesado del prototipo",
                    "Imprimimos nuestro prototipo funcional y lo pulimos con técnicas de acabado. Evaluamos el resultado, detectamos mejoras y planificamos una posible segunda versión.",
                    "Fab Lab de Donostia / San Sebastián",
                    "Presencial"
                ),
            ]
        ),
        new(
            "Analítica de datos con Python y pandas",
            "Convierte montañas de datos en decisiones. Aprende a limpiar, analizar y visualizar información real con las herramientas más usadas del sector.",
            ["Formación", "Inteligencia Artificial"],
            [
                "Cada organización, empresa o proyecto genera datos, pero pocos saben extraer valor de ellos. Este curso te enseña el flujo completo del análisis de datos usando Python y la librería pandas, la combinación más extendida entre analistas de todo el mundo.",
                "Partimos de conjuntos de datos reales y aprendemos a limpiarlos, a explorarlos y a formular preguntas que se puedan responder con evidencia. Trabajamos con ejemplos cercanos para que veas cómo aplicar lo aprendido a tu propio ámbito.",
                "El curso es una excelente base tanto para quien quiere orientarse hacia el análisis de datos como para quien busca entender los cimientos sobre los que se construye la inteligencia artificial. Al terminar habrás elaborado tu propio informe a partir de datos en bruto.",
            ],
            [
                new(
                    "Primeros pasos con pandas",
                    "Aprendemos a cargar datos desde archivos y a manejar tablas con filas y columnas. Descubrimos las operaciones básicas para seleccionar y filtrar la información que nos interesa.",
                    "Distrito Digital, Alicante",
                    "Presencial"
                ),
                new(
                    "Limpieza y preparación de datos",
                    "Nos enfrentamos a datos desordenados, con valores ausentes o duplicados, y aprendemos a dejarlos listos para analizar. Descubrimos que esta fase suele ser la más larga y la más importante.",
                    "Distrito Digital, Alicante",
                    "Presencial"
                ),
                new(
                    "Análisis exploratorio",
                    "Agrupamos, resumimos y cruzamos datos para descubrir patrones y responder preguntas. Aprendemos a calcular estadísticas básicas que nos ayudan a entender el conjunto.",
                    "Sesión online por videollamada (plataforma Zoom)",
                    "Online"
                ),
                new(
                    "Visualización de resultados",
                    "Creamos gráficos claros con matplotlib para comunicar los hallazgos de forma comprensible. Reflexionamos sobre cómo elegir el tipo de gráfico adecuado para cada mensaje.",
                    "Distrito Digital, Alicante",
                    "Presencial"
                ),
                new(
                    "Tu primer informe de datos",
                    "Reunimos todo el proceso en un informe reproducible que cuenta una historia con los datos. Cada participante presenta sus conclusiones y recibe comentarios del grupo.",
                    "Sesión online por videollamada (plataforma Zoom)",
                    "Online"
                ),
            ]
        ),
        new(
            "Game jam infantil y juvenil de fin de semana",
            "Un maratón creativo para inventar videojuegos en equipo a partir de una palabra sorpresa. Imaginación, colaboración y muchas risas garantizadas.",
            ["Videojuegos", "Infantil"],
            [
                "Una game jam es un reto en el que, en muy poco tiempo, hay que crear un videojuego partiendo de una idea o palabra común. En esta versión pensada para niñas, niños y adolescentes, lo importante no es la técnica, sino atreverse a crear y trabajar en equipo.",
                "Combinamos herramientas visuales y muy sencillas con dinámicas de creatividad para que cualquiera, tenga la edad que tenga, pueda aportar. Unos programan, otros dibujan, otros inventan la historia o los sonidos: en un juego cabe el talento de todos.",
                "El acompañamiento de personas voluntarias garantiza que nadie se quede atascado y que el ambiente sea siempre de apoyo, no de competición. Terminamos con una muestra en la que las familias juegan a las creaciones y celebramos juntos lo conseguido en tan poco tiempo.",
            ],
            [
                new(
                    "La palabra sorpresa y las ideas",
                    "Revelamos la palabra que inspirará todos los juegos y hacemos una lluvia de ideas por equipos. Aprendemos a elegir una idea sencilla que se pueda terminar en el tiempo disponible.",
                    "Palacio de Congresos y Exposiciones, Córdoba",
                    "Presencial"
                ),
                new(
                    "Diseñamos personajes y escenarios",
                    "Dibujamos y damos forma a los protagonistas y a los mundos del juego. Quienes prefieren la parte artística tienen aquí su momento de brillar y aportar al equipo.",
                    "Palacio de Congresos y Exposiciones, Córdoba",
                    "Presencial"
                ),
                new(
                    "Construimos los niveles",
                    "Montamos las pantallas y programamos las reglas básicas del juego con herramientas visuales. Probamos una y otra vez para que sea divertido y no demasiado difícil.",
                    "Palacio de Congresos y Exposiciones, Córdoba",
                    "Presencial"
                ),
                new(
                    "Sonidos y últimos retoques",
                    "Añadimos efectos de sonido y música y pulimos los detalles finales. Cada equipo prepara su juego para que otras personas puedan jugarlo sin problemas.",
                    "Palacio de Congresos y Exposiciones, Córdoba",
                    "Presencial"
                ),
                new(
                    "Muestra final para las familias",
                    "Abrimos las puertas para que familias y amistades prueben todos los juegos creados. Cada equipo cuenta cómo lo ha hecho y celebramos el esfuerzo de todo el fin de semana.",
                    "Palacio de Congresos y Exposiciones, Córdoba",
                    "Presencial"
                ),
            ]
        ),
        new(
            "Introducción a Linux y al software libre",
            "Descubre un sistema operativo libre, gratuito y respetuoso con tu privacidad. Da tus primeros pasos con la terminal y la filosofía del código abierto.",
            ["Formación", "Comunidad"],
            [
                "Existe un mundo de software libre y gratuito que mucha gente no conoce, encabezado por el sistema operativo GNU/Linux. Este curso es una invitación a descubrirlo, entender por qué existe y comprobar que es una alternativa real para el día a día.",
                "Aprenderás qué son las distribuciones, cómo probar Linux sin riesgo en tu propio ordenador y a manejarte con lo esencial de la terminal, esa pantalla de comandos que tanto respeto impone al principio y que resulta más sencilla de lo que parece.",
                "Más allá de lo técnico, hablaremos de la filosofía del software libre: la colaboración, las licencias abiertas y la independencia tecnológica. Un curso ideal para personas curiosas que quieren tomar el control de sus herramientas digitales y formar parte de una gran comunidad.",
            ],
            [
                new(
                    "Qué es Linux y por qué usarlo",
                    "Explicamos el origen de GNU/Linux, qué es una distribución y qué ventajas ofrece frente a otros sistemas. Vemos ejemplos de dónde se usa Linux, muchas veces sin que lo sepamos.",
                    "Centro Insular de Cultura, Santa Cruz de Tenerife",
                    "Presencial"
                ),
                new(
                    "Probar Linux sin instalar nada",
                    "Aprendemos a arrancar el sistema desde un USB para probarlo sin tocar nuestro ordenador. Una forma segura de experimentar y decidir si queremos dar el paso.",
                    "Centro Insular de Cultura, Santa Cruz de Tenerife",
                    "Presencial"
                ),
                new(
                    "Perder el miedo a la terminal",
                    "Damos nuestros primeros comandos para movernos por carpetas, crear archivos y buscar información. Descubrimos que la terminal es una herramienta potente y no un obstáculo.",
                    "Sesión online por videollamada (plataforma Jitsi)",
                    "Online"
                ),
                new(
                    "Instalar y actualizar programas",
                    "Aprendemos a gestionar el software mediante los centros de aplicaciones y los repositorios. Vemos lo cómodo y seguro que resulta mantener el sistema al día.",
                    "Centro Insular de Cultura, Santa Cruz de Tenerife",
                    "Presencial"
                ),
                new(
                    "La comunidad y las licencias libres",
                    "Conocemos cómo se construye el software libre de forma colaborativa y qué significan sus licencias. Descubrimos cómo cualquiera puede participar y aportar a los proyectos.",
                    "Sesión online por videollamada (plataforma Jitsi)",
                    "Online"
                ),
            ]
        ),
        new(
            "Hacking ético: introducción a la seguridad ofensiva",
            "Aprende a pensar como un atacante para defender mejor. Fundamentos del pentesting en un laboratorio controlado, legal y siempre con permiso.",
            ["Ciberseguridad", "Formación"],
            [
                "Para proteger un sistema hay que entender cómo se ataca. El hacking ético consiste precisamente en eso: buscar vulnerabilidades de forma legal y con autorización, para corregirlas antes de que lo haga alguien con malas intenciones.",
                "Este curso introduce la metodología del pentesting o test de intrusión, siempre dentro de un entorno de laboratorio controlado y seguro. Insistimos desde el primer minuto en la ética y en la legalidad: estas técnicas solo se aplican con permiso expreso.",
                "Está dirigido a personas con conocimientos básicos de informática y redes que quieren orientarse hacia la ciberseguridad, una de las áreas con más demanda profesional. Al terminar comprenderás las fases de un ataque y cómo elaborar un informe de seguridad útil.",
            ],
            [
                new(
                    "Ética, legalidad y metodología",
                    "Establecemos el marco ético y legal imprescindible antes de tocar nada. Presentamos las fases de un test de intrusión y montamos nuestro laboratorio virtual de prácticas.",
                    "Sesión online por videollamada (plataforma BigBlueButton)",
                    "Online"
                ),
                new(
                    "Reconocimiento y recogida de información",
                    "Aprendemos a reunir información pública sobre un objetivo, la primera fase de cualquier auditoría. Vemos cuánto se puede averiguar de una organización sin acceder a sus sistemas.",
                    "Sesión online por videollamada (plataforma BigBlueButton)",
                    "Online"
                ),
                new(
                    "Vulnerabilidades web más comunes",
                    "Estudiamos los fallos habituales de las aplicaciones web recogidos en las guías de referencia del sector. Entendemos cómo se producen y por qué son tan frecuentes.",
                    "Sesión online por videollamada (plataforma BigBlueButton)",
                    "Online"
                ),
                new(
                    "Explotación en un laboratorio controlado",
                    "En máquinas preparadas para practicar, comprobamos cómo se aprovechan algunas vulnerabilidades. Todo ocurre en un entorno aislado y diseñado expresamente para aprender.",
                    "Sesión online por videollamada (plataforma BigBlueButton)",
                    "Online"
                ),
                new(
                    "El informe de seguridad",
                    "Aprendemos a documentar los hallazgos, valorar su gravedad y proponer soluciones. Un buen informe es el verdadero producto de una auditoría y lo que permite mejorar la defensa.",
                    "Sesión online por videollamada (plataforma BigBlueButton)",
                    "Online"
                ),
            ]
        ),
        new(
            "Cultura maker: electrónica y creación colaborativa",
            "Suelda, conecta y fabrica tus propios inventos. Un espacio abierto donde la electrónica, la programación y la impresión 3D se unen para dar forma a tus ideas.",
            ["Robótica", "Impresión 3D", "Comunidad"],
            [
                "El movimiento maker reivindica el placer de fabricar con las propias manos, combinando electrónica, programación y fabricación digital. Este programa abre un espacio comunitario donde aprender haciendo, compartir conocimiento y dar vida a proyectos personales o colectivos.",
                "Empezamos por las bases de la electrónica y la soldadura, avanzamos hacia los microcontroladores y terminamos integrando piezas impresas en 3D para construir inventos completos. No importa tu nivel: aquí se aprende de las demás personas tanto como de las mentoras.",
                "Más que un curso cerrado, es una invitación a formar parte de una comunidad activa que se reúne periódicamente. Al terminar tendrás las bases para seguir creando y un grupo con el que compartir dudas, materiales y muchas ganas de inventar.",
            ],
            [
                new(
                    "Iníciate en la soldadura",
                    "Aprendemos a soldar de forma segura y limpia, una habilidad básica del mundo maker. Practicamos con un pequeño montaje que nos llevamos a casa funcionando.",
                    "Espacio Maker de la Escuela de Arte y Superior de Diseño, Pamplona",
                    "Presencial"
                ),
                new(
                    "Circuitos electrónicos básicos",
                    "Descubrimos los componentes esenciales y cómo combinarlos en circuitos sencillos que hacen cosas. Entendemos conceptos como la corriente, el voltaje y la resistencia sin agobios.",
                    "Espacio Maker de la Escuela de Arte y Superior de Diseño, Pamplona",
                    "Presencial"
                ),
                new(
                    "Microcontroladores: dar inteligencia al circuito",
                    "Programamos un pequeño microcontrolador para que nuestro circuito reaccione y tome decisiones. Damos el salto de la electrónica fija a los proyectos programables.",
                    "Espacio Maker de la Escuela de Arte y Superior de Diseño, Pamplona",
                    "Presencial"
                ),
                new(
                    "Integrar piezas impresas en 3D",
                    "Diseñamos e imprimimos carcasas y soportes para nuestros montajes electrónicos. Comprobamos cómo la impresión 3D convierte un prototipo suelto en un producto acabado.",
                    "Sesión online por videollamada (plataforma Jitsi) para diseño",
                    "Online"
                ),
                new(
                    "Proyecto maker colaborativo",
                    "En pequeños grupos ideamos y construimos un invento que combine todo lo aprendido. Compartimos los resultados con la comunidad y dejamos la puerta abierta a seguir mejorándolos.",
                    "Espacio Maker de la Escuela de Arte y Superior de Diseño, Pamplona",
                    "Presencial"
                ),
            ]
        ),
    ];

    private static readonly ContentSeed[] DemoNews =
    [
        new(
            "Abrimos la matrícula del curso de introducción a la programación para jóvenes 2026",
            "Doce semanas de clases gratuitas los sábados por la mañana para chicas y chicos de 12 a 17 años que quieran dar sus primeros pasos con el código.",
            [
                "Ya está disponible el formulario de inscripción para la nueva edición de nuestro curso de introducción a la programación dirigido a adolescentes. A lo largo de doce sesiones trabajaremos con Scratch y daremos el salto a Python, siempre partiendo de proyectos que el alumnado elige y construye poco a poco. No hace falta ninguna experiencia previa ni traer ordenador, ya que el aula está equipada con equipos para todo el grupo.",
                "Las plazas son limitadas a veinte participantes por grupo para garantizar un acompañamiento cercano, y este año abrimos un segundo grupo en horario de tarde por la alta demanda de la edición anterior. La actividad es completamente gratuita gracias al apoyo de las entidades colaboradoras y al trabajo del voluntariado de la asociación.",
                "El plazo de inscripción permanecerá abierto hasta el 20 de febrero o hasta cubrir las plazas. Si hay más solicitudes que sitios disponibles, se realizará un sorteo público y se creará una lista de espera. Cualquier duda puede resolverse escribiendo a la secretaría o pasándose por el local en horario de tardes.",
            ]
        ),
        new(
            "El taller de robótica educativa de Código Activo llega a tres institutos de la comarca",
            "Durante el segundo trimestre, más de ciento veinte estudiantes de secundaria montarán y programarán sus propios robots dentro del horario lectivo.",
            [
                "Tras el proyecto piloto del curso pasado, ampliamos nuestro programa de robótica educativa a tres centros de secundaria de la comarca. La iniciativa se desarrolla en colaboración con el profesorado de Tecnología y se integra en las horas de clase, de manera que ningún alumno queda fuera por motivos de horario o de recursos familiares.",
                "El planteamiento es sencillo pero exigente: cada equipo recibe una placa controladora, sensores y motores, y a partir de un reto real (un robot que evita obstáculos, una barrera automática, un pequeño invernadero) debe diseñar, montar y programar su solución. Nuestro voluntariado acompaña las sesiones, pero son los propios estudiantes quienes toman las decisiones y aprenden de sus errores.",
                "Queremos agradecer a los equipos directivos de los centros su confianza y su implicación logística. El material se cede en préstamo y rota entre los grupos, un modelo que nos permite llegar a más personas con un presupuesto ajustado y que esperamos poder extender a más institutos el próximo curso.",
            ]
        ),
        new(
            "Resumen de la Jornada de Ciberseguridad para familias: sala llena y muchas preguntas",
            "Cerca de ochenta personas asistieron a la sesión práctica sobre contraseñas, estafas por mensajería y protección de los menores en la red.",
            [
                "El pasado sábado celebramos nuestra primera Jornada de Ciberseguridad para familias, y la respuesta superó todas nuestras expectativas. La sala se llenó de madres, padres, abuelas y también de adolescentes que acudieron con sus dudas reales sobre el uso cotidiano del móvil y del ordenador en casa.",
                "Durante dos horas trabajamos casos concretos: cómo reconocer un mensaje fraudulento que suplanta a un banco, por qué conviene usar un gestor de contraseñas, cómo configurar la privacidad en las redes sociales y qué hacer si un menor recibe contenido inapropiado. Huimos del alarmismo y apostamos por hábitos sencillos que cualquiera puede aplicar desde el mismo día.",
                "El interés demostrado nos anima a repetir el formato de forma periódica y a preparar una sesión específica sobre protección de personas mayores, uno de los colectivos más afectados por las estafas digitales. Agradecemos a quienes vinieron sus preguntas, que son la mejor guía para orientar nuestros próximos talleres.",
            ]
        ),
        new(
            "Firmamos un convenio de colaboración con la biblioteca municipal para acercar la tecnología al barrio",
            "El acuerdo nos permitirá usar la sala multimedia para actividades gratuitas y crear un punto de asesoramiento digital abierto a toda la vecindad.",
            [
                "Código Activo y la biblioteca municipal hemos firmado un convenio de colaboración que consolida una relación que llevaba tiempo funcionando de manera informal. Gracias a este acuerdo, dispondremos de la sala multimedia en horario de tarde para impartir talleres, y la biblioteca reforzará su papel como espacio de encuentro y de aprendizaje para el barrio.",
                "Una de las novedades más importantes es la puesta en marcha de un punto de asesoramiento digital, atendido por nuestro voluntariado una tarde a la semana. Allí cualquier persona podrá acudir sin cita para resolver dudas prácticas: pedir cita médica por internet, usar el certificado digital, instalar una aplicación o entender un trámite de la administración electrónica.",
                "Creemos que la alfabetización digital no debe depender del poder adquisitivo de cada familia, y las bibliotecas son aliadas naturales de esa idea. Este convenio es un primer paso y esperamos que sirva de ejemplo para tejer acuerdos parecidos con otros equipamientos públicos de la zona.",
            ]
        ),
        new(
            "Nuestro equipo de jóvenes obtiene un tercer puesto en la competición regional de programación",
            "Cuatro participantes de nuestro club de código resolvieron durante cinco horas una batería de retos algorítmicos frente a equipos de toda la región.",
            [
                "Tenemos una gran noticia que compartir: el equipo de jóvenes que representó a Código Activo consiguió el tercer puesto en la competición regional de programación celebrada el fin de semana. Durante cinco horas, las cuatro personas del equipo se enfrentaron a una decena de problemas algorítmicos de dificultad creciente, trabajando en equipo y bajo presión.",
                "Más allá del resultado, que nos llena de orgullo, lo que más valoramos es el proceso. Estos meses de preparación semanal han servido para que el grupo aprenda a leer con calma un enunciado, a repartirse el trabajo, a probar sus soluciones y a no rendirse cuando algo no funciona a la primera. Esas competencias van mucho más allá de cualquier lenguaje de programación.",
                "Queremos felicitar a las personas participantes y al voluntariado que las ha acompañado en los entrenamientos. El club de código sigue abierto a nueva gente con ganas de aprender resolviendo problemas, y no hace falta querer competir para formar parte de él: basta con tener curiosidad.",
            ]
        ),
        new(
            "Presentamos el proyecto de datos abiertos para visualizar el gasto público del municipio",
            "Un grupo de personas voluntarias ha construido un panel interactivo que traduce los presupuestos municipales a gráficos comprensibles para cualquier vecino.",
            [
                "En Código Activo creemos que los datos públicos solo son útiles si la ciudadanía puede entenderlos. Por eso presentamos un proyecto de datos abiertos en el que un grupo de personas voluntarias ha recogido, limpiado y organizado los presupuestos municipales de los últimos años para mostrarlos en un panel interactivo y accesible.",
                "La herramienta permite ver en qué se gasta el dinero público, comparar partidas entre distintos ejercicios y descargar los datos en formatos abiertos para quien quiera reutilizarlos. Todo el código es libre y está publicado en nuestro repositorio, de modo que cualquier persona con conocimientos técnicos puede revisarlo, corregirlo o adaptarlo a otro municipio.",
                "Este trabajo no pretende sustituir a nadie, sino tender un puente entre la información oficial y las personas de a pie. Invitamos a periodistas, asociaciones vecinales y a la propia administración a usar el panel y a enviarnos sus sugerencias para seguir mejorándolo entre todas.",
            ]
        ),
        new(
            "Convocatoria de personas voluntarias para el curso 2026: buscamos ganas, no currículums",
            "Abrimos un proceso de incorporación de nuevo voluntariado para talleres, mentorías y tareas de organización, con formación inicial y acompañamiento continuo.",
            [
                "El motor de Código Activo son las personas que dedican parte de su tiempo libre a compartir lo que saben. De cara al nuevo curso abrimos una convocatoria de voluntariado en la que buscamos gente con ganas de enseñar, de acompañar y de organizar, más allá de su formación previa o de su experiencia técnica.",
                "Hay tareas para perfiles muy distintos: quien domina un lenguaje de programación puede dar apoyo en los talleres, pero también necesitamos personas que ayuden con la comunicación, la coordinación de grupos, la logística de los eventos o la atención en el punto de asesoramiento digital. Ofrecemos una formación inicial y, sobre todo, no dejamos a nadie solo ante sus primeras sesiones.",
                "Si alguna vez has pensado que te gustaría devolver a la comunidad algo de lo que has aprendido, esta es tu oportunidad. Puedes escribirnos contando qué te gustaría aportar y cuánto tiempo puedes dedicar, y te invitaremos a una reunión informal para conocernos sin ningún compromiso.",
            ]
        ),
        new(
            "Estrenamos un taller de impresión 3D aplicada a productos de apoyo para personas con discapacidad",
            "En colaboración con una asociación local, diseñaremos e imprimiremos pequeñas ayudas técnicas adaptadas a necesidades reales de la comunidad.",
            [
                "Ponemos en marcha un nuevo taller de impresión 3D con un enfoque muy concreto: fabricar productos de apoyo que faciliten el día a día de personas con discapacidad. La iniciativa nace de la colaboración con una asociación local, que nos traslada necesidades reales para las que a veces no existe una solución comercial asequible.",
                "El taller combina diseño e impresión: aprenderemos a modelar piezas sencillas, a elegir los materiales adecuados y a iterar sobre los prototipos hasta que resulten cómodos y seguros. Hablamos de objetos como engrosadores para cubiertos, sujeciones para bastones, adaptadores para abrir tapones o soportes para el móvil, siempre partiendo de la experiencia de quien los va a usar.",
                "Todos los diseños que salgan del taller se publicarán con licencia libre para que cualquier persona pueda descargarlos e imprimirlos. Nos parece esencial que este conocimiento circule y que ningún diseño útil quede encerrado. Las inscripciones ya están abiertas y las plazas son reducidas por el uso de las impresoras.",
            ]
        ),
        new(
            "Ampliamos el programa de competencias digitales para personas mayores tras la buena acogida del piloto",
            "Nuevas sesiones en grupos reducidos y a ritmo tranquilo para aprender a usar el móvil, la banca en línea y los trámites con la administración sin agobios.",
            [
                "Después del éxito del proyecto piloto del otoño pasado, ampliamos el programa de competencias digitales dirigido a personas mayores. La experiencia nos ha enseñado que la clave está en ir despacio, en grupos pequeños y sin dar nada por sabido, así que mantenemos esa filosofía y sumamos más horarios y más plazas.",
                "Los contenidos parten siempre de lo cotidiano: enviar fotos a la familia, hacer una videollamada, pedir cita en el centro de salud, usar la banca en línea con seguridad o reconocer un intento de estafa. Cada persona avanza a su ritmo y puede repetir lo que necesite, porque aquí no hay exámenes ni prisas, solo la satisfacción de ganar autonomía.",
                "Sabemos que la brecha digital afecta especialmente a las personas mayores y que muchas veces genera frustración y dependencia. Con este programa queremos que nadie se sienta excluido por no manejar una pantalla. Quien esté interesado, o quien conozca a alguien que pueda beneficiarse, puede inscribirse en secretaría o llamarnos por teléfono.",
            ]
        ),
        new(
            "Organizamos una jornada de puertas abiertas sobre inteligencia artificial y videojuegos",
            "Una tarde para probar demostraciones, entender cómo funcionan estas tecnologías y debatir con calma sobre sus oportunidades y sus riesgos.",
            [
                "Os invitamos a nuestra jornada de puertas abiertas dedicada a dos temas que despiertan mucha curiosidad: la inteligencia artificial y los videojuegos. Será una tarde abierta a todos los públicos, con demostraciones para trastear, explicaciones sencillas y espacio para preguntar sin miedo a quedar en evidencia.",
                "En la zona de videojuegos podrás ver cómo se crea un pequeño juego desde cero y probar proyectos hechos por nuestro grupo de desarrollo. En la parte de inteligencia artificial mostraremos ejemplos prácticos de qué pueden y qué no pueden hacer estas herramientas, alejándonos tanto del entusiasmo ciego como del miedo infundado.",
                "Reservaremos un rato final para un coloquio en el que hablar con tranquilidad de las cuestiones que nos afectan a todos: el uso responsable, la privacidad, el impacto en el empleo o el pensamiento crítico frente a los contenidos generados de forma automática. La entrada es libre y no hace falta inscribirse; solo venir con ganas de curiosear y de conversar.",
            ]
        ),
    ];

    private static readonly ContentSeed[] DemoResources =
    [
        new(
            "Primeros pasos con Python: guía para empezar a programar desde cero",
            "Una introducción pausada al lenguaje más usado en las aulas, pensada para quien nunca ha escrito una línea de código.",
            [
                "Esta guía recorre lo esencial para arrancar con Python sin agobios: cómo instalar el intérprete, elegir un editor sencillo y escribir tu primer programa que salude por pantalla. Explicamos variables, tipos de datos, condicionales y bucles con ejemplos cotidianos, para que cada concepto tenga sentido antes de pasar al siguiente.",
                "Cada apartado incluye pequeños retos con su solución comentada, de forma que puedas comprobar lo aprendido a tu ritmo. No hace falta ordenador potente ni conocimientos previos de matemáticas; basta con curiosidad y ganas de equivocarse, porque del error también se aprende.",
                "Al terminar tendrás una base sólida para seguir con proyectos reales como una calculadora, un pequeño juego de adivinar números o un organizador de tareas. Si te atascas, recuerda que en nuestros talleres presenciales resolvemos dudas y practicamos juntos.",
            ]
        ),
        new(
            "Kit de robótica educativa: monta tu primer robot con material accesible",
            "Lista de componentes, esquema de conexiones y retos progresivos para iniciarse en la robótica sin gastar mucho.",
            [
                "Reunimos en un solo documento todo lo necesario para construir un robot seguidor de línea con una placa microcontroladora asequible, un par de motores, sensores básicos y una protoboard. Detallamos qué comprar, precios orientativos y alternativas más económicas para que ninguna familia se quede fuera por presupuesto.",
                "El kit se acompaña de un esquema de conexiones claro y de un código comentado paso a paso, pensado para que jóvenes y adultos entiendan por qué el robot hace lo que hace. Incluimos consejos de seguridad al manipular baterías y cables, y trucos para depurar cuando algo no funciona a la primera.",
                "Proponemos una serie de retos crecientes: que el robot avance, que gire, que evite obstáculos y, finalmente, que siga una línea negra sobre fondo blanco. Es un material ideal para clubes escolares, bibliotecas y actividades familiares de fin de semana.",
            ]
        ),
        new(
            "Plantilla de proyecto web: estructura base para tu primera página",
            "Un esqueleto ordenado de HTML, CSS y JavaScript listo para personalizar, con buenas prácticas incluidas.",
            [
                "Esta plantilla ofrece la estructura mínima recomendable para cualquier proyecto web: separación de archivos, una hoja de estilos organizada y un punto de entrada de JavaScript preparado para crecer. Viene comentada solo donde ayuda a entender la organización, para que puedas borrar lo que no uses y quedarte con lo esencial.",
                "Hemos cuidado aspectos que suelen olvidarse al empezar: etiquetas semánticas, textos alternativos en las imágenes, contraste de colores adecuado y un diseño que se adapta al móvil. Así tu primera web nace ya accesible y comprensible para el mayor número de personas posible.",
                "Incluimos un breve checklist final para revisar antes de publicar: comprobar enlaces, validar el código y medir la velocidad de carga. Es un buen trampolín para quien quiera pasar de los ejercicios sueltos a un proyecto propio que enseñar con orgullo.",
            ]
        ),
        new(
            "Ciberseguridad en casa: guía práctica para familias y personas cuidadoras",
            "Medidas sencillas para proteger dispositivos, cuentas y datos personales en el día a día del hogar.",
            [
                "Explicamos con lenguaje llano cómo crear contraseñas robustas y distintas para cada servicio, por qué conviene un gestor de contraseñas y cómo activar la verificación en dos pasos en las cuentas importantes. Son gestos que llevan pocos minutos y evitan la mayoría de los sustos.",
                "Dedicamos un apartado a reconocer intentos de engaño: correos y mensajes que suplantan a bancos o administraciones, enlaces sospechosos y llamadas que meten prisa. Damos señales de alarma concretas y un consejo de oro: ante la duda, parar y verificar por un canal oficial antes de hacer clic o facilitar datos.",
                "Cerramos con recomendaciones para acompañar a menores y a personas mayores: configurar el control parental sin invadir la intimidad, mantener los dispositivos actualizados y hablar en familia sobre lo que se comparte en internet. La seguridad es un hábito compartido, no una tarea de una sola persona.",
            ]
        ),
        new(
            "Caja de herramientas: software libre y gratuito para aprender a programar",
            "Selección comentada de editores, entornos y recursos sin coste para montar tu espacio de trabajo.",
            [
                "Hemos reunido una lista de herramientas gratuitas y, en su mayoría, de código abierto, agrupadas por para qué sirven: editar código, controlar versiones, diseñar interfaces, probar bases de datos y practicar en el navegador sin instalar nada. De cada una explicamos en una frase por qué la recomendamos y para quién encaja mejor.",
                "Priorizamos opciones ligeras, en español siempre que es posible y con comunidades activas donde pedir ayuda. Evitamos programas que exigen ordenadores potentes o suscripciones, porque queremos que aprender no dependa del bolsillo de cada familia.",
                "Incluimos además plataformas para practicar con retos, documentación oficial de referencia y foros hispanohablantes de confianza. Es un punto de partida para montar tu propio entorno de trabajo y dejar de depender de tutoriales sueltos de dudosa procedencia.",
            ]
        ),
        new(
            "Impresión 3D para principiantes: del diseño a la pieza terminada",
            "Recorrido por el proceso completo, con consejos para evitar los errores más habituales al imprimir.",
            [
                "Este tutorial explica el flujo de trabajo de la impresión 3D de manera comprensible: buscar o diseñar un modelo, prepararlo con un programa de laminado, elegir los ajustes básicos y enviarlo a la impresora. Desmitificamos el proceso para que cualquier persona vea que no hace falta ser ingeniero para empezar.",
                "Nos detenemos en los problemas típicos de las primeras impresiones, como que la pieza no se adhiere a la base, los hilos sueltos o las capas mal pegadas, y damos soluciones concretas para cada caso. También hablamos de seguridad: ventilación, temperaturas y precaución con las partes calientes y móviles.",
                "Proponemos una primera pieza sencilla y útil, como un llavero o un soporte para el móvil, para practicar todo el ciclo sin frustrarse. La impresión 3D abre la puerta a fabricar herramientas de apoyo, piezas de repuesto y prototipos para otros proyectos de la asociación.",
            ]
        ),
        new(
            "Datos abiertos: cómo encontrarlos, entenderlos y contar historias con ellos",
            "Guía para dar los primeros pasos con conjuntos de datos públicos y convertirlos en información útil.",
            [
                "Empezamos por lo básico: qué son los datos abiertos, dónde localizarlos en portales públicos españoles y europeos, y cómo interpretar los formatos más habituales. Enseñamos a leer las fichas que describen cada conjunto, porque entender de dónde vienen los datos es tan importante como manejarlos.",
                "A continuación mostramos cómo limpiar y ordenar una tabla sencilla con herramientas al alcance de cualquiera, y cómo hacer las primeras preguntas: cuántos, cuándo, dónde y por qué. El objetivo no es acumular números, sino sacar conclusiones que se puedan explicar a otras personas.",
                "Terminamos con ideas para proyectos ciudadanos: analizar datos del propio municipio, comparar la evolución de un servicio público o crear un gráfico que ayude al vecindario a entender un problema común. Los datos abiertos son una herramienta potente para la participación y la transparencia.",
            ]
        ),
        new(
            "Inteligencia artificial explicada sin tecnicismos: qué es y qué puede hacer",
            "Material de divulgación para entender los conceptos clave, sus usos cotidianos y sus límites.",
            [
                "Este recurso aclara, sin fórmulas ni jerga, qué se entiende hoy por inteligencia artificial, en qué se diferencia de un programa tradicional y por qué necesita grandes cantidades de datos para aprender. Usamos comparaciones cotidianas para que la idea quede clara desde el primer momento.",
                "Repasamos usos que ya forman parte de nuestro día a día, como los recomendadores, los asistentes de voz o los filtros de fotos, y también aplicaciones útiles en educación, salud o accesibilidad. Al mismo tiempo, explicamos sus límites: puede equivocarse, arrastrar sesgos y no entiende el mundo como una persona.",
                "Dedicamos un apartado a un uso responsable: revisar siempre lo que genera, cuidar la privacidad, no dar por cierto todo lo que produce y reflexionar sobre su impacto social y medioambiental. Queremos ciudadanía crítica, capaz de aprovechar la tecnología sin dejarse deslumbrar por ella.",
            ]
        ),
        new(
            "Competencias digitales para mayores: guía amable para moverse por internet",
            "Material paso a paso para realizar gestiones habituales con seguridad y sin miedo a equivocarse.",
            [
                "Pensada para personas mayores que se inician, esta guía acompaña en las tareas más útiles del día a día: enviar un mensaje, hacer una videollamada con la familia, consultar la cita médica o iniciar sesión en la web de la administración. Cada paso viene descrito con calma y con indicaciones claras de dónde tocar.",
                "Insistimos en que equivocarse no rompe nada y en que casi todo tiene marcha atrás, para quitar el miedo que muchas veces frena. Damos trucos para agrandar la letra, subir el volumen y aprovechar las opciones de accesibilidad que ya traen los dispositivos.",
                "Incluimos avisos sencillos para no caer en engaños frecuentes y un recordatorio de a quién pedir ayuda de confianza cuando algo no cuadra. Este material sirve tanto para el aprendizaje individual como para las personas voluntarias que acompañan en nuestros talleres intergeneracionales.",
            ]
        ),
        new(
            "Crea tu primer videojuego: guía para pasar de la idea al prototipo jugable",
            "Un itinerario sencillo para diseñar mecánicas, dar los primeros pasos con un motor y compartir tu juego.",
            [
                "Esta guía parte de una idea pequeña y realista, porque el error más común al empezar es querer hacer un juego enorme. Ayudamos a definir una mecánica principal, un objetivo claro y las reglas mínimas, y a plasmarlo en un boceto en papel antes de tocar el ordenador.",
                "Después introducimos un motor de videojuegos accesible y gratuito, con los conceptos básicos de escenas, personajes, movimiento y colisiones. Todo se explica con un ejemplo concreto que puedes seguir para tener, en pocas sesiones, algo que de verdad se pueda jugar.",
                "Cerramos hablando de cómo probar el juego con otras personas, recoger sus impresiones y mejorar a partir de ahí, además de ideas para compartirlo con la comunidad. Desarrollar videojuegos une programación, arte, sonido y narrativa, y es una vía estupenda para engancharse a la tecnología creando algo propio.",
            ]
        ),
    ];

    private static readonly ExternalResourceSeed[] DemoExternalResources =
    [
        new(
            "MDN Web Docs: la referencia abierta para aprender desarrollo web",
            "Documentación y tutoriales oficiales de Mozilla sobre HTML, CSS y JavaScript, disponibles en español y mantenidos por la comunidad.",
            "https://developer.mozilla.org/es/docs/Learn_web_development"
        ),
        new(
            "Scratch: crea historias, juegos y animaciones con bloques",
            "Plataforma gratuita del MIT para iniciarse en la programación visual, ideal para peques y talleres en familia.",
            "https://scratch.mit.edu/"
        ),
        new(
            "freeCodeCamp en español: itinerarios interactivos de programación",
            "Cursos gratuitos con retos prácticos y certificado final para asentar las bases a tu ritmo, desde la web hasta Python.",
            "https://www.freecodecamp.org/espanol/learn/"
        ),
        new(
            "Hora del Código: actividades interactivas para empezar a programar",
            "Tutoriales autoguiados de Code.org pensados para probar la programación en una hora, sin instalar nada y para todas las edades.",
            "https://code.org/es"
        ),
        new(
            "Khan Academy: ciencias de la computación explicadas en español",
            "Cursos gratuitos con vídeos y ejercicios sobre programación, algoritmos y cómo funciona internet, para avanzar a tu ritmo.",
            "https://es.khanacademy.org/computing"
        ),
        new(
            "El tutorial oficial de Python, traducido al español",
            "La guía de referencia del propio lenguaje, traducida por la comunidad hispana: el siguiente paso natural tras nuestros talleres.",
            "https://docs.python.org/es/3/tutorial/"
        ),
        new(
            "INCIBE: ciberseguridad para la ciudadanía",
            "Avisos, guías y herramientas gratuitas del Instituto Nacional de Ciberseguridad para protegerse en el día a día digital.",
            "https://www.incibe.es/ciudadania"
        ),
        new(
            "datos.gob.es: el catálogo de datos abiertos de España",
            "Punto de acceso a miles de conjuntos de datos públicos de las administraciones españolas, listos para reutilizar en proyectos.",
            "https://datos.gob.es/"
        ),
        new(
            "Tinkercad: diseño 3D, circuitos y bloques desde el navegador",
            "Herramienta gratuita de Autodesk para modelar piezas imprimibles en 3D y simular circuitos sin instalar software.",
            "https://www.tinkercad.com/"
        ),
        new(
            "Exercism: practica programación con ejercicios y mentoría",
            "Retos gratuitos en decenas de lenguajes con revisión de mentores voluntarios, perfectos para consolidar lo aprendido.",
            "https://exercism.org/"
        ),
    ];

    private static readonly PartnerSeed[] DemoPartners =
    [
        new("Fundación Telefónica", 0, "https://www.fundaciontelefonica.com"),
        new("Ayuntamiento de Zaragoza", 1, "https://www.zaragoza.es"),
        new("Universitat Politècnica de València", 1, "https://www.upv.es"),
        new("GMV Innovating Solutions", 2, "https://www.gmv.com"),
        new("Fundación Orange España", 2, "https://www.fundacionorange.es"),
        new("Ingeniería y Software Iberdata S.L.", 2, "https://www.iberdata.es"),
        new("Academia de Robótica RoboKids", 3, "https://www.robokids.es"),
        new("Librería Técnica El Compilador", 3, null),
        new("MakerLab 3D Estudio", 3, "https://www.makerlab3d.es"),
        new("Cafetería Bit & Café", 3, null),
    ];
    private static readonly UserSeed[] UserSeeds =
    [
        new("Lucía", "Fernández Ruiz", UserKind.Admin, 1986, null),
        new("Marcos", "Serrano Vidal", UserKind.Member, 1984, null),
        new("Elena", "Navarro Gil", UserKind.Member, 1990, null),
        new("Javier", "Molina Castro", UserKind.Member, 1979, null),
        new("Sara", "Ortega Peña", UserKind.Member, 1993, null),
        new("Daniel", "Ramos León", UserKind.Member, 1988, null),
        new("Carmen", "Delgado Soto", UserKind.Member, 1982, null),
        new("Pablo", "Ibáñez Marín", UserKind.Member, 1995, null),
        new("Ana", "Herrera Cano", UserKind.Member, 1991, null),
        new("Sergio", "Vargas Prieto", UserKind.Member, 1987, null),
        new("Marta", "Reyes Nieto", UserKind.Member, 1996, null),
        new("David", "Campos Rubio", UserKind.Member, 1983, null),
        new("Raquel", "Mendoza Flores", UserKind.Member, 1994, null),
        new("Alberto", "Cortés Lozano", UserKind.Member, 1980, null),
        new("Nuria", "Gallego Bravo", UserKind.Sponsor, 1992, null),
        new("Iván", "Santos Crespo", UserKind.Sponsor, 1998, null),
        new("Cristina", "Vega Aguilar", UserKind.Sponsor, 1989, null),
        new("Rubén", "Márquez Fuentes", UserKind.Sponsor, 1997, null),
        new("Laura", "Domínguez Pardo", UserKind.Sponsor, 1985, null),
        new("Adrián", "Bautista Nogueira", UserKind.Sponsor, 1999, null),
        new("Mateo", "Serrano Ferrer", UserKind.Child, 2013, 1),
        new("Valeria", "Navarro Gil", UserKind.Child, 2014, 2),
        new("Hugo", "Molina Ríos", UserKind.Child, 2012, 3),
        new("Daniela", "Delgado Soto", UserKind.Child, 2015, 6),
        new("Leo", "Ortega Peña", UserKind.Child, 2016, 4),
    ];
}

internal sealed record DemoGraph(
    List<User> Users,
    List<FileEntity> Files,
    List<EventCategoryType> CategoryTypes,
    List<Event> Events,
    List<EventCategory> EventCategories,
    List<Activity> Activities,
    List<ActivityAllowedRoleType> AllowedRoles,
    List<ActivityUserRoleAssignment> Assignments,
    List<Announcement> Announcements,
    List<Resource> Resources,
    List<Partner> Partners
);
