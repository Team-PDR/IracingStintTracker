using IracingStintTracker;

namespace IracingStintTracker.Subscribers;

public abstract class RaceEventSubscriber
{
	protected readonly TelemetryManager manager;
	
	protected RaceEventSubscriber(TelemetryManager manager)
	{
		this.manager = manager;
	}

	protected abstract void OnRaceStarted(TelemetrySnapshot snapshot);

	protected virtual void OnRaceFinished(TelemetrySnapshot snapshot) { }

}
