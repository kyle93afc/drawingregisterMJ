using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using DrawingRegisterMJ.Models;

namespace DrawingRegisterMJ.Services
{
    public class DatabaseService
    {
        private readonly string _dbPath;
        private readonly string _connectionString;

        public DatabaseService(string dbPath = "DrawingRegister.db")
        {
            _dbPath = dbPath;
            _connectionString = $"Data Source={_dbPath};Version=3;";
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            if (!File.Exists(_dbPath))
            {
                SQLiteConnection.CreateFile(_dbPath);
                using var connection = new SQLiteConnection(_connectionString);
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Drawings (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        DocumentNumber TEXT,
                        DocumentType TEXT,
                        Package TEXT,
                        Description TEXT,
                        Size TEXT,
                        Revision TEXT,
                        Project TEXT,
                        Originator TEXT,
                        Volume TEXT,
                        Level TEXT,
                        FileType TEXT,
                        Discipline TEXT,
                        Number TEXT,
                        FilePath TEXT,
                        LastModified TEXT,
                        DateOfIssue TEXT,
                        ProjectFolder TEXT
                    );

                    CREATE TABLE IF NOT EXISTS DistributionLog (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        DrawingId INTEGER,
                        Recipient TEXT,
                        DistributionDate TEXT,
                        Method TEXT,
                        Notes TEXT,
                        FOREIGN KEY (DrawingId) REFERENCES Drawings(Id)
                    );";
                command.ExecuteNonQuery();
            }
        }

        public void InsertOrUpdateDrawing(Drawing drawing)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO Drawings (
                    DocumentNumber, DocumentType, Package, Description, Size, 
                    Revision, Project, Originator, Volume, Level, FileType, 
                    Discipline, Number, FilePath, LastModified, DateOfIssue, ProjectFolder
                ) VALUES (
                    @DocumentNumber, @DocumentType, @Package, @Description, @Size,
                    @Revision, @Project, @Originator, @Volume, @Level, @FileType,
                    @Discipline, @Number, @FilePath, @LastModified, @DateOfIssue, @ProjectFolder
                )";

            command.Parameters.AddWithValue("@DocumentNumber", drawing.DocumentNumber);
            command.Parameters.AddWithValue("@DocumentType", drawing.DocumentType);
            command.Parameters.AddWithValue("@Package", drawing.Package);
            command.Parameters.AddWithValue("@Description", drawing.Description);
            command.Parameters.AddWithValue("@Size", drawing.Size);
            command.Parameters.AddWithValue("@Revision", drawing.Revision);
            command.Parameters.AddWithValue("@Project", drawing.Project);
            command.Parameters.AddWithValue("@Originator", drawing.Originator);
            command.Parameters.AddWithValue("@Volume", drawing.Volume);
            command.Parameters.AddWithValue("@Level", drawing.Level);
            command.Parameters.AddWithValue("@FileType", drawing.FileType);
            command.Parameters.AddWithValue("@Discipline", drawing.Discipline);
            command.Parameters.AddWithValue("@Number", drawing.Number);
            command.Parameters.AddWithValue("@FilePath", drawing.FilePath);
            command.Parameters.AddWithValue("@LastModified", drawing.LastModified.ToString("s"));
            command.Parameters.AddWithValue("@DateOfIssue", drawing.DateOfIssue?.ToString("s"));
            command.Parameters.AddWithValue("@ProjectFolder", drawing.ProjectFolder);

            command.ExecuteNonQuery();
        }

        public List<Drawing> GetAllDrawings()
        {
            var drawings = new List<Drawing>();
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Drawings";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                drawings.Add(new Drawing
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    DocumentNumber = reader.GetString(reader.GetOrdinal("DocumentNumber")),
                    DocumentType = reader.GetString(reader.GetOrdinal("DocumentType")),
                    Package = reader.GetString(reader.GetOrdinal("Package")),
                    Description = reader.GetString(reader.GetOrdinal("Description")),
                    Size = reader.GetString(reader.GetOrdinal("Size")),
                    Revision = reader.GetString(reader.GetOrdinal("Revision")),
                    Project = reader.GetString(reader.GetOrdinal("Project")),
                    Originator = reader.GetString(reader.GetOrdinal("Originator")),
                    Volume = reader.GetString(reader.GetOrdinal("Volume")),
                    Level = reader.GetString(reader.GetOrdinal("Level")),
                    FileType = reader.GetString(reader.GetOrdinal("FileType")),
                    Discipline = reader.GetString(reader.GetOrdinal("Discipline")),
                    Number = reader.GetString(reader.GetOrdinal("Number")),
                    FilePath = reader.GetString(reader.GetOrdinal("FilePath")),
                    LastModified = DateTime.Parse(reader.GetString(reader.GetOrdinal("LastModified"))),
                    DateOfIssue = reader.IsDBNull(reader.GetOrdinal("DateOfIssue")) 
                        ? null 
                        : DateTime.Parse(reader.GetString(reader.GetOrdinal("DateOfIssue"))),
                    ProjectFolder = reader.GetString(reader.GetOrdinal("ProjectFolder"))
                });
            }

            return drawings;
        }
    }
} 