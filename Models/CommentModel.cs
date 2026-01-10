namespace AutoCAC.Models;

public sealed class CommentModel
{
    public long Id { get; set; }
    public string Text { get; set; }
    public AuthUser AuthUser { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsCurrentUser(int? userId)
        => AuthUser != null && AuthUser.Id == userId;
}