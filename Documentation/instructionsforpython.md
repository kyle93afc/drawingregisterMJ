# Instructions for Cleaning Drawing Register Outgoing Folder

## Purpose
This document outlines the process for cleaning up the outgoing folder in the issued drawings directory by moving outdated drawing revisions to the superseded (SS) folder.

## Requirements
- Python 3.x
- Access to the drawing register file system
- Required folder structure:
  - issued/
    - outgoing/
    - SS/ (superseded)

## Process Overview
1. Scan the outgoing folder for all drawing files
2. Group drawings by their base name (excluding revision number)
3. For each group of drawings:
   - Identify the latest revision
   - Move all older revisions to the SS folder
   - Keep the latest revision in the outgoing folder

## Python Script Requirements

### Input
- Path to the outgoing folder
- Path to the SS (superseded) folder

### File Naming Convention
Drawings should follow the standard naming convention:
- Format: `[DrawingNumber]_[Description]_[RevisionNumber].[extension]`
- Example: `DR-001_FloorPlan_Rev2.pdf`

### Processing Steps
1. **File Scanning**
   - Recursively scan the outgoing folder
   - Create a list of all drawing files
   - Parse drawing numbers and revision numbers from filenames

2. **Grouping**
   - Group files by their base drawing number
   - Sort each group by revision number
   - Identify the latest revision in each group

3. **File Movement**
   - For each group:
     - Keep the latest revision in outgoing
     - Move all other revisions to SS folder
   - Log all file movements
   - Create a report of actions taken

4. **Error Handling**
   - Check for file access permissions
   - Verify destination folder exists
   - Handle duplicate files
   - Log any errors encountered

### Safety Measures
- Create a backup before moving files
- Log all operations
- Verify file integrity after moves
- Option to do a "dry run" without actual file movement

## Implementation Notes
1. Use `os` and `shutil` modules for file operations
2. Implement robust error handling
3. Create detailed logs of all operations
4. Add progress indicators for long operations
5. Include validation of file names and folder structure

## Usage Example
```python
python clean_drawings.py --outgoing-dir "./issued/outgoing" --superseded-dir "./issued/SS" --dry-run
```

## Output
The script should generate:
1. Log file of all operations
2. Summary report showing:
   - Number of files processed
   - Number of files moved
   - List of latest revisions kept
   - Any errors encountered

## Safety Checklist
- [ ] Backup current folder structure
- [ ] Verify correct folder paths
- [ ] Check available disk space
- [ ] Validate file naming patterns
- [ ] Test with dry-run first
- [ ] Review generated logs
- [ ] Verify file integrity after moves

## Support
For any issues or questions, please contact the system administrator or refer to the drawing register documentation. 