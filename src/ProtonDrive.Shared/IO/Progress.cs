namespace ProtonDrive.Shared.IO;

public readonly record struct Progress(double Value, double Maximum)
{
    public static readonly Progress Zero = new(0d, 1d);
    public static readonly Progress Completed = new(1d, 1d);

    public Progress()
        : this(0d, 1d)
    {
    }

    public double Ratio => Value / Maximum;
}
