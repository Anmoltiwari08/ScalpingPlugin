using MetaQuotes.MT5ManagerAPI;
using MetaQuotes.MT5CommonAPI;
using System;

namespace TestScalpingBackend.Services
{
    public class MT5Operations
    {

        public CIMTManagerAPI m_manager = null!;

        public MT5Operations(MT5Connection m_manager)
        {
            this.m_manager = m_manager.m_manager;
        }

        public MTRetCode GetDealsWithLoginAndSymbol(ulong[] login, long from, long to, out CIMTDealArray deals)
        {
            CIMTDealArray dealsArray = m_manager.DealCreateArray();
            MTRetCode result = m_manager.DealRequestByLogins(login, from, to, dealsArray);

            deals = dealsArray;
            return result;

        }

        public MTRetCode UpdateCredit(ulong login, double credit, uint type, string comment, out ulong dealid)
        {

            MTRetCode res = m_manager.DealerBalanceRaw(
                login,
                credit,
                type,
                comment,
                out ulong id
            );
            dealid = id;
            return res;

        }

        // public MTRetCode GetAllDealsFromGroups(out CIMTDealArray deals)
        // {

        //     CIMTDealArray dealsArray = m_manager.DealCreateArray();

        //     string mask = "*";
        //     long from = 0;
        //     long to = DateTimeToUnixTime(DateTime.UtcNow);

        //     var response = m_manager.DealRequestByGroup(mask, from, to, dealsArray);

        //     deals = dealsArray;
        //     return response;
        // }

        public long DateTimeToUnixTime(DateTime dateTime)
        {
            return ((DateTimeOffset)dateTime).ToUnixTimeSeconds();
        }

        public MTRetCode GetAllDealsByGroupInSpecifiedTime(ref CIMTDealArray dealsArray, long from, long to, string groupmask)
        {
            var response = m_manager.DealRequestByGroup(groupmask, from, to, dealsArray);

            return response;

        }

        public CIMTDealArray GetDealByLogin(ulong Login, long from, long to)
        {

            CIMTDealArray cIMTDealArray = m_manager.DealCreateArray();
            m_manager.DealRequestByLogins(new ulong[] { Login }, from, to, cIMTDealArray);

            return cIMTDealArray;

        }

        public CIMTDeal GetDealByID(ulong DealID)
        {
            CIMTDeal deal = m_manager.DealCreate();
            var response = m_manager.DealRequest(DealID, deal);
            return deal;

        }

        public CIMTOrder GetOrderByID(ulong DealID)
        {
            CIMTOrder order = m_manager.OrderCreate();
            var response = m_manager.OrderRequest(DealID, order);
            return order;
        }

    }

}


