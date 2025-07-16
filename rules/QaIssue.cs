namespace CadQa.Rules
{
    public class QaIssue
    {
        public int Id { get; set; }
        public string Type { get; set; }
        public string Message { get; set; }
        public string FixAction { get; set; }
    }
}
