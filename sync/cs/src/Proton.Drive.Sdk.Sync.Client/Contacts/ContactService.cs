using Microsoft.Extensions.Logging;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Configuration;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Client.Contacts;

internal sealed class ContactService : IContactService
{
    private readonly IClock _clock;
    private readonly IContactApiClient _contactApiClient;
    private readonly ILogger<ContactService> _logger;

    private readonly TimeSpan _cacheInvalidationInterval;
    // Grace period during which the expired cache is still considered valid if a refresh attempt fails.
    private readonly TimeSpan _cacheGraceInterval;
    private readonly SemaphoreSlim _semaphore = new(1);

    private DateTime _cacheExpirationTime;
    private IReadOnlyDictionary<string, string> _cachedContacts = new Dictionary<string, string>().AsReadOnly();

    public ContactService(IClock clock, IContactApiClient contactApiClient, AppConfig appConfig, ILogger<ContactService> logger)
    {
        _clock = clock;
        _contactApiClient = contactApiClient;
        _logger = logger;
        _cacheInvalidationInterval = appConfig.ContactsCacheInvalidationInterval.RandomizedWithDeviation(0.2);
        _cacheGraceInterval = appConfig.ContactsCacheGraceInterval.RandomizedWithDeviation(0.2);
    }

    public async Task<string?> GetDisplayNameByEmailAddressAsync(string email, CancellationToken cancellationToken)
    {
        var cache = await GetContactsCacheAsync(cancellationToken).ConfigureAwait(false);

        return cache.GetValueOrDefault(email);
    }

    private async Task<IReadOnlyDictionary<string, string>> GetContactsCacheAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!CacheIsValid())
            {
                try
                {
                    var response = await _contactApiClient.GetContactsAsync(cancellationToken).ThrowOnFailure().ConfigureAwait(false);

                    var contacts = response.Contacts.DistinctBy(x => x.Email);

                    _cachedContacts = contacts.ToDictionary(x => x.Email, x => x.Name).AsReadOnly();

                    _cacheExpirationTime = _clock.UtcNow + _cacheInvalidationInterval;
                }
                catch (ApiException ex)
                {
                    _cacheExpirationTime = _clock.UtcNow + _cacheGraceInterval;
                    _logger.LogWarning("Refreshing the list of contacts failed. {Code}: {Message}", ex.ResponseCode, ex.Message);
                }
            }
        }
        finally
        {
            _semaphore.Release();
        }

        return _cachedContacts;

        bool CacheIsValid()
        {
            return _clock.UtcNow < _cacheExpirationTime;
        }
    }
}
