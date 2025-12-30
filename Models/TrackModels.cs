using System.Text.Json.Serialization;

namespace MusicCat.Rpc.Models;

public record Track(
    [property: JsonPropertyName("identifier")] string Identifier,
    [property: JsonPropertyName("author")] string Author,
    [property: JsonPropertyName("length")] long Length,
    [property: JsonPropertyName("isStream")] bool IsStream,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("uri")] string Uri,
    [property: JsonPropertyName("sourceName")] string SourceName,
    [property: JsonPropertyName("position")] long Position,
    [property: JsonPropertyName("artworkUrl")] string ArtworkUrl
);

public record MusicStatus(
    [property: JsonPropertyName("is_paused")] bool IsPaused,
    [property: JsonPropertyName("is_playing")] bool IsPlaying,
    [property: JsonPropertyName("track")] Track Track
);