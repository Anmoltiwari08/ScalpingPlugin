using Microsoft.AspNetCore.Mvc;
using TestScalpingBackend.Services;
using MetaQuotes.MT5ManagerAPI;
using TestScalpingBackend.Models;
using Microsoft.EntityFrameworkCore;

namespace TestScalpingBackend.Controllers
{
    [ApiController]
    [Route("/api/v1/[controller]")]
    public class HomeController : ControllerBase
    {

        private readonly DealSubscribe _dealSubscribe;
        private readonly MT5Operations mT5Operations;
        private readonly CIMTManagerAPI m_manager;
        private readonly AppDbContext _context;

        public HomeController(MT5Connection mt5Connection, AppDbContext context)
        {
            // _dealSubscribe = dealSubscribe;
            // this.mT5Operations = new MT5Operations(dealSubscribe.m_manager);
            m_manager = mt5Connection.m_manager;
            _context = context;

        }

        [HttpGet("toggle-creditprofit")]
        public IActionResult ToggleCreditProfit()
        {
            _dealSubscribe.CreditProfit = !_dealSubscribe.CreditProfit;
            Console.WriteLine(_dealSubscribe.CreditProfit);
            return Ok(new { CreditProfit = _dealSubscribe.CreditProfit });
        }

        [HttpGet("current-creditProfit")]
        public IActionResult CurrentProfitCredit()
        {
            return Ok(new { CreditProfit = _dealSubscribe.CreditProfit });
        }
           
        [HttpPost("ClosedDealsWithRemovedProfit")]
        public async Task<IActionResult> GetClosedPositionsWithRemovedProfit([FromBody] RequestModel requestModel)
        {
            // Console.WriteLine("GetClosedPositionsWithRemovedProfit");

            if (!ModelState.IsValid)
            {
                Console.WriteLine("Request Model is invalid ");
                return BadRequest(ModelState);
            }

            var profitDealsQuery = _context.ProfitOutDeals.AsQueryable();

            // Search filter
            profitDealsQuery = ApplySearchFilter(profitDealsQuery, requestModel.SearchColumn, requestModel.Search);

            // Sorting
            profitDealsQuery = ApplySorting(profitDealsQuery, requestModel.SortKey, requestModel.SortDir);

            // Pagination
            var totalDeals = await profitDealsQuery.CountAsync();
            var totalPages = (int)Math.Ceiling(totalDeals / (double)requestModel.pageSize);

            var pagedDeals = await profitDealsQuery
                .Skip((requestModel.page - 1) * requestModel.pageSize)
                .Take(requestModel.pageSize)
                .ToListAsync();
 
            return Ok(new
            {
                Deals = pagedDeals,
                TotalDeals = totalDeals,
                Pagination = new
                {
                    Page = requestModel.page,
                    PageSize = requestModel.pageSize,
                    TotalPages = totalPages
                }
            });

        }
         
        private IQueryable<ProfitOutDeals> ApplySearchFilter(IQueryable<ProfitOutDeals> query, string column, string term)
        {
            if (string.IsNullOrWhiteSpace(column) || string.IsNullOrWhiteSpace(term) )
                return query;

            term = term.ToLower();

            switch (column.ToLower())
            {
                case "profit": return query.Where(d => d.Profit.ToString().Contains(term));
                case "dealid": return query.Where(d => d.DealId.ToString().Contains(term));
                case "symbol": return query.Where(d => d.Symbol.ToLower().Contains(term));
                case "entry": return query.Where(d => d.Entry.ToString().Contains(term));
                case "action": return query.Where(d => d.Action.ToString().Contains(term));
                case "positionid": return query.Where(d => d.PositionID.ToString().Contains(term));
                case "login": return query.Where(d => d.Login.ToString().Contains(term));
                case "volume": return query.Where(d => d.Volume.ToString().Contains(term));
                case "profitout": return query.Where(d => d.ProfitOut.ToString().Contains(term));
                case "comment": return query.Where(d => d.Comment.ToLower().Contains(term));
                default: return query;
            }
        }
        
        private IQueryable<ProfitOutDeals> ApplySorting(IQueryable<ProfitOutDeals> query, string sortBy, string direction)
        {
            bool descending = direction?.ToLower() == "desc";

            return sortBy?.ToLower() switch
            {
                "profit" => descending ? query.OrderByDescending(d => d.Profit) : query.OrderBy(d => d.Profit),
                "dealid" => descending ? query.OrderByDescending(d => d.DealId) : query.OrderBy(d => d.DealId),
                "symbol" => descending ? query.OrderByDescending(d => d.Symbol) : query.OrderBy(d => d.Symbol),
                "closingtime" => descending ? query.OrderByDescending(d => d.ClosingTime) : query.OrderBy(d => d.ClosingTime),
                "entry" => descending ? query.OrderByDescending(d => d.Entry) : query.OrderBy(d => d.Entry),
                "action" => descending ? query.OrderByDescending(d => d.Action) : query.OrderBy(d => d.Action),
                "positionid" => descending ? query.OrderByDescending(d => d.PositionID) : query.OrderBy(d => d.PositionID),
                "openingtime" => descending ? query.OrderByDescending(d => d.OpeningTime) : query.OrderBy(d => d.OpeningTime),
                "login" => descending ? query.OrderByDescending(d => d.Login) : query.OrderBy(d => d.Login),
                "volume" => descending ? query.OrderByDescending(d => d.Volume) : query.OrderBy(d => d.Volume),
                "profitout" => descending ? query.OrderByDescending(d => d.ProfitOut) : query.OrderBy(d => d.ProfitOut),
                "comment" => descending ? query.OrderByDescending(d => d.Comment) : query.OrderBy(d => d.Comment),
                _ => query
            };
        }

 

    }

}


