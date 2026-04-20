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
                // FIXED: Using a versioned model string to bypass alias retirement issues.
                // If this still fails, try "command-r-08-2024" (the standard version).
                model = "command-r-plus-08-2024",
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.cohere.com/v2/chat");
            request.Headers.Add("Authorization", $"Bearer {_cohereApiKey}");

            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorDetails = await response.Content.ReadAsStringAsync();
                return $"AI Service Error: {response.StatusCode} - {errorDetails}";
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            try
            {
                return doc.RootElement
                    .GetProperty("message")
                    .GetProperty("content")[0]
                    .GetProperty("text")
                    .GetString() ?? "I received an empty response from the AI.";
            }
            catch (Exception ex)
            {
                return $"Error parsing AI response: {ex.Message}";
            }
        }

        public async Task<ChatResponse> GetResponse(string message)
        {
            // 1. Fetch real status from your fixed Intelligence Engine
            var dataFacts = await _waterService.GetAiStatusPacketAsync();

            string dataContextJson = dataFacts != null
                ? JsonSerializer.Serialize(dataFacts)
                : "No live sensor data available.";

            // 2. Build the "Grounded" prompt (RAG pattern)
            var prompt = $@"
                ROLE: You are the 'Green Blanket Assistant,' a friendly and professional expert on the Hartbeespoort Dam. 
                TASK: Answer the User Question politely and accurately using ONLY the provided SENSOR DATA and UI RULES.

                SENSOR DATA (JSON):
                {dataContextJson}

                STRICT OPERATING RULES:
                1. HUMAN-LIKE GROUNDING: Answer the user's question directly. If the SENSOR DATA is missing a specific value, say you don't have that reading yet and pivot to a metric you DO have that might be helpful. Never invent data or draw conclusions about things not in the JSON.
                2. SUSPICIOUS ACTIVITY ONLY: Only direct the user to the 'Alert Us' button (top left) if they explicitly mention seeing something wrong, suspicious, or harmful, such as bad odors, illegal dumping, dead fish, or heavy sewage. 
                3. UI NAVIGATION: For account or login issues, suggest the buttons at the top right. For historical trends or farming data, suggest the icons in the sidebar on the left.
                4. SAFETY FIRST: If 'swimSafety' is not 'Safe', you must advise caution. If 'skinIrritationRisk' is not 'None', you must warn about potential rashes.

                CONSTRAINTS:
                - Tone: Polite, punctual, and helpful (like a human peer).
                - Answer ONLY the user's question.
                - Length: STRICT MAXIMUM OF 2 SENTENCES.

                User Question: {message}
                ";

            var aiResponse = await GetCohereResponse(prompt);

            return new ChatResponse { Response = aiResponse };
        }
    }
}