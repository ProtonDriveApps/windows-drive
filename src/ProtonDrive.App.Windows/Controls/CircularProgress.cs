using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ProtonDrive.App.Windows.Controls;

internal class CircularProgress : Shape
{
    private static readonly FrameworkPropertyMetadata ValueMetadata = new(0.0, FrameworkPropertyMetadataOptions.AffectsRender, null, CoerceValue);

    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress),
        typeof(double),
        typeof(CircularProgress),
        ValueMetadata);

    static CircularProgress()
    {
        var defaultBrush = new SolidColorBrush(Color.FromArgb(255, 6, 176, 37));

        defaultBrush.Freeze();

        StrokeProperty.OverrideMetadata(typeof(CircularProgress), new FrameworkPropertyMetadata(defaultBrush));

        FillProperty.OverrideMetadata(typeof(CircularProgress), new FrameworkPropertyMetadata(Brushes.Transparent));
    }

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    protected override Geometry DefiningGeometry
    {
        get
        {
            const double startAngle = 90.0;
            var endAngle = 90.0 - (Progress * 360.0);

            var maxWidth = Math.Max(0.0, RenderSize.Width - StrokeThickness);
            var maxHeight = Math.Max(0.0, RenderSize.Height - StrokeThickness);

            var xStart = maxWidth / 2.0 * Math.Cos(startAngle * Math.PI / 180.0);
            var yStart = maxHeight / 2.0 * Math.Sin(startAngle * Math.PI / 180.0);

            var xEnd = maxWidth / 2.0 * Math.Cos(endAngle * Math.PI / 180.0);
            var yEnd = maxHeight / 2.0 * Math.Sin(endAngle * Math.PI / 180.0);

            var streamGeometry = new StreamGeometry();
            using var streamGeometryContext = streamGeometry.Open();

            streamGeometryContext.BeginFigure(
                new Point((RenderSize.Width / 2.0) + xStart, (RenderSize.Height / 2.0) - yStart),
                isFilled: true,
                isClosed: false);

            streamGeometryContext.ArcTo(
                new Point((RenderSize.Width / 2.0) + xEnd, (RenderSize.Height / 2.0) - yEnd),
                new Size(maxWidth / 2.0, maxHeight / 2),
                rotationAngle: 0.0,
                isLargeArc: (startAngle - endAngle) > 180,
                SweepDirection.Clockwise,
                isStroked: true,
                isSmoothJoin: false);

            return streamGeometry;
        }
    }

    private static object CoerceValue(DependencyObject dependencyObject, object baseValue)
    {
        var value = (double)baseValue;
        value = Math.Min(value, 0.999);
        value = Math.Max(value, 0.0);
        return value;
    }
}
