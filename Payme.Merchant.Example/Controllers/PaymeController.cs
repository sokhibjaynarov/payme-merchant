using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Payme.Merchant.Example.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymeController : ControllerBase
    {
        private readonly PaymeDispatcher _dispatcher;

        public PaymeController(PaymeDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        [HttpPost]
        public async Task<IActionResult> Post()
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            var authHeader = Request.Headers["Authorization"].ToString();

            var responseJson = await _dispatcher.DispatchAsync(authHeader, body);

            return Content(responseJson, "application/json");
        }
    }
}
