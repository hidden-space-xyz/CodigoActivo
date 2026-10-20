using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.TermsDecisions;

/// <summary>
/// Signature a person draws on screen to accept a terms document that asks for one, kept as the
/// PNG image the drawing was exported to: at most <see cref="MaxBytes"/> bytes and
/// <see cref="MaxWidth"/> by <see cref="MaxHeight"/> pixels.
/// </summary>
public sealed class HandwrittenSignature
{
    /// <summary>
    /// Largest image accepted, in bytes.
    /// </summary>
    public const int MaxBytes = 131072;

    /// <summary>
    /// Widest image accepted, in pixels.
    /// </summary>
    public const int MaxWidth = 2000;

    /// <summary>
    /// Tallest image accepted, in pixels.
    /// </summary>
    public const int MaxHeight = 1000;

    private readonly byte[] png;

    private HandwrittenSignature(byte[] png)
    {
        this.png = png;
    }

    /// <summary>
    /// Gets the PNG image.
    /// </summary>
    public ReadOnlyMemory<byte> Png => png;

    /// <summary>
    /// Checks and copies a drawn signature.
    /// </summary>
    /// <param name="png">PNG image of the drawing.</param>
    /// <returns>The signature, or <see cref="DomainErrorCode.EventTermsSignatureInvalid"/> when it is not a PNG image within the limits.</returns>
    public static Result<HandwrittenSignature> Create(ReadOnlySpan<byte> png)
    {
        return
            png.Length <= MaxBytes
            && PngImage.TryReadSize(png, out var width, out var height)
            && width <= MaxWidth
            && height <= MaxHeight
            ? new HandwrittenSignature(png.ToArray())
            : Error.Validation(DomainErrorCode.EventTermsSignatureInvalid);
    }

    /// <summary>
    /// Restores a signature that was checked before it was stored.
    /// </summary>
    /// <param name="png">Stored PNG image.</param>
    /// <returns>The signature.</returns>
    public static HandwrittenSignature FromStored(byte[] png)
    {
        ArgumentNullException.ThrowIfNull(png);
        return new HandwrittenSignature(png);
    }

    /// <summary>
    /// Copies the PNG image.
    /// </summary>
    /// <returns>A copy of the image bytes.</returns>
    public byte[] ToArray()
    {
        return [.. png];
    }
}
