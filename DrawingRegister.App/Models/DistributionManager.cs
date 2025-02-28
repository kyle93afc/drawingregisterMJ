using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace DrawingRegister.App.Models
{
    public class DistributionManager
    {
        private const string DISTRIBUTION_FILENAME = "distribution_list.json";
        private readonly string _filePath;
        
        public ObservableCollection<DistributionCompany> Companies { get; private set; }
        
        public DistributionManager(string projectBasePath)
        {
            _filePath = Path.Combine(projectBasePath, DISTRIBUTION_FILENAME);
            Companies = new ObservableCollection<DistributionCompany>();
            LoadCompanies();
        }
        
        public void LoadCompanies()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    var companies = JsonSerializer.Deserialize<List<DistributionCompany>>(json);
                    
                    if (companies != null)
                    {
                        Companies.Clear();
                        foreach (var company in companies)
                        {
                            Companies.Add(company);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading distribution list: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        public void SaveCompanies()
        {
            try
            {
                var companies = Companies.ToList();
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(companies, options);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving distribution list: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        public void AddCompany(DistributionCompany company)
        {
            // Check for duplicates
            if (Companies.Any(c => c.Name.Equals(company.Name, StringComparison.OrdinalIgnoreCase) && 
                                  c.Category.Equals(company.Category, StringComparison.OrdinalIgnoreCase)))
            {
                System.Windows.MessageBox.Show($"A company with the name '{company.Name}' already exists in the '{company.Category}' category.", 
                                "Duplicate Company", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            Companies.Add(company);
            SaveCompanies();
        }
        
        public void UpdateCompany(DistributionCompany company)
        {
            // Check for duplicates (excluding the current company)
            if (Companies.Any(c => c.Id != company.Id && 
                                  c.Name.Equals(company.Name, StringComparison.OrdinalIgnoreCase) && 
                                  c.Category.Equals(company.Category, StringComparison.OrdinalIgnoreCase)))
            {
                System.Windows.MessageBox.Show($"A company with the name '{company.Name}' already exists in the '{company.Category}' category.", 
                                "Duplicate Company", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var existingCompany = Companies.FirstOrDefault(c => c.Id == company.Id);
            if (existingCompany != null)
            {
                existingCompany.Name = company.Name;
                existingCompany.Category = company.Category;
                SaveCompanies();
            }
        }
        
        public void RemoveCompany(DistributionCompany company)
        {
            Companies.Remove(company);
            SaveCompanies();
        }
        
        public List<string> GetCategories()
        {
            return Companies
                .Select(c => c.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
        }
        
        public List<DistributionCompany> GetCompaniesByCategory(string category)
        {
            return Companies
                .Where(c => c.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.Name)
                .ToList();
        }
    }
} 