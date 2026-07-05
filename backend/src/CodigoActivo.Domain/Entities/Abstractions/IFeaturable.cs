namespace CodigoActivo.Domain.Entities.Abstractions;

/// <summary>An entity of which at most one row is highlighted ("featured") at a time.</summary>
public interface IFeaturable
{
    bool Featured { get; set; }
}
