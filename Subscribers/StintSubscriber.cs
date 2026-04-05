using System;
using System.Collections.Generic;
using System.Text;

namespace IracingStintTracker.Subscribers
{
    internal class StintSubscriber : RaceEventSubscriber
    {
        public StintSubscriber(TelemetryManager manager) : base(manager)
        {
            this.manager.RaceStarted += OnRaceStarted;
            this.manager.RaceFinished += OnRaceFinished;
        }

        protected override void OnRaceStarted(TelemetrySnapshot snapshot)
        {
            this.manager.CarEntered += OnCarEntered;
            this.manager.PitBoxEntered += OnPitBoxEntered;
            this.manager.PitBoxExited += OnPitBoxExited;
        }

        private void CreateStint(TelemetrySnapshot snapshot) { }
        private void UpdateStint(TelemetrySnapshot snapshot) { }
        private void EndStint(TelemetrySnapshot snapshot) { }

        private void OnCarEntered(TelemetrySnapshot snapshot) { }
        private void OnPitBoxEntered(TelemetrySnapshot snapshot) { }
        private void OnPitBoxExited(TelemetrySnapshot snapshot) { }
    }
}
