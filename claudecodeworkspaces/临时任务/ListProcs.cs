using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace ZenergyBFSI.Workspace.VerifyProject
{
    class ListProcs
    {
        private const string CONNECTION_STRING = "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=VisionProgram;User ID=sa;Password=123456789;TrustServerCertificate=True";

        static void Main(string[] args)
        {
            Console.WriteLine("========== 数据库结构查询 ==========\n");

            using (var conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                Console.WriteLine($"数据库: VisionProgram");
                Console.WriteLine($"服务器: {(localdb)\\MSSQLLocalDB}\n");

                // 查询所有存储过程
                Console.WriteLine("---------- 存储过程列表 ----------");
                string procSql = @"SELECT name, create_date, modify_date
                                   FROM sys.procedures
                                   WHERE name LIKE '%BlueFilm%' OR name LIKE '%Harness%' OR name LIKE '%T_BlueFilm%' OR name LIKE '%T_Harness%'
                                   ORDER BY name";
                using (var cmd = new SqlCommand(procSql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    bool hasAny = false;
                    while (reader.Read())
                    {
                        hasAny = true;
                        Console.WriteLine($"  {reader["name"]}");
                    }
                    if (!hasAny)
                    {
                        Console.WriteLine("  (未找到与 BlueFilm 或 Harness 相关的存储过程)");
                    }
                }

                // 查询所有表
                Console.WriteLine("\n---------- 表结构 ----------");
                string tableSql = @"SELECT TABLE_NAME
                                    FROM INFORMATION_SCHEMA.TABLES
                                    WHERE TABLE_TYPE = 'BASE TABLE'
                                    ORDER BY TABLE_NAME";
                using (var cmd = new SqlCommand(tableSql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string tableName = reader["TABLE_NAME"].ToString();
                        Console.WriteLine($"  {tableName}");
                    }
                }
            }

            Console.WriteLine("\n===================================");
        }
    }
}
