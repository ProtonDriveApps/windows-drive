using System.Text.Json;
using Proton.Drive.Native.Authentication.Contracts;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Authentication;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Networking.WindowsWebServices;

namespace Proton.Drive.Native.Authentication;

public static class WebAuthN
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly Lazy<uint?> ApiVersion = new(GetApiVersion);

    public static bool IsAvailable => ApiVersion.Value >= PInvoke.WEBAUTHN_API_VERSION_1;

    public static Task<Fido2AssertionResult> GetAssertionResponseAsync(
        Fido2AssertionParameters parameters,
        nint hWnd = 0,
        CancellationToken cancellationToken = default)
    {
        var windowHandle = hWnd == 0 ? PInvoke.GetForegroundWindow() : new HWND(hWnd);

        return Task.Run(GetAssertionResponseWithExceptionMapping, cancellationToken);

        Fido2AssertionResult GetAssertionResponseWithExceptionMapping()
        {
            try
            {
                return GetAssertionResponse(parameters, windowHandle, cancellationToken);
            }
            catch (Exception ex) when (WebAuthNExceptionMapping.TryMapException(ex, out var mappedException))
            {
                throw mappedException;
            }
        }
    }

    private static uint? GetApiVersion()
    {
        try
        {
            return PInvoke.WebAuthNGetApiVersionNumber();
        }
        catch (TypeLoadException)
        {
            // The WebAuthNGetApiVersionNumber() function was added in Windows 10 1903.
            return null;
        }
    }

    private static Fido2AssertionResult GetAssertionResponse(
        Fido2AssertionParameters parameters,
        HWND hWnd,
        CancellationToken cancellationToken)
    {
        var authenticationOptions = parameters.AuthenticationOptions.Deserialize<Fido2AuthenticationOptions>(JsonSerializerOptions);
        Ensure.NotNull(authenticationOptions, nameof(authenticationOptions));

        var publicKey = authenticationOptions.PublicKey;
        Ensure.NotNullOrEmpty(publicKey.RpId, nameof(publicKey), nameof(publicKey.RpId));
        Ensure.NotNullOrEmpty(publicKey.Challenge, nameof(publicKey), nameof(publicKey.Challenge));
        Ensure.NotNullOrEmpty(publicKey.AllowCredentials, nameof(publicKey), nameof(publicKey.AllowCredentials));

        var clientData = new WebAuthNClientData
        {
            Type = "webauthn.get",
            Challenge = Base64UrlEncode(publicKey.Challenge.ToArray()),
            Origin = NormalizeOrigin(publicKey.RpId),
            CrossOrigin = false,
        };

        var clientDataUtf8 = JsonSerializer.SerializeToUtf8Bytes(clientData, JsonSerializerOptions);

        var cancellationId = Guid.NewGuid();
        using var cancellationRegistration = cancellationToken.Register(OnCancellationRequested, cancellationId);

        unsafe
        {
            fixed (byte* clientDataUtf8Pointer = clientDataUtf8)
            {
                fixed (char* hashAlgorithmNamePointer = PInvoke.WEBAUTHN_HASH_ALGORITHM_SHA_256)
                {
                    fixed (char* credentialTypePointer = PInvoke.WEBAUTHN_CREDENTIAL_TYPE_PUBLIC_KEY)
                    {
                        var totalCredentialIdSize = publicKey.AllowCredentials.Sum(x => x.Id.Count);
                        Span<byte> credentialIds = new byte[totalCredentialIdSize];

                        var nativeCredentials = new WEBAUTHN_CREDENTIAL[publicKey.AllowCredentials.Count];

                        fixed (byte* credentialIdPointer = credentialIds)
                        {
                            fixed (WEBAUTHN_CREDENTIAL* nativeCredentialsPointer = nativeCredentials)
                            {
                                int start = 0;
                                for (var i = 0; i < publicKey.AllowCredentials.Count; i++)
                                {
                                    var credential = publicKey.AllowCredentials[i].Id.ToArray();
                                    credential.CopyTo(credentialIds[start..(start + credential.Length)]);

                                    nativeCredentials[i].dwVersion = PInvoke.WEBAUTHN_CREDENTIAL_CURRENT_VERSION;
                                    nativeCredentials[i].cbId = (uint)credential.Length;
                                    nativeCredentials[i].pbId = credentialIdPointer + start;
                                    nativeCredentials[i].pwszCredentialType = credentialTypePointer;

                                    start += credential.Length;
                                }

                                var nativeClientData = new WEBAUTHN_CLIENT_DATA
                                {
                                    dwVersion = PInvoke.WEBAUTHN_CLIENT_DATA_CURRENT_VERSION,
                                    cbClientDataJSON = (uint)clientDataUtf8.Length,
                                    pbClientDataJSON = clientDataUtf8Pointer,
                                    pwszHashAlgId = hashAlgorithmNamePointer,
                                };

                                var getAssertionOptions = new WEBAUTHN_AUTHENTICATOR_GET_ASSERTION_OPTIONS
                                {
                                    dwVersion = PInvoke.WEBAUTHN_AUTHENTICATOR_GET_ASSERTION_OPTIONS_CURRENT_VERSION,
                                    dwTimeoutMilliseconds = authenticationOptions.PublicKey.Timeout,
                                    CredentialList = new WEBAUTHN_CREDENTIALS
                                    {
                                        cCredentials = (uint)nativeCredentials.Length,
                                        pCredentials = nativeCredentialsPointer,
                                    },
                                    Extensions = new WEBAUTHN_EXTENSIONS
                                    {
                                        cExtensions = 0,
                                        pExtensions = null,
                                    },
                                    dwAuthenticatorAttachment = 0,
                                    dwUserVerificationRequirement = 0,
                                    dwFlags = 0,
                                    pwszU2fAppId = null,
                                    pbU2fAppId = null,
                                    pCancellationId = &cancellationId,
                                    pAllowCredentialList = null,
                                };

                                PInvoke.WebAuthNAuthenticatorGetAssertion(
                                        hWnd,
                                        publicKey.RpId,
                                        nativeClientData,
                                        getAssertionOptions,
                                        out var pAssertion)
                                    .ThrowOnFailure();

                                try
                                {
                                    var authenticatorData = new ReadOnlySpan<byte>(pAssertion->pbAuthenticatorData, (int)pAssertion->cbAuthenticatorData);
                                    var signature = new ReadOnlySpan<byte>(pAssertion->pbSignature, (int)pAssertion->cbSignature);
                                    var credLen = (int)pAssertion->Credential.cbId;
                                    var credentialId = new ReadOnlySpan<byte>(pAssertion->Credential.pbId, credLen);

                                    return new Fido2AssertionResult
                                    {
                                        AuthenticationOptions = parameters.AuthenticationOptions,
                                        ClientData = clientDataUtf8,
                                        AuthenticatorData = authenticatorData.ToArray(),
                                        Signature = signature.ToArray(),
                                        CredentialId = credentialId.ToArray(),
                                    };
                                }
                                finally
                                {
                                    PInvoke.WebAuthNFreeAssertion(pAssertion);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Adds "https://" to relaying party identifier if missing
    /// </summary>
    private static string NormalizeOrigin(string origin)
    {
        var uriBuilder = new UriBuilder(origin)
        {
            Scheme = Uri.UriSchemeHttps,
        };

        return uriBuilder.Uri.ToString();
    }

    private static void OnCancellationRequested(object? state)
    {
        if (state is not Guid cancellationId)
        {
            return;
        }

        PInvoke.WebAuthNCancelCurrentOperation(cancellationId).ThrowOnFailure();
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> data)
    {
        var s = Convert.ToBase64String(data);

        return s.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
