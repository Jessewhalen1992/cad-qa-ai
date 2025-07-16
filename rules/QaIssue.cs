using System;
using Autodesk.AutoCAD.DatabaseServices;

namespace CadQa.Rules
{
    public class QaIssue
    {
        public int Id { get; set; }

        public IssueType Type { get; set; }

        public ObjectId EntityId { get; set; }

        public string Message { get; set; } = string.Empty;

        public Action? FixAction { get; set; }
    }
}
