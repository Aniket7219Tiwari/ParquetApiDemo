using Microsoft.AspNetCore.Mvc;
using Parquet;
using Parquet.Data;
using Parquet.Schema;
using ParquetApiDemo.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ParquetApiDemo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UploadController : ControllerBase
    {
        private readonly Microsoft.Data.SqlClient.SqlConnectionStringBuilder _connectionString;
        private readonly IConfiguration _configuration;

        public UploadController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
                _configuration.GetConnectionString("DefaultConnection"));
        }

        /// <summary>
        /// Uploads a Parquet file, converts it to tabular format, and stores in SQL Server with dynamic schema
        /// </summary>
        [HttpPost("upload")]
        public async Task<IActionResult> UploadParquet(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            if (!file.FileName.EndsWith(".parquet"))
                return BadRequest("File must be a .parquet file.");

            try
            {
                using var stream = file.OpenReadStream();
                using var reader = await ParquetReader.CreateAsync(stream);

                var dataFields = reader.Schema.DataFields.ToList();
                var tableName = ParquetSchemaService.GenerateTableName(file.FileName);
                int totalRowsInserted = 0;

                // Create table based on Parquet schema
                var createTableSql = ParquetSchemaService.GenerateCreateTableSql(tableName, dataFields);

                using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString.ConnectionString))
                {
                    await connection.OpenAsync();

                    // Drop table if exists
                    using (var dropCommand = connection.CreateCommand())
                    {
                        dropCommand.CommandText = ParquetSchemaService.GenerateDropTableSql(tableName);
                        await dropCommand.ExecuteNonQueryAsync();
                    }

                    // Create new table
                    using (var createCommand = connection.CreateCommand())
                    {
                        createCommand.CommandText = createTableSql;
                        await createCommand.ExecuteNonQueryAsync();
                    }

                    // Process each row group
                    for (int i = 0; i < reader.RowGroupCount; i++)
                    {
                        using var groupReader = reader.OpenRowGroupReader(i);

                        var columns = new Dictionary<string, DataColumn>();

                        // Read all columns for this row group
                        foreach (var field in dataFields)
                        {
                            var column = await groupReader.ReadColumnAsync(field);
                            columns[field.Name] = column;
                        }

                        int rowCount = columns.First().Value.Data.Length;

                        // Insert rows
                        for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
                        {
                            var rowValues = new object[dataFields.Count];
                            var parameters = new List<Microsoft.Data.SqlClient.SqlParameter>();

                            for (int colIndex = 0; colIndex < dataFields.Count; colIndex++)
                            {
                                var field = dataFields[colIndex];
                                var columnData = columns[field.Name].Data;
                                var value = columnData.GetValue(rowIndex) ?? DBNull.Value;

                                rowValues[colIndex] = value;
                                parameters.Add(new Microsoft.Data.SqlClient.SqlParameter($"@p{colIndex}", value));
                            }

                            var insertSql = ParquetSchemaService.GenerateInsertSql(tableName, dataFields, rowValues);
                            using (var insertCommand = connection.CreateCommand())
                            {
                                insertCommand.CommandText = insertSql;
                                insertCommand.Parameters.AddRange(parameters.ToArray());
                                await insertCommand.ExecuteNonQueryAsync();
                            }
                            totalRowsInserted++;
                        }
                    }
                }

                return Ok(new
                {
                    Message = "Parquet file uploaded successfully with tabular storage",
                    Status = "Success",
                    RowsInserted = totalRowsInserted,
                    TableName = tableName,
                    FileName = file.FileName,
                    ColumnCount = dataFields.Count,
                    Columns = dataFields.Select(f => new { Name = f.Name }).ToList()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Message = "Error processing Parquet file",
                    Error = ex.Message,
                    Status = "Failed"
                });
            }
        }
    }
}