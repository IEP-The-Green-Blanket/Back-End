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
            // 1. Get real-time facts from your Intelligence Engine
            var dataFacts = await _waterService.GetAiStatusPacketAsync();
            string factSheet = dataFacts != null
                ? JsonSerializer.Serialize(dataFacts)
                : "Live sensors are currently offline.";

            // 2. Capture the REAL current time so the AI knows today's date
            string today = DateTime.Now.ToString("MMMM dd, yyyy");

            // 3. REFINED HUMAN-CENTRIC PROMPT WITH DATE AWARENESS
            var prompt = $@"
                ROLE: You are the 'Green Blanket Assistant,' a friendly and professional human expert on the Hartbeespoort Dam.
                TODAY'S DATE: {today}
                SENSOR DATA (JSON): {factSheet}

                STRICT OPERATING RULES:
                1. OUTDATED DATA RULE: If the 'timestamp' in the JSON is not within a week range from {today}, you must politely mention that the data is from a previous reading (state the date) and that you are currently awaiting a fresh sensor update.
                2. BE HUMAN: Speak naturally and politely, as if to a peer. Answer ONLY the user's specific question.
                3. DATA-FIRST: If you don't have a specific reading, say you don't have it and pivot to a metric you DO have from the JSON. Never invent data.
                4. SAFETY: If 'swimSafety' is not 'Safe', advise caution. If 'skinIrritationRisk' is not 'None', warn about rashes.
                5. REPORTING: Suggest 'Alert Us' (top left) ONLY if they describe a real problem (bad odors, dead fish, etc).

                CONSTRAINTS:
                - STRICT MAXIMUM OF 3 SENTENCES.
                - Answer ONLY the question asked.

                User Question: {message}
                ";

            var aiResponse = await GetCohereResponse(prompt);
            return new ChatResponse { Response = aiResponse };
        }
    }
}