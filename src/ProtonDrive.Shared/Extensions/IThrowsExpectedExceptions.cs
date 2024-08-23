using System;

namespace ProtonDrive.Shared.Extensions;

public interface IThrowsExpectedExceptions
{
    bool IsExpectedException(Exception ex);
}
