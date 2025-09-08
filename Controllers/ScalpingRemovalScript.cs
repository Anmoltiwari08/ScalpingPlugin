using Microsoft.AspNetCore.Mvc;
using TestScalpingBackend.Services;
using MetaQuotes.MT5CommonAPI;
using MetaQuotes.MT5ManagerAPI;
using TestScalpingBackend.Models;
using System.Data;
using DictionaryExample;
using TestScalpingBackend.Helper;
using Microsoft.AspNetCore.Http.HttpResults;

namespace TestScalpingBackend.Controllers
{

    [ApiController]
    [Route("/api/v1/[controller]")]
    public class ProfitRemovalScriptController : ControllerBase
    {
        private MT5Operations mT5Operations;
        private ILogger<ProfitRemovalScriptController> logger;
        private AppDbContext appDbContext;
        private readonly CIMTManagerAPI m_manager;
        private SymbolStore symbolStore;
        private readonly IProfitDeductionQueue _profitDeductionQueue;
        private ScalpingFindHelper scalpingFindHelper;

        public ProfitRemovalScriptController(MT5Connection mT5Connection, MT5Operations mT5Operations, ILogger<ProfitRemovalScriptController> logger, AppDbContext context, IProfitDeductionQueue profitDeductionQueue, SymbolStore symbolStore, ScalpingFindHelper scalpingFindHelper)
        {
            this.mT5Operations = mT5Operations;
            this.logger = logger;
            appDbContext = context;
            m_manager = mT5Connection.m_manager;
            _profitDeductionQueue = profitDeductionQueue;
            this.symbolStore = symbolStore;
            this.scalpingFindHelper = scalpingFindHelper;
        }

        [HttpPost("ProfitDeductionByTimeWindow")]
        public async Task<IActionResult> ScalpingProfitDeduction([FromBody] ProfitRemovalInTimeRange requestModel)
        {
            try
            {

                if (!ModelState.IsValid)
                {
                    logger.LogInformation("Request model is invalid" + ModelState);
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return BadRequest(new { errors = errors });
                }

                string mask = "*";

                DateTime StartTime = requestModel.StartTime;
                DateTime EndTime = requestModel.EndTime;
                logger.LogInformation("From " + StartTime + " To " + EndTime);

                var NewStartOne = new DateTimeOffset(StartTime.ToUniversalTime());
                var NewEndOne = new DateTimeOffset(EndTime.ToUniversalTime());

                logger.LogInformation("From " + NewStartOne + " To " + NewEndOne);

                long from = NewStartOne.ToUnixTimeSeconds();
                long to = NewEndOne.ToUnixTimeSeconds();

                logger.LogInformation("From " + DateTimeOffset.FromUnixTimeSeconds(from) + " To " + DateTimeOffset.FromUnixTimeSeconds(to) );
                CIMTDealArray accounts = m_manager.DealCreateArray();

                logger.LogInformation("Accounts creates its initial value is " + accounts.Total() + accounts);
                mT5Operations.GetAllDealsByGroupInSpecifiedTime(out accounts, from, to, mask);

                logger.LogInformation("Total Deals In the Specified Time Range" + accounts.Total());

                if (accounts.Total() == 0 || accounts == null)
                {
                    accounts?.Dispose();
                    return Ok(new
                    {
                        message = $"No Deals Found in the Given time Range {requestModel.StartTime} to {requestModel.EndTime}"
                    });
                }

                scalpingFindHelper.OutDealsWithoutScalpingDeductionFound(accounts, out CIMTDealArray EntryOutDealsWithoutScalpingDeductionInGivenTime);
                logger.LogInformation("Total out deals may need to remove or not both  scalping DeductioninGivenTime" + EntryOutDealsWithoutScalpingDeductionInGivenTime.Total());

                if (EntryOutDealsWithoutScalpingDeductionInGivenTime.Total() == 0)
                {
                    EntryOutDealsWithoutScalpingDeductionInGivenTime?.Dispose();
                    return Ok(new { message = $"No out Deals whose Scalping Deduction is Found in the Given time Range {requestModel.StartTime} to {requestModel.EndTime}" });
                }

                scalpingFindHelper.CheckDealsWithin3MinGap(EntryOutDealsWithoutScalpingDeductionInGivenTime, out CIMTDealArray GetDealsWithin3MinGap, out CIMTOrderArray GetEnrtryInOrderArray);
                logger.LogInformation("Total out deals whose scalping DeductioninGivenTime is under 3 minutes are " + GetDealsWithin3MinGap.Total());
                if (GetDealsWithin3MinGap.Total() == 0)
                {
                    GetDealsWithin3MinGap?.Dispose();
                    GetEnrtryInOrderArray?.Dispose();
                    return Ok(new { message = $"No Deals Fouund for actual scalpoing deduction ,  in the Given time Range {requestModel.StartTime} to {requestModel.EndTime}" });
                }

                scalpingFindHelper.CheckDealsInDatabase(GetDealsWithin3MinGap, out CIMTDealArray GetDealsNotFoundInDatabaseHistory);
                logger.LogInformation("Removed all the deals taht exist in the database  " + GetDealsNotFoundInDatabaseHistory.Total());

                if (GetDealsNotFoundInDatabaseHistory.Total() == 0)
                {
                    GetDealsNotFoundInDatabaseHistory?.Dispose();
                    GetEnrtryInOrderArray?.Dispose();
                    return Ok(new { message = $"All Deals Found in database  in the Given time Range {requestModel.StartTime} to {requestModel.EndTime}" });
                }

                scalpingFindHelper.CheckDealsInDealsHistory(GetDealsNotFoundInDatabaseHistory, out CIMTDealArray FinalUnmatchedDeals, from);
                logger.LogInformation("Finally check the deals in their deal histroy maybe some of them are deducted , after that final unmatched deals are  " + FinalUnmatchedDeals.Total());

                if (FinalUnmatchedDeals.Total() == 0)
                {
                    FinalUnmatchedDeals?.Dispose();
                    GetEnrtryInOrderArray?.Dispose();
                    return Ok(new { message = $"No Deals Found in the Given time Range {requestModel.StartTime} to {requestModel.EndTime}" });
                }

                List<ProfitOutDeals> dataofdeals = scalpingFindHelper.UnmatchedDealModal(FinalUnmatchedDeals, GetEnrtryInOrderArray);

                await _profitDeductionQueue.EnqueueRangeAsync(dataofdeals);

                FinalUnmatchedDeals?.Dispose();

                GetEnrtryInOrderArray?.Dispose();

                return Ok(new
                {
                    message = "Deals Fetched Successfully",
                    Status = true,
                    Data = dataofdeals
                });

            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in ProfitDeductionByTimeWindow");
                return Problem(
                "Error in ProfitDeductionByTimeWindow",
                 statusCode: 500);
            }
        }

        [HttpPost("CheckTimeZone")]
        public async Task<IActionResult> CheckTimeZone([FromBody] ProfitRemovalInTimeRange requestModel)
        {
            // Step 1: Define the range
            var from = requestModel.StartTime;
            var to = requestModel.EndTime;

            Console.WriteLine($"Original DateTime From: {from}, To: {to}");

            // Step 2: Convert to UTC
            var fromUtc = from.ToUniversalTime();
            var toUtc = to.ToUniversalTime();
            Console.WriteLine($"UTC DateTime From: {fromUtc}, To: {toUtc}");

            // Step 3: Create DateTimeOffset in UTC
            var fromOffset = new DateTimeOffset(fromUtc, TimeSpan.Zero);
            var toOffset = new DateTimeOffset(toUtc, TimeSpan.Zero);
            Console.WriteLine($"DateTimeOffset (UTC) From: {fromOffset}, To: {toOffset}");

            // Step 4: Convert to Unix time
            var fromUnix = fromOffset.ToUnixTimeSeconds();
            var toUnix = toOffset.ToUnixTimeSeconds();
            Console.WriteLine($"Unix Time From: {fromUnix}, To: {toUnix}");

            // Step 5: Convert Unix time back to DateTimeOffset
            var fromOffsetBack = DateTimeOffset.FromUnixTimeSeconds(fromUnix);
            var toOffsetBack = DateTimeOffset.FromUnixTimeSeconds(toUnix);
            Console.WriteLine($"Back to DateTimeOffset From: {fromOffsetBack}, To: {toOffsetBack}");

            // Step 6: Convert DateTimeOffset back to DateTime (UTC)
            var fromDateTimeBack = fromOffsetBack.UtcDateTime;
            var toDateTimeBack = toOffsetBack.UtcDateTime;
            Console.WriteLine($"Back to DateTime (UTC) From: {fromDateTimeBack}, To: {toDateTimeBack}");

            return Ok(new { message = "Checked UTC conversion successfully", Status = true });
        }


    }

}


