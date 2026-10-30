using Windows.Media.Control;

namespace ChoppaAI.Media;

public sealed record NowPlayingTrack(
    string SessionId,
    string AppName,
    string Title,
    string Artist,
    string Album);

public static class WindowsMediaSessionReader
{
    public static async Task<IReadOnlyList<NowPlayingTrack>> GetCurrentlyPlayingAsync()
    {
        var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        var tracks = new List<NowPlayingTrack>();

        foreach (var session in manager.GetSessions())
        {
            if (session.GetPlaybackInfo().PlaybackStatus
                != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            {
                continue;
            }

            var properties = await session.TryGetMediaPropertiesAsync();
            if (string.IsNullOrWhiteSpace(properties.Title)
                && string.IsNullOrWhiteSpace(properties.Artist))
            {
                continue;
            }

            var sessionId = session.SourceAppUserModelId;
            tracks.Add(new NowPlayingTrack(
                sessionId,
                GetFriendlyAppName(sessionId),
                properties.Title,
                properties.Artist,
                properties.AlbumTitle));
        }

        return tracks;
    }

    public static async Task<bool> SkipToNextAsync(string sessionId)
    {
        var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        var session = manager.GetSessions().FirstOrDefault(
            candidate => string.Equals(
                candidate.SourceAppUserModelId,
                sessionId,
                StringComparison.OrdinalIgnoreCase));
        return session is not null && await session.TrySkipNextAsync();
    }

    private static string GetFriendlyAppName(string appUserModelId)
    {
        var name = appUserModelId.Split('!').LastOrDefault() ?? appUserModelId;
        if (appUserModelId.Contains("chrome", StringComparison.OrdinalIgnoreCase))
        {
            return "Google Chrome";
        }
        if (appUserModelId.Contains("msedge", StringComparison.OrdinalIgnoreCase))
        {
            return "Microsoft Edge";
        }
        if (appUserModelId.Contains("firefox", StringComparison.OrdinalIgnoreCase))
        {
            return "Firefox";
        }
        if (appUserModelId.Contains("brave", StringComparison.OrdinalIgnoreCase))
        {
            return "Brave";
        }
        if (appUserModelId.Contains("spotify", StringComparison.OrdinalIgnoreCase))
        {
            return "Spotify";
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            return "a media app";
        }

        return name;
    }
}
