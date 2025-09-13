#if WINDOWS
[DllImport("ODBCCP32.DLL", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool SQLConfigDataSourceW(UInt32 hwndParent, RequestFlags fRequest, string lpszDriver, string lpszAttributes);

namespace cdc_lib.Dsn
{
    public class DsnEditor
    {
        public DsnEditor()
        {
        }

        enum RequestFlags : int
        {
            ODBC_ADD_DSN = 1,
            ODBC_CONFIG_DSN = 2,
            ODBC_REMOVE_DSN = 3,
            ODBC_ADD_SYS_DSN = 4,
            ODBC_CONFIG_SYS_DSN = 5,
            ODBC_REMOVE_SYS_DSN = 6,
            ODBC_REMOVE_DEFAULT_DSN = 7
        }

        bool UpdateDsnServer(string name, string server)
        {
            var flag = RequestFlags.ODBC_CONFIG_SYS_DSN;
            string dsnNameLine = "DSN=" + name;
            string serverLine = "Server=" + server;

            string configString = new[] { dsnNameLine, serverLine }.Aggregate("", (str, line) => str + line + "\0");

            return SQLConfigDataSourceW(0, flag, "SQL Server", configString);
        }
    }


}

#endif
