using Microsoft.AspNetCore.Mvc;
using GB.Application.Services;


namespace Green_Blanket_Project___Backend.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class WaterQualityController : ControllerBase
    {
        private readonly WaterQualityService _service;

        public WaterQualityController(WaterQualityService service) { 
        
            _service = service;
        
        }


        [HttpGet("status")]

        public IActionResult GetStatus()
        {
            var status = _service.GetRandomStatus();

            return Ok(new { status = status });
        }

        [HttpGet("chemicals")]
        public IActionResult GetChemicals()
        {
            var chemicals = _service.GetRandomChemicals();

            return Ok(chemicals);
        }
    }
}
