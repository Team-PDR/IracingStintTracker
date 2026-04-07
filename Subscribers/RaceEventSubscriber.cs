using System.Threading.Channels;

namespace IracingStintTracker.Subscribers;

public abstract class RaceEventSubscriber
{
	protected readonly TelemetryManager manager;
	protected bool subscribed = false;
	protected Channel<object> apiChannel;
	
	protected RaceEventSubscriber(TelemetryManager manager, Channel<object> channel)
	{
		this.manager = manager;
        apiChannel = channel;
	}

	protected virtual void SubscribeToCarEvents() { subscribed = true; }
	protected virtual void UnsubscribeFromCarEvents() { subscribed = false; }
	protected abstract void OnRaceStarted(TelemetrySnapshot snapshot);

	protected virtual void OnRaceFinished(TelemetrySnapshot snapshot) { }

}
