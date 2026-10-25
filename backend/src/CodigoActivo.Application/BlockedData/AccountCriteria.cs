using System.Text.Json;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.BlockedData;

/// <summary>
/// What a search of the blocked data looks an account up by, read from the text typed: an
/// identifier, an email, a DNI/NIE or a phone number, each compared in the normalized form it is
/// stored in, or else part of the name.
/// </summary>
internal sealed record AccountCriteria(
    Guid? Id,
    string? Email,
    string? NationalId,
    string? Phone,
    string? Name
)
{
    /// <summary>
    /// Reads the criteria from the text typed.
    /// </summary>
    /// <param name="query">Text typed.</param>
    /// <returns>The criteria.</returns>
    public static AccountCriteria Parse(string query)
    {
        var text = query.Trim();
        if (Guid.TryParse(text, out var id))
        {
            return new AccountCriteria(id, null, null, null, null);
        }

        if (
            text.Contains('@', StringComparison.Ordinal)
            && EmailAddress.Create(text) is { IsSuccess: true } email
        )
        {
            return new AccountCriteria(null, email.Value.Value, null, null, null);
        }

        var nationalId = SpanishNationalId.Normalize(text);
        if (SpanishNationalId.IsValid(nationalId))
        {
            return new AccountCriteria(null, null, nationalId, null, null);
        }

        return PhoneNumber.Create(text) is { IsSuccess: true } phone
            ? new AccountCriteria(null, null, null, phone.Value.Value, null)
            : new AccountCriteria(null, null, null, null, TextSearch.Normalize(text));
    }

    /// <summary>
    /// Narrows the existing accounts to those that match.
    /// </summary>
    /// <param name="users">Existing accounts.</param>
    /// <returns>The accounts that match.</returns>
    public IQueryable<UserRow> Filter(IQueryable<UserRow> users)
    {
        if (Id is { } id)
        {
            return users.Where(u => u.Id == id);
        }

        if (Email is { } email)
        {
            return users.Where(u => u.Email == email);
        }

        if (NationalId is { } nationalId)
        {
            return users.Where(u => u.NationalId == nationalId);
        }

        return Phone is { } phone
            ? users.Where(u => u.Phone == phone || u.SecondaryPhone == phone)
            : users.WhereContains(u => u.FirstName + " " + u.LastName, Name);
    }

    /// <summary>
    /// Tells whether a past state of an account matches.
    /// </summary>
    /// <param name="values">Values of the account kept in the data history.</param>
    /// <returns><see langword="true"/> when it matches.</returns>
    public bool Matches(IReadOnlyDictionary<string, JsonElement> values)
    {
        if (Id is { } id)
        {
            return Text(values, nameof(User.Id)) == id.ToString();
        }

        if (Email is not null)
        {
            return Text(values, nameof(User.Email)) == Email;
        }

        if (NationalId is not null)
        {
            return Text(values, nameof(User.NationalId)) == NationalId;
        }

        if (Phone is not null)
        {
            return Text(values, nameof(User.Phone)) == Phone
                || Text(values, nameof(User.SecondaryPhone)) == Phone;
        }

        var name = $"{Text(values, nameof(User.FirstName))} {Text(values, nameof(User.LastName))}";
        return TextSearch.Normalize(name).Contains(Name ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// Reads a text value kept in the data history.
    /// </summary>
    /// <param name="values">Values kept.</param>
    /// <param name="property">Name of the property.</param>
    /// <returns>The text, or <see langword="null"/> when there is none.</returns>
    public static string? Text(IReadOnlyDictionary<string, JsonElement> values, string property)
    {
        return
            values.TryGetValue(property, out var value) && value.ValueKind is JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
