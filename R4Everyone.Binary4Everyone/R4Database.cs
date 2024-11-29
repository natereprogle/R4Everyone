using System.Text;

namespace R4Everyone.Binary4Everyone;

public class R4Database
{
    // Data to confirm the file is a valid R4 cheat database
    private const int HeaderSize = 0x100;
    private const string MagicString = "R4 CheatCode";

    // Database properties
    private R4Encoding _fileEncoding;
    private string _title = "My Database Title";
    private bool Enabled { get; set; }

    // Database data, this is private to prevent scary things from happening
    private Dictionary<uint, R4Game> Games { get; } = [];

    private string filePath;

    // Methods
    public R4Database(string r4FilePath)
    {
        this.filePath = r4FilePath;
    }

    public R4Database(string r4FilePath, string title, R4Encoding encoding, bool enabled)
    {
        this.filePath = r4FilePath;
        _title = title;
        _fileEncoding = encoding;
        Enabled = enabled;
    }

    public async Task ParseDatabaseAsync()
    {
        var validDb = await ValidateDatabaseAsync(filePath);
        if (validDb)
        {
            await LoadDatabaseAsync(filePath);
        }
        else
        {
            throw new FileLoadException("Failed to load R4 cheat database, are you sure this is a valid file?");
        }
    }

    private static async Task<bool> ValidateDatabaseAsync(string filePath)
    {
        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(fs);
        var header = reader.ReadBytes(HeaderSize);
        // Validate that the database is at minimum 100 bytes. If not, we can guarantee it's invalid
        if (header.Length != HeaderSize)
            throw new FileLoadException("Failed to load R4 cheat database, are you sure this is a valid file?");

        // Validate that the first 12 bytes are the magic string, which is required for an R4 cheat database
        var magicString = Encoding.ASCII.GetString(header, 0, 12);
        if (magicString != MagicString)
            return false;

        // Validate that the bytes at 0x4C and 0x4D are a valid encoding method. Technically we could ignore the last
        // two bytes, and we could even validate just on the first byte, but we're being extra safe here
        // This is checking that at least one of the encoding methods matches the bytes at these locations and, if so,
        // returns that value so we can set the file encoding
        var isValidEncoding = Enum.GetValues<R4Encoding>().Select(R4EncodingHelper.GetBytes)
            .Any(bytes => bytes[0] == header[0x4C] && bytes[1] == header[0x4D] && bytes[2] == header[0x4E] &&
                          bytes[3] == header[0x4F]);

        // We've already passed the last check, so we just need to return its value since we know everything was true up to this point
        return isValidEncoding;
    }

    private async Task LoadDatabaseAsync(string filePath)
    {
        // Get the info we need from the header, which is just title, encoding, and enabled status
        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(fs);
        reader.BaseStream.Seek(0x10, SeekOrigin.Begin);
        _title = Encoding.ASCII.GetString(reader.ReadBytes(0x4B - 0x10 + 1), 0, 0x4B - 0x10 + 1);
        _fileEncoding = R4EncodingHelper.GetEncoding(reader.ReadBytes(4));
        Enabled = reader.ReadByte() == 1;
        // Seek to the beginning of the games section, which we know is 0x100 from the beginning of the file
        reader.BaseStream.Seek(0x100, SeekOrigin.Begin);

        // Read the game pointer section
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            var chunk = reader.ReadBytes(16);
            // A row of 16 0x00 bytes is the end of the section
            if (chunk.All(b => b == 0x00)) break;

            var gameId = Encoding.ASCII.GetString(chunk.Take(4).ToArray());
            var offset =
                BitConverter.ToUInt32(chunk.Skip(8).Take(4).ToArray(), 0); // Read little-endian offset
            Console.WriteLine("Game ID: " + gameId + " Offset: " + offset.ToString("x8"));
            Games.Add(offset, new R4Game(gameId));
        }

        // Read the games
        foreach (var (offset, game) in Games)
        {
            Console.WriteLine("Seeking to offset: " + offset.ToString("x8"));
            reader.BaseStream.Seek(offset, SeekOrigin.Begin);
            R4GameDeserializer gameDeserializer = new(game);
            await gameDeserializer.Deserialize(reader);
        }

        Console.WriteLine("Title: " + _title);
        Console.WriteLine("File Encoding: " + _fileEncoding);
        Console.WriteLine("Enabled: " + Enabled);
        Console.Write(string.Join(Games.ToString(), ", "));
        reader.Close();
    }

    public static void Main()
    {
        var r4Db = new R4Database(@"C:\Users\Nate Reprogle\Downloads\usrcheat.dat");

        _ = r4Db.ParseDatabaseAsync();
    }
}