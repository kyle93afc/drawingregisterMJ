using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DrawingRegister.App.Models
{
    public class DocumentMetadata
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Document number is required")]
        [StringLength(50)]
        public string DocumentNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Document type is required")]
        [StringLength(30)]
        public string DocumentType { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Package { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [StringLength(20)]
        public string Size { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        public DateTime? IssueDate { get; set; }

        [StringLength(50)]
        public string PurposeOfIssue { get; set; } = string.Empty;

        [StringLength(50)]
        public string MethodOfIssue { get; set; } = string.Empty;

        [StringLength(100)]
        public string IssuedBy { get; set; } = string.Empty;

        [StringLength(10)]
        public string Revision { get; set; } = string.Empty;

        [Required]
        public string FilePath { get; set; } = string.Empty;

        // Project Information
        [Required]
        [StringLength(50)]
        public string ProjectNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string ProjectName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Discipline { get; set; } = string.Empty;

        [StringLength(50)]
        public string ClientNumber { get; set; } = string.Empty;

        [StringLength(50)]
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