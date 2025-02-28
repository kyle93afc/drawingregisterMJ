using System;
using System.Collections.Generic;
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

        public static DistributionSummary GenerateForDate(ProjectManager project, DateTime selectedDate)
        {
            var summary = new DistributionSummary
            {
                IssueDate = selectedDate,
                TotalDocuments = 0,
                TotalRecipients = 0
            };

            // Get all documents issued on the selected date
            var issuedDocuments = project.Documents
                .Where(d => d.RevisionHistory.Any(r => r.Key.Date == selectedDate.Date))
                .ToList();

            summary.TotalDocuments = issuedDocuments.Count;

            // Get all distributions for these documents on the selected date
            foreach (var document in issuedDocuments)
            {
                // Check for stakeholders with distribution dates matching the selected date
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