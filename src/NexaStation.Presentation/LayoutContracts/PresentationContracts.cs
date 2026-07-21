using NexaStation.Customization.Models;

namespace NexaStation.Presentation.LayoutContracts;

public sealed record LayoutPresentationState(string LayoutId,InterfaceLayoutType LayoutType,bool Active,string NavigationMode,double OuterMargin,double RowSpacing,double CardWidth,double CardHeight,bool HeroVisible,bool StatusBarVisible,bool FooterHintsVisible,bool ReducedArtwork,bool TouchFriendly);
public interface IInterfacePresentationService{string ActiveLayoutId{get;}event EventHandler<LayoutPresentationState>? PresentationChanged;LayoutPresentationState Create(InterfaceLayoutDefinition layout,DashboardConfiguration dashboard,PerformanceSettings performance);void Activate(LayoutPresentationState presentation);bool IsInputOwner(string layoutId);}
public interface ISemanticFocusService{Task SaveAsync(SemanticFocusState focusState,CancellationToken cancellationToken=default);Task<FocusRestoreResult> RestoreAsync(SemanticFocusRequest request,CancellationToken cancellationToken=default);}
public interface ICardPresentationService{NexaStation.Customization.Models.CardPresentation Resolve(CardStyle style,CardSize size,InterfaceLayoutType layout,bool reducedMotion=false);}
