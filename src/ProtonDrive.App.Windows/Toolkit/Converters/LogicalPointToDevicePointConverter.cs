using System.Windows;
using System.Windows.Media;

namespace ProtonDrive.App.Windows.Toolkit.Converters;

public class LogicalPointToDevicePointConverter : LogicalPixelsToDevicePixelsConverter<Point>
{
    public LogicalPointToDevicePointConverter(Visual referenceVisual)
        : base(referenceVisual)
    {
    }

    public override Point Convert(Point value)
    {
        return ConvertPoint(value);
    }

    public override Point ConvertBack(Point value)
    {
        return ConvertBackPoint(value);
    }
}
