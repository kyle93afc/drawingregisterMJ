using System;
using System.Collections.Generic;
using System.Linq;

namespace DrawingRegister.App.Models;

/// <summary>
/// Domain profile representing an organization or sister company (e.g. M+J Engineers, DCF Glasgow).
/// Encapsulates naming conventions, originator codes, branding colors, and register number generation.
/// </summary>
public record OrganizationProfile(
    string Id,
    string DisplayName,
    string ShortName,
    string OriginatorCode,
    string BrandColorHex,
    string? LogoResourceName = null,
    string? CustomLogoPath = null,
    string CopyrightNotice = "",
    string DefaultServerPath = "",
    string BrandHoverColorHex = "",
    string BrandPressedColorHex = "",
    string BrandLightColorHex = "")
{
    public string FormatRegisterNumber(string? projectNumber, string? disciplineCode)
    {
        if (string.IsNullOrWhiteSpace(projectNumber))
            return string.Empty;

        var code = string.IsNullOrWhiteSpace(disciplineCode) ? "Z" : disciplineCode.Trim();
        return $"{projectNumber.Trim()}-{OriginatorCode}-00-XX-RE-{code}-00-01";
    }
}

public static class OrganizationRegistry
{
    public static readonly OrganizationProfile MJ = new(
        Id: "MJ",
        DisplayName: "M+J Engineers",
        ShortName: "M+J",
        OriginatorCode: "M+J",
        BrandColorHex: "#eb1845",
        BrandHoverColorHex: "#c91438",
        BrandPressedColorHex: "#a8102f",
        BrandLightColorHex: "#FEF2F4",
        LogoResourceName: "DrawingRegister.App.Resources.company-logo.png",
        CopyrightNotice: "Copyright (c) 2026 M+J Engineers");

    public static readonly OrganizationProfile DCF = new(
        Id: "DCF",
        DisplayName: "DCF Design Consultants",
        ShortName: "DCF",
        OriginatorCode: "DCF",
        BrandColorHex: "#00b282",
        BrandHoverColorHex: "#009e74",
        BrandPressedColorHex: "#008a65",
        BrandLightColorHex: "#E6F7F2",
        LogoResourceName: "DrawingRegister.App.Resources.dcf-logo.png",
        CopyrightNotice: "Copyright (c) 2026 DCF Design Consultants");

    public static readonly IReadOnlyList<OrganizationProfile> All = [MJ, DCF];

    public static OrganizationProfile Default => MJ;

    public static OrganizationProfile GetById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Default;

        return All.FirstOrDefault(p => string.Equals(p.Id, id.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? Default;
    }

    public static OrganizationProfile GetByOriginatorCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Default;

        var clean = code.Trim();
        if (clean.Equals("DCF", StringComparison.OrdinalIgnoreCase))
            return DCF;
        if (clean.Equals("M+J", StringComparison.OrdinalIgnoreCase) || clean.Equals("MJ", StringComparison.OrdinalIgnoreCase))
            return MJ;

        return All.FirstOrDefault(p => string.Equals(p.OriginatorCode, clean, StringComparison.OrdinalIgnoreCase))
            ?? Default;
    }

    public static OrganizationProfile DetectFrom(IEnumerable<string?>? documentNumbers, string? registerNumber = null)
    {
        if (!string.IsNullOrWhiteSpace(registerNumber))
        {
            if (registerNumber.Contains("-DCF-", StringComparison.OrdinalIgnoreCase))
                return DCF;
            if (registerNumber.Contains("-M+J-", StringComparison.OrdinalIgnoreCase) || registerNumber.Contains("-MJ-", StringComparison.OrdinalIgnoreCase))
                return MJ;
        }

        if (documentNumbers != null)
        {
            foreach (var doc in documentNumbers)
            {
                if (string.IsNullOrWhiteSpace(doc)) continue;
                if (doc.Contains("-DCF-", StringComparison.OrdinalIgnoreCase))
                    return DCF;
                if (doc.Contains("-M+J-", StringComparison.OrdinalIgnoreCase) || doc.Contains("-MJ-", StringComparison.OrdinalIgnoreCase))
                    return MJ;
            }
        }

        return Default;
    }
}
