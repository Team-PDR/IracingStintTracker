using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

using IracingStintTracker.Dtos;

namespace ApiClient;

public class ApiClient 
{ 
	static HttpClient client = new HttpClient();
	private const string baseUrl = "http://127.0.0.1:8000";

	public ApiClient() {client.BaseAddress = new Uri(baseUrl); }

	internal static async Task CreateSessionAsync(SessionPostDto session) 
	{
		HttpResponseMessage response = await client.PostAsJsonAsync("/sessions", session);

	}
	internal static async Task CreateStintAsync(StintPostDto stint) { }
	internal static async Task UpdateStintAsync(StintPatchDto stint) { }
	internal static async Task GetLatestStintAsync(int sessionId) { }
	internal static async Task CreatePitstopAsync(PitstopPostDto pitstop) { }
	internal static async Task UpdatePitstopAsync(PitstopPatchDto pitstop) { }
	internal static async Task CreateLapAsync(LapPostDto lap) { }

}