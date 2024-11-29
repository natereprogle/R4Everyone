namespace R4Everyone.Binary4Everyone;

public class R4Folder
{
    public string Title { get; set; } = "New Folder";
    public string Description { get; set; } = "New Folder Description";
    public bool OneHot { get; set; } = false;
    public List<R4Folder> Folders { get; } = [];
    public List<R4Cheat> Cheats { get; } = [];
}