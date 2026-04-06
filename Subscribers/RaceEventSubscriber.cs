using IracingStintTracker;

namespace IracingStintTracker.Subscribers;

public abstract class RaceEventSubscriber
{
	protected readonly TelemetryManager manager;
	protected bool subscribed = false;
	
	protected RaceEventSubscriber(TelemetryManager manager)
	{
		this.manager = manager;
	}

	protected virtual void SubscribeToCarEvents() { subscribed = true; }
	protected virtual void UnsubscribeFromCarEvents() { subscribed = false; }
	protected abstract void OnRaceStarted(TelemetrySnapshot snapshot);

	protected virtual void OnRaceFinished(TelemetrySnapshot snapshot) { }

}
