using System;
using System.Collections.Generic;
using System.Text;
using IracingStintTracker.Dtos;
using IRSDKSharper;

namespace IracingStintTracker.Subscribers
{
    internal class StintSubscriber : RaceEventSubscriber
    {

        private StintPostDto? currentStint = null;
        public StintSubscriber(TelemetryManager manager) : base(manager)
        {
            this.manager.RaceStarted += OnRaceStarted;
            this.manager.RaceFinished += OnRaceFinished;
        }

        protected override void OnRaceStarted(TelemetrySnapshot snapshot)
        {
            manager.UserStartedRace += OnUserStartedRace;
            manager.LapCompleted += OnLapCompleted;
            manager.PitBoxEntered += OnPitBoxEntered;
            manager.PitBoxExited += OnPitBoxExited;
        }

        protected override void OnRaceFinished(TelemetrySnapshot snapshot)
        {
            manager.UserStartedRace -= OnUserStartedRace;
            manager.LapCompleted -= OnLapCompleted;
            manager.PitBoxEntered -= OnPitBoxEntered;
            manager.PitBoxExited -= OnPitBoxExited;
            if (currentStint != null) 
            {
                EndStint(snapshot);
            }
        }

        private static string GetDriverName(IRacingSdkSessionInfo sessionInfo, int carId)
        {
            IRacingSdkSessionInfo.DriverInfoModel driverInfo = sessionInfo.DriverInfo;
            return driverInfo.Drivers[carId].UserName;
        }

        private void CreateStint(TelemetrySnapshot snapshot) 
        {
            currentStint = new StintPostDto ()
            {
                sessionId = snapshot.SessionInfo.WeekendInfo.SubSessionID,
                driverName = GetDriverName(snapshot.SessionInfo, snapshot.PlayerCarIdx),
                startTime = snapshot.SessionTime,
                startPosition = snapshot.PlayerCarClassPosition,
                startIncidents = snapshot.PlayerCarMyIncidentCount,
                startFuel = snapshot.FuelLevel,
                trackTemp = snapshot.TrackTempCrew,
                trackWetness = snapshot.TrackWetness,
                skyCover = snapshot.Skies,
                precipitation = snapshot.Precipitation,
            };
            Console.WriteLine($"Creating stint: {currentStint}");
            // TODO: Post stint to API
        }
        private void UpdateStint(TelemetrySnapshot snapshot, bool completed) 
        {   
            if (currentStint == null) 
            {
                Console.WriteLine("No current stint to update");
                return;
            }
            
            StintPatchDto stintUpdate = new StintPatchDto()
            {
                endTime = snapshot.SessionTime,
                endPosition = snapshot.PlayerCarClassPosition,
                endIncidents = snapshot.PlayerCarMyIncidentCount,
                endFuel = snapshot.FuelLevel,
                isComplete = completed,
            };
            Console.WriteLine($"Updating stint: {stintUpdate}");
            // TODO: Patch stint to API
        }
        private void EndStint(TelemetrySnapshot snapshot) 
        {
            UpdateStint(snapshot, true);

            Console.WriteLine("Ending stint");

            currentStint = null;
        }

        private void OnUserStartedRace(TelemetrySnapshot snapshot) 
        { 
            CreateStint(snapshot);
        }
        private void OnLapCompleted(TelemetrySnapshot snapshot) 
        {
            UpdateStint(snapshot, false);
        }
        private void OnPitBoxEntered(TelemetrySnapshot snapshot) 
        { 
            EndStint(snapshot);
        }
        private void OnPitBoxExited(TelemetrySnapshot snapshot) 
        {
            CreateStint(snapshot);
        }
    }
}
