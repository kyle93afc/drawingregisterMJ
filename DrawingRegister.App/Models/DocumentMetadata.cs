using System;
using System.Collections.Generic;

namespace DrawingRegister.App.Models
{
    public class DocumentMetadata
    {
        public int Id { get; set; }
        public string DocumentNumber { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public string Package { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public DateTime? IssueDate { get; set; }
        public string PurposeOfIssue { get; set; } = string.Empty;
        public string MethodOfIssue { get; set; } = string.Empty;
        public string IssuedBy { get; set; } = string.Empty;
        public string Revision { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public byte[]? ThumbnailData { get; set; }

        // Project Information
        public string ProjectNumber { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string Discipline { get; set; } = string.Empty;
        public string ClientNumber { get; set; } = string.Empty;
        public string RegisterNumber { get; set; } = string.Empty;

        // Distribution Information
        public Dictionary<string, string> Stakeholders { get; set; } = new();

        // Revision History - Dictionary of date to revision info
        public Dictionary<DateTime, RevisionInfo> RevisionHistory { get; set; } = new();
    }

    public class RevisionInfo
    {
        public string Revision { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public string IssuedBy { get; set; } = string.Empty;
        public bool IsDistributed { get; set; }
    }

    public class StakeholderInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public List<DateTime> ReceivedRevisions { get; set; } = new();
    }
} 