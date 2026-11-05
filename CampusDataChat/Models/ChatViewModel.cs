using System.ComponentModel.DataAnnotations;
namespace CampusDataChat.Models;

public sealed class ChatViewModel
{
    [Required, StringLength(500, MinimumLength = 2)]
    [Display(Name = "Ask about the course data")]
    public string Question { get; set; } = "";
    public IReadOnlyList<ChatTurn> Turns { get; set; } = [];
    public string ProviderLabel { get; set; } = "Demo";
}