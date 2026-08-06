using System.ComponentModel.DataAnnotations;

namespace Proton.Drive.App.Windows.Toolkit;

public interface IDeferredValidationResolver
{
    ValidationResult? Validate(string? memberName);
}
