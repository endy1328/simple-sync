namespace SimpleSync;

public sealed class AppSkin
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required Color AppBackground { get; init; }
    public required Color Surface { get; init; }
    public required Color SurfaceAlt { get; init; }
    public required Color Accent { get; init; }
    public required Color AccentDark { get; init; }
    public required Color AccentSoft { get; init; }
    public required Color Text { get; init; }
    public required Color MutedText { get; init; }
    public required Color Border { get; init; }
    public required Color LogBackground { get; init; }
    public required Color LogText { get; init; }

    public override string ToString() => DisplayName;
}

public static class AppSkins
{
    private static readonly AppSkin[] Skins =
    [
        new()
        {
            Key = "fluent_light",
            DisplayName = "Fluent Light",
            AppBackground = Color.FromArgb(244, 247, 251),
            Surface = Color.White,
            SurfaceAlt = Color.FromArgb(248, 250, 252),
            Accent = Color.FromArgb(33, 118, 255),
            AccentDark = Color.FromArgb(21, 101, 216),
            AccentSoft = Color.FromArgb(219, 234, 254),
            Text = Color.FromArgb(16, 24, 40),
            MutedText = Color.FromArgb(102, 112, 133),
            Border = Color.FromArgb(208, 213, 221),
            LogBackground = Color.White,
            LogText = Color.FromArgb(52, 64, 84)
        },
        new()
        {
            Key = "syncback_blue",
            DisplayName = "SyncBack Blue",
            AppBackground = Color.FromArgb(238, 244, 252),
            Surface = Color.White,
            SurfaceAlt = Color.FromArgb(239, 247, 255),
            Accent = Color.FromArgb(0, 132, 210),
            AccentDark = Color.FromArgb(0, 92, 166),
            AccentSoft = Color.FromArgb(204, 232, 255),
            Text = Color.FromArgb(24, 39, 63),
            MutedText = Color.FromArgb(90, 105, 128),
            Border = Color.FromArgb(190, 210, 232),
            LogBackground = Color.FromArgb(250, 253, 255),
            LogText = Color.FromArgb(48, 68, 92)
        },
        new()
        {
            Key = "graphite_dark",
            DisplayName = "Graphite Dark",
            AppBackground = Color.FromArgb(24, 28, 36),
            Surface = Color.FromArgb(34, 39, 49),
            SurfaceAlt = Color.FromArgb(43, 49, 61),
            Accent = Color.FromArgb(90, 169, 255),
            AccentDark = Color.FromArgb(55, 132, 218),
            AccentSoft = Color.FromArgb(54, 74, 102),
            Text = Color.FromArgb(236, 241, 247),
            MutedText = Color.FromArgb(166, 176, 190),
            Border = Color.FromArgb(70, 78, 94),
            LogBackground = Color.FromArgb(27, 31, 40),
            LogText = Color.FromArgb(209, 217, 230)
        },
        new()
        {
            Key = "warm_folder",
            DisplayName = "Warm Folder",
            AppBackground = Color.FromArgb(252, 247, 237),
            Surface = Color.FromArgb(255, 253, 248),
            SurfaceAlt = Color.FromArgb(255, 247, 226),
            Accent = Color.FromArgb(230, 126, 34),
            AccentDark = Color.FromArgb(180, 88, 18),
            AccentSoft = Color.FromArgb(255, 229, 190),
            Text = Color.FromArgb(54, 42, 31),
            MutedText = Color.FromArgb(120, 96, 72),
            Border = Color.FromArgb(232, 207, 174),
            LogBackground = Color.FromArgb(255, 253, 248),
            LogText = Color.FromArgb(86, 66, 48)
        },
        new()
        {
            Key = "soft_mint",
            DisplayName = "Soft Mint",
            AppBackground = Color.FromArgb(238, 248, 246),
            Surface = Color.White,
            SurfaceAlt = Color.FromArgb(241, 252, 249),
            Accent = Color.FromArgb(18, 166, 145),
            AccentDark = Color.FromArgb(8, 122, 105),
            AccentSoft = Color.FromArgb(197, 240, 232),
            Text = Color.FromArgb(20, 48, 47),
            MutedText = Color.FromArgb(83, 111, 109),
            Border = Color.FromArgb(188, 220, 214),
            LogBackground = Color.FromArgb(250, 255, 253),
            LogText = Color.FromArgb(48, 82, 78)
        }
    ];

    public static IReadOnlyList<AppSkin> All => Skins;

    public static AppSkin Get(string? key)
    {
        return Skins.FirstOrDefault(skin => skin.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) ?? Skins[1];
    }
}
