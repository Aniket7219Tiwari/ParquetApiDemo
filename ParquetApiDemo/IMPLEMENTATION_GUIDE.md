# Parquet API - Tabular Storage Implementation

## 📊 Implementation Summary

Your project has been successfully updated to store Parquet data in **tabular format** instead of JSON. The system now dynamically creates SQL Server tables based on the uploaded Parquet file schema.

---

## 🚀 API Endpoints (2 Simple APIs)

### 1️⃣ Upload API
**POST** `/api/upload/upload`

Accepts `.parquet` file upload, reads schema, creates dynamic SQL table, and stores data in tabular format.

**Request:**
```bash
POST /api/upload/upload
Content-Type: multipart/form-data

file: @userdata.parquet
```

**Response (Success):**
```json
{
  "message": "Parquet file uploaded successfully with tabular storage",
  "status": "Success",
  "rowsInserted": 1000,
  "tableName": "tbl_userdata",
  "fileName": "userdata.parquet",
  "columnCount": 5,
  "columns": [
    { "name": "id" },
    { "name": "name" },
    { "name": "age" },
    { "name": "salary" },
    { "name": "department" }
  ]
}
```

**Response (Error):**
```json
{
  "message": "Error processing Parquet file",
  "error": "Error message details",
  "status": "Failed"
}
```

---

### 2️⃣ Export API
**GET** `/api/export/export`

Exports data from dynamically created tables (uploaded Parquet files) as a Parquet file.

**Request (Export most recent table):**
```bash
GET /api/export/export
```

**Request (Export specific table):**
```bash
GET /api/export/export?tableName=tbl_userdata
```

**Response:**
- File download: `export_tbl_userdata.parquet`
- If no table specified, exports the first available uploaded table

---

## 📁 Database Schema

When you upload a Parquet file named `userdata.parquet` with columns:
- id (any type)
- name (string)
- age (integer)
- salary (decimal)
- department (string)

The system creates a table `tbl_userdata` with:
```sql
CREATE TABLE [tbl_userdata] (
  [id] NVARCHAR(MAX),
  [name] NVARCHAR(MAX),
  [age] NVARCHAR(MAX),
  [salary] NVARCHAR(MAX),
  [department] NVARCHAR(MAX)
)
```

**Note:** 
- All columns stored as `NVARCHAR(MAX)` for universal compatibility
- If Parquet has an `id` column, NO auto-generated RecordId is added
- If Parquet does NOT have `id`, a `RecordId INT PRIMARY KEY IDENTITY(1,1)` is auto-added

---

## 🔄 Data Flow

```
UPLOAD FLOW:
1. Upload Parquet File
   ↓
2. Extract Schema from Parquet
   ↓
3. Generate Table Name from filename (e.g., userdata.parquet → tbl_userdata)
   ↓
4. Create Dynamic SQL Table with Parquet columns
   ↓
5. Read Parquet Data Row by Row
   ↓
6. Insert Data into Table (Tabular Format)
   ↓
7. Return Success Response

EXPORT FLOW:
1. Call GET /api/export/export (with optional ?tableName=)
   ↓
2. If no table specified, auto-select first uploaded table
   ↓
3. Read data from SQL table
   ↓
4. Create Parquet schema from columns
   ↓
5. Write data to Parquet file
   ↓
6. Return file for download
```

---

## 🛠️ Modified Files

| File | Changes |
|------|---------|
| `UploadController.cs` | Dynamically creates tables from Parquet schema, stores data in columns |
| `ExportController.cs` | **NEW** - Exports from dynamically created tables, auto-selects first table if not specified |
| `ParquetSchemaService.cs` | Utility for table/SQL generation, handles duplicate column names |

---

## 💡 Key Features

✅ **Dynamic Schema**: Tables created automatically from Parquet structure
✅ **Tabular Storage**: Data in actual database columns (not JSON)
✅ **Duplicate Column Handling**: Prevents errors when Parquet has 'id' column
✅ **Safe Table Names**: Filename sanitization for valid SQL identifiers
✅ **Type Flexibility**: All columns as NVARCHAR(MAX) for any data
✅ **Error Handling**: Clear error messages with status codes
✅ **Simple API**: Just 2 endpoints - upload and export

---

## 🔧 Configuration

Ensure your `appsettings.json` has the SQL Server connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=ParquetDB;Trusted_Connection=true;"
  }
}
```

---

## ⚠️ Important Notes

1. **Column Name Conflict**: If Parquet has 'id' column, auto-generated Id is NOT added
2. **Re-uploads**: Uploading same filename drops and recreates the table
3. **SQL Server**: Requires SQL Server with read/write permissions
4. **Nullable Values**: NULL values handled as `DBNull.Value`
5. **Table Naming**: Filename converted to `tbl_` prefix (e.g., `sales.parquet` → `tbl_sales`)
6. **Special Characters**: Replaced with underscores in table names

---

## 🧪 Quick Test

1. Create `employees.parquet` with columns: `id`, `name`, `age`
2. POST to `/api/upload/upload` with file
   - Response: `"tableName": "tbl_employees"`
3. Check SQL Server for table `tbl_employees`
4. Query: `SELECT * FROM tbl_employees`
5. GET `/api/export/export` to download data (auto-exports first table)
   - OR GET `/api/export/export?tableName=tbl_employees` to export specific table
6. Verify downloaded `.parquet` file contains your data

---

## ✨ Fixed Issues

✅ **Duplicate Column Error**: Handles Parquet files with 'id' column
✅ **Simplified API**: Only 2 endpoints (Upload & Export)
✅ **Dynamic Schema**: No more JSON storage - pure tabular data

---

**Your API is ready!** 🎉

