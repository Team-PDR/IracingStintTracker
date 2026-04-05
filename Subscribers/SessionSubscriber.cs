using System;
using System.Collections.Generic;
using System.Text;
using IracingStintTracker.Dtos;
using IRSDKSharper;

namespace IracingStintTracker.Subscribers;

internal class SessionSubscriber : RaceEventSubscriber
{
    public SessionSubscriber(TelemetryManager manager) : base(manager) 
    {
        this.manager.RaceStarted += OnRaceStarted;
        this.manager.RaceFinished += OnRaceFinished;
    }

    private int GetRaceDuration(IRacingSdkSessionInfo sessionInfo, int sessionNum)
    {
        List<IRacingSdkSessionInfo.SessionInfoModel.SessionModel> sessions = sessionInfo.SessionInfo.Sessions;

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

        IRacingSdkSessionInfo.WeekendInfoModel weekendInfo = snapshot.SessionInfo.WeekendInfo;
        IRacingSdkSessionInfo.DriverInfoModel driverInfo = snapshot.SessionInfo.DriverInfo;

        int playerCarId = snapshot.PlayerCarIdx;
        IRacingSdkSessionInfo.DriverInfoModel.DriverModel userCarInfo = driverInfo.Drivers[playerCarId];
        string carClassName = userCarInfo.CarClassShortName;
        string carName = userCarInfo.CarScreenName;

        SessionPostDto session = new SessionPostDto
        {
            sessionId = weekendInfo.SubSessionID,
            trackName = weekendInfo.TrackDisplayName,
            carClass = carClassName,
            car = carName,
            raceDuration = GetRaceDuration(snapshot.SessionInfo, snapshot.SessionNum),
            sessionDate = DateOnly.FromDateTime(DateTime.Now)
        };

        Console.WriteLine($"New session created: {session}");
    }




}

