using System;

namespace DrawingRegisterMJ.Models
{
    public class Drawing
    {
        public int Id { get; set; }
        public string DocumentNumber { get; set; }
        public string DocumentType { get; set; }
        public string Package { get; set; }
        public string Description { get; set; }
        public string Size { get; set; }
        public string Revision { get; set; }
        public string Project { get; set; }
        public string Originator { get; set; }
        public string Volume { get; set; }
        public string Level { get; set; }
        public string FileType { get; set; }
        public string Discipline { get; set; }
        public string Number { get; set; }
        public string FilePath { get; set; }
        public DateTime LastModified { get; set; }
        public DateTime? DateOfIssue { get; set; }
        public string ProjectFolder { get; set; }
    }
} 