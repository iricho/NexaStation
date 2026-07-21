namespace NexaStation.Domain;

public sealed record Asset(
    string Id,
    string LibraryItemId,
    AssetType AssetType,
    string LocalPath,
    int? Width,
    int? Height,
    long? FileSize,
    DateTime AddedAtUtc);

public enum AssetType { Cover, Background, Icon, Logo, Thumbnail }
