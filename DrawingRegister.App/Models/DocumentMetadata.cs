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
        public Dictionary<string, StakeholderInfo> Stakeholders { get; set; } = new();
        
        // New property for distribution companies
        public Dictionary<DateTime, List<string>> DistributionCompanyIds { get; set; } = new();

        // Revision History - Dictionary of date to revision info
        public Dictionary<DateTime, RevisionInfo> RevisionHistory { get; set; } = new();

        public bool IsLatestRevision =>
            RevisionHistory.Any() &&
            Revision == RevisionHistory
                .Where(r => !r.Value.IsSuperseded)
                .Select(r => r.Value.Revision)
                .DefaultIfEmpty(string.Empty)
                .Max();

        /// <summary>
        /// The most recently issued revision that has NOT been superseded. Used by the register,
        /// the "Latest Revision" column, and Collect Latest PDFs so a withdrawn issue doesn't
        /// masquerade as current.
        /// </summary>
        public KeyValuePair<DateTime, RevisionInfo>? LatestNonSupersededRevision =>
            RevisionHistory
                .Where(r => !r.Value.IsSuperseded)
                .OrderByDescending(r => r.Key)
                .Select(r => (KeyValuePair<DateTime, RevisionInfo>?)r)
                .FirstOrDefault();

        public static string GenerateRevisionCode(string purpose, Dictionary<DateTime, RevisionInfo> revisionHistory, bool useNumericRevisions = false)
            => GenerateRevisionCode(purpose, revisionHistory, useNumericRevisions ? RevisionScheme.Numeric : RevisionScheme.Legacy, isFormalIssue: false);

        public static string GenerateRevisionCode(string purpose, Dictionary<DateTime, RevisionInfo> revisionHistory, RevisionScheme scheme, bool isFormalIssue = false)
        {
            // Numeric revision scheme (SSEN-style): revisions are plain numbers 1, 2, 3...
            if (scheme == RevisionScheme.Numeric)
            {
                var numericRevs = revisionHistory.Values
                    .Select(r => r.Revision)
                    .Where(r => !string.IsNullOrEmpty(r) && r != "-" && r.All(char.IsDigit))
                    .Select(r => int.Parse(r))
                    .ToList();

                if (!numericRevs.Any())
                    return "1";

                return (numericRevs.Max() + 1).ToString();
            }

            // Project 124660 procedure: internal drafts 1A, 1B, 1C... then formal issue drops letter to "1"
            if (scheme == RevisionScheme.SubLetterNumeric)
                return GetNextSubLetterRevision(revisionHistory, isFormalIssue);

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

        private static string GetNextSubLetterRevision(Dictionary<DateTime, RevisionInfo> revisionHistory, bool isFormalIssue)
        {
            var parsed = revisionHistory.Values
                .Select(r => r.Revision)
                .Select(TryParseSubLetter)
                .Where(p => p.HasValue)
                .Select(p => p!.Value)
                .ToList();

            int maxFormal = parsed.Where(p => p.letter == null).Select(p => p.number).DefaultIfEmpty(0).Max();
            int maxCycle = parsed.Select(p => p.number).DefaultIfEmpty(0).Max();

            // Determine the "active" cycle. If every entry at maxCycle is already a formal issue
            // (letter == null at that cycle), the next cycle is maxCycle + 1. Otherwise we're still
            // iterating on maxCycle.
            bool maxCycleFormalIssued = parsed.Any(p => p.number == maxCycle && p.letter == null);
            int activeCycle = maxCycleFormalIssued ? maxCycle + 1 : Math.Max(maxCycle, maxFormal + 1);
            if (activeCycle == 0) activeCycle = 1;

            if (isFormalIssue)
                return activeCycle.ToString();

            // Internal draft: find the highest letter already used at activeCycle and increment.
            var lettersAtCycle = parsed
                .Where(p => p.number == activeCycle && p.letter.HasValue)
                .Select(p => p.letter!.Value)
                .ToList();

            if (!lettersAtCycle.Any())
                return $"{activeCycle}A";

            char nextLetter = (char)(lettersAtCycle.Max() + 1);
            return $"{activeCycle}{nextLetter}";
        }

        private static (int number, char? letter)? TryParseSubLetter(string? revision)
        {
            if (string.IsNullOrWhiteSpace(revision) || revision == "-")
                return null;

            var trimmed = revision.Trim();
            int letterIndex = -1;
            for (int i = 0; i < trimmed.Length; i++)
            {
                if (char.IsLetter(trimmed[i])) { letterIndex = i; break; }
            }

            if (letterIndex == 0)
                return null; // legacy prefix revision like "C01" — skip

            string numberPart = letterIndex < 0 ? trimmed : trimmed[..letterIndex];
            if (!int.TryParse(numberPart, out int number))
                return null;

            char? letter = null;
            if (letterIndex >= 0)
            {
                var letterPart = trimmed[letterIndex..];
                if (letterPart.Length != 1 || !char.IsLetter(letterPart[0]))
                    return null;
                letter = char.ToUpperInvariant(letterPart[0]);
            }

            return (number, letter);
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
        
        // Helper method to check if a document was distributed to a stakeholder at a specific issue date
        public bool WasDistributedTo(string stakeholderId, DateTime issueDate)
        {
            if (!Stakeholders.TryGetValue(stakeholderId, out var stakeholder))
                return false;
                
            return stakeholder.DistributionDates.Contains(issueDate);
        }
        
        // Helper method to toggle distribution status for a stakeholder at a specific issue date
        public void ToggleDistribution(string stakeholderId, string stakeholderName, string company, DateTime issueDate)
        {
            if (!Stakeholders.TryGetValue(stakeholderId, out var stakeholder))
            {
                stakeholder = new StakeholderInfo
                {
                    Name = stakeholderName,
                    Company = company,
                    DistributionDates = new List<DateTime>()
                };
                Stakeholders[stakeholderId] = stakeholder;
            }
            
            if (stakeholder.DistributionDates.Contains(issueDate))
                stakeholder.DistributionDates.Remove(issueDate);
            else
                stakeholder.DistributionDates.Add(issueDate);
        }
        
        // New helper methods for distribution companies
        
        // Check if a document was distributed to a company at a specific issue date
        public bool WasDistributedToCompany(string companyId, DateTime issueDate)
        {
            if (!DistributionCompanyIds.TryGetValue(issueDate.Date, out var companyIds))
                return false;
                
            return companyIds.Contains(companyId);
        }
        
        // Toggle distribution status for a company at a specific issue date
        public void ToggleCompanyDistribution(string companyId, DateTime issueDate)
        {
            if (!DistributionCompanyIds.ContainsKey(issueDate.Date))
            {
                DistributionCompanyIds[issueDate.Date] = new List<string>();
            }
            
            var companyIds = DistributionCompanyIds[issueDate.Date];
            
            if (companyIds.Contains(companyId))
                companyIds.Remove(companyId);
            else
                companyIds.Add(companyId);
        }
        
        // Set distribution for multiple companies at once
        public void SetCompanyDistributions(List<string> companyIds, DateTime issueDate)
        {
            DistributionCompanyIds[issueDate.Date] = new List<string>(companyIds);
        }
    }

    public class RevisionInfo
    {
        public const string SupersededPurpose = "Superseded";

        public string Revision { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public string IssuedBy { get; set; } = string.Empty;
        public bool IsDistributed { get; set; }
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// When true, this revision was issued in error (or otherwise withdrawn) and should not
        /// count as the current revision. Kept as a bool for fast filtering; also derivable from
        /// Purpose == "Superseded".
        /// </summary>
        public bool IsSuperseded { get; set; }
    }

    public class StakeholderInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public List<DateTime> DistributionDates { get; set; } = new();
    }
} 