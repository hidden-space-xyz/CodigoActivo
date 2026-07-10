using Xunit;

// Every class in this assembly runs against ONE PostgreSQL database (PostgresContainerFixture, an
// assembly fixture), and IntegrationTestBase.InitializeAsync TRUNCATEs every table and reseeds before
// each test. Two classes running concurrently would truncate each other's rows mid-test.
//
// The tests are independent and order-free -- each one arranges everything it needs and leaves the
// database clean for the next -- they are simply not allowed to run *simultaneously*. Lifting this
// would mean handing every test class its own database (CREATE DATABASE ... TEMPLATE), which buys a
// few seconds on a ~20-second suite in exchange for real complexity. If you ever do it, note that
// LocalFileSystemRepositoryTests also touches the shared "./files" fallback path.
//
// CodigoActivo.UnitTests carries no such attribute on purpose: it owns no shared resource, so xUnit
// runs its classes in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
