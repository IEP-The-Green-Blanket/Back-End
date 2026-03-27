using System;
using System.Collections.Generic;
using System.Text;
using GB.Application.DTOs;

namespace GB.Application.Services
{
    public class ChatbotService
    {

        private readonly HttpClient _httpClient;

        public ChatbotService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }
        public ChatResponse GetResponse(string message)
        {

            
            var text = message.ToLower();

            // Intent: explanation
            if (text.Contains("why") || text.Contains("reason"))
            {
                return new ChatResponse
                {
                    Response = "The water may be unsafe due to high levels of pollutants like nitrates and phosphates."
                };
            }

            // Intent: safety
            if (text.Contains("swim") || text.Contains("safe"))
            {
                var response = _httpClient.GetAsync("https://localhost:7166/api/waterquality/status").Result;

                var json = response.Content.ReadAsStringAsync().Result;

                var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);

                var status = data["status"];

                if (status == "Safe")
                {
                    return new ChatResponse
                    {
                        Response = "The water is currently safe. Swimming is allowed."
                    };
                }
                else if (status == "Neutral")
                {
                    return new ChatResponse
                    {
                        Response = "The water is currently Neutral. Swimming is allowed but don't swallow water."
                    };
                }
                else
                {
                    return new ChatResponse
                    {
                        Response = $"Swimming is not recommended because the water is currently {status}."
                    };
                }

                
            }

            // Intent: chemicals
            if (text.Contains("nitrate"))
            {
                return new ChatResponse
                {
                    Response = "Nitrates are nutrients often found in fertilizers. High levels can indicate pollution from agricultural runoff."
                };
            }

            if (text.Contains("phosphate"))
            {
                return new ChatResponse
                {
                    Response = "Phosphates can cause excessive algae growth, which reduces oxygen in the water."
                };
            }

            // Intent: reporting
            if (text.Contains("report") || text.Contains("pollution"))
            {
                return new ChatResponse
                {
                    Response = "You can report pollution by filling in the report form. I can guide you through it."
                };
            }
            if (text.Contains("chemical") || text.Contains("levels") || text.Contains("nitrate") || text.Contains("phosphate"))
            {
                var response = _httpClient.GetAsync("https://localhost:7166/api/waterquality/chemicals").Result;

                var json = response.Content.ReadAsStringAsync().Result;

                var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, double>>(json);

                var nitrate = data["nitrate"];
                var phosphate = data["phosphate"];
                var oxygen = data["oxygen"];

                string advice;

                if (nitrate > 5 || phosphate > 3)
                {
                    advice = "These levels are high so it indicates that the water is quite polluted. Avoid swimming";
                }else
                {
                    advice = "These levels are on the lower side so the water is on the safer side";
                }

                return new ChatResponse
                {
                    Response = $"Current water chemical levels are:\n" +
                   $"Nitrate: {nitrate}\n" +
                   $"Phosphate: {phosphate}\n" +
                   $"Oxygen: {oxygen}"
                };

            }
            
            // Fallback
            return new ChatResponse
            {
                Response = "I'm not sure I understood that. You can ask me about water safety, chemicals, or reporting pollution."
            };
        
        }
    }
}
