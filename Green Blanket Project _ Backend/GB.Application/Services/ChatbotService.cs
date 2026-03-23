using System;
using System.Collections.Generic;
using System.Text;
using GB.Application.DTOs;

namespace GB.Application.Services
{
    public class ChatbotService
    {
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
                return new ChatResponse
                {
                    Response = "Swimming is currently not recommended due to unsafe water conditions."
                };
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

            // Fallback
            return new ChatResponse
            {
                Response = "I'm not sure I understood that. You can ask me about water safety, chemicals, or reporting pollution."
            };
        
        }
    }
}
