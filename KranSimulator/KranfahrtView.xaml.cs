using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;

namespace KranSimulator;

public partial class KranfahrtView : UserControl
{
    private const double CraneRailHeight = 8.0;
    private const double HoistAnchorY = 7.35;
    private const double RaisedY = 6.55;
    private const double BoxSize = 5.0;
    private const double BoxWallHeight = 5.0;
    private const double BoxWallThickness = 0.18;
    private const double HomeX = 0.0;
    private const double HomeZ = 0.0;
    private const double DefaultCameraHeight = 26.0;
    private const double DefaultCameraDistance = 40.0;
    private const double DefaultCameraTargetHeight = 2.5;

    private readonly TranslateTransform3D bridgeTransform = new();
    private readonly TranslateTransform3D trolleyTransform = new();
    private readonly TranslateTransform3D magnetTransform = new();
    private readonly TranslateTransform3D cableTransform = new();
    private readonly ScaleTransform3D cableScale = new(1, HoistAnchorY - RaisedY, 1);
    private readonly TranslateTransform3D loadTransform = new();
    private readonly Model3DGroup scene = new();
    private readonly Model3DGroup loadGroup = new();
    private GeometryModel3D? loadModel;
    private bool isRunning;
    private int cameraIndex;

    private static readonly IReadOnlyDictionary<string, CranePoint> CranePoints =
        new Dictionary<string, CranePoint>(StringComparer.OrdinalIgnoreCase)
        {
            ["LKW1"] = new(-8, -9, 1.2),
            ["LKW2"] = new(0, -9, 1.2),
            ["LKW3"] = new(8, -9, 1.2),
            ["BOX 1"] = new(-10, -2.5, 1.0),
            ["BOX 2"] = new(-5, -2.5, 1.0),
            ["BOX 3"] = new(0, -2.5, 1.0),
            ["BOX 4"] = new(5, -2.5, 1.0),
            ["BOX 5"] = new(10, -2.5, 1.0),
            ["BOX 6"] = new(-10, 2.5, 1.0),
            ["BOX 7"] = new(-5, 2.5, 1.0),
            ["BOX 8"] = new(0, 2.5, 1.0),
            ["BOX 9"] = new(5, 2.5, 1.0),
            ["BOX 10"] = new(10, 2.5, 1.0),
            ["CW1"] = new(-8, 9, 1.25),
            ["CW2"] = new(0, 9, 1.25),
            ["CW3"] = new(8, 9, 1.25)
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

        scene.Children.Add(CreateBox(0, -0.25, 0, 30, 0.5, 24, Color.FromRgb(72, 76, 79)));
        AddFloorMarkings();
        AddTrucks();
        AddStorageBoxes();
        AddChargingCars();
        AddCrane();

        SceneVisual.Content = scene;
    }

    private void AddFloorMarkings()
    {
        Material lineMaterial = CreateMaterial(Color.FromRgb(108, 112, 114));

        for (int x = -14; x <= 14; x += 2)
        {
            scene.Children.Add(CreateBox(x, 0.02, 0, 0.035, 0.025, 23.5, lineMaterial));
        }

        for (int z = -11; z <= 11; z += 2)
        {
            scene.Children.Add(CreateBox(0, 0.025, z, 29.5, 0.03, 0.035, lineMaterial));
        }
    }

    private void AddTrucks()
    {
        AddTruck(-8, -9, "LKW1");
        AddTruck(0, -9, "LKW2");
        AddTruck(8, -9, "LKW3");
    }

    private void AddTruck(double x, double z, string label)
    {
        scene.Children.Add(CreateBox(x, 0.65, z, 4.2, 1.1, 2.0, Color.FromRgb(59, 71, 78)));
        scene.Children.Add(CreateBox(x + 1.35, 1.35, z, 1.25, 1.4, 1.9, Color.FromRgb(34, 43, 49)));
        scene.Children.Add(CreateBox(x - 0.6, 1.25, z, 2.4, 0.7, 1.65, Color.FromRgb(130, 145, 151)));
        scene.Children.Add(CreateLabelPlate(label, x, 1.92, z - 1.03, 2.2, 0.65));
    }

    private void AddStorageBoxes()
    {
        Material steelMaterial = CreateMaterial(Color.FromRgb(104, 116, 120));
        Material darkSteelMaterial = CreateMaterial(Color.FromRgb(69, 80, 84));

        double blockWidth = BoxSize * 5;
        double blockDepth = BoxSize * 2;

        // Shared walls create ten contiguous, open-top boxes without gaps.
        for (int wallIndex = 0; wallIndex <= 5; wallIndex++)
        {
            double wallX = -blockWidth / 2 + wallIndex * BoxSize;
            scene.Children.Add(CreateBox(
                wallX,
                BoxWallHeight / 2,
                0,
                BoxWallThickness,
                BoxWallHeight,
                blockDepth + BoxWallThickness,
                wallIndex is 0 or 5 ? darkSteelMaterial : steelMaterial));
        }

        for (int wallIndex = 0; wallIndex <= 2; wallIndex++)
        {
            double wallZ = -blockDepth / 2 + wallIndex * BoxSize;
            scene.Children.Add(CreateBox(
                0,
                BoxWallHeight / 2,
                wallZ,
                blockWidth + BoxWallThickness,
                BoxWallHeight,
                BoxWallThickness,
                wallIndex is 0 or 2 ? darkSteelMaterial : steelMaterial));
        }

        Color[] scrapColors =
        [
            Color.FromRgb(116, 80, 55),
            Color.FromRgb(139, 93, 54),
            Color.FromRgb(96, 85, 71),
            Color.FromRgb(151, 103, 60)
        ];

        for (int index = 1; index <= 10; index++)
        {
            CranePoint point = CranePoints[$"BOX {index}"];
            double fillHeight = 0.55 + (index % 4) * 0.22;
            scene.Children.Add(CreateBox(
                point.X,
                fillHeight / 2,
                point.Z,
                BoxSize - 0.45,
                fillHeight,
                BoxSize - 0.45,
                scrapColors[(index - 1) % scrapColors.Length]));

            double labelZ = index <= 5 ? 0.12 : 5.12;
            scene.Children.Add(CreateLabelPlate(
                $"BOX {index}",
                point.X,
                4.25,
                labelZ,
                2.3,
                0.55));
        }
    }

    private void AddChargingCars()
    {
        AddChargingCar(-8, 9, "CW1");
        AddChargingCar(0, 9, "CW2");
        AddChargingCar(8, 9, "CW3");
    }

    private void AddChargingCar(double x, double z, string label)
    {
        scene.Children.Add(CreateBox(x, 0.7, z, 4.0, 1.25, 2.5, Color.FromRgb(43, 78, 94)));
        scene.Children.Add(CreateBox(x, 1.28, z, 3.3, 0.55, 1.9, Color.FromRgb(67, 113, 132)));
        scene.Children.Add(CreateLabelPlate(label, x, 1.8, z - 1.28, 1.8, 0.58));
    }

    private void AddCrane()
    {
        Material columnMaterial = CreateMaterial(Color.FromRgb(63, 75, 81));
        Material railMaterial = CreateMaterial(Color.FromRgb(37, 45, 49));
        Material craneMaterial = CreateMaterial(Color.FromRgb(226, 174, 44));
        Material darkMaterial = CreateMaterial(Color.FromRgb(36, 41, 44));

        const double runwayX = 13.35;
        const double runwayLength = 22.5;
        double columnHeight = CraneRailHeight;

        foreach (double x in new[] { -runwayX, runwayX })
        {
            foreach (double z in new[] { -10.5, 0.0, 10.5 })
            {
                scene.Children.Add(CreateBox(
                    x,
                    columnHeight / 2,
                    z,
                    0.55,
                    columnHeight,
                    0.55,
                    columnMaterial));
                scene.Children.Add(CreateBox(
                    x,
                    0.18,
                    z,
                    1.35,
                    0.35,
                    1.35,
                    darkMaterial));
            }

            scene.Children.Add(CreateBox(
                x,
                CraneRailHeight,
                0,
                0.75,
                0.65,
                runwayLength,
                railMaterial));
            scene.Children.Add(CreateBox(
                x,
                CraneRailHeight + 0.4,
                0,
                0.24,
                0.14,
                runwayLength,
                craneMaterial));
        }

        Model3DGroup bridge = new();
        bridge.Children.Add(CreateBox(0, CraneRailHeight + 0.65, 0, 27.0, 0.85, 0.75, craneMaterial));
        bridge.Children.Add(CreateBox(0, CraneRailHeight + 0.08, 0, 26.4, 0.25, 0.38, darkMaterial));
        bridge.Children.Add(CreateBox(-12.8, CraneRailHeight + 0.35, 0, 1.0, 0.65, 1.25, darkMaterial));
        bridge.Children.Add(CreateBox(12.8, CraneRailHeight + 0.35, 0, 1.0, 0.65, 1.25, darkMaterial));
        bridge.Transform = bridgeTransform;
        scene.Children.Add(bridge);

        Model3DGroup trolley = new();
        trolley.Children.Add(CreateBox(0, CraneRailHeight + 0.18, 0, 1.65, 0.75, 1.45, darkMaterial));
        trolley.Children.Add(CreateBox(0, HoistAnchorY + 0.22, 0, 0.9, 0.45, 0.9, craneMaterial));
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
        magnet.Children.Add(CreateCylinder(0.82, 0.34, Color.FromRgb(57, 64, 68), 24));
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
        double cableLength = HoistAnchorY - targetY;
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
        cableScale.ScaleY = HoistAnchorY - RaisedY;
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
