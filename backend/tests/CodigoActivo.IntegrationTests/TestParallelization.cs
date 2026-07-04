using Xunit;

// Each integration test class boots its own in-process host. Running them concurrently would race on
// Serilog's static Log.Logger and its shared rolling log file, so the whole assembly runs serially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
