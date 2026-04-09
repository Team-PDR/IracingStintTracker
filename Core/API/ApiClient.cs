using StintTracker.Core.Dtos;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Channels;


namespace StintTracker.Core.API;

public class ApiClient
{
    public Action<bool>? HealthChecked;
	private Channel<ITrackerDto> channel;
	private int? sessionId = null;
	private int? carId = null;
	private int? stintId = null;

	private static readonly HttpClient client = new HttpClient() 
	{ 
		BaseAddress = new Uri("http://127.0.0.1:8000")
	};

	public ApiClient(Channel<ITrackerDto> channel)
	{
		this.channel = channel;
        _ = CheckHealthAsync();
	}

    private async Task CheckHealthAsync()
    {
        bool isHealthy;
        try
        {
            var response = await client.GetAsync("/health");
            isHealthy = response.IsSuccessStatusCode;
        }
        catch
        {
            isHealthy = false;
        }

        HealthChecked?.Invoke(isHealthy);
    }

	private async Task<bool> CreateSessionAsync(SessionPostDto session) 
	{
        sessionId = session.SessionId;
        carId = session.CarId;
        stintId = null;

		var response = await client.PostAsJsonAsync("/sessions", session);
		
		if (response.IsSuccessStatusCode)
		{
			var newSession = await response.Content.ReadFromJsonAsync<SessionResponseDto>();
			Console.WriteLine($"sessionId updated: {sessionId}, carId updated: {carId}");
        }

		return response.IsSuccessStatusCode;

	}
	private async Task<bool> CreateStintAsync(StintPostDto stint) 
	{
        if (sessionId == null || carId == null)
        {
            Console.WriteLine("Session or car not initialized");
            return false;
        }
		

        string url = $"/sessions/{sessionId}/cars/{carId}/stints";
		var response = await client.PostAsJsonAsync(url, stint);

        if (response.IsSuccessStatusCode)
        {
            var newStint = await response.Content.ReadFromJsonAsync<StintResponseDto>();
            stintId = newStint!.StintId;
            Console.WriteLine($"stintId updated: {stintId}");
        }
		else
		{
            Console.WriteLine($"Request failed: {response.StatusCode}");
        }

        return response.IsSuccessStatusCode;
    }
	private async Task<bool> UpdateStintAsync(StintPatchDto stint) 
	{
        if (stintId == null)
        {
            Console.WriteLine("No active stint - skipping update");
            return false;
        }

        string url = $"/sessions/{sessionId}/cars/{carId}/stints/{stintId}";
		HttpResponseMessage response = await client.PatchAsJsonAsync(url, stint);

		if (response.IsSuccessStatusCode)
		{
			Console.WriteLine($"Stint with id {stintId} successfully updated");
		}
		else
		{
            Console.WriteLine($"Request failed: {response.StatusCode}");
        }

		return response.IsSuccessStatusCode;
	}

    private async Task<bool> CreateLapAsync(LapPostDto lap)
    {
        if (stintId == null)
        {
            Console.WriteLine("No active stint - skipping lap");
            return false;
        }

        string url = $"/stints/{stintId}/laps";
        var response = await client.PostAsJsonAsync(url, lap);
        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("Lap successfully created");
        }
		else
		{
            Console.WriteLine($"Request failed: {response.StatusCode}");
        }

        return response.IsSuccessStatusCode;
    }

    private async Task<bool> CreatePitstopAsync(PitstopPostDto pitstop) 
	{
        if (sessionId == null || carId == null)
        {
            Console.WriteLine("Session or car not initialized");
            return false;
        }

        string url = $"/sessions/{sessionId}/cars/{carId}/pitstops";
		var response = await client.PostAsJsonAsync(url, pitstop);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Request failed: {response.StatusCode}");
        }

        return response.IsSuccessStatusCode;
	}
	private async Task<bool> UpdatePitstopAsync(PitstopPatchDto pitstop)
	{
        if (sessionId == null || carId == null)
        {
            Console.WriteLine("Session or car not initialized");
            return false;
        }

        string url = $"/sessions/{sessionId}/cars/{carId}/pitstops";
		var response = await client.PatchAsJsonAsync(url, pitstop);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Request failed: {response.StatusCode}");
        }

        return response.IsSuccessStatusCode;
	}
	private async Task SendToApi(ITrackerDto dto)
	{
        try
        {
            switch (dto)
            {
                case SessionPostDto session:
                    await CreateSessionAsync(session);
                    break;
                case StintPostDto stintPost:
                    await CreateStintAsync(stintPost);
                    break;
                case StintPatchDto stintPatch:
                    await UpdateStintAsync(stintPatch);
                    break;
                case LapPostDto lap:
                    await CreateLapAsync(lap);
                    break;
                case PitstopPostDto pitstopPost:
                    await CreatePitstopAsync(pitstopPost);
                    break;
                case PitstopPatchDto pitstopPatch:
                    await UpdatePitstopAsync(pitstopPatch);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"API client error: {ex.Message}");
        }

    }

	public async Task Run(CancellationToken token)
	{
        try
        {
            await foreach (var dto in channel.Reader.ReadAllAsync(token))
            {
                await SendToApi(dto);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("API Client stopped.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"API client error: {ex.Message}");
        }
		

	}
}