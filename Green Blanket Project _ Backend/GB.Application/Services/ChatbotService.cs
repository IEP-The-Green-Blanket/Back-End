using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Net.Http;
using System.Threading.Tasks;
using GB.Application.DTOs;
using Microsoft.Extensions.Configuration;

namespace GB.Application.Services
{
    public class ChatbotService
    {
        private readonly HttpClient _httpClient;
        private readonly string _cohereApiKey;
        private readonly WaterQualityService _waterService;

        public ChatbotService(HttpClient httpClient, IConfiguration config, WaterQualityService waterService)
        {
            _httpClient = httpClient;
            _cohereApiKey = config["Cohere:ApiKey"] ?? "";
            _waterService = waterService;
        }

        private async Task<string> GetCohereResponse(string prompt)
        {
            if (string.IsNullOrEmpty(_cohereApiKey))
                return "Cohere API Key is missing in appsettings.json.";

            var requestBody = new
            {
                model = "command-r-plus-08-2024",
                messages = new[] { new { role = "user", content = prompt } }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.cohere.com/v2/chat");
            request.Headers.Add("Authorization", $"Bearer {_cohereApiKey}");
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return "AI Uplink Error.";

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("message").GetProperty("content")[0].GetProperty("text").GetString() ?? "No response.";
        }

        public async Task<ChatResponse> GetResponse(string message)
        {
            // 1. Fetch the data packet from our service
            var dataFacts = await _waterService.GetAiStatusPacketAsync();
            string factSheet = dataFacts != null ? JsonSerializer.Serialize(dataFacts) : "OFFLINE";

            // 2. Capture current time for the "Freshness Check"
            string now = DateTime.Now.ToString("MMMM dd, yyyy HH:mm");

            // 3. HUMAN-CENTRIC SYSTEM PROMPT
            var prompt = $@"
                ROLE: 
                You are the 'Green Blanket Project' assistant. You are a friendly local expert who translates water science into plain English for the Harties community.

                CONTEXT:
                - Current Server Time: {now}
                - Latest Telemetry Packet (JSON): {factSheet}

                STRICT BEHAVIOR RULES:
                1. BE DIRECT: Answer the user's specific question in the very first sentence. Do not bury the answer behind pleasantries or long introductions.
                2. DATA FRESHNESS: Check the 'isStale' flag in the JSON. If true, you MUST include a concise warning (e.g., 'Note: My latest readings are from [dataTimestamp]...') within your response.
                3. NO JARGON: Always use 'Average Joe' language. Translate terms like 'Sodium Adsorption Ratio' to 'soil safety for crops' and 'Larson-Skold Index' to 'motor corrosion risk'.
                4. RELEVANCE ONLY: Provide only the telemetry requested. If the user asks about swimming, do not mention boat motors or farming unless the safety status is critical for everyone.
                5. TRENDS: Use 'dailySummary' to state if values are currently rising or falling compared to the 24-hour average.
                6. HONESTY: If data is 'OFFLINE', state clearly that sensor connection is lost and you cannot provide live data.

                WEBSITE NAVIGATION MAP:
                - If they need GRAPHS/TRENDS: Click the 'Analytics' tab in the sidebar.
                - If they see POLLUTION/DEAD FISH: Use the 'Alert Us' button (top left).
                - If they want Community Sightings: Go to the 'Reports' page.
                - If they are LOST: The 'Home' logo returns you to the main dashboard.

                CONSTRAINTS: 
                - MAXIMUM 2.5 SENTENCES. 
                - Lead with the answer, follow with guidance.

                USER INQUIRY: {message}
            ";

            var aiResponse = await GetCohereResponse(prompt);
            return new ChatResponse { Response = aiResponse };
        }
    }
}