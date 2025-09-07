
using Microsoft.AspNetCore.Mvc;
using TestScalpingBackend.Services;
using MetaQuotes.MT5CommonAPI;
using MetaQuotes.MT5ManagerAPI;
using TestScalpingBackend.Models;
using Microsoft.AspNetCore.SignalR;
using System.Data;
using System.Runtime.Serialization;

namespace TestScalpingBackend.Controllers
{

    [ApiController]
    [Route("/api/v1/[controller]")]
    public class FindDuplicationController : ControllerBase
    {
        public CIMTManagerAPI cIMTManagerAPI;
        public FindDuplicationController(MT5Connection connection)
        {
            cIMTManagerAPI = connection.m_manager;
        }

        [HttpGet("getDealsWithDoubleProfitremoval")]
        public IActionResult getDealsWithDoubleProfitremoval()
        {

            CIMTDealArray dealAray = cIMTManagerAPI.DealCreateArray();

            DateTimeOffset start = DateTimeOffset.UtcNow.AddDays(-5);
            DateTimeOffset End = DateTimeOffset.UtcNow.AddDays(2);

            long from = start.ToUnixTimeSeconds();
            long to = End.ToUnixTimeSeconds();


            cIMTManagerAPI.DealRequestByGroup("*", from, to, dealAray);

            var OutDealsWithTwoTimesProfitDeducted = filteronlydoubleoutdeal(dealAray);

            Console.WriteLine("Total Deals with double profit out is  " + OutDealsWithTwoTimesProfitDeducted.Total());
            var response = OutDealsWithTwoTimesProfitDeducted.Total();

            Console.WriteLine("deal with id and and prfoti and login is " + OutDealsWithTwoTimesProfitDeducted.Next(0)?.Deal() + " " + OutDealsWithTwoTimesProfitDeducted.Next(0)?.Profit() + " " + OutDealsWithTwoTimesProfitDeducted.Next(0)?.Login());

            // GiveProfitAgain(OutDealsWithTwoTimesProfitDeducted);

            return Ok(new { message = "Deals Fetched Successfully", Status = true, Response = response });

        }

        public CIMTDealArray filteronlydoubleoutdeal(CIMTDealArray deals)
        {

            Dictionary<ulong, CIMTDeal> dict = new Dictionary<ulong, CIMTDeal>();

            // HashSet<CIMTDeal> OutDealsWithTwoTimesProfitDeducted = new HashSet<CIMTDeal>();

            CIMTDealArray OutDealsWithTwoTimesProfitDeducted = cIMTManagerAPI.DealCreateArray();

            for (uint i = 0, count = deals.Total(); i < count; i++)
            {
                CIMTDeal deal = deals.Next(i);

                if (deal.Action() == 2 && deal.Comment().Contains("Scalping Deduction"))
                {

                    var DealId = deal.Comment().Split('#')[1].Trim();

                    if (ulong.TryParse(DealId, out ulong dealid))
                    {
                        if (dict.ContainsKey(dealid))
                        {
                            OutDealsWithTwoTimesProfitDeducted.Add(deal);
                        }
                        else
                        {
                            dict.Add(dealid, deal);
                        }
                    }
                    else
                    {
                        Console.WriteLine("DealId is not a valid ulong: {DealId}");
                    }

                }

            }

            return OutDealsWithTwoTimesProfitDeducted;


        }

        public MTRetCode GiveProfitAgain(CIMTDealArray deals)
        {

            for (uint i = 0, count = deals.Total(); i < count; i++)
            {
                CIMTDeal deal = deals.Next(i);

                var profit = deal.Profit();
                var login = deal.Login();

                MTRetCode res = cIMTManagerAPI.DealerBalanceRaw(
                login,
                -profit,
                2,
                "",
                out ulong id
                );

                Console.WriteLine("Res " + res + " login " + login + " profit added  " + -profit + " with id of deal is " + id);
            }



            return MTRetCode.MT_RET_OK;

        }

    }

}

