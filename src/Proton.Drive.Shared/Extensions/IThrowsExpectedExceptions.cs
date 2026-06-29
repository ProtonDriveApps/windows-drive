namespace Proton.Drive.Shared.Extensions;

public interface IThrowsExpectedExceptions
{
    bool IsExpectedException(Exception ex);
}
