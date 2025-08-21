using MetaQuotes.MT5ManagerAPI;
using MetaQuotes.MT5CommonAPI;
using System;

namespace TestScalpingBackend.Services
{
    public class MT5Connection
    {
        uint MT5_CONNECT_TIMEOUT = 52000000;

        public CIMTManagerAPI m_manager = null!;

        public MT5Connection()
        {
            LoggedInManager("./Datalogs");
        }

        public void CreateManager(string? path)
        {
            string message = string.Empty;

            MTRetCode res = MTRetCode.MT_RET_OK_NONE;

            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;

            string dllPath = Path.Combine(appDirectory, "MT5APIManager64.dll");

            //--- loading manager API
            if ((res = SMTManagerAPIFactory.Initialize(dllPath)) != MTRetCode.MT_RET_OK)
            {
                message = string.Format("Loading manager API failed ({0})", res.ToString());
                Console.WriteLine(message);
                return;
            }
            //--- creating manager interface
            m_manager = SMTManagerAPIFactory.CreateManager(SMTManagerAPIFactory.ManagerAPIVersion, path != null ? path : "./", out res);
            if ((res != MTRetCode.MT_RET_OK) || (m_manager == null))
            {
                SMTManagerAPIFactory.Shutdown();
                message = string.Format("Creating manager interface failed ({0})", (res == MTRetCode.MT_RET_OK ? "Managed API is null" : res.ToString()));
                Console.WriteLine(message);
                return;
            }

            Console.WriteLine("Creating manager");
        }

        public bool Login(string server, ulong login, string password)
        {
            //--- connect
            MTRetCode res = m_manager.Connect(server, login, password, null, CIMTManagerAPI.EnPumpModes.PUMP_MODE_FULL, MT5_CONNECT_TIMEOUT);
            if (res != MTRetCode.MT_RET_OK)
            {
                string message = string.Format(res.ToString());

                Console.WriteLine(message);
                Console.WriteLine("Connection failed");
                m_manager.LoggerOut(EnMTLogCode.MTLogErr, "Connection failed ({0})", res);
                return (false);
            }
            Console.WriteLine("Connection success");

            return (true);
        }

        public CIMTManagerAPI? LoggedInManager(string? path)
        {
            CreateManager(path);

            if (m_manager != null)
            {

                string? ip = "77.76.9.166:443";
                string? login = "1047";
                string? password = "Test@123";

                if (ip == null || login == null || password == null)
                {
                    Console.WriteLine("Credentials are invalid provided ");
                    return null;
                }

                Console.WriteLine("LOGINNG IN");
                if (Login(ip, ulong.Parse(login), password))
                {
                    Console.WriteLine("LOGIN  success");
                    return m_manager;
                }
                else
                {
                    Console.WriteLine("LOGIN  failed invalid credentials");
                }

            }

            return null;
        }
        
    }

}

     
