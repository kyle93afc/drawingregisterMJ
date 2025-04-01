$targetDir = "."

# Create necessary directories
New-Item -ItemType Directory -Force -Path "$targetDir\.cursor\rules\core-rules" | Out-Null
New-Item -ItemType Directory -Force -Path "$targetDir\docs" | Out-Null
New-Item -ItemType Directory -Force -Path "$targetDir\xnotes" | Out-Null

# Copy rules and templates
Copy-Item "cursor-auto-rules-agile-workflow\.cursor\*" "$targetDir\.cursor\" -Recurse -Force
Copy-Item "cursor-auto-rules-agile-workflow\xnotes\*" "$targetDir\xnotes\" -Recurse -Force

# Create workflow documentation
@"
# Cursor Workflow Rules

This project has been updated to use the auto rule generator from [cursor-auto-rules-agile-workflow](https://github.com/bmadcode/cursor-auto-rules-agile-workflow)

> **Note**: This script can be safely re-run at any time to update the template rules to their latest versions. It will not impact or overwrite any custom rules you've created.

## Core Features

- Automated rule generation
- Standardized documentation formats
- Supports all four Note Types automatically
- AI behavior control and optimization
- Flexible workflow integration options

## Getting Started

1. Review the templates in \`xnotes/\`
2. Choose your preferred workflow approach
3. Start using the AI with confidence!

For demos and tutorials, visit: [BMad Code Videos](https://youtube.com/bmadcode)
"@ | Set-Content "$targetDir\docs\workflow-rules.md"

# Update .gitignore
if (-not (Select-String -Path "$targetDir\.gitignore" -Pattern ".cursor/rules/_\*.mdc" -Quiet)) {
    @"

# Private individual user cursor rules
.cursor/rules/_*.mdc

# Documentation and templates
xnotes/
docs/
"@ | Add-Content "$targetDir\.gitignore"
}

# Update .cursorignore
if (-not (Test-Path "$targetDir\.cursorignore")) {
    @"
# Project notes and templates
xnotes/
"@ | Set-Content "$targetDir\.cursorignore"
} elseif (-not (Select-String -Path "$targetDir\.cursorignore" -Pattern "xnotes/" -Quiet)) {
    @"

# Project notes and templates
xnotes/
"@ | Add-Content "$targetDir\.cursorignore"
}

# Update .cursorindexingignore
if (-not (Test-Path "$targetDir\.cursorindexingignore")) {
    @"
# Templates - accessible but not indexed
.cursor/templates/
"@ | Set-Content "$targetDir\.cursorindexingignore"
} elseif (-not (Select-String -Path "$targetDir\.cursorindexingignore" -Pattern ".cursor/templates/" -Quiet)) {
    @"

# Templates - accessible but not indexed
.cursor/templates/
"@ | Add-Content "$targetDir\.cursorindexingignore"
}

Write-Host "`n✨ Deployment Complete!"
Write-Host "📁 Core rule generator: $targetDir\.cursor\rules\core-rules\rule-generating-agent.mdc"
Write-Host "📁 Sample sub-folders and rules: $targetDir\.cursor\rules\{sub-folders}\"
Write-Host "📁 Sample Agile Workflow Templates: $targetDir\.cursor\templates\"
Write-Host "📄 Workflow Documentation: $targetDir\docs\workflow-rules.md"
Write-Host "🔒 Updated .gitignore, .cursorignore, and .cursorindexingignore" 