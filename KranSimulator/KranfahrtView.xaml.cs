using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace KranSimulator;

public partial class KranfahrtView : UserControl
{
    private const double HomeX = 510;
    private const double RaisedMagnetTop = 160;
    private bool isRunning;

    private static readonly IReadOnlyDictionary<string, CranePoint> CranePoints =
        new Dictionary<string, CranePoint>(StringComparer.OrdinalIgnoreCase)
        {
            ["LKW1"] = new(180, 95),
            ["LKW2"] = new(510, 95),
            ["LKW3"] = new(840, 95),
            ["BOX 1"] = new(135, 205),
            ["BOX 2"] = new(317, 205),
            ["BOX 3"] = new(499, 205),
            ["BOX 4"] = new(681, 205),
            ["BOX 5"] = new(863, 205),
            ["BOX 6"] = new(135, 300),
            ["BOX 7"] = new(317, 300),
            ["BOX 8"] = new(499, 300),
            ["BOX 9"] = new(681, 300),
            ["BOX 10"] = new(863, 300),
            ["CW1"] = new(180, 475),
            ["CW2"] = new(510, 475),
            ["CW3"] = new(840, 475)
        };

    public KranfahrtView()
    {
        InitializeComponent();
    }

    private async void BtnStartSimulation_Click(object sender, RoutedEventArgs e)
    {
        if (isRunning)
        {
            return;
        }

        string? source = GetSelectedValue(CmbSimulationSource);
        string? target = GetSelectedValue(CmbSimulationTarget);

        if (source is null || target is null)
        {
            TxtSimulationStatus.Text = "Quelle und Ziel auswählen";
            return;
        }

        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
        {
            TxtSimulationStatus.Text = "Quelle und Ziel sind identisch";
            return;
        }

        if (!CranePoints.TryGetValue(source, out CranePoint sourcePoint)
            || !CranePoints.TryGetValue(target, out CranePoint targetPoint))
        {
            TxtSimulationStatus.Text = "Position nicht verfügbar";
            return;
        }

        isRunning = true;
        SetControlsEnabled(false);

        try
        {
            double speedFactor = GetSpeedFactor();
            TxtSimulationStatus.Text = $"Fahrt zu {source}";
            await MoveCraneAsync(sourcePoint.X, speedFactor);

            TxtSimulationStatus.Text = $"Materialaufnahme an {source}";
            await MoveMagnetAsync(sourcePoint.MagnetTop, speedFactor);
            ScrapLoad.Visibility = Visibility.Visible;
            await PauseAsync(350, speedFactor);
            await MoveMagnetAsync(RaisedMagnetTop, speedFactor);

            TxtSimulationStatus.Text = $"Transport zu {target}";
            await MoveCraneAsync(targetPoint.X, speedFactor);

            TxtSimulationStatus.Text = $"Materialabgabe an {target}";
            await MoveMagnetAsync(targetPoint.MagnetTop, speedFactor);
            ScrapLoad.Visibility = Visibility.Collapsed;
            await PauseAsync(350, speedFactor);
            await MoveMagnetAsync(RaisedMagnetTop, speedFactor);

            TxtSimulationStatus.Text = $"Fahrt abgeschlossen: {source} → {target}";
        }
        finally
        {
            isRunning = false;
            SetControlsEnabled(true);
        }
    }

    private void BtnResetSimulation_Click(object sender, RoutedEventArgs e)
    {
        if (isRunning)
        {
            return;
        }

        CraneAssembly.BeginAnimation(Canvas.LeftProperty, null);
        MagnetAssembly.BeginAnimation(Canvas.TopProperty, null);
        ScrapLoad.BeginAnimation(Canvas.TopProperty, null);
        Canvas.SetLeft(CraneAssembly, HomeX);
        Canvas.SetTop(MagnetAssembly, RaisedMagnetTop);
        Canvas.SetTop(ScrapLoad, RaisedMagnetTop + 44);
        HoistCable.Y2 = RaisedMagnetTop + 10;
        ScrapLoad.Visibility = Visibility.Collapsed;
        TxtSimulationStatus.Text = "Bereit";
    }

    private async Task MoveCraneAsync(double targetX, double speedFactor)
    {
        double currentX = Canvas.GetLeft(CraneAssembly);
        double distance = Math.Abs(targetX - currentX);
        double seconds = Math.Max(0.35, distance / 320) * speedFactor;
        await AnimateAsync(CraneAssembly, Canvas.LeftProperty, targetX, seconds);
    }

    private async Task MoveMagnetAsync(double targetTop, double speedFactor)
    {
        double currentTop = Canvas.GetTop(MagnetAssembly);
        double distance = Math.Abs(targetTop - currentTop);
        double seconds = Math.Max(0.3, distance / 260) * speedFactor;

        Task magnetTask = AnimateAsync(MagnetAssembly, Canvas.TopProperty, targetTop, seconds);
        Task loadTask = AnimateAsync(ScrapLoad, Canvas.TopProperty, targetTop + 44, seconds);
        Task cableTask = AnimateAsync(HoistCable, System.Windows.Shapes.Line.Y2Property, targetTop + 10, seconds);
        await Task.WhenAll(magnetTask, loadTask, cableTask);
    }

    private static Task AnimateAsync(
        UIElement target,
        DependencyProperty property,
        double value,
        double seconds)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var animation = new DoubleAnimation
        {
            To = value,
            Duration = TimeSpan.FromSeconds(seconds),
            AccelerationRatio = 0.2,
            DecelerationRatio = 0.2,
            FillBehavior = FillBehavior.HoldEnd
        };

        animation.Completed += (_, _) =>
        {
            target.BeginAnimation(property, null);
            target.SetValue(property, value);
            completion.TrySetResult();
        };
        target.BeginAnimation(property, animation);
        return completion.Task;
    }

    private static string? GetSelectedValue(ComboBox comboBox)
    {
        return (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
    }

    private double GetSpeedFactor()
    {
        if (CmbSimulationSpeed.SelectedItem is ComboBoxItem item
            && double.TryParse(
                item.Tag?.ToString(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double factor))
        {
            return factor;
        }

        return 1.0;
    }

    private void SetControlsEnabled(bool enabled)
    {
        CmbSimulationSource.IsEnabled = enabled;
        CmbSimulationTarget.IsEnabled = enabled;
        CmbSimulationSpeed.IsEnabled = enabled;
        BtnStartSimulation.IsEnabled = enabled;
        BtnResetSimulation.IsEnabled = enabled;
    }

    private static Task PauseAsync(int milliseconds, double speedFactor)
    {
        return Task.Delay(TimeSpan.FromMilliseconds(milliseconds * speedFactor));
    }

    private readonly record struct CranePoint(double X, double MagnetTop);
}
