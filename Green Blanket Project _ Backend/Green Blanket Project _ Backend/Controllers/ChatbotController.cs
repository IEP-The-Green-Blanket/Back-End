using Microsoft.AspNetCore.Mvc;
using GB.Application.Services;
using GB.Application.DTOs;
using System.Threading.Tasks;

namespace Green_Blanket_Project___Backend.Controllers
{
    [ApiController]
    // Note: If you encounter the "/api/api" bug on live, check your BasePath in Program.cs
    [Route("api/[controller]")]
    public class ChatbotController : ControllerBase
    {
        private readonly ChatbotService _chatbotService;

        public ChatbotController(ChatbotService chatbotService)
        {
            _chatbotService = chatbotService;
        }

        /// <summary>
        /// Receives a question from the user and returns an AI-generated answer 
        /// grounded in real-time water quality data.
        /// </summary>
        [HttpPost("ask")]
        public async Task<IActionResult> Chat([FromBody] ChatRequest request)
        {
            // 1. Validation
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { error = "Message cannot be empty." });
            }

            try
            {
                // 2. Call the service (which now handles the WaterQualityService injection)
                var result = await _chatbotService.GetResponse(request.Message);

                // 3. Return the ChatResponse object
                return Ok(result);
            }
            catch (System.Exception ex)
            {
                // Log exception here if you have a logger
                return StatusCode(500, new { error = "An internal error occurred while processing the AI request.", details = ex.Message });
            }
        }
    }
}