using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Parquet;
using Parquet.Data;
using Parquet.Schema;
using ParquetApiDemo.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DataColumn = Parquet.Data.DataColumn;

namespace ParquetApiDemo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExportController : ControllerBase
    {
        private readonly YourDbContext _context;
        private readonly Microsoft.Data.SqlClient.SqlConnectionStringBuilder _connectionString;
        private readonly IConfiguration _configuration;

        public ExportController(YourDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
            _connectionString = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
                _configuration.GetConnectionString("DefaultConnection"));
        }

        /// <summary>
        /// Exports data from a dynamically created table as Parquet file
        /// If tableName is not provided, exports from the first available table
        /// </summary>
        [HttpGet("export")]
        public async Task<IActionResult> ExportParquet([FromQuery] string tableName = null)
        {
            try
            {
                // If no table name provided, get the first available table
                if (string.IsNullOrWhiteSpace(tableName))
                {
                    tableName = await GetFirstAvailableTableAsync();

                    if (string.IsNullOrWhiteSpace(tableName))
                        return BadRequest("No data available to export. Please upload a Parquet file first.");
                }

                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"SELECT * FROM [{tableName}]";

                    using var reader = await command.ExecuteReaderAsync();

                    // Build schema from columns - all as string
                    var dataFields = new List<DataField>();
                    var columnNames = new List<string>();

                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var fieldName = reader.GetName(i);
                        columnNames.Add(fieldName);
                        dataFields.Add(new DataField<string>(fieldName));
                    }

                    if (!dataFields.Any())
                        return BadRequest("Table has no columns.");

                    var schema = new ParquetSchema(dataFields.ToArray());

                    // Read all data into columns
                    var columnData = new Dictionary<string, string[]>();
                    foreach (var columnName in columnNames)
                    {
                        columnData[columnName] = new string[0];
                    }

                    var rowsList = new List<string[]>();
                    while (await reader.ReadAsync())
                    {
                        var rowValues = new string[columnNames.Count];
                        for (int i = 0; i < columnNames.Count; i++)
                        {
                            rowValues[i] = reader.IsDBNull(i) ? null : reader.GetValue(i)?.ToString() ?? string.Empty;
                        }
                        rowsList.Add(rowValues);
                    }

                    if (!rowsList.Any())
                        return BadRequest($"Table '{tableName}' is empty.");

                    // Create column arrays for Parquet (convert to string[] arrays)
                    for (int colIndex = 0; colIndex < columnNames.Count; colIndex++)
                    {
                        var columnName = columnNames[colIndex];
                        columnData[columnName] = rowsList.Select(r => r[colIndex]).ToArray();
                    }

                    // Write to Parquet
                    using var ms = new MemoryStream();
                    using (var writer = await ParquetWriter.CreateAsync(schema, ms))
                    {
                        using (var groupWriter = writer.CreateRowGroup())
                        {
                            for (int i = 0; i < dataFields.Count; i++)
                            {
                                var field = dataFields[i];
                                var stringColumn = columnData[field.Name];
                                var column = new DataColumn(field, stringColumn);
                                await groupWriter.WriteColumnAsync(column);
                            }
                        }
                    }

                    ms.Position = 0;
                    var fileName = $"export_{tableName}.parquet";
                    return File(ms.ToArray(), "application/octet-stream", fileName);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Error exporting Parquet file",
                    error = ex.Message,
                    status = "Failed"
                });
            }
        }

        /// <summary>
        /// Gets the first available uploaded table
        /// </summary>
        private async Task<string> GetFirstAvailableTableAsync()
        {
            using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString.ConnectionString))
            {
                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT TOP 1 TABLE_NAME 
                        FROM INFORMATION_SCHEMA.TABLES 
                        WHERE TABLE_TYPE='BASE TABLE' 
                        AND TABLE_NAME LIKE 'tbl_%'
                        ORDER BY TABLE_NAME";

                    var result = await command.ExecuteScalarAsync();
                    return result?.ToString();
                }
            }
        }
    }
}