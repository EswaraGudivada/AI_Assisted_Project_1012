namespace UrlShortener.Domain;

using System.Security.Cryptography;

/// <summary>
/// Domain Service for generating cryptographically secure random short codes.
/// </summary>
public class ShortCodeGenerator
{
    private const string Base62Characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const int CodeLength = 6;

    /// <summary>
    /// Generates a random short code of 6 characters using Base62 encoding.
    /// </summary>
    public string Generate()
    {
        var buffer = new byte[CodeLength];
        RandomNumberGenerator.Fill(buffer);

        var result = new char[CodeLength];
        for (int i = 0; i < CodeLength; i++)
        {
            result[i] = Base62Characters[buffer[i] % Base62Characters.Length];
        }

        return new string(result);
    }
}
