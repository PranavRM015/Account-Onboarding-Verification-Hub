using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Risk.Api.Contracts;
using Risk.Api.Services;

namespace Risk.Api.Controllers
{
    [ApiController]
    [Authorize(Roles = "admin")]
    [Route("internal/analyze")]
    public class AnalyzeController : ControllerBase
    {
        private readonly RiskAnalyzer _analyzer;

        public AnalyzeController(RiskAnalyzer analyzer)
        {
            _analyzer = analyzer;
        }

        [HttpPost]
        public ActionResult<AnalyzeResponse> Analyze([FromBody] AnalyzeRequest customer)
        {
            return Ok(_analyzer.Analyze(customer));
        }
    }
}
