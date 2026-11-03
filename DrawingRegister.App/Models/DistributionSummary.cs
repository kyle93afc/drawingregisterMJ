using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DrawingRegister.App.Models
{
    public class DistributionSummary
    {
        public int TotalDocuments { get; set; }
        public int TotalRecipients { get; set; }
        public Dictionary<string, int> RecipientCounts { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> CompanyCounts { get; set; } = new Dictionary<string, int>();
        public DateTime IssueDate { get; set; }

        public static DistributionSummary GenerateForDate(ProjectManager project, DateTime selectedDate, string? selectedSubfolderPath = null)
        {
            var summary = new DistributionSummary
            {
                IssueDate = selectedDate,
                TotalDocuments = 0,
                TotalRecipients = 0
            };

            // Get all documents issued on the selected date
            // And optionally filtered by subfolder
            var issuedDocuments = project.Documents
                .Where(d => d.RevisionHistory.Any(r => r.Key.Date == selectedDate.Date))
                .ToList();

            // Further filter by subfolder if specified
            if (!string.IsNullOrEmpty(selectedSubfolderPath))
            {
                issuedDocuments = issuedDocuments.Where(d => d.RevisionHistory.Any(r => 
                    r.Key.Date == selectedDate.Date &&
                    !string.IsNullOrEmpty(r.Value.FilePath) && 
                    string.Equals(Path.GetDirectoryName(r.Value.FilePath), selectedSubfolderPath, StringComparison.OrdinalIgnoreCase)
                )).ToList();
            }

            summary.TotalDocuments = issuedDocuments.Count;

            // Get all distributions for these documents on the selected date
            foreach (var document in issuedDocuments)
            {
                // Check for company distributions on the selected date
                if (document.DistributionCompanyIds.TryGetValue(selectedDate, out var companyIds))
                {
                    // Get the actual company objects
                    var companies = companyIds
                        .Select(id => project.DistributionManager.Companies.FirstOrDefault(c => c.Id == id))
                        .Where(c => c != null)
                        .ToList();

                    foreach (var company in companies)
                    {
                        summary.TotalRecipients++;

                        // Count by recipient (company name)
                        string recipientKey = company.Name;
                        if (summary.RecipientCounts.ContainsKey(recipientKey))
                        {
                            summary.RecipientCounts[recipientKey]++;
                        }
                        else
                        {
                            summary.RecipientCounts[recipientKey] = 1;
                        }

                        // Count by company category
                        string companyKey = company.Category;
                        if (summary.CompanyCounts.ContainsKey(companyKey))
                        {
                            summary.CompanyCounts[companyKey]++;
                        }
                        else
                        {
                            summary.CompanyCounts[companyKey] = 1;
                        }
                    }
                }

                // Also check for legacy stakeholder distributions on the selected date
                foreach (var stakeholder in document.Stakeholders.Values)
                {
                    if (stakeholder.DistributionDates.Any(d => d.Date == selectedDate.Date))
                    {
                        summary.TotalRecipients++;

                        // Count by recipient
                        string recipientKey = stakeholder.Name;
                        if (summary.RecipientCounts.ContainsKey(recipientKey))
                        {
                            summary.RecipientCounts[recipientKey]++;
                        }
                        else
                        {
                            summary.RecipientCounts[recipientKey] = 1;
                        }

                        // Count by company
                        string companyKey = stakeholder.Company;
                        if (summary.CompanyCounts.ContainsKey(companyKey))
                        {
                            summary.CompanyCounts[companyKey]++;
                        }
                        else
                        {
                            summary.CompanyCounts[companyKey] = 1;
                        }
                    }
                }
            }

            return summary;
        }

        public string GetFormattedSummary()
        {
            if (TotalRecipients == 0)
            {
                return "No distributions on this date.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Distribution Summary for {IssueDate:dd/MM/yyyy}");
            sb.AppendLine($"Total Documents: {TotalDocuments}");
            sb.AppendLine($"Total Recipients: {TotalRecipients}");
            
            if (CompanyCounts.Any())
            {
                sb.AppendLine("\nBy Company:");
                foreach (var company in CompanyCounts.OrderByDescending(c => c.Value))
                {
                    sb.AppendLine($"  {company.Key}: {company.Value} document(s)");
                }
            }

            if (RecipientCounts.Any())
            {
                sb.AppendLine("\nBy Recipient:");
                foreach (var recipient in RecipientCounts.OrderByDescending(r => r.Value))
                {
                    sb.AppendLine($"  {recipient.Key}: {recipient.Value} document(s)");
                }
            }

            return sb.ToString();
        }
    }
} 