using System;
using System.Collections.Generic;
using System.Linq;

namespace ProtonDrive.Shared.Extensions;

public static class ExceptionMessageExtensions
{
    public static string CombinedMessage(this Exception exception)
    {
        return string.Join(" ---> ", ThisAndInnerExceptions(exception).Select(ex => ex.Message));
    }

    private static IEnumerable<Exception> ThisAndInnerExceptions(Exception? e)
    {
        for (; e != null; e = e.InnerException)
        {
            yield return e;
        }
    }
}
