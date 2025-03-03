using System.Text;
using Serilog;

namespace R4Everyone.Binary4Everyone;

/// <summary>
/// An instance of an R4Database represents an entire usrcheat.dat cheat database. This class is responsible for reading and writing the cheat database.
/// </summary>
public class R4Database : IAsyncDisposable
{
    // Data to confirm the file is a valid R4 cheat database
    private const int HeaderSize = 0x100;
    private const string MagicString = "R4 CheatCode";

    // Database properties
    public R4Encoding FileEncoding;
    public string Title = "User cheat code v1.0";
    public bool Enabled { get; set; }

    public string? R4FilePath;

    // Database data
    public List<R4Game> Games { get; } = [];

    // Other properties

    /// <summary>
    /// Creates a new instance of an R4Database. This is supposed to be used when creating a net-new file instead of reading from one.
    /// </summary>
    /// <param name="r4FilePath">The file path of the new usrcheat.dat file</param>
    /// <param name="title">The title of the cheat database</param>
    /// <param name="encoding">The encoding method to use for cheats, titles, and descriptions</param>
    /// <param name="enabled">Whether the cheat database is enabled as a whole</param>
    public R4Database(string r4FilePath, string title, R4Encoding encoding, bool enabled) :
        this(r4FilePath)
    {
        Title = title;
        FileEncoding = encoding;
        Enabled = enabled;
    }

    /// <summary>
    /// A constructor for creating a completely blank R4Database with default properties. A filepath should be set at a later time
    /// </summary>
    public R4Database()
    {
        Title = "My Database";
        FileEncoding = R4Encoding.UTF8;
        Enabled = true;
    }

    /// <summary>
    /// A constructor meant for opening an existing R4Database, since it can easily be deserialized by the class.
    /// </summary>
    /// <param name="r4FilePath">The filepath of the usrcheat.dat file. This constructor is used when reading from an existing file</param>
    public R4Database(string r4FilePath)
    {
        R4FilePath = r4FilePath;
    }

    // Methods
    /// <summary>
    /// Reads the database from the file and parses it into a usable format. Since this is an async method, it must be called outside the constructor
    /// </summary>
    /// <exception cref="FileLoadException">Thrown if the file failed validation</exception>
    public async Task ParseDatabaseAsync()
    {
        try
        {
            Log.Debug("Beginning validation of R4 cheat database");
            await ValidateDatabaseAsync(R4FilePath);
            
            Log.Debug("Validation succeeded, loading database from file");

            if (!Path.Exists(R4FilePath) || string.IsNullOrWhiteSpace(R4FilePath))
                throw new FileNotFoundException("R4Database doesn't exist at this location or no path provided");

            Dictionary<uint, R4Game> games = [];
            // Get the info we need from the header, which is just title, encoding, and enabled status
            await using var fs = new FileStream(R4FilePath, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(fs);

            Log.Debug("Reading R4 cheat database title and encoding method");
            Log.Verbose("Skipping magic string, moving to title. Pointer is at {Position}", reader.BaseStream.Position);
            reader.BaseStream.Seek(0x10, SeekOrigin.Begin);
            Title = Encoding.ASCII.GetString(reader.ReadBytes(0x4B - 0x10 + 1), 0, 0x4B - 0x10 + 1);

            Log.Verbose("Found title: \"{Title}\", reading encoding method. Pointer is at {Position}", Title,
                reader.BaseStream.Position);
            FileEncoding = R4EncodingHelper.GetEncoding(reader.ReadBytes(4));
            Log.Verbose("Found encoding method: \"{Encoding}\", reading master enable flag. Pointer is at {Position}",
                FileEncoding, reader.BaseStream.Position);

            Enabled = reader.ReadByte() == 1;
            Log.Verbose("Master enable flag is {Enabled}, moving to game pointers. Pointer is at {Position}", Enabled,
                reader.BaseStream.Position);

            // Seek to the beginning of the games section, which we know is 0x100 from the beginning of the file
            reader.BaseStream.Seek(0x100, SeekOrigin.Begin);

            // Read the game pointer section
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                Log.Verbose("Reading game pointer at {Position}", reader.BaseStream.Position);
                var chunk = reader.ReadBytes(16);
                Log.Verbose("Bytes read: {Bytes}", BitConverter.ToString(chunk).Replace("-", " "));

                // A row of 16 0x00 bytes is the end of the section
                if (chunk.All(b => b == 0x00)) break;

                var gameId = Encoding.ASCII.GetString(chunk.Take(4).ToArray());
                var offset =
                    BitConverter.ToUInt32(chunk.Skip(8).Take(4).ToArray(), 0); // Read little-endian offset
                Log.Information("Found game with Game ID: {gameId}", gameId);
                Log.Verbose("Game ID {gameId} has offset: {offset}", gameId, offset.ToString("x8"));
                games.Add(offset, new R4Game(gameId));
            }

            Log.Debug("Finished reading game pointers, moving to game data. Pointer is at {Position}",
                reader.BaseStream.Position);

            // Read the games
            foreach (var (offset, game) in games)
            {
                Log.Verbose("Seeking to game at offset: {Offset}. Pointer is currently at {Pointer}",
                    offset.ToString("x8"),
                    reader.BaseStream.Position);
                reader.BaseStream.Seek(offset, SeekOrigin.Begin);
                R4GameDeserializer gameDeserializer = new(game);
                try
                {
                    Log.Debug("Reading game with ID {gameId}", game.GameId);
                    games[offset] = await gameDeserializer.Deserialize(reader);
                }
                catch (IOException e)
                {
                    Log.Error(e, "Failed to read game with ID {gameId}, moving to next game", game.GameId);
                }
            }

            // Copy the dictionary values to the List. We only need the offsets during the deserialization process
            foreach (var game in games.Values) Games.Add(game);

            Log.Information("Title: " + Title);
            Log.Information("File Encoding: " + FileEncoding);
            Log.Information("Enabled: " + Enabled);
            Log.Information("Games: " + string.Join(", ", Games.Select(g => g.GameTitle)));
            try
            {
                reader.Close();
            }
            catch (ObjectDisposedException)
            {
                Log.Debug("Attempt to close reader failed, reader is already closed");
            }
        }
        catch (FileLoadException e)
        {
            Log.Error(e, "Failed to load R4 cheat database, are you sure this is a valid file?");
            throw;
        }
        catch (FileNotFoundException e)
        {
            Log.Error(e, "The specified file does not exist");
            throw;
        }
        catch (ArgumentNullException e)
        {
            Log.Error(e, "The specified file path is null or empty");
            throw;
        }
        catch (Exception e)
        {
            Log.Error(e, "An unexpected error occurred while loading the R4 cheat database");
            throw;
        }
    }

    /// <summary>
    /// Validates that a cheat file is valid. The file name, but usrcheat.dat is standard. 
    /// If this method exits without error then validation passed
    /// </summary>
    /// <param name="filePath">The filepath of the cheat database</param>
    /// <exception cref="FileNotFoundException">Thrown if the cheat file doesn't exist</exception>
    /// <exception cref="FileLoadException">Thrown if validation fails</exception>
    private static async Task ValidateDatabaseAsync(string? filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("The specified file does not exist.", filePath);

        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(fs);
        Log.Verbose(
            "Checking file size to ensure it meets the minimum size of 0x100 bytes (The size of an R4 file header)");
        var header = reader.ReadBytes(HeaderSize);
        // Validate that the database is at minimum 100 bytes. If not, we can guarantee it's invalid
        if (header.Length < HeaderSize)
            throw new FileLoadException("File size is smaller than the minimum required size for an R4 cheat database");
        Log.Verbose("File is at least 100 bytes long, checking magic string");

        // Validate that the first 12 bytes are the magic string, which is required for an R4 cheat database
        var magicString = Encoding.ASCII.GetString(header, 0, 12);
        if (magicString != MagicString)
            throw new FileLoadException("Database header could not be verified");
        Log.Verbose("Magic string is correct, checking encoding method");

        // Validate that the bytes at 0x4C and 0x4D are a valid encoding method. Technically we could ignore the last
        // two bytes, and we could even validate just on the first byte, but we're being extra safe here
        // This is checking that at least one of the encoding methods matches the bytes at these locations and, if so,
        // returns that value so we can set the file encoding
        var isValidEncoding = Enum.GetValues<R4Encoding>().Select(R4EncodingHelper.GetBytes)
            .Any(bytes => bytes[0] == header[0x4C] && bytes[1] == header[0x4D] && bytes[2] == header[0x4E] &&
                          bytes[3] == header[0x4F]);

        // We've already passed the last check, so we just need to return its value since we know everything was true up to this point
        if (!isValidEncoding)
            throw new FileLoadException(
                "Encoding method is not valid");

        Log.Verbose("Encoding method is valid");
    }

    /// <summary>
    /// Handles disposing of the database, and flushes the log
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await Log.CloseAndFlushAsync();
    }
}