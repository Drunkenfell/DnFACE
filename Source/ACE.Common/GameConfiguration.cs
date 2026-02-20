using System.Collections.Generic;

namespace ACE.Common
{
    public class GameConfiguration
    {
        public string WorldName { get; set; } = "ACEmulator";

        public NetworkSettings Network { get; set; } = new NetworkSettings();

        public AccountDefaults Accounts { get; set; } = new AccountDefaults();

        public string DatFilesDirectory { get; set; } = "c:\\ACE\\Dats\\";

        public string ModsDirectory { get; set; }

        /// <summary>
        /// The amount of seconds to wait before turning off the server. Default value is 60 (for 1 minute).
        /// </summary>
        public uint ShutdownInterval { get; set; } = 60;

        public bool ServerPerformanceMonitorAutoStart { get; set; } = false;

        public ThreadConfiguration Threading { get; set; } = new ThreadConfiguration();

        /// <summary>
        /// Global desired maximum player level. Defaults to 275 (no extension).
        /// Operators may set this to a higher value to enable server-side projected XP tables.
        /// </summary>
        public int MaxPlayerLevel { get; set; } = 275;

        /// <summary>
        /// For testing: assume every player has the content unlock quest flag.
        /// When true, server will treat all players as eligible for extended level caps.
        /// </summary>
        public bool AssumeAllPlayersHaveUnlock { get; set; } = true;
        /// <summary>
        /// The amount of minutes to keep a player object from shard database in memory. Default value is 31 minutes.
        /// </summary>
        public uint ShardPlayerBiotaCacheTime { get; set; } = 31;

        /// <summary>
        /// The amount of minutes to keep a non player object from shard database in memory. Default value is 11 minutes.
        /// </summary>
        public uint ShardNonPlayerBiotaCacheTime { get; set; } = 11;

        public bool WorldDatabasePrecaching { get; set; } = false;

        public bool LandblockPreloading { get; set; } = true;

        public List<PreloadedLandblocks> PreloadedLandblocks { get; set; } = new List<PreloadedLandblocks>()
        {
            new PreloadedLandblocks()
            {
                Id                  = "E74EFFFF",
                Description         = "Hebian-To (Global Events)",
                Permaload           = true,
                IncludeAdjacents    = false,
                Enabled             = true
            },
            new PreloadedLandblocks()
            {
                Id                  = "A9B4FFFF",
                Description         = "Holtburg",
                Permaload           = true,
                IncludeAdjacents    = true,
                Enabled             = false
            },
            new PreloadedLandblocks()
            {
                Id                  = "DA55FFFF",
                Description         = "Shoushi",
                Permaload           = true,
                IncludeAdjacents    = true,
                Enabled             = false
            },
            new PreloadedLandblocks()
            {
                Id                  = "7D64FFFF",
                Description         = "Yaraq",
                Permaload           = true,
                IncludeAdjacents    = true,
                Enabled             = false
            },
            new PreloadedLandblocks()
            {
                Id                  = "0007FFFF",
                Description         = "Town Network",
                Permaload           = true,
                IncludeAdjacents    = false,
                Enabled             = false
            },
            new PreloadedLandblocks()
            {
                Id                  = "00000000",
                Description         = "Apartment Landblocks",
                Permaload           = true,
                IncludeAdjacents    = false,
                Enabled             = false
            }
        };
    }
}
