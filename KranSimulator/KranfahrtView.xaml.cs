using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;
using Microsoft.Data.SqlClient;

namespace KranSimulator;

public partial class KranfahrtView : UserControl
{
    private const double CraneRailHeight = 8.0;
    private const double HoistAnchorY = 7.35;
    private const double RaisedY = 6.55;
    private const double BoxWallHeight = 5.0;
    private const double BoxWallThickness = 0.18;
    private const double StorageWidth = 25.0;
    private const double StorageDepth = 10.0;
    private const double LeftStorageWidth = 8.0;
    private const double RightStorageColumnWidth = 8.5;
    private const double LeftStorageRowDepth = 2.5;
    private const double RightStorageRowDepth = StorageDepth / 3.0;
    private const double HomeX = 0.0;
    private const double HomeZ = 0.0;
    private const double DefaultCameraHeight = 52.0;
    private const double DefaultCameraDistance = 38.0;
    private const double DefaultCameraTargetHeight = 1.8;

    private readonly TranslateTransform3D bridgeTransform = new();
    private readonly TranslateTransform3D trolleyTransform = new();
    private readonly TranslateTransform3D magnetTransform = new();
    private readonly TranslateTransform3D cableTransform = new();
    private readonly ScaleTransform3D cableScale = new(1, HoistAnchorY - RaisedY, 1);
    private readonly TranslateTransform3D loadTransform = new();
    private readonly Model3DGroup scene = new();
    private readonly Model3DGroup loadGroup = new();
    private readonly IReadOnlyDictionary<string, string> positionLabels;
    private GeometryModel3D? loadModel;
    private bool isRunning;
    private int cameraIndex;

    private static readonly IReadOnlyDictionary<string, CranePoint> CranePoints =
        new Dictionary<string, CranePoint>(StringComparer.OrdinalIgnoreCase)
        {
            ["LKW1"] = new(-8, -9, 1.2),
            ["LKW2"] = new(0, -9, 1.2),
            ["LKW3"] = new(8, -9, 1.2),
            ["BOX 1"] = new(-8.5, 3.75, 1.0),
            ["BOX 2"] = new(-0.25, 10.0 / 3.0, 1.0),
            ["BOX 3"] = new(8.25, 10.0 / 3.0, 1.0),
            ["BOX 4"] = new(-8.5, 1.25, 1.0),
            ["BOX 5"] = new(-0.25, 0, 1.0),
            ["BOX 6"] = new(8.25, 0, 1.0),
            ["BOX 7"] = new(-8.5, -1.25, 1.0),
            ["BOX 8"] = new(-0.25, -10.0 / 3.0, 1.0),
            ["BOX 9"] = new(8.25, -10.0 / 3.0, 1.0),
            ["BOX 10"] = new(-8.5, -3.75, 1.0),
            ["CW1"] = new(-8, 9, 1.25),
            ["CW2"] = new(0, 9, 1.25),
            ["CW3"] = new(8, 9, 1.25)
        };

    private static readonly IReadOnlyDictionary<int, StorageBoxLayout> StorageBoxes =
        new Dictionary<int, StorageBoxLayout>
        {
            [1] = new(-8.5, 3.75, LeftStorageWidth, LeftStorageRowDepth),
            [2] = new(-0.25, 10.0 / 3.0, RightStorageColumnWidth, RightStorageRowDepth),
            [3] = new(8.25, 10.0 / 3.0, RightStorageColumnWidth, RightStorageRowDepth),
            [4] = new(-8.5, 1.25, LeftStorageWidth, LeftStorageRowDepth),
            [5] = new(-0.25, 0, RightStorageColumnWidth, RightStorageRowDepth),
            [6] = new(8.25, 0, RightStorageColumnWidth, RightStorageRowDepth),
            [7] = new(-8.5, -1.25, LeftStorageWidth, LeftStorageRowDepth),
            [8] = new(-0.25, -10.0 / 3.0, RightStorageColumnWidth, RightStorageRowDepth),
            [9] = new(8.25, -10.0 / 3.0, RightStorageColumnWidth, RightStorageRowDepth),
            [10] = new(-8.5, -3.75, LeftStorageWidth, LeftStorageRowDepth)
        };

    public KranfahrtView()
    {
        InitializeComponent();
        positionLabels = LoadPositionLabels();
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
        AddScrapPile(-8, -9, GetPositionLabel("LKW_PLATZ", 1, "LKW1"), 0);
        AddScrapPile(0, -9, GetPositionLabel("LKW_PLATZ", 2, "LKW2"), 1);
        AddScrapPile(8, -9, GetPositionLabel("LKW_PLATZ", 3, "LKW3"), 2);
    }

    private void AddScrapPile(
        double x,
        double z,
        string label,
        int colorOffset)
    {
        Color[] scrapColors =
        [
            Color.FromRgb(107, 91, 75),
            Color.FromRgb(130, 78, 48),
            Color.FromRgb(83, 91, 94),
            Color.FromRgb(151, 112, 68),
            Color.FromRgb(72, 78, 80)
        ];
        (double X, double Z, double Width, double Height, double Depth, double Angle)[] pieces =
        [
            (-1.25, -0.45, 1.45, 0.55, 0.75, 14),
            (-0.25, -0.72, 1.15, 0.75, 0.65, -21),
            (0.95, -0.48, 1.55, 0.62, 0.72, 8),
            (-1.05, 0.34, 1.10, 0.82, 0.72, -12),
            (0.05, 0.18, 1.55, 1.02, 0.82, 19),
            (1.15, 0.38, 1.05, 0.78, 0.72, -17),
            (-0.55, 0.78, 1.25, 0.68, 0.62, 29),
            (0.62, 0.82, 1.35, 0.72, 0.68, -8),
            (-0.05, -0.05, 0.85, 1.28, 0.72, 41)
        ];

        for (int index = 0; index < pieces.Length; index++)
        {
            var piece = pieces[index];
            GeometryModel3D scrapPiece = CreateBox(
                x + piece.X,
                piece.Height / 2,
                z + piece.Z,
                piece.Width,
                piece.Height,
                piece.Depth,
                scrapColors[(index + colorOffset) % scrapColors.Length]);
            scrapPiece.Transform = new RotateTransform3D(
                new AxisAngleRotation3D(new Vector3D(0, 1, 0), piece.Angle),
                new Point3D(x + piece.X, piece.Height / 2, z + piece.Z));
            scene.Children.Add(scrapPiece);
        }

        scene.Children.Add(CreateLabelPlate(
            label,
            x,
            2.35,
            z - 2.15,
            6.2,
            1.45,
            68));
    }

    private void AddStorageBoxes()
    {
        Material steelMaterial = CreateMaterial(Color.FromRgb(104, 116, 120));
        Material darkSteelMaterial = CreateMaterial(Color.FromRgb(69, 80, 84));

        double leftEdge = -StorageWidth / 2;
        double rightEdge = StorageWidth / 2;
        double backEdge = -StorageDepth / 2;
        double frontEdge = StorageDepth / 2;
        double leftDividerX = leftEdge + LeftStorageWidth;
        double rightDividerX = leftDividerX + RightStorageColumnWidth;

        // Außenwände der unverändert großen Lagerfläche.
        AddStorageWallX(leftEdge, 0, StorageDepth, darkSteelMaterial);
        AddStorageWallX(rightEdge, 0, StorageDepth, darkSteelMaterial);
        AddStorageWallZ(0, backEdge, StorageWidth, darkSteelMaterial);
        AddStorageWallZ(0, frontEdge, StorageWidth, darkSteelMaterial);

        // Unterteilung exakt nach Anlagenplan:
        // links vier Boxen, rechts zwei Spalten mit jeweils drei Boxen.
        AddStorageWallX(leftDividerX, 0, StorageDepth, steelMaterial);
        AddStorageWallX(rightDividerX, 0, StorageDepth, steelMaterial);

        for (int divider = 1; divider <= 3; divider++)
        {
            double z = backEdge + divider * LeftStorageRowDepth;
            AddStorageWallZ(
                leftEdge + LeftStorageWidth / 2,
                z,
                LeftStorageWidth,
                steelMaterial);
        }

        for (int divider = 1; divider <= 2; divider++)
        {
            double z = backEdge + divider * RightStorageRowDepth;
            AddStorageWallZ(
                leftDividerX + (StorageWidth - LeftStorageWidth) / 2,
                z,
                StorageWidth - LeftStorageWidth,
                steelMaterial);
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
            StorageBoxLayout box = StorageBoxes[index];
            double fillHeight = 0.55 + (index % 4) * 0.22;
            scene.Children.Add(CreateBox(
                box.X,
                fillHeight / 2,
                box.Z,
                box.Width - 0.45,
                fillHeight,
                box.Depth - 0.45,
                scrapColors[(index - 1) % scrapColors.Length]));

            scene.Children.Add(CreateLabelPlate(
                GetPositionLabel("LAGERBOX", index, $"BOX {index}"),
                box.X,
                4.25,
                box.Z + box.Depth / 2 + 0.12,
                Math.Min(box.Width - 0.35, 6.4),
                1.35,
                76));
        }
    }

    private void AddStorageWallX(
        double x,
        double z,
        double depth,
        Material material)
    {
        scene.Children.Add(CreateBox(
            x,
            BoxWallHeight / 2,
            z,
            BoxWallThickness,
            BoxWallHeight,
            depth + BoxWallThickness,
            material));
    }

    private void AddStorageWallZ(
        double x,
        double z,
        double width,
        Material material)
    {
        scene.Children.Add(CreateBox(
            x,
            BoxWallHeight / 2,
            z,
            width + BoxWallThickness,
            BoxWallHeight,
            BoxWallThickness,
            material));
    }

    private void AddChargingCars()
    {
        AddChargingCar(-8, 9, GetPositionLabel("CHARGIERWAGEN", 1, "CW1"));
        AddChargingCar(0, 9, GetPositionLabel("CHARGIERWAGEN", 2, "CW2"));
        AddChargingCar(8, 9, GetPositionLabel("CHARGIERWAGEN", 3, "CW3"));
    }

    private void AddChargingCar(double x, double z, string label)
    {
        Material frameMaterial = CreateMaterial(Color.FromRgb(37, 72, 89));
        Material bodyMaterial = CreateMaterial(Color.FromRgb(69, 137, 174));
        Material edgeMaterial = CreateMaterial(Color.FromRgb(105, 174, 207));
        Material innerMaterial = CreateMaterial(Color.FromRgb(29, 49, 58));
        Material driveMaterial = CreateMaterial(Color.FromRgb(56, 61, 64));
        Material warningMaterial = CreateMaterial(Color.FromRgb(220, 155, 40));

        // Grundrahmen und langer Beschickungstrog.
        scene.Children.Add(CreateBox(x, 0.28, z, 4.1, 0.45, 6.4, frameMaterial));
        scene.Children.Add(CreateBox(x, 0.82, z, 3.55, 0.85, 5.85, bodyMaterial));
        scene.Children.Add(CreateBox(x, 1.28, z, 2.75, 0.18, 5.15, innerMaterial));

        // Umlaufende Oberkante und sichtbare Querstege wie bei einer schweren Industrieanlage.
        scene.Children.Add(CreateBox(x - 1.72, 1.42, z, 0.18, 0.22, 5.95, edgeMaterial));
        scene.Children.Add(CreateBox(x + 1.72, 1.42, z, 0.18, 0.22, 5.95, edgeMaterial));
        scene.Children.Add(CreateBox(x, 1.42, z - 2.92, 3.6, 0.22, 0.18, edgeMaterial));
        scene.Children.Add(CreateBox(x, 1.42, z + 2.92, 3.6, 0.22, 0.18, edgeMaterial));

        foreach (double ribZ in new[] { -1.95, -0.98, 0.0, 0.98, 1.95 })
        {
            scene.Children.Add(CreateBox(x, 1.48, z + ribZ, 3.5, 0.18, 0.14, edgeMaterial));
        }

        // Einlaufgehäuse, Antrieb und seitliche Maschinenkästen.
        scene.Children.Add(CreateBox(x, 1.12, z - 2.45, 3.25, 1.15, 0.85, bodyMaterial));
        scene.Children.Add(CreateBox(x - 1.38, 0.92, z - 2.3, 0.85, 1.35, 1.2, frameMaterial));
        scene.Children.Add(CreateBox(x + 1.45, 0.72, z + 1.95, 0.72, 0.85, 1.25, driveMaterial));
        scene.Children.Add(CreateBox(x + 1.48, 1.28, z + 1.95, 0.48, 0.28, 0.72, warningMaterial));

        // Bodenfüße geben dem Wagen in der Draufsicht eine klar technische Silhouette.
        foreach (double footX in new[] { -1.55, 1.55 })
        {
            foreach (double footZ in new[] { -2.45, 2.45 })
            {
                scene.Children.Add(CreateBox(
                    x + footX,
                    0.18,
                    z + footZ,
                    0.65,
                    0.35,
                    0.85,
                    driveMaterial));
            }
        }

        scene.Children.Add(CreateLabelPlate(
            label,
            x,
            2.25,
            z - 3.15,
            5.6,
            1.25,
            56));
    }

    private static IReadOnlyDictionary<string, string> LoadPositionLabels()
    {
        const string sql = """
            SELECT PositionsTyp, PositionsNr, Bezeichnung
            FROM dbo.FALCOM_KRAN_POSITION
            WHERE PositionsTyp IN (N'LKW_PLATZ', N'LAGERBOX', N'CHARGIERWAGEN');
            """;

        try
        {
            var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using var connection = new SqlConnection(DatabaseConfig.LoadConnectionString());
            using var command = new SqlCommand(sql, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string positionType = reader.GetString(0);
                int positionNumber = reader.GetInt32(1);
                string description = reader.GetString(2);

                if (!string.IsNullOrWhiteSpace(description))
                {
                    labels[CreatePositionLabelKey(positionType, positionNumber)] = description.Trim();
                }
            }

            return labels;
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private string GetPositionLabel(
        string positionType,
        int positionNumber,
        string fallback)
    {
        return positionLabels.TryGetValue(
            CreatePositionLabelKey(positionType, positionNumber),
            out string? label)
            ? label
            : fallback;
    }

    private static string CreatePositionLabelKey(
        string positionType,
        int positionNumber)
    {
        return $"{positionType}:{positionNumber}";
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
        double height,
        double fontSize = 22)
    {
        var label = new TextBlock
        {
            Text = text,
            Foreground = Brushes.White,
            Background = Brushes.Transparent,
            FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = fontSize,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 800,
            Height = 120
        };
        label.Measure(new Size(800, 120));
        label.Arrange(new Rect(0, 0, 800, 120));

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

    private readonly record struct StorageBoxLayout(
        double X,
        double Z,
        double Width,
        double Depth);
}
