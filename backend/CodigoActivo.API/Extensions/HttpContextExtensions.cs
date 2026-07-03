namespace CodigoActivo.API.Extensions;

public static class HttpContextExtensions
{
    private const string TraceIdItemKey = "CodigoActivo.TraceId";

    public static string GetOrSetTraceId(this HttpContext context)
    {
        if (context.Items.TryGetValue(TraceIdItemKey, out var existing) && existing is string traceId) return traceId;

        var newTraceId = context.TraceIdentifier;
        context.Items[TraceIdItemKey] = newTraceId;
        return newTraceId;
    }
}