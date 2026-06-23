namespace KranSimulator;

public sealed class CraneSimulation
{
    public const int MinPosKranX = 1_000;
    public const int MaxPosKranX = 24_000;
    public const int MinPosKatzeY = 500;
    public const int MaxPosKatzeY = 23_500;
    public const int MinPosHubZ = 200;
    public const int MaxPosHubZ = 8_500;

    public const double MinSceneX = -13.35;
    public const double MaxSceneX = 13.35;
    public const double MinSceneY = -16.0;
    public const double MaxSceneY = 16.0;
    public const double RaisedSceneZ = 6.55;
    public const double LoweredSceneZ = 0.20;

    private const double HorizontalSpeedMmPerSecond = 2_000;
    private const double HoistSpeedMmPerSecond = 4_500;
    private static readonly TimeSpan StatusInterval = TimeSpan.FromMilliseconds(33);

    private readonly Dictionary<string, CraneTargetPosition> targetPositions;
    private TaskCompletionSource pauseCompletion = CreateCompletedPauseCompletion();
    private CraneStatus status;

    public CraneSimulation()
    {
        targetPositions = new Dictionary<string, CraneTargetPosition>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["LKW1"] = CreateTarget("LKW1", -8, -14, 1.20),
            ["LKW2"] = CreateTarget("LKW2", 0, -14, 1.20),
            ["LKW3"] = CreateTarget("LKW3", 8, -14, 1.20),
            ["BOX 1"] = CreateTarget("BOX 1", -5, 5.625, 1.00),
            ["BOX 2"] = CreateTarget("BOX 2", 0, 5, 1.00),
            ["BOX 3"] = CreateTarget("BOX 3", 5, 5, 1.00),
            ["BOX 4"] = CreateTarget("BOX 4", -5, 1.875, 1.00),
            ["BOX 5"] = CreateTarget("BOX 5", 0, 0, 1.00),
            ["BOX 6"] = CreateTarget("BOX 6", 5, 0, 1.00),
            ["BOX 7"] = CreateTarget("BOX 7", -5, -1.875, 1.00),
            ["BOX 8"] = CreateTarget("BOX 8", 0, -5, 1.00),
            ["BOX 9"] = CreateTarget("BOX 9", 5, -5, 1.00),
            ["BOX 10"] = CreateTarget("BOX 10", -5, -5.625, 1.00),
            ["CW1"] = CreateTarget("CW1", -8, 14, 1.25),
            ["CW2"] = CreateTarget("CW2", 0, 14, 1.25),
            ["CW3"] = CreateTarget("CW3", 8, 14, 1.25)
        };

        status = new CraneStatus(
            MapSceneXToMillimeters(0),
            MapSceneYToMillimeters(0),
            MinPosHubZ);
    }

    public event EventHandler<CraneStatus>? StatusChanged;

    public CraneStatus Status => status;

    public bool IsPaused { get; private set; }

    public IReadOnlyDictionary<string, CraneTargetPosition> TargetPositions =>
        targetPositions;

    public bool TryGetTarget(
        string name,
        out CraneTargetPosition position)
    {
        return targetPositions.TryGetValue(name, out position);
    }

    public async Task MoveHorizontalAsync(
        CraneTargetPosition target,
        double speedFactor,
        CancellationToken cancellationToken = default)
    {
        CraneStatus start = status;
        double distance = Math.Sqrt(
            Math.Pow(target.PosKranX - start.PosKranX, 2)
            + Math.Pow(target.PosKatzeY - start.PosKatzeY, 2));
        double seconds = Math.Max(0.45, distance / HorizontalSpeedMmPerSecond)
            * speedFactor;

        await InterpolateAsync(
            start,
            start with
            {
                PosKranX = target.PosKranX,
                PosKatzeY = target.PosKatzeY
            },
            seconds,
            cancellationToken);
    }

    public async Task MoveHoistAsync(
        int targetPosHubZ,
        double speedFactor,
        CancellationToken cancellationToken = default)
    {
        targetPosHubZ = Math.Clamp(
            targetPosHubZ,
            MinPosHubZ,
            MaxPosHubZ);
        CraneStatus start = status;
        double seconds = Math.Max(
            0.35,
            Math.Abs(targetPosHubZ - start.PosHubZ) / HoistSpeedMmPerSecond)
            * speedFactor;

        await InterpolateAsync(
            start,
            start with { PosHubZ = targetPosHubZ },
            seconds,
            cancellationToken);
    }

    public void Pause()
    {
        if (IsPaused)
        {
            return;
        }

        IsPaused = true;
        pauseCompletion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public void Resume()
    {
        if (!IsPaused)
        {
            return;
        }

        IsPaused = false;
        pauseCompletion.TrySetResult();
    }

    public Task WaitWhilePausedAsync(CancellationToken cancellationToken = default)
    {
        return IsPaused
            ? pauseCompletion.Task.WaitAsync(cancellationToken)
            : Task.CompletedTask;
    }

    public void Reset()
    {
        Resume();
        SetStatus(new CraneStatus(
            MapSceneXToMillimeters(0),
            MapSceneYToMillimeters(0),
            MinPosHubZ));
    }

    public static double MapPosKranXToScene(int millimeters)
    {
        return Map(
            millimeters,
            MinPosKranX,
            MaxPosKranX,
            MinSceneX,
            MaxSceneX);
    }

    public static double MapPosKatzeYToScene(int millimeters)
    {
        return Map(
            millimeters,
            MinPosKatzeY,
            MaxPosKatzeY,
            MinSceneY,
            MaxSceneY);
    }

    public static double MapPosHubZToScene(int millimeters)
    {
        return Map(
            millimeters,
            MinPosHubZ,
            MaxPosHubZ,
            RaisedSceneZ,
            LoweredSceneZ);
    }

    private async Task InterpolateAsync(
        CraneStatus start,
        CraneStatus target,
        double seconds,
        CancellationToken cancellationToken)
    {
        int steps = Math.Max(
            1,
            (int)Math.Ceiling(
                TimeSpan.FromSeconds(seconds).TotalMilliseconds
                / StatusInterval.TotalMilliseconds));

        for (int step = 1; step <= steps; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WaitWhilePausedAsync(cancellationToken);
            double progress = (double)step / steps;
            double easedProgress = SmoothStep(progress);

            SetStatus(new CraneStatus(
                Interpolate(start.PosKranX, target.PosKranX, easedProgress),
                Interpolate(start.PosKatzeY, target.PosKatzeY, easedProgress),
                Interpolate(start.PosHubZ, target.PosHubZ, easedProgress)));

            if (step < steps)
            {
                await Task.Delay(StatusInterval, cancellationToken);
            }
        }
    }

    private void SetStatus(CraneStatus newStatus)
    {
        status = newStatus;
        StatusChanged?.Invoke(this, newStatus);
    }

    private static CraneTargetPosition CreateTarget(
        string name,
        double sceneX,
        double sceneY,
        double pickupSceneZ)
    {
        return new CraneTargetPosition(
            name,
            MapSceneXToMillimeters(sceneX),
            MapSceneYToMillimeters(sceneY),
            MapSceneZToMillimeters(pickupSceneZ));
    }

    private static int MapSceneXToMillimeters(double sceneX)
    {
        return (int)Math.Round(Map(
            sceneX,
            MinSceneX,
            MaxSceneX,
            MinPosKranX,
            MaxPosKranX));
    }

    private static int MapSceneYToMillimeters(double sceneY)
    {
        return (int)Math.Round(Map(
            sceneY,
            MinSceneY,
            MaxSceneY,
            MinPosKatzeY,
            MaxPosKatzeY));
    }

    private static int MapSceneZToMillimeters(double sceneZ)
    {
        return (int)Math.Round(Map(
            sceneZ,
            RaisedSceneZ,
            LoweredSceneZ,
            MinPosHubZ,
            MaxPosHubZ));
    }

    private static int Interpolate(
        int start,
        int target,
        double progress)
    {
        return (int)Math.Round(start + (target - start) * progress);
    }

    private static double SmoothStep(double value)
    {
        return value * value * (3 - 2 * value);
    }

    private static double Map(
        double value,
        double sourceMin,
        double sourceMax,
        double targetMin,
        double targetMax)
    {
        double normalized = (value - sourceMin) / (sourceMax - sourceMin);
        return targetMin + normalized * (targetMax - targetMin);
    }

    private static TaskCompletionSource CreateCompletedPauseCompletion()
    {
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        completion.SetResult();
        return completion;
    }
}

public readonly record struct CraneStatus(
    int PosKranX,
    int PosKatzeY,
    int PosHubZ);

public readonly record struct CraneTargetPosition(
    string Name,
    int PosKranX,
    int PosKatzeY,
    int PosHubZ);
