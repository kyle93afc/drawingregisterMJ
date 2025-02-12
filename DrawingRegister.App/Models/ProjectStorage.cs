using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DrawingRegister.App.Models;

public class ProjectStorage
{
    public string ProjectNumber { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string Discipline { get; set; } = string.Empty;
    public string RegisterNumber { get; set; } = string.Empty;
    public string ClientNumber { get; set; } = string.Empty;
    public string BaseFolderPath { get; set; } = string.Empty;
    public DateTime LastScanDate { get; set; }
    public List<DocumentStorageInfo> Documents { get; set; } = new();
}

public class DocumentStorageInfo
{
    public string DocumentNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Package { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public Dictionary<DateTime, RevisionStorageInfo> RevisionHistory { get; set; } = new();
}

public class RevisionStorageInfo
{
    public string Revision { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string IssuedBy { get; set; } = string.Empty;
    public bool IsDistributed { get; set; }
    public string FilePath { get; set; } = string.Empty;
} 