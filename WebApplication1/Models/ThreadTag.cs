namespace CrimeCode.Models;

public class ThreadTag
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty; // Tutorial, Question, Release, Guide, Tool, etc.
    public string Color { get; set; } = "#00e5a0";
}
