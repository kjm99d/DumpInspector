namespace DumpInspector.Server.Models
{
    public class AppOption
    {
        public int Id { get; set; }
        public string Key { get; set; } = default!;
        // Store JSON string or simple value
        public string Value { get; set; } = default!;
    }
}
