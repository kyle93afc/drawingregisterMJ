using System;
using System.Collections.Generic;
using System.IO;
using DrawingRegister.App.Models;
using Xunit;

namespace DrawingRegister.App.Tests.Models;

public sealed class OrganizationProfileTests
{
    [Fact]
    public void FormatRegisterNumber_formats_mj_register_number_correctly()
    {
        var mj = OrganizationRegistry.MJ;
        var regNo = mj.FormatRegisterNumber("124660", "S");

        Assert.Equal("124660-M+J-00-XX-RE-S-00-01", regNo);
    }

    [Fact]
    public void FormatRegisterNumber_formats_dcf_register_number_correctly()
    {
        var dcf = OrganizationRegistry.DCF;
        var regNo = dcf.FormatRegisterNumber("124660", "C");

        Assert.Equal("124660-DCF-00-XX-RE-C-00-01", regNo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FormatRegisterNumber_returns_empty_when_project_number_missing(string? projectNo)
    {
        var dcf = OrganizationRegistry.DCF;
        var regNo = dcf.FormatRegisterNumber(projectNo, "C");

        Assert.Equal(string.Empty, regNo);
    }

    [Fact]
    public void FormatRegisterNumber_defaults_discipline_to_Z_when_unspecified()
    {
        var dcf = OrganizationRegistry.DCF;
        var regNo = dcf.FormatRegisterNumber("12345", null);

        Assert.Equal("12345-DCF-00-XX-RE-Z-00-01", regNo);
    }

    [Theory]
    [InlineData("DCF", "DCF")]
    [InlineData("dcf", "DCF")]
    [InlineData("MJ", "MJ")]
    [InlineData("mj", "MJ")]
    [InlineData("M+J", "MJ")]
    [InlineData(null, "MJ")]
    [InlineData("unknown", "MJ")]
    public void OrganizationRegistry_GetById_resolves_expected_profile(string? id, string expectedId)
    {
        var profile = OrganizationRegistry.GetById(id);
        Assert.Equal(expectedId, profile.Id);
    }

    [Theory]
    [InlineData("DCF", "DCF")]
    [InlineData("dcf", "DCF")]
    [InlineData("M+J", "MJ")]
    [InlineData("MJ", "MJ")]
    [InlineData(null, "MJ")]
    public void OrganizationRegistry_GetByOriginatorCode_resolves_expected_profile(string? code, string expectedId)
    {
        var profile = OrganizationRegistry.GetByOriginatorCode(code);
        Assert.Equal(expectedId, profile.Id);
    }

    [Fact]
    public void OrganizationRegistry_DetectFrom_detects_dcf_from_register_number()
    {
        var detected = OrganizationRegistry.DetectFrom(null, "12345-DCF-00-XX-RE-S-00-01");
        Assert.Equal("DCF", detected.Id);
    }

    [Fact]
    public void OrganizationRegistry_DetectFrom_detects_dcf_from_drawing_list()
    {
        var drawings = new List<string>
        {
            "12345-DCF-00-XX-DR-C-0001-P01_Plan",
            "12345-DCF-00-XX-DR-C-0002-P01_Section"
        };

        var detected = OrganizationRegistry.DetectFrom(drawings);
        Assert.Equal("DCF", detected.Id);
    }

    [Fact]
    public void OrganizationRegistry_DetectFrom_detects_mj_from_drawing_list()
    {
        var drawings = new List<string>
        {
            "124660-M+J-V1-XX-DR-A-01-02-1A-GROUND_FLOOR_PLAN"
        };

        var detected = OrganizationRegistry.DetectFrom(drawings);
        Assert.Equal("MJ", detected.Id);
    }

    [Fact]
    public void AppSettings_saves_and_loads_configuration_correctly()
    {
        var settings = new AppSettings
        {
            DefaultOrganizationId = "DCF",
            DefaultProjectsFolder = @"\\dcf-glasgow-server\projects",
            LastOpenedFolder = @"\\dcf-glasgow-server\projects\12345"
        };

        settings.Save();
        AppSettings.ResetCache();

        var loaded = AppSettings.Load();
        Assert.Equal("DCF", loaded.DefaultOrganizationId);
        Assert.Equal(@"\\dcf-glasgow-server\projects", loaded.DefaultProjectsFolder);
        Assert.Equal(@"\\dcf-glasgow-server\projects\12345", loaded.LastOpenedFolder);
    }

    [Fact]
    public void ProjectInfo_migrates_dcf_organization_from_register_number()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "ProjectInfoTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);
        try
        {
            var rawJson = @"{
                ""ProjectNumber"": ""12345"",
                ""ProjectName"": ""Glasgow Site"",
                ""RegisterNumber"": ""12345-DCF-00-XX-RE-C-00-01"",
                ""UseNumericRevisions"": true
            }";

            File.WriteAllText(Path.Combine(tempFolder, "project_info.json"), rawJson);

            var info = ProjectInfo.Load(tempFolder);
            Assert.Equal("DCF", info.OrganizationId);
            Assert.Equal(RevisionScheme.Numeric, info.RevisionScheme);
        }
        finally
        {
            if (Directory.Exists(tempFolder))
                Directory.Delete(tempFolder, recursive: true);
        }
    }
}
