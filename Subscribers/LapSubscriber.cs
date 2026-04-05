using System;
using System.Collections.Generic;
using System.Text;
using IracingStintTracker.Dtos;

namespace IracingStintTracker.Subscribers
{
    internal class LapSubscriber : RaceEventSubscriber
    {
        public LapSubscriber(TelemetryManager manager) : base(manager)
        {
            this.manager.RaceStarted += OnRaceStarted;
        }

        protected override void OnRaceStarted(TelemetrySnapshot snapshot)
        {
            this.manager.LapCompleted += OnLapCompleted;
        }

        private void OnLapCompleted(TelemetrySnapshot snapshot)
        {
            LapPostDto lap = new LapPostDto()
            {
                stintId = 1, // TODO: Get stintId from somewhere
                lapNumber = snapshot.LapCompleted,
                lapTime = snapshot.LapLastLapTime,
                position = snapshot.PlayerCarClassPosition,
                incidents = snapshot.PlayerCarMyIncidentCount,
                startFuel = snapshot.FuelLevel,
            };

            Console.WriteLine($"Created Lap: {lap}");
            // TODO: Post lap to API
        }
    }
}
