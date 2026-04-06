using Microsoft.AspNetCore.Mvc;
using GB.Application.Services;
using GB.Application.DTOs;


namespace Green_Blanket_Project___Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatbotController : ControllerBase
    {
        private readonly ChatbotService _service;

        public ChatbotController(ChatbotService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> Chat(ChatRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest("Message cannot be empty.");
            }

            var response = await _service.GetResponse(request.Message);

            return Ok(response);
        }
    }
}
