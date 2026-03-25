using Parquet.Data;
using Parquet.Schema;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ParquetApiDemo.Services
{
    public class ParquetSchemaService
    {
        /// <summary>
        /// Maps common types to SQL Server data types
        /// </summary>
        public static string MapTypeToSqlType(string fieldName, object sampleValue)
        {
            if (sampleValue == null || sampleValue == DBNull.Value)
                return "NVARCHAR(MAX)";

            var valueType = sampleValue.GetType();

            return valueType.Name switch
            {
                "Boolean" => "BIT",
                "Byte" => "TINYINT",
                "Int16" => "SMALLINT",
                "Int32" => "INT",
                "Int64" => "BIGINT",
                "Single" => "REAL",
                "Double" => "FLOAT",
                "Decimal" => "DECIMAL(18, 2)",
                "DateTime" => "DATETIME2",
                "String" => "NVARCHAR(MAX)",
                "Byte[]" => "VARBINARY(MAX)",
                _ => "NVARCHAR(MAX)"
            };
        }

        /// <summary>
        /// Generates SQL CREATE TABLE statement from Parquet schema
        /// </summary>
        public static string GenerateCreateTableSql(string tableName, IEnumerable<DataField> dataFields)
        {
            var columns = new List<string>();
            var fieldList = dataFields.ToList();

            // Check if any field is named 'id' (case-insensitive)
            bool hasIdColumn = fieldList.Any(f => f.Name.Equals("id", StringComparison.OrdinalIgnoreCase));

            // Only add auto-generated Id if the Parquet doesn't have an 'id' column
            if (!hasIdColumn)
                columns.Add("[RecordId] INT PRIMARY KEY IDENTITY(1,1)");

            foreach (var field in fieldList)
            {
                var sqlType = "NVARCHAR(MAX)"; // Default type for Parquet fields
                columns.Add($"[{field.Name}] {sqlType}");
            }

            return $"CREATE TABLE [{tableName}] ({string.Join(", ", columns)})";
        }

        /// <summary>
        /// Generates SQL INSERT statement for Parquet data
        /// </summary>
        public static string GenerateInsertSql(string tableName, IEnumerable<DataField> dataFields, object[] rowValues)
        {
            var columnNames = string.Join(", ", dataFields.Select(f => $"[{f.Name}]"));
            var parameterNames = string.Join(", ", Enumerable.Range(0, dataFields.Count()).Select(i => $"@p{i}"));

            return $"INSERT INTO [{tableName}] ({columnNames}) VALUES ({parameterNames})";
        }

        /// <summary>
        /// Generates table name from file name (removes extension and special characters)
        /// </summary>
        public static string GenerateTableName(string fileName)
        {
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            var tableName = System.Text.RegularExpressions.Regex.Replace(nameWithoutExtension, @"[^a-zA-Z0-9_]", "_");

            // Ensure it doesn't start with a number
            if (string.IsNullOrEmpty(tableName) || char.IsDigit(tableName[0]))
                tableName = "_" + tableName;

            // Ensure it's not too long (SQL Server limit is 128 characters)
            if (tableName.Length > 128)
                tableName = tableName.Substring(0, 128);

            return "tbl_" + tableName;
        }

        /// <summary>
        /// Checks if a table exists in the database
        /// </summary>
        public static string GenerateTableExistsSql(string tableName)
        {
            return $@"
                IF EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.TABLES 
                    WHERE TABLE_TYPE='BASE TABLE' AND TABLE_NAME='{tableName}'
                ) 
                SELECT 1 
                ELSE 
                SELECT 0";
        }

        /// <summary>
        /// Generates SQL to drop a table
        /// </summary>
        public static string GenerateDropTableSql(string tableName)
        {
            return $"DROP TABLE IF EXISTS [{tableName}]";
        }
    }
}
 