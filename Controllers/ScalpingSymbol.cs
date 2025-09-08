using Microsoft.AspNetCore.Mvc;
using DictionaryExample;

namespace TestScalpingBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SymbolController : ControllerBase
    {

        private readonly SymbolStore _symbolStore;
        private readonly ILogger<SymbolController> _logger;
          
        public SymbolController(SymbolStore symbolStore, ILogger<SymbolController> logger)
        {
            _symbolStore = symbolStore;
            _logger = logger;
        }
        
        [HttpGet("all")]
        public IActionResult GetAllSymbols()
        {
            try
            {
                var symbols = _symbolStore.GetAllSymbols();
                return Ok(symbols);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all symbols");
                return StatusCode(500, "Error getting all symbols");
            }
        }

        [HttpPost("add")]
        public IActionResult AddSymbol([FromBody] string symbolName)
        {
            if (string.IsNullOrWhiteSpace(symbolName))
                return BadRequest("Symbol name cannot be empty.");

            try
            {
                var result = _symbolStore.AddSymbol(symbolName);

                if (result)
                    return Ok($"Symbol '{symbolName}' added successfully.");
                else
                    return Conflict($"Symbol '{symbolName}' already exists.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding symbol '{symbolName}'", symbolName);
                return StatusCode(500, $"Error adding symbol '{symbolName}'");
            }
        }

        [HttpDelete("remove/{symbolName}")]
        public IActionResult RemoveSymbol(string symbolName)
        {

            if (string.IsNullOrWhiteSpace(symbolName))
                return BadRequest("Symbol name cannot be empty.");

            try
            {
                var result = _symbolStore.RemoveSymbol(symbolName);

                if (result)
                    return Ok($"Symbol '{symbolName}' removed successfully.");
                else
                    return NotFound($"Symbol '{symbolName}' not found.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing symbol '{symbolName}'", symbolName);
                return StatusCode(500, $"Error removing symbol '{symbolName}'");
            }
        }

        [HttpGet("contains/{symbolName}")]
        public IActionResult ContainsSymbol(string symbolName)
        {
            try
            {
                var result = _symbolStore.ContainsSymbol(symbolName);
                return Ok(new { Symbol = symbolName, Exists = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if symbol '{symbolName}' exists", symbolName);
                return StatusCode(500, $"Error checking if symbol '{symbolName}' exists");
            }
        }
        
    }
}
