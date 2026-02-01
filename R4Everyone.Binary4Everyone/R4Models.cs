using System.Text;

namespace R4Everyone.Binary4Everyone;

public interface IR4Item
{
    string Title { get; set; }
    string Description { get; set; }
}

public interface IR4Container
{
    List<IR4Item> Items { get; }
}