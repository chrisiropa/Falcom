using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;

namespace KranSimulator;

public partial class KranfahrtView : UserControl
{
    private const double RaisedY = 7.0;
    private const double HomeX = 0.0;
    private const double HomeZ = 0.0;
    private const double DefaultCameraHeight = 19.0;
    private const double DefaultCameraDistance = 36.0;
    private const double DefaultCameraTargetHeight = 5.5;

    private readonly TranslateTransform3D bridgeTransform = new();
    private readonly TranslateTransform3D trolleyTransform = new();
    private readonly TranslateTransform3D magnetTransform = new();
    private readonly TranslateTransform3D cableTransform = new();
    private readonly ScaleTransform3D cableScale = new(1, RaisedY - 1, 1);
    private readonly TranslateTransform3D loadTransform = new();
    private readonly Model3DGroup scene = new();
    private readonly Model3DGroup loadGroup = new();
    private GeometryModel3D? loadModel;
    private bool isRunning;
    private int cameraIndex;

    private static readonly IReadOnlyDictionary<string, CranePoint> CranePoints =
        new Dictionary<string, CranePoint>(StringComparer.OrdinalIgnoreCase)
        {
            ["LKW1"] = new(-7, -7, 1.25),
            ["LKW2"] = new(0, -7, 1.25),
            ["LKW3"] = new(7, -7, 1.25),
            ["BOX 1"] = new(-8, -2.3, 1.35),
            ["BOX 2"] = new(-4, -2.3, 1.35),
            ["BOX 3"] = new(0, -2.3, 1.35),
            ["BOX 4"] = new(4, -2.3, 1.35),
            ["BOX 5"] = new(8, -2.3, 1.35),
            ["BOX 6"] = new(-8, 2.3, 1.35),
            ["BOX 7"] = new(-4, 2.3, 1.35),
            ["BOX 8"] = new(0, 2.3, 1.35),
            ["BOX 9"] = new(4, 2.3, 1.35),
            ["BOX 10"] = new(8, 2.3, 1.35),
            ["CW1"] = new(-7, 7, 1.4),
            ["CW2"] = new(0, 7, 1.4),
            ["CW3"] = new(7, 7, 1.4)
        };

    public KranfahrtView()
    {
        InitializeComponent();
        BuildScene();
        ResetCrane();
        SetDefaultCamera();
    }

    private void BuildScene()
    {
        scene.Children.Clear();
        scene.Children.Add(new AmbientLight(Color.FromRgb(92, 96, 102)));
        scene.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-1, -2, -1)));
        scene.Children.Add(new DirectionalLight(Color.FromRgb(150, 170, 190), new Vector3D(1, -1, 1)));

        scene.Children.Add(CreateBox(0, -0.25, 0, 22, 0.5, 18, Color.FromRgb(62, 66, 70)));
        AddFloorMarkings();
        AddTrucks();
        AddStorageBoxes();
        AddChargingCars();
        AddCrane();

        SceneVisual.Content = scene;
    }

    private void AddFloorMarkings()
    {
        Material lineMaterial = CreateMaterial(Color.FromRgb(120, 124, 128));

        for (int x = -10; x <= 10; x += 2)
        {
            scene.Children.Add(CreateBox(x, 0.02, 0, 0.035, 0.025, 17.5, lineMaterial));
        }

        for (int z = -8; z <= 8; z += 2)
        {
            scene.Children.Add(CreateBox(0, 0.025, z, 21.5, 0.03, 0.035, lineMaterial));
        }
    }

    private void AddTrucks()
    {
        AddTruck(-7, -7, "LKW1");
        AddTruck(0, -7, "LKW2");
        AddTruck(7, -7, "LKW3");
    }

    private void AddTruck(double x, double z, string label)
    {
        scene.Children.Add(CreateBox(x, 0.65, z, 4.2, 1.1, 2.0, Color.FromRgb(69, 76, 84)));
        scene.Children.Add(CreateBox(x + 1.35, 1.35, z, 1.25, 1.4, 1.9, Color.FromRgb(42, 48, 54)));
        scene.Children.Add(CreateBox(x - 0.6, 1.25, z, 2.4, 0.7, 1.65, Color.FromRgb(175, 179, 184)));
        scene.Children.Add(CreateLabelPlate(label, x, 1.92, z - 1.03, 2.2, 0.65));
    }

    private void AddStorageBoxes()
    {
        for (int index = 1; index <= 10; index++)
        {
            CranePoint point = CranePoints[$"BOX {index}"];
            scene.Children.Add(CreateBox(point.X, 0.65, point.Z, 3.25, 1.2, 3.2, Color.FromRgb(118, 123, 128)));
            scene.Children.Add(CreateBox(point.X, 1.22, point.Z, 2.75, 0.55, 2.7, Color.FromRgb(194, 137, 60)));
            scene.Children.Add(CreateLabelPlate($"BOX {index}", point.X, 1.72, point.Z - 1.63, 2.2, 0.55));
        }
    }

    private void AddChargingCars()
    {
        AddChargingCar(-7, 7, "CW1");
        AddChargingCar(0, 7, "CW2");
        AddChargingCar(7, 7, "CW3");
    }

    private void AddChargingCar(double x, double z, string label)
    {
        scene.Children.Add(CreateBox(x, 0.7, z, 4.0, 1.25, 2.5, Color.FromRgb(134, 34, 28)));
        scene.Children.Add(CreateBox(x, 1.28, z, 3.3, 0.55, 1.9, Color.FromRgb(196, 64, 53)));
        scene.Children.Add(CreateLabelPlate(label, x, 1.8, z - 1.28, 1.8, 0.58));
    }

    private void AddCrane()
    {
        Material railMaterial = CreateMaterial(Color.FromRgb(168, 173, 179));
        Material craneMaterial = CreateMaterial(Color.FromRgb(179, 38, 30));
        Material darkMaterial = CreateMaterial(Color.FromRgb(45, 49, 54));

        scene.Children.Add(CreateBox(-10.2, 4.6, -8.4, 0.45, 9.2, 0.45, railMaterial));
        scene.Children.Add(CreateBox(-10.2, 4.6, 8.4, 0.45, 9.2, 0.45, railMaterial));
        scene.Children.Add(CreateBox(10.2, 4.6, -8.4, 0.45, 9.2, 0.45, railMaterial));
        scene.Children.Add(CreateBox(10.2, 4.6, 8.4, 0.45, 9.2, 0.45, railMaterial));
        scene.Children.Add(CreateBox(-10.2, 8.9, 0, 0.4, 0.35, 17.5, railMaterial));
        scene.Children.Add(CreateBox(10.2, 8.9, 0, 0.4, 0.35, 17.5, railMaterial));
        scene.Children.Add(CreateBox(0, 8.9, -8.4, 20.8, 0.35, 0.4, railMaterial));
        scene.Children.Add(CreateBox(0, 8.9, 8.4, 20.8, 0.35, 0.4, railMaterial));

        Model3DGroup bridge = new();
        bridge.Children.Add(CreateBox(0, 8.45, 0, 20.4, 0.65, 0.65, craneMaterial));
        bridge.Children.Add(CreateBox(0, 7.95, 0, 20.4, 0.28, 0.35, darkMaterial));
        bridge.Transform = bridgeTransform;
        scene.Children.Add(bridge);

        Model3DGroup trolley = new();
        trolley.Children.Add(CreateBox(0, 8.05, 0, 1.4, 0.75, 1.25, darkMaterial));
        trolley.Children.Add(CreateBox(0, 7.55, 0, 0.8, 0.35, 0.8, craneMaterial));
        trolley.Transform = trolleyTransform;
        scene.Children.Add(trolley);

        Model3DGroup cable = new();
        cable.Children.Add(CreateBox(0, 0, 0, 0.12, 1, 0.12, darkMaterial));
        Transform3DGroup cableTransforms = new();
        cableTransforms.Children.Add(cableScale);
        cableTransforms.Children.Add(cableTransform);
        cable.Transform = cableTransforms;
        scene.Children.Add(cable);

        Model3DGroup magnet = new();
        magnet.Children.Add(CreateCylinder(0.75, 0.32, Color.FromRgb(70, 75, 80), 24));
        magnet.Children.Add(CreateBox(0, 0.38, 0, 0.7, 0.45, 0.7, darkMaterial));
        magnet.Transform = magnetTransform;
        scene.Children.Add(magnet);

        loadModel = CreateIrregularLoad(Color.FromRgb(205, 143, 57));
        loadGroup.Children.Add(loadModel);
        loadGroup.Transform = loadTransform;
        scene.Children.Add(loadGroup);
        SetLoadVisible(false);
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
            await MoveCraneAsync(sourcePoint, speedFactor);

            TxtSimulationStatus.Text = $"Materialaufnahme an {source}";
            await MoveHoistAsync(sourcePoint.PickY, speedFactor);
            SetLoadVisible(true);
            await PauseAsync(300, speedFactor);
            await MoveHoistAsync(RaisedY, speedFactor);

            TxtSimulationStatus.Text = $"Transport zu {target}";
            await MoveCraneAsync(targetPoint, speedFactor);

            TxtSimulationStatus.Text = $"Materialabgabe an {target}";
            await MoveHoistAsync(targetPoint.PickY, speedFactor);
            SetLoadVisible(false);
            await PauseAsync(300, speedFactor);
            await MoveHoistAsync(RaisedY, speedFactor);

            TxtSimulationStatus.Text = $"Abgeschlossen: {source} → {target}";
        }
        finally
        {
            isRunning = false;
            SetControlsEnabled(true);
        }
    }

    private void BtnResetSimulation_Click(object sender, RoutedEventArgs e)
    {
        if (!isRunning)
        {
            ResetCrane();
        }
    }

    private void BtnChangeView_Click(object sender, RoutedEventArgs e)
    {
        cameraIndex = (cameraIndex + 1) % 3;

        (Point3D position, Vector3D lookDirection) = cameraIndex switch
        {
            1 => (new Point3D(24, 20, 28), new Vector3D(-24, -14, -28)),
            2 => (new Point3D(0, 25, 0.1), new Vector3D(0, -25, -0.1)),
            _ => CreateDefaultCameraPosition()
        };

        SceneCamera.Position = position;
        SceneCamera.LookDirection = lookDirection;
        SceneCamera.UpDirection = cameraIndex == 2
            ? new Vector3D(0, 0, -1)
            : new Vector3D(0, 1, 0);
    }

    private void SetDefaultCamera()
    {
        (Point3D position, Vector3D lookDirection) = CreateDefaultCameraPosition();
        SceneCamera.Position = position;
        SceneCamera.LookDirection = lookDirection;
        SceneCamera.UpDirection = new Vector3D(0, 1, 0);
    }

    private static (Point3D Position, Vector3D LookDirection) CreateDefaultCameraPosition()
    {
        var position = new Point3D(0, DefaultCameraHeight, DefaultCameraDistance);
        var target = new Point3D(0, DefaultCameraTargetHeight, 0);
        return (position, target - position);
    }

    private async Task MoveCraneAsync(CranePoint point, double speedFactor)
    {
        double distance = Math.Sqrt(
            Math.Pow(point.X - trolleyTransform.OffsetX, 2)
            + Math.Pow(point.Z - trolleyTransform.OffsetZ, 2));
        double seconds = Math.Max(0.45, distance / 6.5) * speedFactor;

        await Task.WhenAll(
            AnimateAsync(bridgeTransform, TranslateTransform3D.OffsetZProperty, point.Z, seconds),
            AnimateAsync(trolleyTransform, TranslateTransform3D.OffsetXProperty, point.X, seconds),
            AnimateAsync(trolleyTransform, TranslateTransform3D.OffsetZProperty, point.Z, seconds),
            AnimateAsync(magnetTransform, TranslateTransform3D.OffsetXProperty, point.X, seconds),
            AnimateAsync(magnetTransform, TranslateTransform3D.OffsetZProperty, point.Z, seconds),
            AnimateAsync(cableTransform, TranslateTransform3D.OffsetXProperty, point.X, seconds),
            AnimateAsync(cableTransform, TranslateTransform3D.OffsetZProperty, point.Z, seconds),
            AnimateAsync(loadTransform, TranslateTransform3D.OffsetXProperty, point.X, seconds),
            AnimateAsync(loadTransform, TranslateTransform3D.OffsetZProperty, point.Z, seconds));
    }

    private async Task MoveHoistAsync(double targetY, double speedFactor)
    {
        double cableLength = 7.55 - targetY;
        double cableCenterY = targetY + cableLength / 2;
        double seconds = Math.Max(0.35, Math.Abs(targetY - magnetTransform.OffsetY) / 3.5) * speedFactor;

        await Task.WhenAll(
            AnimateAsync(magnetTransform, TranslateTransform3D.OffsetYProperty, targetY, seconds),
            AnimateAsync(loadTransform, TranslateTransform3D.OffsetYProperty, targetY - 0.55, seconds),
            AnimateAsync(cableScale, ScaleTransform3D.ScaleYProperty, cableLength, seconds),
            AnimateAsync(cableTransform, TranslateTransform3D.OffsetYProperty, cableCenterY, seconds));
    }

    private void ResetCrane()
    {
        StopAnimations(bridgeTransform);
        StopAnimations(trolleyTransform);
        StopAnimations(magnetTransform);
        StopAnimations(cableTransform);
        StopAnimations(cableScale);
        StopAnimations(loadTransform);

        bridgeTransform.OffsetZ = HomeZ;
        trolleyTransform.OffsetX = HomeX;
        trolleyTransform.OffsetZ = HomeZ;
        magnetTransform.OffsetX = HomeX;
        magnetTransform.OffsetY = RaisedY;
        magnetTransform.OffsetZ = HomeZ;
        cableScale.ScaleY = 7.55 - RaisedY;
        cableTransform.OffsetX = HomeX;
        cableTransform.OffsetY = RaisedY + cableScale.ScaleY / 2;
        cableTransform.OffsetZ = HomeZ;
        loadTransform.OffsetX = HomeX;
        loadTransform.OffsetY = RaisedY - 0.55;
        loadTransform.OffsetZ = HomeZ;
        SetLoadVisible(false);
        TxtSimulationStatus.Text = "Bereit";
    }

    private void SetLoadVisible(bool visible)
    {
        if (loadModel is null)
        {
            return;
        }

        bool isVisible = loadGroup.Children.Contains(loadModel);
        if (visible && !isVisible)
        {
            loadGroup.Children.Add(loadModel);
        }
        else if (!visible && isVisible)
        {
            loadGroup.Children.Remove(loadModel);
        }
    }

    private static Task AnimateAsync(
        Animatable target,
        DependencyProperty property,
        double value,
        double seconds)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var animation = new DoubleAnimation
        {
            To = value,
            Duration = TimeSpan.FromSeconds(seconds),
            AccelerationRatio = 0.18,
            DecelerationRatio = 0.18,
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

    private static void StopAnimations(Animatable target)
    {
        target.BeginAnimation(TranslateTransform3D.OffsetXProperty, null);
        target.BeginAnimation(TranslateTransform3D.OffsetYProperty, null);
        target.BeginAnimation(TranslateTransform3D.OffsetZProperty, null);
        target.BeginAnimation(ScaleTransform3D.ScaleYProperty, null);
    }

    private static GeometryModel3D CreateBox(
        double centerX,
        double centerY,
        double centerZ,
        double sizeX,
        double sizeY,
        double sizeZ,
        Color color)
    {
        return CreateBox(centerX, centerY, centerZ, sizeX, sizeY, sizeZ, CreateMaterial(color));
    }

    private static GeometryModel3D CreateBox(
        double centerX,
        double centerY,
        double centerZ,
        double sizeX,
        double sizeY,
        double sizeZ,
        Material material)
    {
        double x = sizeX / 2;
        double y = sizeY / 2;
        double z = sizeZ / 2;
        Point3D[] points =
        [
            new(centerX - x, centerY - y, centerZ - z),
            new(centerX + x, centerY - y, centerZ - z),
            new(centerX + x, centerY + y, centerZ - z),
            new(centerX - x, centerY + y, centerZ - z),
            new(centerX - x, centerY - y, centerZ + z),
            new(centerX + x, centerY - y, centerZ + z),
            new(centerX + x, centerY + y, centerZ + z),
            new(centerX - x, centerY + y, centerZ + z)
        ];
        int[] triangles =
        [
            0,2,1, 0,3,2, 1,2,6, 1,6,5,
            5,6,7, 5,7,4, 4,7,3, 4,3,0,
            3,7,6, 3,6,2, 4,0,1, 4,1,5
        ];

        var mesh = new MeshGeometry3D
        {
            Positions = new Point3DCollection(points),
            TriangleIndices = new Int32Collection(triangles)
        };

        return new GeometryModel3D(mesh, material)
        {
            BackMaterial = material
        };
    }

    private static GeometryModel3D CreateCylinder(
        double radius,
        double height,
        Color color,
        int segments)
    {
        var positions = new Point3DCollection();
        var triangles = new Int32Collection();

        for (int i = 0; i < segments; i++)
        {
            double angle = i * Math.PI * 2 / segments;
            positions.Add(new Point3D(Math.Cos(angle) * radius, 0, Math.Sin(angle) * radius));
            positions.Add(new Point3D(Math.Cos(angle) * radius, -height, Math.Sin(angle) * radius));
        }

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            int top = i * 2;
            int bottom = top + 1;
            int nextTop = next * 2;
            int nextBottom = nextTop + 1;
            triangles.Add(top);
            triangles.Add(nextTop);
            triangles.Add(bottom);
            triangles.Add(bottom);
            triangles.Add(nextTop);
            triangles.Add(nextBottom);
        }

        var mesh = new MeshGeometry3D
        {
            Positions = positions,
            TriangleIndices = triangles
        };
        Material material = CreateMaterial(color);
        return new GeometryModel3D(mesh, material) { BackMaterial = material };
    }

    private static GeometryModel3D CreateIrregularLoad(Color color)
    {
        GeometryModel3D load = CreateBox(0, 0, 0, 1.15, 0.6, 1.0, color);
        load.Transform = new RotateTransform3D(
            new AxisAngleRotation3D(new Vector3D(0, 1, 0), 18));
        return load;
    }

    private static GeometryModel3D CreateLabelPlate(
        string text,
        double x,
        double y,
        double z,
        double width,
        double height)
    {
        var label = new TextBlock
        {
            Text = text,
            Foreground = Brushes.White,
            Background = Brushes.Transparent,
            FontWeight = FontWeights.SemiBold,
            FontSize = 22,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 220,
            Height = 58
        };
        label.Measure(new Size(220, 58));
        label.Arrange(new Rect(0, 0, 220, 58));

        var brush = new VisualBrush(label);
        Material material = new DiffuseMaterial(brush);
        double halfWidth = width / 2;
        double halfHeight = height / 2;
        var mesh = new MeshGeometry3D
        {
            Positions = new Point3DCollection
            {
                new(x - halfWidth, y - halfHeight, z),
                new(x + halfWidth, y - halfHeight, z),
                new(x + halfWidth, y + halfHeight, z),
                new(x - halfWidth, y + halfHeight, z)
            },
            TextureCoordinates = new PointCollection
            {
                new(0, 1), new(1, 1), new(1, 0), new(0, 0)
            },
            TriangleIndices = new Int32Collection { 0, 1, 2, 0, 2, 3 }
        };

        return new GeometryModel3D(mesh, material) { BackMaterial = material };
    }

    private static Material CreateMaterial(Color color)
    {
        var group = new MaterialGroup();
        group.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
        group.Children.Add(new SpecularMaterial(new SolidColorBrush(Colors.White), 22));
        return group;
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
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
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

    private readonly record struct CranePoint(double X, double Z, double PickY);
}
