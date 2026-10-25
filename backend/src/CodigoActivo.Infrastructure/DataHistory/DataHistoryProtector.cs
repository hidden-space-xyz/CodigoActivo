using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;

namespace CodigoActivo.Infrastructure.DataHistory;

/// <summary>
/// Encrypts and decrypts the records of the data history with the ASP.NET Core Data Protection key
/// ring, so a copy of the database alone reveals nothing of them. Each record is bound to the
/// identifier of its entry: a payload moved to another entry no longer decrypts.
/// </summary>
public sealed class DataHistoryProtector
{
    private const string Purpose = "CodigoActivo.DataHistory.v1";

    private static readonly JsonSerializerOptions SerializerOptions = new(
        JsonSerializerDefaults.Web
    )
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IDataProtector protector;

    /// <summary>
    /// Initializes the protector for the data history purpose.
    /// </summary>
    /// <param name="provider">Data protection provider configured by the host.</param>
    public DataHistoryProtector(IDataProtectionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        protector = provider.CreateProtector(Purpose);
    }

    /// <summary>
    /// Encrypts the record of an entry.
    /// </summary>
    /// <param name="entryId">Identifier of the entry that stores it.</param>
    /// <param name="record">Record to encrypt.</param>
    /// <returns>The payload to store.</returns>
    public byte[] Protect(Guid entryId, DataHistoryRecord record)
    {
        return ForEntry(entryId)
            .Protect(JsonSerializer.SerializeToUtf8Bytes(record, SerializerOptions));
    }

    /// <summary>
    /// Decrypts the record of an entry.
    /// </summary>
    /// <param name="entryId">Identifier of the entry that stores it.</param>
    /// <param name="payload">Stored payload.</param>
    /// <returns>The record; its values come back as JSON elements.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// The payload was altered, belongs to another entry or its key is no longer in the key ring.
    /// </exception>
    public DataHistoryRecord Unprotect(Guid entryId, byte[] payload)
    {
        return JsonSerializer.Deserialize<DataHistoryRecord>(
                ForEntry(entryId).Unprotect(payload),
                SerializerOptions
            ) ?? throw new JsonException("The data history record is empty.");
    }

    private IDataProtector ForEntry(Guid entryId)
    {
        return protector.CreateProtector(entryId.ToString("N"));
    }
}
