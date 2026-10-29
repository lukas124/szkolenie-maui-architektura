namespace FieldService.UI.Models;

// Element listy wizyt
public class VisitItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string TimeText { get; set; } = "";
    public string StatusText { get; set; } = "";
    public Color StatusColor { get; set; } = Colors.Gray;
    public ImageSource? Avatar { get; set; }
    public bool IsBrigade { get; set; }
}
