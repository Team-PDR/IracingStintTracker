using IRSDKSharper;
using StintTracker.Core.Tracking;
using System.Diagnostics;

namespace StintTracker.Core;

/// <summary>
/// Adapter over the iRacing SDK: reads each telemetry tick into a plain
/// <see cref="TelemetryFrame"/>. All race-state logic lives in
/// <see cref="RaceStateTracker"/>.
/// </summary>
public class TelemetryManager
{
    private readonly IRacingSdk irsdk = new();

    private IRacingSdkSessionInfo? contextSource;
    private SessionContext? context;

    public event Action<bool>? SdkConnectionChange;
    public event Action<TelemetryFrame>? FrameReceived;

    public TelemetryManager()
    {
        irsdk.UpdateInterval = 10;

        irsdk.OnException += OnException;
        irsdk.OnStopped += OnStopped;
        irsdk.OnConnected += OnConnected;
        irsdk.OnDisconnected += OnDisconnected;
        irsdk.OnTelemetryData += OnTelemetryData;
        irsdk.OnDebugLog += message => Debug.WriteLine(message);
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
        contextSource = null;
        context = null;
        Console.WriteLine("Stopped");
    }

    private void OnConnected()
    {
        SdkConnectionChange?.Invoke(true);
        Console.WriteLine("Connected!");
    }

    private void OnDisconnected()
    {
        SdkConnectionChange?.Invoke(false);
        Console.WriteLine("Disconnected!");
    }

    private void OnException(Exception exception)
    {
        Console.WriteLine($"Exception: {exception.Message}");
        irsdk.Stop();
        irsdk.Start();
    }

    private void OnTelemetryData()
    {
        IRacingSdkSessionInfo? sessionInfo = irsdk.Data.SessionInfo;
        if (sessionInfo?.SessionInfo == null)
        {
            return;
        }

        // Session info only changes occasionally; rebuild the context then.
        if (!ReferenceEquals(sessionInfo, contextSource))
        {
            context = BuildContext(sessionInfo);
            contextSource = sessionInfo;
        }

        if (context == null)
        {
            return;
        }

        FrameReceived?.Invoke(ReadFrame(context));
    }

    private TelemetryFrame ReadFrame(SessionContext session)
    {
        var d = irsdk.Data;
        return new TelemetryFrame
        {
            Session = session,

            SessionNum = d.GetInt("SessionNum"),
            SessionTime = d.GetDouble("SessionTime"),
            SessionTick = d.GetInt("SessionTick"),
            SessionState = (IRacingSdkEnum.SessionState)d.GetInt("SessionState"),
            SessionFlags = d.GetBitField("SessionFlags"),

            IsOnTrackCar = d.GetBool("IsOnTrackCar"),
            IsOnTrack = d.GetBool("IsOnTrack"),
            TowTime = d.GetFloat("PlayerCarTowTime"),
            OnPitRoad = d.GetBool("OnPitRoad"),
            PitstopActive = d.GetBool("PitstopActive"),

            Lap = d.GetInt("Lap"),
            LapCompleted = d.GetInt("LapCompleted"),
            LapLastLapTime = d.GetFloat("LapLastLapTime"),
            ClassPosition = d.GetInt("PlayerCarClassPosition"),
            Incidents = d.GetInt("PlayerCarMyIncidentCount"),
            FuelLevel = d.GetFloat("FuelLevel"),

            TrackTemp = d.GetFloat("TrackTempCrew"),
            TrackWetness = d.GetInt("TrackWetness"),
            Skies = d.GetInt("Skies"),
            Precipitation = d.GetFloat("Precipitation"),

            PitRepairLeft = d.GetFloat("PitRepairLeft"),
            PitOptRepairLeft = d.GetFloat("PitOptRepairLeft"),
            FastRepairAvailable = d.GetInt("FastRepairAvailable"),
            TireChangeLF = d.GetFloat("dpLFTireChange") > 0,
            TireChangeRF = d.GetFloat("dpRFTireChange") > 0,
            TireChangeLR = d.GetFloat("dpLRTireChange") > 0,
            TireChangeRR = d.GetFloat("dpRRTireChange") > 0,
        };
    }

    private static SessionContext? BuildContext(IRacingSdkSessionInfo info)
    {
        var driverInfo = info.DriverInfo;
        if (driverInfo?.Drivers == null)
        {
            return null;
        }

        int carIdx = driverInfo.DriverCarIdx;
        int userId = driverInfo.DriverUserID;

        // The Drivers list has one entry per car (its current driver), and is
        // not guaranteed to be indexed by CarIdx, so match on CarIdx.
        var car = driverInfo.Drivers.FirstOrDefault(d => d.CarIdx == carIdx);

        // A spectating teammate isn't the car's current driver, so their name
        // isn't in the list. They only send session-level events.
        string name = car != null && car.UserID == userId ? car.UserName : $"User {userId}";

        var sessions = info.SessionInfo.Sessions;

        return new SessionContext
        {
            SubSessionId = info.WeekendInfo.SubSessionID,
            CarIdx = carIdx,
            Driver = new DriverIdentity(userId, name),
            Track = info.WeekendInfo.TrackDisplayName ?? "",
            CarName = car?.CarScreenName ?? "",
            CarClass = car?.CarClassShortName ?? "",
            TeamId = car?.TeamID ?? 0,
            TeamName = car?.TeamName ?? "",
            CarNumber = car?.CarNumber ?? "",
            SessionTypes = sessions.Select(s => s.SessionType ?? "").ToList(),
            SessionDurationsS = sessions.Select(s => ParseDuration(s.SessionTime)).ToList(),
        };
    }

    /// <summary>Parses iRacing's "10800.0000 sec" format; "unlimited" gives 0.</summary>
    private static int ParseDuration(string? sessionTime)
    {
        string first = (sessionTime ?? "").Split(' ')[0];
        return double.TryParse(first, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double seconds)
            ? (int)seconds
            : 0;
    }
}
