using NexaStation.Domain;

namespace NexaStation.Customization.Models;

public enum InterfaceLayoutType { ModernDashboard, ClassicCrossbar, CompactHandheld }
public enum DashboardCardDensity { Comfortable, Balanced, Dense }
public enum DashboardRowType { BuiltIn, ManualCollection, SmartCollection, Pinned, Provider, Gallery, Media, Recent, CustomQuery }
public enum CardStyle { VerticalCover, Wide, Square, Compact, Icon, List }
public enum CardSize { Small, Medium, Large }
public enum PinTargetType { LibraryItem, GalleryMediaItem, GalleryAlbum, GallerySource, Collection, SystemShortcut, SettingsPage }
public enum PinLocation { Home, Games, Apps, Media, Gallery, QuickAccess, CrossbarCategory, HandheldFavoritesStrip }
public enum CollectionType { Manual, Smart, Folder }
public enum CollectionTargetType { LibraryItem, GalleryMediaItem, GalleryAlbum, GallerySource, ApplicationCategory, Platform, Provider, Collection, SystemShortcut }
public enum SmartRuleField { ContentType, Provider, Platform, Favorite, Hidden, Installed, Available, RecentlyPlayed, RecentlyOpened, AddedDate, LastActivityDate, Playtime, ApplicationCategory, MediaType, GallerySourceKind, Emulator, ControllerProfileEnabled, Title, UserTag }
public enum SmartRuleOperator { Equals, NotEquals, Contains, StartsWith, GreaterThan, LessThan, Before, After, IsTrue, IsFalse, In, NotIn }
public enum SmartRuleMatchMode { MatchAll, MatchAny }
public enum ThemeType { Dark, Light, HighContrast, Custom }
public enum BackgroundType { Solid, Gradient, Image, DynamicArtwork, None }
public enum BackgroundStretch { Fit, Fill, Center }
public enum CardShape { Rounded, Soft, Square }
public enum AnimationIntensity { None, Low, Balanced, High }
public enum UiSoundEvent { Navigate, Confirm, Back, Open, Close, Error, Notification, Startup, Shutdown, Favorite, HoldComplete }
public enum VisualPerformancePreset { Minimal, Balanced, Cinematic, Custom }

public sealed record InterfaceLayoutDefinition(string Id,string DisplayName,string Description,InterfaceLayoutType LayoutType,IReadOnlyList<CardStyle> SupportedCardStyles,bool SupportsHeroBanner,bool SupportsDashboardRows,bool SupportsCrossbarCategories,bool SupportsCompactMode,string DefaultThemeId,string PreviewAssetKey,bool BuiltIn=true,bool Enabled=true);
public sealed class InterfaceLayoutChangedEventArgs(InterfaceLayoutDefinition layout):EventArgs{public InterfaceLayoutDefinition Layout{get;}=layout;}

public sealed record DashboardConfiguration(string Id,string LayoutId,string Name,bool IsDefault,bool HeroEnabled,DashboardCardDensity CardDensity,int RowSpacing,bool ShowStatusBar,bool ShowFooterHints,DateTime CreatedAtUtc,DateTime UpdatedAtUtc);
public sealed record DashboardRowConfiguration(string Id,string DashboardConfigurationId,string RowKey,string DisplayName,DashboardRowType RowType,string? SourceDefinitionJson,int SortOrder,bool Enabled,int MaximumItems,CardStyle CardStyle,CardSize CardSize,bool ShowTitle,bool ShowProviderBadge,bool ShowProgress,bool ShowEmptyState,DateTime CreatedAtUtc,DateTime UpdatedAtUtc);

public sealed record PinnedItem(string Id,PinTargetType TargetType,string TargetId,PinLocation PinLocation,string? GroupName,int SortOrder,DateTime CreatedAtUtc,DateTime UpdatedAtUtc,bool Available=true,string? DisplayName=null);
public sealed record PinRequest(PinTargetType TargetType,string TargetId,PinLocation PinLocation,string? GroupName=null,int? SortOrder=null,string? DisplayName=null);
public sealed record PinReorderRequest(PinLocation Location,IReadOnlyList<string> PinIds);

public sealed record CollectionDefinition(string Id,string Name,string? Description,CollectionType CollectionType,string? IconKey,string? CoverAssetPath,string? ParentCollectionId,int SortOrder,bool Hidden,SmartRuleMatchMode MatchMode,DateTime CreatedAtUtc,DateTime UpdatedAtUtc);
public sealed record CollectionItem(string CollectionId,CollectionTargetType TargetType,string TargetId,int SortOrder,DateTime AddedAtUtc);
public sealed record SmartCollectionRule(string Id,string CollectionId,int RuleGroup,SmartRuleField Field,SmartRuleOperator Operator,string? ValueJson,int SortOrder,DateTime CreatedAtUtc,DateTime UpdatedAtUtc);
public sealed record SmartCollectionDefinition(CollectionDefinition Collection,IReadOnlyList<SmartCollectionRule> Rules);
public sealed record SmartCollectionValidationResult(bool Valid,IReadOnlyList<string> Errors);
public sealed record CollectionResolvedItem(CollectionTargetType TargetType,string TargetId,string DisplayName,bool Available,LibraryItem? LibraryItem=null);

public sealed record BackgroundDefinition(BackgroundType Type,string? Value=null,BackgroundStretch Stretch=BackgroundStretch.Fill,int BlurAmount=0,double DarkeningOverlay=.35,double Opacity=1,bool ManagedCopy=false);
public sealed record ThemeDefinition(string Id,string Name,string Description,ThemeType ThemeType,string AccentColor,string? SecondaryAccentColor,BackgroundDefinition Background,CardShape CardShape,double SurfaceOpacity,bool BlurEnabled,AnimationIntensity AnimationIntensity,string? SoundPackId,bool BuiltIn,string Version,string AuthorDisplayName,string MinimumNexaStationVersion,bool Enabled=true);
public sealed class ThemeChangedEventArgs(ThemeDefinition theme,bool preview):EventArgs{public ThemeDefinition Theme{get;}=theme;public bool Preview{get;}=preview;}

public sealed record UiSoundPack(string Id,string Name,string Description,IReadOnlyDictionary<UiSoundEvent,string?> EventMappings,double Volume,bool BuiltIn,bool Enabled,string License);
public sealed record PerformanceSettings(VisualPerformancePreset Preset,bool BlurEnabled,int ShadowQuality,AnimationIntensity AnimationIntensity,bool BackgroundTransitions,int ArtworkDecodeQuality,int MaximumArtworkMemoryCacheMb,int MaximumArtworkDiskCacheMb,bool PauseAnimationsWhenInactive,bool PauseVisualEffectsDuringLaunch,bool ReduceEffectsOnBattery,int MaximumDashboardItemsPerRow,bool ReducedMotion);

public sealed record CardPresentation(CardStyle Style,CardSize Size,double AspectRatio,bool ShowArtwork,bool ShowTitle,bool ShowSubtitle,bool ShowProvider,bool ShowPlatform,bool ShowProgress,bool ShowFavorite,bool ShowMissingState,bool ShowActivityTime,AnimationIntensity FocusAnimation,double CornerRadius);
public sealed record SemanticFocusState(NavigationSection Section,string? RowKey,CollectionTargetType? TargetType,string? TargetId,string? CollectionId,string LayoutId,int FallbackIndex);
public sealed record SemanticFocusRequest(NavigationSection Section,string LayoutId,IReadOnlyList<string> VisibleTargetIds,string? PreferredTargetId=null,int FallbackIndex=0);
public sealed record FocusRestoreResult(bool Restored,string? TargetId,int FallbackIndex,string? Reason=null);

public sealed record InterfaceConfigurationSnapshot(int FormatVersion,DateTime ExportedAtUtc,string LayoutId,IReadOnlyList<DashboardConfiguration> Dashboards,IReadOnlyList<DashboardRowConfiguration> Rows,IReadOnlyList<PinnedItem> Pins,IReadOnlyList<CollectionDefinition> Collections,IReadOnlyList<CollectionItem> CollectionItems,IReadOnlyList<SmartCollectionRule> SmartRules,string ThemeId,ThemeDefinition? CustomTheme,string SoundPackId,double SoundVolume,PerformanceSettings Performance);
public sealed record InterfaceConfigurationPreview(bool Valid,int DashboardCount,int RowCount,int PinCount,int CollectionCount,IReadOnlyList<string> Warnings);
public sealed record InterfaceConfigurationImportResult(bool Succeeded,string? BackupId,IReadOnlyList<string> Errors);
