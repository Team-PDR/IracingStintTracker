using IracingStintTracker.Dtos;
using IRSDKSharper;

namespace IracingStintTracker.Subscribers;

internal class SessionSubscriber : RaceEventSubscriber
{
    public SessionSubscriber(TelemetryManager manager) : base(manager) 
    {
        this.manager.RaceStarted += OnRaceStarted;
    }

    private int GetRaceDuration(IRacingSdkSessionInfo sessionInfo, int sessionNum)
    {
        var sessions = sessionInfo.SessionInfo.Sessions;

        string duration = sessions[sessionNum].SessionTime.Split(' ')[0];

        if (int.TryParse(duration, out int result))
        {
            return result;
        }
        else
        {
            return 0;
        }
    }

    protected override void OnRaceStarted(TelemetrySnapshot snapshot) 
    {

        var weekendInfo = snapshot.SessionInfo.WeekendInfo;
        var driverInfo = snapshot.SessionInfo.DriverInfo;

        int playerCarId = snapshot.PlayerCarIdx;
        var userCarInfo = driverInfo.Drivers[playerCarId];
        string carClassName = userCarInfo.CarClassShortName;
        string carName = userCarInfo.CarScreenName;

        if (string.IsNullOrEmpty(carClassName))
        {
            carClassName = carName;
        }

        var session = new SessionPostDto
        {
            SessionId = weekendInfo.SubSessionID,
            TrackName = weekendInfo.TrackDisplayName,
            CarClass = carClassName,
            Car = carName,
            RaceDuration = GetRaceDuration(snapshot.SessionInfo, snapshot.SessionNum),
            SessionDate = DateOnly.FromDateTime(DateTime.Now)
        };

        Console.WriteLine($"New session created: {session}");

        // TODO: post session to API
    }
}

