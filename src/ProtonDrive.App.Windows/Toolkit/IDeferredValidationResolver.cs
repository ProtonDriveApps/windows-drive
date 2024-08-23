using System.ComponentModel.DataAnnotations;

namespace ProtonDrive.App.Windows.Toolkit;

public interface IDeferredValidationResolver
{
    ValidationResult? Validate(string? memberName);
}
