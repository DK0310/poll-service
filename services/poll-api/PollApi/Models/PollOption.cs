namespace PollApi.Models;

public class PollOption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PollId { get; set; }
    public int OptionIndex { get; set; }
    public string Text { get; set; } = "";

    // Navigation back to the owning poll
    public Poll Poll { get; set; } = null!;
}
