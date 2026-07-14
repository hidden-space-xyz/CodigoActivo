namespace CodigoActivo.Application.Caching;

public interface ICacheInvalidator
{
    ValueTask InvalidateAsync(params IReadOnlyCollection<string> tags);
}
