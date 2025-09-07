using System.Collections.Concurrent;

namespace DictionaryExample
{
    public class SymbolStore
    {
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
                if (!_symbols.TryAdd(symbol, true))
                {
                    _logger.LogError($"In the initial creating of the dictionary ,  adding Found symbols in the conncurrent dictionary giving error in Symbol :{symbol}");

                }
            }

        }

        public bool AddSymbol(string symbolName)
        {
            try
            {
                if (_symbols.TryAdd(symbolName, true))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, $"Error adding symbol {symbolName}");
                _logger.LogError($"Error adding symbol {symbolName}", ex);
                return false;
            }

        }

        public bool RemoveSymbol(string symbolName)
        {
            try
            {
                if (_symbols.TryRemove(symbolName, out _))
                {
                    return true;
                }
                else
                {
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
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error checking if symbol from dicionary {symbolName} ", ex);
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

