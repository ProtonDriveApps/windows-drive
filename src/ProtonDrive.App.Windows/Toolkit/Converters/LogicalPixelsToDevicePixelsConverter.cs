using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ProtonDrive.App.Windows.Toolkit.Converters;

public abstract class LogicalPixelsToDevicePixelsConverter<T> : IValueConverter
    where T : struct
{
    private readonly Visual _referenceVisual;

    protected LogicalPixelsToDevicePixelsConverter(Visual referenceVisual)
    {
        _referenceVisual = referenceVisual;
    }

    public abstract T Convert(T value);
    public abstract T ConvertBack(T value);

    object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is T logicalValue ? Convert(logicalValue) : DependencyProperty.UnsetValue;
    }

    object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is T physicalValue ? Convert(physicalValue) : DependencyProperty.UnsetValue;
    }

    protected Point ConvertPoint(Point point)
    {
        return ConvertPoint(point, ct => ct.TransformToDevice);
    }

    protected Vector ConvertVector(Vector vector)
    {
        return ConvertVector(vector, ct => ct.TransformToDevice);
    }

    protected Point ConvertBackPoint(Point point)
    {
        return ConvertPoint(point, ct => ct.TransformFromDevice);
    }

    protected Vector ConvertBackVector(Vector vector)
    {
        return ConvertVector(vector, ct => ct.TransformFromDevice);
    }

    private Point ConvertPoint(Point point, Func<CompositionTarget, Matrix> getMatrix)
    {
        var presentationSource = PresentationSource.FromVisual(_referenceVisual);
        return presentationSource?.CompositionTarget != null
            ? getMatrix(presentationSource.CompositionTarget).Transform(point)
            : new Point(double.NaN, double.NaN);
    }

    private Vector ConvertVector(Vector vector, Func<CompositionTarget, Matrix> getMatrix)
    {
        var presentationSource = PresentationSource.FromVisual(_referenceVisual);
        return presentationSource?.CompositionTarget != null
            ? getMatrix(presentationSource.CompositionTarget).Transform(vector)
            : new Vector(double.NaN, double.NaN);
    }
}
