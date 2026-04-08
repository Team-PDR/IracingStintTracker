using IRSDKSharper;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace IracingStintTracker;
public class TelemetryManager
{

    readonly IRacingSdk irsdk = new();

    const int LapTickDelay = 500; // delay in iracing ticks to ensure lap time is registered after crossing line
    int? lapCompletedTick = null;
    TelemetrySnapshot? tempSnapshot = null;

    bool shouldRestart = false;
    bool initialized = false;
    int sessionId = -1;
    bool raceStarted = false;
    bool raceFinished = false;

    bool wasUserDriving = false;
    bool wasOnPitRoad = false;
    bool wasInPitBox = false;
    int finalLapCompleted = -1;
    int lastCompletedLap = -1;

    IRacingSdkDatum? sessionTickDatum = null;
    IRacingSdkDatum? sessionStateDatum = null;
    IRacingSdkDatum? sessionNumDatum = null;
    IRacingSdkDatum? sessionFlagsDatum = null;
    IRacingSdkDatum? onTrackDatum = null;
    IRacingSdkDatum? onPitRoadDatum = null;
    IRacingSdkDatum? pitActiveDatum = null;
    IRacingSdkDatum? carTowingDatum = null;
    IRacingSdkDatum? lapCompletedDatum = null;

    public event Action<TelemetrySnapshot>? RaceStarted;
    public event Action<TelemetrySnapshot>? RaceFinished;
    public event Action<TelemetrySnapshot>? PitRoadEntered;
    public event Action<TelemetrySnapshot>? PitRoadExited;
    public event Action<TelemetrySnapshot>? PitBoxEntered;
    public event Action<TelemetrySnapshot>? PitBoxExited;
    public event Action<TelemetrySnapshot>? LapCompleted;
    public event Action<TelemetrySnapshot>? UserStartedRace;
    public event Action? CarEntered;
    public event Action? CarExited;


    public TelemetryManager()
    {
        irsdk.UpdateInterval = 10;

        irsdk.OnException += OnException;
        irsdk.OnStopped += OnStopped;
        irsdk.OnConnected += OnConnected;
        irsdk.OnDisconnected += OnDisconnected;
        irsdk.OnTelemetryData += OnTelemetryData;
        irsdk.OnDebugLog += OnDebugLog;
    }

    public void Start()
    {
        if (!irsdk.IsStarted)
        {
            irsdk.Start();
        }
    }

    public void Stop()
    {
        if (irsdk.IsStarted)
        {
            irsdk.Stop();
        }
    }
    private void OnStopped()
    {

        lapCompletedTick = null;
        tempSnapshot = null;

        sessionId = -1;
        initialized = false;
        raceStarted = false;
        raceFinished = false;

        lastCompletedLap = -1;
        wasUserDriving = false;
        wasOnPitRoad = false;
        wasInPitBox = false;
        finalLapCompleted = -1;

        sessionTickDatum = null;
        sessionStateDatum = null;
        sessionNumDatum = null;
        sessionFlagsDatum = null;
        onTrackDatum = null;
        onPitRoadDatum = null;
        pitActiveDatum = null;
        carTowingDatum = null;
        lapCompletedDatum = null;


        Console.WriteLine("Stopped");

        if (shouldRestart)
        {
            shouldRestart = false;
            irsdk.Start();
        }
    }

    private void OnConnected()
    {
        Console.WriteLine("Connected!");
    }

    private void OnDisconnected()
    {
        Console.WriteLine("Disconnected!");
    }

    private void OnException(Exception exception)
    {
        Console.WriteLine($"Exception: {exception.Message}");
        irsdk.Stop();
        irsdk.Start();
    }

    private void OnDebugLog(string message)
    {
        Debug.WriteLine(message);
    }

    private void reset()
    {
        shouldRestart = true;
        irsdk.Stop();
    }

    private TelemetrySnapshot GetSnapshot(IRacingSdkSessionInfo sessionInfo)
    {
        return new TelemetrySnapshot()
        {
            SessionInfo = sessionInfo,

            SessionTick = irsdk.Data.GetInt(sessionTickDatum),
            SessionNum = irsdk.Data.GetInt(sessionNumDatum),
            SessionTime = irsdk.Data.GetDouble("SessionTime"),

            PlayerCarIdx = irsdk.Data.GetInt("PlayerCarIdx"),
            PlayerCarClassPosition = irsdk.Data.GetInt("PlayerCarClassPosition"),
            PlayerCarMyIncidentCount = irsdk.Data.GetInt("PlayerCarMyIncidentCount"),


            TrackTempCrew = irsdk.Data.GetFloat("TrackTempCrew"),
            TrackWetness = irsdk.Data.GetInt("TrackWetness"),
            Skies = irsdk.Data.GetInt("Skies"),
            Precipitation = irsdk.Data.GetFloat("Precipitation"),


            PitRepairLeft = irsdk.Data.GetFloat("PitRepairLeft"),
            PitOptRepairLeft = irsdk.Data.GetFloat("PitOptRepairLeft"),
            dpRFTireChange = irsdk.Data.GetFloat("dpRFTireChange"),
            dpLFTireChange = irsdk.Data.GetFloat("dpLFTireChange"),
            dpRRTireChange = irsdk.Data.GetFloat("dpRRTireChange"),
            dpLRTireChange = irsdk.Data.GetFloat("dpLRTireChange"),
            FastRepairAvailable = irsdk.Data.GetInt("FastRepairAvailable"),
            FuelLevel = irsdk.Data.GetFloat("FuelLevel"),


            LapLastLapTime = irsdk.Data.GetFloat("LapLastLapTime"),
            Lap = irsdk.Data.GetInt("Lap"),
            LapCompleted = irsdk.Data.GetInt(lapCompletedDatum),
        };
    }

    private bool IsRaceSession(IRacingSdkSessionInfo sessionInfo)
    {
        int sessionNum = irsdk.Data.GetInt(sessionNumDatum);
        string sessionType = sessionInfo.SessionInfo.Sessions[sessionNum].SessionType;

        return sessionType == "Race";
    }

    private bool RaceHasStarted()
    {

        var sessionState = (IRacingSdkEnum.SessionState)irsdk.Data.GetInt(sessionStateDatum);

        bool isRacing = sessionState == IRacingSdkEnum.SessionState.Racing;

        int position = irsdk.Data.GetInt("PlayerCarClassPosition");

        return isRacing && position > 0;
    }

    private bool RaceHasEnded()
    {
        var sessionState = (IRacingSdkEnum.SessionState)irsdk.Data.GetInt(sessionStateDatum);

        if (sessionState == IRacingSdkEnum.SessionState.CoolDown)
        {
            return true;
        }

        bool driverInCar = irsdk.Data.GetBool("IsOnTrack");
        bool isTowing = irsdk.Data.GetBool(carTowingDatum);

        bool isOffTrack = !driverInCar || isTowing;
        if (sessionState == IRacingSdkEnum.SessionState.Checkered && isOffTrack)
        {
            return true;
        }

        uint flags = irsdk.Data.GetBitField(sessionFlagsDatum);
        int lapCompleted = irsdk.Data.GetInt(lapCompletedDatum);

        bool isCheckered = (flags & (uint)IRacingSdkEnum.Flags.Checkered) != 0;

        if ((finalLapCompleted != -1) && !isCheckered)
        {
            finalLapCompleted = -1;
        }

        if (isCheckered)
        {
            if (finalLapCompleted == -1)
            {
                finalLapCompleted = lapCompleted;
                return false;
            }

            bool isFinalLapFinished = lapCompleted > finalLapCompleted;

            return isFinalLapFinished;
        }

        return false;
    }

    private void OnTelemetryData()
    {
        IRacingSdkSessionInfo? sessionInfo = irsdk.Data.SessionInfo;
        if (sessionInfo?.SessionInfo == null)
        {
            return;
        }

        if (!initialized)
        {
            initialized = true;
            sessionId = sessionInfo.WeekendInfo.SubSessionID;
            sessionTickDatum = irsdk.Data.TelemetryDataProperties["SessionTick"];
            sessionStateDatum = irsdk.Data.TelemetryDataProperties["SessionState"];
            sessionNumDatum = irsdk.Data.TelemetryDataProperties["SessionNum"];
            sessionFlagsDatum = irsdk.Data.TelemetryDataProperties["SessionFlags"];
            onTrackDatum = irsdk.Data.TelemetryDataProperties["IsOnTrackCar"];
            onPitRoadDatum = irsdk.Data.TelemetryDataProperties["OnPitRoad"];
            pitActiveDatum = irsdk.Data.TelemetryDataProperties["PitstopActive"];
            carTowingDatum = irsdk.Data.TelemetryDataProperties["PlayerCarTowTime"];
            lapCompletedDatum = irsdk.Data.TelemetryDataProperties["LapCompleted"];

        }

        if (sessionId != sessionInfo.WeekendInfo.SubSessionID)
        {
            reset();
        }

        if (!IsRaceSession(sessionInfo))
        {
            return;
        }

        var snapshot = GetSnapshot(sessionInfo);

        
        bool isCarOnTrack = irsdk.Data.GetBool(onTrackDatum);
        float towTime = irsdk.Data.GetFloat(carTowingDatum);
        bool isUserDriving = isCarOnTrack || (towTime > 0.0 && wasUserDriving);


        if (!raceStarted && RaceHasStarted())
        {
            raceStarted = true;
            Console.WriteLine("Race started!");
            RaceStarted?.Invoke(snapshot);

            if (isUserDriving)
            {
                UserStartedRace?.Invoke(snapshot);
            }
        }

        if (!raceFinished && RaceHasEnded())
        {
            raceFinished = true;
            Console.WriteLine("Race finished!");
            RaceFinished?.Invoke(snapshot);
        }

        if (raceStarted)
        {
            
            bool isOnPitRoad = irsdk.Data.GetBool(onPitRoadDatum);
            bool isInPitBox = irsdk.Data.GetBool(pitActiveDatum);  
            int lapCompleted = irsdk.Data.GetInt(lapCompletedDatum);

            

            if (isUserDriving)
            {
                int currentTick = irsdk.Data.GetInt(sessionTickDatum);

                if (lapCompleted > 0 && lapCompleted > lastCompletedLap)
                {
                    lapCompletedTick = currentTick;
                    tempSnapshot = snapshot;
                }

                if (lapCompletedTick.HasValue && (currentTick - lapCompletedTick.Value) >= LapTickDelay)
                {
                    lapCompletedTick = null;
                    float lapTime = irsdk.Data.GetFloat("LapLastLapTime");
                    Console.WriteLine($"Lap completed: {lapCompleted} Lap Time {lapTime} after delay: {LapTickDelay}");
                    tempSnapshot!.LapLastLapTime = lapTime;
                    LapCompleted?.Invoke(tempSnapshot!);
                    tempSnapshot = null;
                }

                if (!raceFinished)
                {
                    if (!wasOnPitRoad && (isOnPitRoad || (towTime > 0.0)))
                    {
                        Console.WriteLine("Entered pit road");
                        PitRoadEntered?.Invoke(snapshot);
                    }

                    if (wasOnPitRoad && !isOnPitRoad)
                    {
                        Console.WriteLine("Exited pit road");
                        PitRoadExited?.Invoke(snapshot);
                    }

                    if (!wasInPitBox && isInPitBox)
                    {
                        Console.WriteLine("Entered pit box");
                        PitBoxEntered?.Invoke(snapshot);
                    }

                    if (wasInPitBox && !isInPitBox)
                    {
                        Console.WriteLine("Exited pit box");
                        PitBoxExited?.Invoke(snapshot);
                    }
                }

            }

            if (!raceFinished)
            {
                // driver swaps
                if (wasUserDriving && !isUserDriving)
                {
                    Console.WriteLine("User exited car");
                    CarExited?.Invoke();
                }

                if (!wasUserDriving && isUserDriving)
                {
                    Console.WriteLine("User Entered car");
                    CarEntered?.Invoke();
                }
            }

            lastCompletedLap = lapCompleted;
            wasUserDriving = isUserDriving;
            wasOnPitRoad = isOnPitRoad;
            wasInPitBox = isInPitBox;
        }

    }
}

public record TelemetrySnapshot
{
    // Session / Weekend / Driver info
    public required IRacingSdkSessionInfo SessionInfo { get; init; }
    public required int PlayerCarIdx { get; init; }

    // Session state
    public required int SessionTick { get; init; }
    public required int SessionNum { get; init; }
    public double SessionTime { get; init; }
    public int PlayerCarClassPosition { get; init; }
    public int PlayerCarMyIncidentCount { get; init; }
       
        
    public float TrackTempCrew { get; init; }
    public int TrackWetness { get; init; }
    public int Skies { get; init; }
    public float Precipitation { get; init; }

    // Pit info
    public float PitRepairLeft { get; init; }
    public float PitOptRepairLeft { get; init; }
    public float dpRFTireChange { get; init; }
    public float dpLFTireChange { get; init; }
    public float dpRRTireChange { get; init; }
    public float dpLRTireChange { get; init; }
    public int FastRepairAvailable { get; init; }
    public float FuelLevel { get; init; }

    // Lap info
    public float LapLastLapTime { get; set; }
    public int Lap { get; init; }
    public int LapCompleted { get; init; }
}

