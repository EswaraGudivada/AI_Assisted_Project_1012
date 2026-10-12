namespace UrlShortener.Domain;

/// <summary>
/// Value Object representing a short code with validation and immutability.
/// </summary>
public sealed class ShortCode : IEquatable<ShortCode>
{
    private const int MinLength = 6;
    private const int MaxLength = 8;
    private static readonly string ValidCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public string Value { get; }

    public ShortCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Short code cannot be null or empty", nameof(value));
        }

        if (value.Length < MinLength || value.Length > MaxLength)
        {
            throw new ArgumentException($"Short code must be between {MinLength} and {MaxLength} characters", nameof(value));
        }

        if (!value.All(c => ValidCharacters.Contains(c)))
        {
            throw new ArgumentException("Short code must contain only alphanumeric characters", nameof(value));
        }

        Value = value;
    }

    public override bool Equals(object? obj) => Equals(obj as ShortCode);

    public bool Equals(ShortCode? other) => other is not null && Value == other.Value;

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value;

    public static bool operator ==(ShortCode? left, ShortCode? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(ShortCode? left, ShortCode? right) =>
        !(left == right);
}
