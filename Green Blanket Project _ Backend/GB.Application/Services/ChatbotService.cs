using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using GB.Application.DTOs;
using Microsoft.Extensions.Configuration;

namespace GB.Application.Services
{
    public class ChatbotService
    {

        private readonly HttpClient _httpClient;
        private readonly string _cohereApiKey;

        public ChatbotService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _cohereApiKey = config["Cohere:ApiKey"];
        }
        private async Task<string> GetCohereResponse(string prompt)
        {
            var requestBody = new
            {
                model = "command-a-03-2025",
                messages = new[]
                {
                new
                {
                    role = "user",
                    content = prompt
                }
            }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.cohere.com/v2/chat");

            request.Headers.Add("Authorization", $"Bearer {_cohereApiKey}");
            request.Headers.Add("Cohere-Version", "2024-05-01");

            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return $"Cohere API Error: {error}";
            }

            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);

            try
            {
                return doc.RootElement
                    .GetProperty("message")
                    .GetProperty("content")[0]
                    .GetProperty("text")
                    .GetString();
            }
            catch
            {
                return "Sorry, I couldn't generate a response.";
            }
        }

        public async Task<ChatResponse> GetResponse(string message)
        {
            //Get water status
            var statusResponse = await _httpClient.GetAsync("https://localhost:5050/api/waterquality/status");

            if (!statusResponse.IsSuccessStatusCode)
            {
                return new ChatResponse { Response = "Error fetching water status." };
            }

            var statusJson = await statusResponse.Content.ReadAsStringAsync();
            var statusData = JsonSerializer.Deserialize<Dictionary<string, string>>(statusJson);

            var status = statusData != null && statusData.ContainsKey("status")
                ? statusData["status"]
                : "Unknown";

            //Get chemical data
            var chemResponse = await _httpClient.GetAsync("https://localhost:5050/api/waterquality/chemicals");

            if (!chemResponse.IsSuccessStatusCode)
            {
                return new ChatResponse { Response = "Error fetching chemical data." };
            }

            var chemJson = await chemResponse.Content.ReadAsStringAsync();
            var chemData = JsonSerializer.Deserialize<Dictionary<string, double>>(chemJson);

            var nitrate = chemData != null && chemData.ContainsKey("nitrate") ? chemData["nitrate"] : 0;
            var phosphate = chemData != null && chemData.ContainsKey("phosphate") ? chemData["phosphate"] : 0;
            var oxygen = chemData != null && chemData.ContainsKey("oxygen") ? chemData["oxygen"] : 0;

            //Build prompt
            var prompt = $@"
            You are a helpful assistant for a dam water quality system.

            If the question is about water safety, swimming, or chemicals:
            - Use the data below
            - Safe → swimming allowed
            - Neutral → swimming allowed with caution
            - Unsafe → swimming not recommended
            - Only explain chemicals if asked

            If the question is NOT related to water:
            - Answer normally like a friendly chatbot

            Keep answers under 3 sentences.

            Current Data:
            Status: {status}
            Nitrate: {nitrate}
            Phosphate: {phosphate}
            Oxygen: {oxygen}

            User Question: {message}
            ";

            //Call Cohere
            var aiResponse = await GetCohereResponse(prompt);

            return new ChatResponse
            {
                Response = aiResponse
            };
        }
    }
}
