using System.IO;

namespace ProtonDrive.Shared.Text.Serialization;

public interface IBinarySerializer
{
    // TODO: Convert to an async method
    T? Deserialize<T>(Stream stream);

    // TODO: Convert to an async method
    void Serialize<T>(T? value, Stream stream);
}
