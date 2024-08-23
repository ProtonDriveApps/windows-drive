using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ProtonDrive.Client.BugReport;

public sealed record BugReportBody
{
    private const string OsPropertyName = "OS";
    private const string OsVersionPropertyName = "OSVersion";
    private const string EmailAddressPropertyName = "Email";

    private readonly Dictionary<string, string> _values = new();

    public string Os
    {
        get => _values[OsPropertyName];
        set => _values[OsPropertyName] = value;
    }

    public string OsVersion
    {
        get => _values[OsVersionPropertyName];
        set => _values[OsVersionPropertyName] = value;
    }

    public string Client
    {
        get => _values[nameof(Client)];
        set => _values[nameof(Client)] = value;
    }

    public string ClientVersion
    {
        get => _values[nameof(ClientVersion)];
        set => _values[nameof(ClientVersion)] = value;
    }

    public string ClientType
    {
        get => _values[nameof(ClientType)];
        set => _values[nameof(ClientType)] = value;
    }

    public string Title
    {
        get => _values[nameof(Title)];
        set => _values[nameof(Title)] = value;
    }

    public string Description
    {
        get => _values[nameof(Description)];
        set => _values[nameof(Description)] = value;
    }

    public string? Username
    {
        get => _values[nameof(Username)];
        set => _values[nameof(Username)] = value ?? string.Empty;
    }

    public string EmailAddress
    {
        get => _values[EmailAddressPropertyName];
        set => _values[EmailAddressPropertyName] = value;
    }

    public IReadOnlyDictionary<string, string> AsDictionary() => new ReadOnlyDictionary<string, string>(_values);
}
