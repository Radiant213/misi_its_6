using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FormProduk
{
    internal class Koneksi
    {
        public static SqlConnection GetConnection()
        {
            string connectionString = @"Data Source=DESKTOP-KJM4I64\SQLEXPRESS;Initial Catalog=TokoDB;Integrated Security=True;";
            return new SqlConnection(connectionString);
        }
    }
}

