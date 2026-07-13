namespace PollApi.Models;

public enum PollQuestionType
{
    SingleChoice, // 2–6 creator-defined options
    YesNo,        // options auto-set to Yes / No
    Rating,       // options auto-set to 1–5
    OpenText      // no options; voters submit free text (stored, not tallied)
}

/// <summary>
/// One question within a poll (survey). A poll has many questions; each question owns its options
/// and its own type. Distinct from the Vote API's audience "Ask" questions.
/// </summary>
public class Question
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PollId { get; set; }
    public int QuestionIndex { get; set; }   // display order within the poll, 0-based
    public string Text { get; set; } = "";
    public PollQuestionType Type { get; set; } = PollQuestionType.SingleChoice;

    // Navigation (1-to-many)
    public ICollection<PollOption> Options { get; set; } = new List<PollOption>();

    // Navigation back to the owning poll
    public Poll Poll { get; set; } = null!;
}
