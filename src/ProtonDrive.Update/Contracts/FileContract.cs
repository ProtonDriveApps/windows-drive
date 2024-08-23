using System;

namespace ProtonDrive.Update.Contracts;

internal class FileContract : IEquatable<FileContract>
{
    public string Url { get; set; } = string.Empty;

    public string Sha512Checksum { get; set; } = string.Empty;

    public string? Arguments { get; set; }

    public string? SilentArguments { get; set; }

    public bool Equals(FileContract? other)
    {
        if (other == null)
        {
            return false;
        }

        return string.Equals(Url, other.Url) &&
               string.Equals(Sha512Checksum, other.Sha512Checksum) &&
               string.Equals(Arguments, other.Arguments);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != GetType())
        {
            return false;
        }

        return Equals(obj as FileContract);
    }

    public override int GetHashCode()
    {
        throw new InvalidOperationException();
    }
}
