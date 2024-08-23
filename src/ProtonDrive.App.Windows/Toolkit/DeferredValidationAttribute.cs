using System;
using System.ComponentModel.DataAnnotations;

namespace ProtonDrive.App.Windows.Toolkit;

[AttributeUsage(AttributeTargets.Property)]
public sealed class DeferredValidationAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (validationContext.ObjectInstance is not IDeferredValidationResolver resolver)
        {
            throw new InvalidOperationException();
        }

        return resolver.Validate(validationContext.MemberName);
    }
}
