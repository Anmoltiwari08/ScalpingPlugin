using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace DictionaryExample
{
    public class SymbolStore
    {
        // Just store symbols as keys, bool as a dummy value
        private readonly ILogger<SymbolStore> _logger;
        private readonly IServiceProvider _serviceProvider;
        private ConcurrentDictionary<string, bool> _symbols = new ConcurrentDictionary<string, bool>();

        public SymbolStore(IServiceProvider serviceProvider, ILogger<SymbolStore> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            InitialDictionaryContainingSymbols();
        }

        public void InitialDictionaryContainingSymbols()
        {
            var Scope = _serviceProvider.CreateScope();
            var context = Scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var symbols = context.ScalpingSymbols.Select(x => x.SymbolName).ToList();
            foreach (var symbol in symbols)
            {
                _symbols.TryAdd(symbol, true);
            }
        }

        public bool AddSymbol(string symbolName)
        {
            try
            {
                if (_symbols.TryAdd(symbolName, true))
                {
                    _logger.LogInformation($"Added symbol {symbolName}");
                    return true;
                }
                else
                {
                    _logger.LogInformation($"Symbol {symbolName} already exists");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding symbol {symbolName}");
                return false;
            }

        }

        public bool RemoveSymbol(string symbolName)
        {
            try
            {
                if (_symbols.TryRemove(symbolName, out _))
                {
                    _logger.LogInformation($"Removed symbol {symbolName}");
                    return true;
                }
                else
                {
                    _logger.LogInformation($"Symbol {symbolName} does not exist");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing symbol {symbolName}");
                return false;
            }
        }

        public bool ContainsSymbol(string symbolName)
        {
            try
            {
                if (_symbols.ContainsKey(symbolName))
                {
                    _logger.LogInformation($"Symbol {symbolName} exists");
                    return true;
                }
                else
                {
                    _logger.LogInformation($"Symbol {symbolName} does not exist");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking if symbol {symbolName} exists");
                return false;
            }
        }

        public IEnumerable<string> GetAllSymbols()
        {
            try
            {
                return _symbols.Keys;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all symbols");
                return Enumerable.Empty<string>();
            }
        }
    }
}

