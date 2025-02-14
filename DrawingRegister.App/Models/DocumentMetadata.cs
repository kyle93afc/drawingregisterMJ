using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

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

        public bool IsLatestRevision => 
            RevisionHistory.Any() && 
            Revision == RevisionHistory.Max(r => r.Value.Revision);

        public static string GenerateRevisionCode(string purpose, Dictionary<DateTime, RevisionInfo> revisionHistory)
        {
            // If no purpose specified, use alphabetical sequence
            if (string.IsNullOrEmpty(purpose))
                return GetNextAlphabeticalRevision(revisionHistory);

            // Get prefix based on purpose
            string? prefix = purpose.ToUpper() switch
            {
                "CONSTRUCTION" => "C",
                "TENDER" => "T",
                "PLANNING" => "P",
                "INFORMATION" => "I",
                _ => null
            };

            // If no specific prefix found, use alphabetical sequence
            if (prefix == null)
                return GetNextAlphabeticalRevision(revisionHistory);

            // Get existing revisions with this prefix
            var existingRevs = revisionHistory.Values
                .Where(r => r.Revision != null && 
                           r.Revision.TrimStart().StartsWith(prefix) && 
                           r.Purpose?.ToUpper() == purpose.ToUpper())
                .Select(r => r.Revision.TrimStart())
                .ToList();

            if (!existingRevs.Any())
                return $"{prefix}01";

            // Find highest number and increment
            int maxNum = existingRevs
                .Select(r => 
                {
                    if (r.Length <= 1) return 0;
                    var numStr = r[1..].TrimStart();
                    return int.TryParse(numStr, out int num) ? num : 0;
                })
                .Max();

            return $"{prefix}{(maxNum + 1):D2}";
        }

        private static string GetNextAlphabeticalRevision(Dictionary<DateTime, RevisionInfo> revisionHistory)
        {
            if (!revisionHistory.Any())
                return "A";

            var alphabeticalRevs = revisionHistory.Values
                .Where(r => r.Revision.Length == 1 && char.IsLetter(r.Revision[0]))
                .Select(r => r.Revision[0])
                .ToList();

            if (!alphabeticalRevs.Any())
                return "A";

            char maxChar = alphabeticalRevs.Max();
            return ((char)(maxChar + 1)).ToString();
        }
    }

    public class RevisionInfo
    {
        public string Revision { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public string IssuedBy { get; set; } = string.Empty;
        public bool IsDistributed { get; set; }
        public string FilePath { get; set; } = string.Empty;
    }

    public class StakeholderInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public List<DateTime> ReceivedRevisions { get; set; } = new();
    }
} 