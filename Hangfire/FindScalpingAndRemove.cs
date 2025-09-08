using Microsoft.AspNetCore.Mvc;
using TestScalpingBackend.Services;
using MetaQuotes.MT5CommonAPI;
using MetaQuotes.MT5ManagerAPI;
using TestScalpingBackend.Models;
using System.Data;
using DictionaryExample;
using TestScalpingBackend.Helper;

namespace TestScalpingBackend.Hangfire
{
    public class FindScalpingAndRemove
    {
        private readonly MT5Operations mT5Operations;
        private readonly ILogger<FindScalpingAndRemove> logger;
        private readonly AppDbContext appDbContext;
        private readonly CIMTManagerAPI m_manager;
        private readonly SymbolStore symbolStore;
        private readonly IProfitDeductionQueue _profitDeductionQueue;
        private readonly ScalpingFindHelper scalpingFindHelper;

        public FindScalpingAndRemove(
            MT5Connection mT5Connection,
            MT5Operations mT5Operations,
            ILogger<FindScalpingAndRemove> logger,
            AppDbContext context,
            IProfitDeductionQueue profitDeductionQueue,
            SymbolStore symbolStore,
            ScalpingFindHelper scalpingFindHelper)
        {
            this.mT5Operations = mT5Operations;
            this.logger = logger;
            appDbContext = context;
            m_manager = mT5Connection.m_manager;
            _profitDeductionQueue = profitDeductionQueue;
            this.symbolStore = symbolStore;
            this.scalpingFindHelper = scalpingFindHelper;
        }

        public async Task Execute()
        {
            try
            {
                string mask = "*";
                DateTime StartTime = DateTime.Now;
                DateTime EndTime = DateTime.Now.AddHours(24);

                logger.LogInformation("[{Time}] Starting Hangfire job {JobName} from {Start} to {End}",
                    DateTime.UtcNow, nameof(FindScalpingAndRemove), StartTime, EndTime);

                long from = new DateTimeOffset(StartTime).ToUnixTimeSeconds();
                long to = new DateTimeOffset(EndTime).ToUnixTimeSeconds();
                CIMTDealArray accounts = m_manager.DealCreateArray();

                var response = mT5Operations.GetAllDealsByGroupInSpecifiedTime(out accounts, from, to, mask);

                if (accounts == null || accounts.Total() == 0)
                {
                    logger.LogWarning("[{Time}] No deals found in given range {Start} - {End}.",
                        DateTime.UtcNow, StartTime, EndTime);
                    accounts?.Dispose();
                    return;
                }

                logger.LogInformation("[{Time}] Found {Count} deals in given range.",
                    DateTime.UtcNow, accounts.Total());

                scalpingFindHelper.OutDealsWithoutScalpingDeductionFound(accounts,
                    out CIMTDealArray EntryOutDealsWithoutScalpingDeductionInGivenTime);

                if (EntryOutDealsWithoutScalpingDeductionInGivenTime.Total() == 0)
                {
                    logger.LogInformation("[{Time}] No deals found without scalping deduction.",
                        DateTime.UtcNow);
                    EntryOutDealsWithoutScalpingDeductionInGivenTime?.Dispose();
                    return;
                }

                logger.LogInformation("[{Time}] Found {Count} deals without scalping deduction.",
                    DateTime.UtcNow, EntryOutDealsWithoutScalpingDeductionInGivenTime.Total());

                scalpingFindHelper.CheckDealsWithin3MinGap(EntryOutDealsWithoutScalpingDeductionInGivenTime,
                    out CIMTDealArray GetDealsWithin3MinGap,
                    out CIMTOrderArray GetEnrtryInOrderArray);

                if (GetDealsWithin3MinGap.Total() == 0)
                {
                    logger.LogInformation("[{Time}] No deals found within 3 minute gap.",
                        DateTime.UtcNow);
                    GetDealsWithin3MinGap?.Dispose();
                    GetEnrtryInOrderArray?.Dispose();
                    return;
                }

                logger.LogInformation("[{Time}] Found {Count} deals within 3 minute gap.",
                    DateTime.UtcNow, GetDealsWithin3MinGap.Total());

                scalpingFindHelper.CheckDealsInDatabase(GetDealsWithin3MinGap,
                    out CIMTDealArray GetDealsNotFoundInDatabaseHistory);

                if (GetDealsNotFoundInDatabaseHistory.Total() == 0)
                {
                    logger.LogInformation("[{Time}] All deals already exist in database history.",
                        DateTime.UtcNow);
                    GetDealsNotFoundInDatabaseHistory?.Dispose();
                    GetEnrtryInOrderArray?.Dispose();
                    return;
                }

                logger.LogInformation("[{Time}] Found {Count} deals not in database history.",
                    DateTime.UtcNow, GetDealsNotFoundInDatabaseHistory.Total());

                scalpingFindHelper.CheckDealsInDealsHistory(GetDealsNotFoundInDatabaseHistory,
                    out CIMTDealArray FinalUnmatchedDeals, from);

                if (FinalUnmatchedDeals.Total() == 0)
                {
                    logger.LogInformation("[{Time}] No unmatched deals found after checking deal history.",
                        DateTime.UtcNow);
                    FinalUnmatchedDeals?.Dispose();
                    GetEnrtryInOrderArray?.Dispose();
                    return;
                }

                logger.LogInformation("[{Time}] Found {Count} unmatched deals. Preparing for profit deduction queue.",
                    DateTime.UtcNow, FinalUnmatchedDeals.Total());

                List<ProfitOutDeals> dataofdeals =
                    scalpingFindHelper.UnmatchedDealModal(FinalUnmatchedDeals, GetEnrtryInOrderArray);

                await _profitDeductionQueue.EnqueueRangeAsync(dataofdeals);

                logger.LogInformation("[{Time}] Successfully enqueued {Count} deals into profit deduction queue.",
                    DateTime.UtcNow, dataofdeals.Count);

                FinalUnmatchedDeals?.Dispose();
                GetEnrtryInOrderArray?.Dispose();
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "[{Time}] Hangfire job {JobName} failed. Exception: {ExceptionType}, Message: {Message}",
                    DateTime.UtcNow, nameof(FindScalpingAndRemove), ex.GetType().Name, ex.Message);
            }
        }
    }
}
