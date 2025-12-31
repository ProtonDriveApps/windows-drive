namespace ProtonDrive.Shared.Extensions;

public static class ExceptionMessageExtensions
{
    public static string CombinedMessage(this Exception exception)
    {
        var previousMessage = string.Empty;

        return string.Join(
            " ---> ",
            ThisAndInnerExceptions(exception)
                .Select(ex => ex.Message)
                .Where(m => previousMessage != (previousMessage = m)));
    }

    private static IEnumerable<Exception> ThisAndInnerExceptions(Exception? e)
    {
        for (; e != null; e = e.InnerException)
        {
            yield return e;
        }
    }
}
