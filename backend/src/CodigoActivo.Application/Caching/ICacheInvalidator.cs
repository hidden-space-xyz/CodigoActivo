namespace CodigoActivo.Application.Caching;

public interface ICacheInvalidator
{
    public ValueTask InvalidateAsync(params IReadOnlyCollection<string> tags);
}
