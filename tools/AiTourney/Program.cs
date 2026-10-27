using System.Globalization;
using ContingencyZero.Sim;
using ContingencyZero.Sim.Data;
using ContingencyZero.Sim.Map;
using ContingencyZero.Sim.Match;
using ContingencyZero.Sim.Replay;

const int GamesPerMap = 2;
const int CapMinutes = 40;
string[] maps = args.Length > 0 ? args : new[] { "proving_grounds", "ember_rift", "shoal_chain" };
string data = DataDirectory();
UnitCatalog catalog = UnitCatalog.LoadDirectory(Path.Combine(data, "units"));
bool allFinished = true;

foreach (string mapName in maps)
{
    MapData map = MapData.FromJson(File.ReadAllText(Path.Combine(data, "maps", mapName + ".json")));
    for (int game = 0; game < GamesPerMap; game++)
    {
        ulong seed = (ulong)(11 + game);
        Scenario scenario = Scenarios.AiSkirmish(map, seed, AiDifficulty.Hard, AiDifficulty.Easy);
        World world = scenario.CreateWorld(catalog);
        int cap = CapMinutes * 60 * World.TicksPerSecond;
        while (!world.IsOver && world.Tick < cap) world.Step();
        bool finished = world.IsOver;
        allFinished &= finished;
        string winner = !finished ? "TIMEOUT" : world.WinningTeam switch
        {
            0 => "Hard",
            1 => "Easy",
            _ => "Draw",
        };
        double minutes = world.Tick / (60.0 * World.TicksPerSecond);
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{mapName} game={game + 1} seed={seed} winner={winner} game_minutes={minutes:F2} ticks={world.Tick}"));
    }
}

return allFinished ? 0 : 2;

static string DataDirectory()
{
    foreach (string start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
    {
        string? dir = start;
        while (dir is not null)
        {
            string candidate = Path.Combine(dir, "data");
            if (Directory.Exists(Path.Combine(candidate, "units"))) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
    }
    throw new DirectoryNotFoundException("Couldn't find data/units above the tourney output or working folder");
}
