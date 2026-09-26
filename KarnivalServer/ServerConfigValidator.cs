public static class ServerConfigValidator
{
    public static void Validate(ServerConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        Positive(config.Port, nameof(config.Port));
        Positive(config.MaxClientCount, nameof(config.MaxClientCount));
        Positive(config.TargetTicksPerSecond, nameof(config.TargetTicksPerSecond));
        Positive(config.ConnectionTimeoutMilliseconds, nameof(config.ConnectionTimeoutMilliseconds));
        Positive(config.ReliableMaxSendAttempts, nameof(config.ReliableMaxSendAttempts));
        NotBlank(config.DatabasePath, nameof(config.DatabasePath));
        Positive(config.RoundDurationSeconds, nameof(config.RoundDurationSeconds));
        Positive(config.ResultsDurationSeconds, nameof(config.ResultsDurationSeconds));
        Positive(config.SessionRoundCount, nameof(config.SessionRoundCount));
        Positive(config.LobbyDurationSeconds, nameof(config.LobbyDurationSeconds));
        Positive(config.VictoryDurationSeconds, nameof(config.VictoryDurationSeconds));
        Positive(config.FinalSummaryDurationSeconds, nameof(config.FinalSummaryDurationSeconds));

        ArgumentNullException.ThrowIfNull(config.Betting);
        Positive(config.Betting.DurationSeconds, "Betting.DurationSeconds");
        NonNegative(config.Betting.MaximumWager, "Betting.MaximumWager");
        ArgumentNullException.ThrowIfNull(config.Betting.AfterRounds);
        if (config.Betting.AfterRounds.Distinct().Count() != config.Betting.AfterRounds.Length ||
            config.Betting.AfterRounds.Any(round => round <= 0 || round >= config.SessionRoundCount))
        {
            throw Error("Betting.AfterRounds must contain unique rounds inside the session.");
        }

        ValidateNetwork(config.NetworkSimulation);
        ValidateSimulatedPlayers(config.SimulatedPlayers);
        ValidateMiniGames(config.MiniGames);
    }

    private static void ValidateNetwork(NetworkSimulationConfig value)
    {
        ArgumentNullException.ThrowIfNull(value);
        NonNegative(value.IncomingLatencyMilliseconds, "NetworkSimulation.IncomingLatencyMilliseconds");
        NonNegative(value.OutgoingLatencyMilliseconds, "NetworkSimulation.OutgoingLatencyMilliseconds");
        NonNegative(value.JitterMilliseconds, "NetworkSimulation.JitterMilliseconds");
        Range(value.IncomingLossChance, 0f, 1f, "NetworkSimulation.IncomingLossChance");
        Range(value.OutgoingLossChance, 0f, 1f, "NetworkSimulation.OutgoingLossChance");
    }

    private static void ValidateSimulatedPlayers(SimulatedPlayerConfig value)
    {
        ArgumentNullException.ThrowIfNull(value);
        NonNegative(value.InitialCount, "SimulatedPlayers.InitialCount");
        NonNegative(value.MaximumCount, "SimulatedPlayers.MaximumCount");
        if (value.InitialCount > value.MaximumCount)
            throw Error("SimulatedPlayers.InitialCount cannot exceed MaximumCount.");
    }

    private static void ValidateMiniGames(MiniGameSettings value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(value.SliderTarget);
        Positive(value.SliderTarget.FingerPeriodSeconds, "SliderTarget.FingerPeriodSeconds");
        Range(value.SliderTarget.TargetHitRadiusNormalized, 0.0001f, 1f,
            "SliderTarget.TargetHitRadiusNormalized");

        ValidateObstacleRunner(value.ObstacleRunner);
        ValidatePhotoPuzzle(value.PhotoPuzzleSlider);
        ValidateFallingObject(value.FallingObjectCatcher);
        ValidateHorde(value.HordeClicker);
        ValidateColorMatch(value.ColorMatch);
        ValidateColorSequence(value.ColorSequence);
        ValidateCardMatch(value.CardMatch);
        ValidateCupAndBall(value.CupAndBall);
        ValidateCarPark(value.CarPark);
        ValidateArchery(value.ArcheryPractice);
        ValidateStopGo(value.StopGo);
        ValidatePotatoFace(value.PotatoFace);
        ValidateSpotTheDifference(value.SpotTheDifference);
        ValidatePlateStacker(value.PlateStacker);
        ValidateMaze(value.Maze);
        ValidateSmackMan(value.SmackMan);
        ValidateWheresBaldo(value.WheresBaldo);
        ValidateWheelSpinner(value.WheelSpinner);
        ValidateStepIntoTraffic(value.StepIntoTraffic);
    }

    private static void ValidateStepIntoTraffic(StepIntoTrafficSettings value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Positive(value.DurationSeconds, "StepIntoTraffic.DurationSeconds");
        Range(value.LaneCount, 1, byte.MaxValue, "StepIntoTraffic.LaneCount");
        Positive(value.HopDurationSeconds, "StepIntoTraffic.HopDurationSeconds");
        NonNegative(value.CollisionRecoverySeconds,
            "StepIntoTraffic.CollisionRecoverySeconds");
        OrderedRange(value.MinimumCarSpeedNormalizedPerSecond,
            value.MaximumCarSpeedNormalizedPerSecond, 0.0001f, float.MaxValue,
            "StepIntoTraffic car-speed range");
        OrderedRange(value.MinimumSafeGapSeconds, value.MaximumSafeGapSeconds,
            0.0001f, value.DurationSeconds, "StepIntoTraffic safe-gap range");
        Range(value.CarHalfWidthNormalized, 0.001f, 0.5f,
            "StepIntoTraffic.CarHalfWidthNormalized");
        Range(value.CarHalfHeightRows, 0.001f, 0.5f,
            "StepIntoTraffic.CarHalfHeightRows");
        Range(value.PedestrianHalfWidthNormalized, 0.001f, 0.5f,
            "StepIntoTraffic.PedestrianHalfWidthNormalized");
        Range(value.PedestrianHalfHeightRows, 0.001f, 0.5f,
            "StepIntoTraffic.PedestrianHalfHeightRows");
        Positive(value.SimulationStepSeconds,
            "StepIntoTraffic.SimulationStepSeconds");
        NonNegative(value.CorrectScore, "StepIntoTraffic.CorrectScore");
        Tolerances(value.InputGraceSeconds, value.FutureInputToleranceSeconds,
            "StepIntoTraffic");
        Positive(value.MaximumInputSamples,
            "StepIntoTraffic.MaximumInputSamples");
    }
    private static void ValidateWheelSpinner(WheelSpinnerSettings value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Positive(value.DurationSeconds, "WheelSpinner.DurationSeconds");
        Positive(value.AngularSpeedDegreesPerSecond,
            "WheelSpinner.AngularSpeedDegreesPerSecond");
        ArgumentNullException.ThrowIfNull(value.Slices);
        if (value.Slices.Length != WheelSpinnerMiniGame.SliceCount)
            throw Error("WheelSpinner must contain exactly 10 slices.");

        float totalArcDegrees = 0f;
        for (int index = 0; index < value.Slices.Length; index++)
        {
            WheelSpinnerSliceSettings slice = value.Slices[index]
                ?? throw Error($"WheelSpinner.Slices[{index}] must not be null.");
            NonNegative(slice.Points,
                $"WheelSpinner.Slices[{index}].Points");
            Positive(slice.ArcDegrees,
                $"WheelSpinner.Slices[{index}].ArcDegrees");
            totalArcDegrees += slice.ArcDegrees;
        }

        if (Math.Abs(totalArcDegrees - 360f) > 0.001f)
            throw Error("WheelSpinner slice arcs must total 360 degrees.");
        Tolerances(value.InputGraceSeconds,
            value.FutureInputToleranceSeconds, "WheelSpinner");
    }

    private static void ValidateWheresBaldo(WheresBaldoSettings value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Positive(value.DurationSeconds, "WheresBaldo.DurationSeconds");
        NonNegative(value.CorrectScore, "WheresBaldo.CorrectScore");
        NonNegative(value.WrongSelectionPenalty, "WheresBaldo.WrongSelectionPenalty");
        Tolerances(
            value.InputGraceSeconds,
            value.FutureInputToleranceSeconds,
            "WheresBaldo");
    }

    private static void ValidateSmackMan(SmackManSettings value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Positive(value.DurationSeconds, "SmackMan.DurationSeconds");
        Range(value.GroupCount, 1, ushort.MaxValue, "SmackMan.GroupCount");
        Range(value.HeadsPerGroup, 1, SmackManScheduleGenerator.HoleCount,
            "SmackMan.HeadsPerGroup");
        if ((long)value.GroupCount * value.HeadsPerGroup > ushort.MaxValue)
            throw Error("SmackMan cannot schedule more than 65535 heads.");
        NonNegative(value.PointsPerHit, "SmackMan.PointsPerHit");
        NonNegative(value.FirstGroupSeconds, "SmackMan.FirstGroupSeconds");
        Positive(value.GroupIntervalSeconds, "SmackMan.GroupIntervalSeconds");
        Positive(value.RiseDurationSeconds, "SmackMan.RiseDurationSeconds");
        Positive(value.HoldDurationSeconds, "SmackMan.HoldDurationSeconds");
        Positive(value.RetractDurationSeconds, "SmackMan.RetractDurationSeconds");
        float activeDuration = value.RiseDurationSeconds +
            value.HoldDurationSeconds + value.RetractDurationSeconds;
        float finalExpiry = value.FirstGroupSeconds +
            ((value.GroupCount - 1) * value.GroupIntervalSeconds) +
            activeDuration;
        if (finalExpiry > value.DurationSeconds + 0.0001f)
            throw Error("SmackMan appearances must expire before the round ends.");
        int overlappingGroups = Math.Min(
            value.GroupCount,
            (int)Math.Ceiling(activeDuration / value.GroupIntervalSeconds));
        int maximumConcurrent = overlappingGroups * value.HeadsPerGroup;
        if (maximumConcurrent > SmackManScheduleGenerator.HoleCount)
            throw Error("SmackMan timing cannot show more heads than there are holes.");
        Tolerances(value.InputGraceSeconds, value.FutureInputToleranceSeconds, "SmackMan");
    }

    private static void ValidateObstacleRunner(ObstacleRunnerSettings value)
    {
        ArgumentNullException.ThrowIfNull(value);
        NonNegative(value.AdditionalDurationSeconds, "ObstacleRunner.AdditionalDurationSeconds");
        Positive(value.ObstacleCount, "ObstacleRunner.ObstacleCount");
        OrderedRange(value.FirstCollisionNormalized, value.LastCollisionNormalized, 0f, 1f,
            "ObstacleRunner collision range");
        Positive(value.ObstacleTravelSeconds, "ObstacleRunner.ObstacleTravelSeconds");
        Positive(value.JumpDurationSeconds, "ObstacleRunner.JumpDurationSeconds");
        Positive(value.SlideDurationSeconds, "ObstacleRunner.SlideDurationSeconds");
        Tolerances(value.InputGraceSeconds, value.FutureInputToleranceSeconds, "ObstacleRunner");
    }

    private static void ValidatePhotoPuzzle(PhotoPuzzleSliderSettings value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Positive(value.DurationSeconds, "PhotoPuzzleSlider.DurationSeconds");
        Positive(value.ShuffleMoveCount, "PhotoPuzzleSlider.ShuffleMoveCount");
        NonNegative(value.MinimumManhattanDistance, "PhotoPuzzleSlider.MinimumManhattanDistance");
        NonNegative(value.CorrectTilePoints, "PhotoPuzzleSlider.CorrectTilePoints");
        NonNegative(value.CompletionBonus, "PhotoPuzzleSlider.CompletionBonus");
    }

    private static void ValidateFallingObject(FallingObjectCatcherSettings value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Positive(value.DurationSeconds, "FallingObjectCatcher.DurationSeconds");
        Positive(value.TargetObjectCount, "FallingObjectCatcher.TargetObjectCount");
        NonNegative(value.DistractorCountPerShape, "FallingObjectCatcher.DistractorCountPerShape");
        OrderedRange(value.FirstCatchSeconds, value.LastCatchSeconds, 0f, value.DurationSeconds,
            "FallingObjectCatcher catch range");
        OrderedRange(value.MinimumFallDurationSeconds, value.MaximumFallDurationSeconds,
            0.0001f, value.DurationSeconds, "FallingObjectCatcher fall-duration range");
        Positive(value.CatcherSpeedNormalizedPerSecond,
            "FallingObjectCatcher.CatcherSpeedNormalizedPerSecond");
        Range(value.CatcherHalfWidthNormalized, 0.01f, 0.49f,
            "FallingObjectCatcher.CatcherHalfWidthNormalized");
        Range(value.CatchToleranceNormalized, 0.01f, 0.49f,
            "FallingObjectCatcher.CatchToleranceNormalized");
        Range(value.CatchWindowFallDurationFraction, 0.05f, 1f,
            "FallingObjectCatcher.CatchWindowFallDurationFraction");
        NonNegative(value.CorrectCatchPoints, "FallingObjectCatcher.CorrectCatchPoints");
        NonNegative(value.WrongCatchPenalty, "FallingObjectCatcher.WrongCatchPenalty");
        Tolerances(value.InputGraceSeconds, value.FutureInputToleranceSeconds,
            "FallingObjectCatcher");
    }

    private static void ValidateHorde(HordeClickerSettings value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Positive(value.DurationSeconds, "HordeClicker.DurationSeconds");
        Range(value.CharacterCount, 1, 25, "HordeClicker.CharacterCount");
        NonNegative(value.PointsPerCharacter, "HordeClicker.PointsPerCharacter");
        OrderedRange(value.FirstSpawnSeconds, value.LastSpawnSeconds, 0f, value.DurationSeconds,
            "HordeClicker spawn range");
        OrderedRange(value.MinimumTravelDurationSeconds, value.MaximumTravelDurationSeconds,
            0.0001f, value.DurationSeconds, "HordeClicker travel-duration range");
        OrderedRange(value.MinimumYNormalized, value.MaximumYNormalized, 0f, 1f,
            "HordeClicker vertical range");
        Tolerances(value.InputGraceSeconds, value.FutureInputToleranceSeconds, "HordeClicker");
    }

    private static void ValidateColorMatch(ColorMatchSettings value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Positive(value.DurationSeconds, "ColorMatch.DurationSeconds");
        if (value.MinimumTargetChannel > value.MaximumTargetChannel)
            throw Error("ColorMatch target-channel range is reversed.");
        Tolerances(value.InputGraceSeconds, value.FutureInputToleranceSeconds, "ColorMatch");
    }

    private static void ValidateColorSequence(ColorSequenceSettings value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Positive(value.BallCount, "ColorSequence.BallCount");
        Positive(value.SequenceLength, "ColorSequence.SequenceLength");
        NonNegative(value.PlaybackLeadInSeconds, "ColorSequence.PlaybackLeadInSeconds");
        Positive(value.PlaybackLitSeconds, "ColorSequence.PlaybackLitSeconds");
        NonNegative(value.PlaybackGapSeconds, "ColorSequence.PlaybackGapSeconds");
        Positive(value.InputDurationSeconds, "ColorSequence.InputDurationSeconds");
        NonNegative(value.PointsPerCorrectPosition, "ColorSequence.PointsPerCorrectPosition");
        NonNegative(value.PerfectSequenceBonus, "ColorSequence.PerfectSequenceBonus");
        Tolerances(value.InputGraceSeconds, value.FutureInputToleranceSeconds, "ColorSequence");
    }

    private static void ValidateCardMatch(CardMatchSettings value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Positive(value.DurationSeconds, "CardMatch.DurationSeconds");
        Positive(value.PairCount, "CardMatch.PairCount");
        if (value.AvailableShapeCount < value.PairCount)
            throw Error("CardMatch.AvailableShapeCount cannot be less than PairCount.");
        NonNegative(value.PointsPerPair, "CardMatch.PointsPerPair");
        NonNegative(value.MismatchRevealSeconds, "CardMatch.MismatchRevealSeconds");
        Tolerances(value.InputGraceSeconds, value.FutureInputToleranceSeconds, "CardMatch");
    }

    private static void ValidateCupAndBall(CupAndBallSettings value)
    {
        ArgumentNullException.ThrowIfNull(value);
        NonNegative(value.BallRevealSeconds, "CupAndBall.BallRevealSeconds");
        NonNegative(value.CupDropSeconds, "CupAndBall.CupDropSeconds");
        NonNegative(value.PreShufflePauseSeconds, "CupAndBall.PreShufflePauseSeconds");
        NonNegative(value.ShuffleCount, "CupAndBall.ShuffleCount");
        NonNegative(value.ShuffleStepSeconds, "CupAndBall.ShuffleStepSeconds");
        Positive(value.InputDurationSeconds, "CupAndBall.InputDurationSeconds");
        NonNegative(value.OutcomeRevealSeconds, "CupAndBall.OutcomeRevealSeconds");
        NonNegative(value.CorrectScore, "CupAndBall.CorrectScore");
        Tolerances(value.InputGraceSeconds, value.FutureInputToleranceSeconds, "CupAndBall");
    }

    private static void ValidateCarPark(CarParkSettings value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Range(value.ParkingSpotCount, 2, byte.MaxValue, "CarPark.ParkingSpotCount");
        NonNegative(value.LeadInSeconds, "CarPark.LeadInSeconds");
        Positive(value.DrivingDurationSeconds, "CarPark.DrivingDurationSeconds");
        NonNegative(value.OutcomeBufferSeconds, "CarPark.OutcomeBufferSeconds");
        Positive(value.FixedStepSeconds, "CarPark.FixedStepSeconds");
        Positive(value.ForwardSpeed, "CarPark.ForwardSpeed");
        Positive(value.MaximumTurnDegreesPerSecond, "CarPark.MaximumTurnDegreesPerSecond");
        if (value.FirstParkingX >= value.LastParkingX)
            throw Error("CarPark parking X range must be increasing.");
        Positive(value.ParkingHalfWidth, "CarPark.ParkingHalfWidth");
        Positive(value.ParkingHalfHeight, "CarPark.ParkingHalfHeight");
        Positive(value.CarHalfWidth, "CarPark.CarHalfWidth");
        Positive(value.CarHalfHeight, "CarPark.CarHalfHeight");
        Positive(value.BoundsHalfWidth, "CarPark.BoundsHalfWidth");
        Positive(value.BoundsHalfHeight, "CarPark.BoundsHalfHeight");
        Range(value.ParkingAngleToleranceDegrees, 0f, 90f,
            "CarPark.ParkingAngleToleranceDegrees");
        Positive(value.SteeringSendIntervalSeconds, "CarPark.SteeringSendIntervalSeconds");
        Range(value.SteeringChangeThreshold, 0f, 1f, "CarPark.SteeringChangeThreshold");
        Positive(value.WheelMaximumRotationDegrees, "CarPark.WheelMaximumRotationDegrees");
        Positive(value.WheelReturnDegreesPerSecond, "CarPark.WheelReturnDegreesPerSecond");
        NonNegative(value.CorrectScore, "CarPark.CorrectScore");
        Tolerances(value.InputGraceSeconds, value.FutureInputToleranceSeconds, "CarPark");
        int normalSamples = (int)Math.Ceiling(
            value.DrivingDurationSeconds / value.SteeringSendIntervalSeconds) + 8;
        if (value.MaximumInputSamples < normalSamples)
            throw Error("CarPark.MaximumInputSamples is too small for the configured send rate.");
    }

    private static void ValidateArchery(ArcheryPracticeSettings value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Positive(value.InputDurationSeconds, "ArcheryPractice.InputDurationSeconds");
        NonNegative(value.OutcomeBufferSeconds, "ArcheryPractice.OutcomeBufferSeconds");
        Positive(value.MaximumChargeSeconds, "ArcheryPractice.MaximumChargeSeconds");
        OrderedRange(value.MinimumLaunchSpeed, value.MaximumLaunchSpeed, 0.0001f,
            float.MaxValue, "ArcheryPractice launch-speed range");
        Positive(value.Gravity, "ArcheryPractice.Gravity");
        OrderedRange(value.MinimumWindAcceleration, value.MaximumWindAcceleration, 0f,
            float.MaxValue, "ArcheryPractice wind range");
        Positive(value.MaximumFlightSeconds, "ArcheryPractice.MaximumFlightSeconds");
        Positive(value.SimulationStepSeconds, "ArcheryPractice.SimulationStepSeconds");
        if (value.MinimumAimDegrees >= value.MaximumAimDegrees)
            throw Error("ArcheryPractice aim range must be increasing.");
        Positive(value.PlayAreaHalfWidth, "ArcheryPractice.PlayAreaHalfWidth");
        Positive(value.PlayAreaHalfHeight, "ArcheryPractice.PlayAreaHalfHeight");
        Positive(value.ArrowHalfLength, "ArcheryPractice.ArrowHalfLength");
        Positive(value.TargetHalfWidth, "ArcheryPractice.TargetHalfWidth");
        Positive(value.TargetHalfHeight, "ArcheryPractice.TargetHalfHeight");
        NonNegative(value.CorrectScore, "ArcheryPractice.CorrectScore");
        Tolerances(value.InputGraceSeconds, value.FutureInputToleranceSeconds,
            "ArcheryPractice");
    }

    private static void ValidateStopGo(StopGoSettings value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Positive(value.DurationSeconds, "StopGo.DurationSeconds");
        Positive(value.ContinuousTraversalSeconds, "StopGo.ContinuousTraversalSeconds");
        OrderedRange(value.MinimumGreenSeconds, value.MaximumGreenSeconds, 0.0001f,
            value.DurationSeconds, "StopGo green range");
        OrderedRange(value.MinimumRedSeconds, value.MaximumRedSeconds, 0.0001f,
            value.DurationSeconds, "StopGo red range");
        NonNegative(value.RedGraceSeconds, "StopGo.RedGraceSeconds");
        Tolerances(value.InputGraceSeconds, value.FutureInputToleranceSeconds, "StopGo");
        Positive(value.SnapshotIntervalSeconds, "StopGo.SnapshotIntervalSeconds");
        if (value.MaximumInputSamples < 2)
            throw Error("StopGo.MaximumInputSamples must be at least 2.");
    }

    private static void ValidatePotatoFace(PotatoFaceSettings value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Positive(value.DurationSeconds, "PotatoFace.DurationSeconds");
        Range(value.PositionToleranceNormalized, 0.01f, 1f,
            "PotatoFace.PositionToleranceNormalized");
        NonNegative(value.EyesPoints, "PotatoFace.EyesPoints");
        NonNegative(value.MouthPoints, "PotatoFace.MouthPoints");
        NonNegative(value.NosePoints, "PotatoFace.NosePoints");
        if ((long)value.EyesPoints + value.MouthPoints + value.NosePoints > int.MaxValue)
            throw Error("PotatoFace point values are too large.");
        Tolerances(value.InputGraceSeconds, value.FutureInputToleranceSeconds,
            "PotatoFace");
    }

    private static void ValidateSpotTheDifference(
        SpotTheDifferenceSettings value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Positive(value.DurationSeconds, "SpotTheDifference.DurationSeconds");
        Range(value.AvailableDifferenceCount, 1, 6,
            "SpotTheDifference.AvailableDifferenceCount");
        Range(value.SelectedDifferenceCount, 1, value.AvailableDifferenceCount,
            "SpotTheDifference.SelectedDifferenceCount");
        NonNegative(value.PointsPerDifference,
            "SpotTheDifference.PointsPerDifference");
        NonNegative(value.FullCompletionScore,
            "SpotTheDifference.FullCompletionScore");
        long partialMaximum =
            (long)value.SelectedDifferenceCount * value.PointsPerDifference;
        if (partialMaximum > int.MaxValue)
            throw Error("SpotTheDifference maximum score is too large.");
        if (value.FullCompletionScore < partialMaximum)
        {
            throw Error(
                "SpotTheDifference.FullCompletionScore must be at least the " +
                "combined per-difference score.");
        }
        Tolerances(value.InputGraceSeconds, value.FutureInputToleranceSeconds,
            "SpotTheDifference");
    }

    private static void ValidatePlateStacker(PlateStackerSettings value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Positive(value.DurationSeconds, "PlateStacker.DurationSeconds");
        Range(value.PlateCount, 1,
            PlateStackerSettings.MaximumSupportedPlateCount,
            "PlateStacker.PlateCount");
        OrderedRange(value.FirstLandingSeconds, value.LastLandingSeconds,
            0f, value.DurationSeconds, "PlateStacker landing range");
        OrderedRange(value.MinimumFallDurationSeconds,
            value.MaximumFallDurationSeconds, 0.0001f,
            value.DurationSeconds, "PlateStacker fall-duration range");
        Positive(value.StackSpeedNormalizedPerSecond,
            "PlateStacker.StackSpeedNormalizedPerSecond");
        Range(value.PlateWidthNormalized, 0.01f, 0.49f,
            "PlateStacker.PlateWidthNormalized");
        Range(value.StartingPlateWidthNormalized, 0.01f, 0.49f,
            "PlateStacker.StartingPlateWidthNormalized");
        Positive(value.CollapseOffsetPlateWidths,
            "PlateStacker.CollapseOffsetPlateWidths");
        NonNegative(value.PointsPerPlate, "PlateStacker.PointsPerPlate");
        Tolerances(value.InputGraceSeconds,
            value.FutureInputToleranceSeconds, "PlateStacker");
        if (value.MaximumInputSamples < 2)
            throw Error("PlateStacker.MaximumInputSamples must be at least 2.");
        if ((long)value.PlateCount * value.PointsPerPlate > int.MaxValue)
            throw Error("PlateStacker maximum score is too large.");
    }

    private static void ValidateMaze(MazeSettings value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Positive(value.DurationSeconds, "Maze.DurationSeconds");
        if (value.GridSize != 12)
            throw Error("Maze.GridSize must be 12.");
        Range(value.PlayerRadiusNormalized, 0.001f, 0.1f,
            "Maze.PlayerRadiusNormalized");
        Range(value.WallThicknessNormalized, 0.0001f, 0.05f,
            "Maze.WallThicknessNormalized");
        Range(value.ExitRadiusNormalized, 0.001f, 0.15f,
            "Maze.ExitRadiusNormalized");
        Range(value.OuterPaddingNormalized, 0.01f, 0.25f,
            "Maze.OuterPaddingNormalized");
        Positive(value.CorrectScore, "Maze.CorrectScore");
        Range(value.MaximumBatchSamples, 1, byte.MaxValue,
            "Maze.MaximumBatchSamples");
        if (value.MaximumInputSamples < value.MaximumBatchSamples)
            throw Error("Maze.MaximumInputSamples must fit at least one full batch.");
        Tolerances(value.InputGraceSeconds,
            value.FutureInputToleranceSeconds, "Maze");
    }

    private static void Tolerances(float grace, float future, string prefix)
    {
        NonNegative(grace, $"{prefix}.InputGraceSeconds");
        NonNegative(future, $"{prefix}.FutureInputToleranceSeconds");
    }

    private static void OrderedRange(float minimum, float maximum, float lower,
        float upper, string name)
    {
        Range(minimum, lower, upper, name);
        Range(maximum, lower, upper, name);
        if (minimum > maximum)
            throw Error($"{name} is reversed.");
    }

    private static void Positive(float value, string name)
    {
        if (!float.IsFinite(value) || value <= 0f)
            throw Error($"{name} must be finite and positive.");
    }

    private static void Positive(int value, string name)
    {
        if (value <= 0)
            throw Error($"{name} must be positive.");
    }

    private static void Positive(ushort value, string name)
    {
        if (value == 0)
            throw Error($"{name} must be positive.");
    }

    private static void NonNegative(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0f)
            throw Error($"{name} must be finite and nonnegative.");
    }

    private static void NonNegative(int value, string name)
    {
        if (value < 0)
            throw Error($"{name} must be nonnegative.");
    }

    private static void Range(float value, float minimum, float maximum, string name)
    {
        if (!float.IsFinite(value) || value < minimum || value > maximum)
            throw Error($"{name} must be between {minimum} and {maximum}.");
    }

    private static void Range(int value, int minimum, int maximum, string name)
    {
        if (value < minimum || value > maximum)
            throw Error($"{name} must be between {minimum} and {maximum}.");
    }

    private static void NotBlank(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw Error($"{name} must not be blank.");
    }

    private static InvalidDataException Error(string message) => new(message);
}
