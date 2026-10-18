namespace NexaStation.UI.Settings;

public static class BuiltInSettingsCatalog
{
    public static ISettingsCatalog Create()
    {
        SettingsCategoryDescriptor[] categories =
        [
            C("personalization","Personalization","Theme, layout, appearance, motion, and sounds.","\uE771",10),
            C("input","Input and Controllers","Controllers, prompts, mapping, navigation, and desktop control.","\uE7FC",20),
            C("providers","Games and Providers","Game libraries, launcher providers, refresh, and imports.","\uE7FC",30),
            C("emulators","Emulators","Installed emulators, discovery, platforms, and game folders.","\uE7FC",40),
            C("artwork","Artwork and Metadata","Metadata providers, matching, artwork jobs, and caches.","\uE91B",50),
            C("applications","Applications","Windows application discovery, categories, and app behavior.","\uECAA",60),
            C("media-player","Media Player","Native and external playback, resume, tracks, and privacy.","\uE714",70),
            C("gallery","Gallery","Sources, Pictures, privacy, albums, thumbnails, and recordings.","\uEB9F",80),
            C("capture","Capture","Explicit screenshots, folders, format, Gallery, and hotkeys.","\uE722",90),
            C("audio","Audio and Media Sessions","System media controls, metadata, and preferred media apps.","\uE767",100),
            C("display","Display and Television","Display choice, fullscreen, safe area, scale, and calibration.","\uE7F4",110),
            C("network","Network and Remote Play","Network status and approved remote-play applications.","\uE968",120),
            C("privacy","Activity and Privacy","History, Guest Mode, diagnostics, online access, and presence.","\uE72E",130),
            C("startup","Startup and Windows","Sign-in launch, exit behavior, integration, and startup health.","\uE7E8",140),
            C("updates","Updates","Version, channel, checks, notes, and update diagnostics.","\uE895",150),
            C("storage","Storage and Data","Data location, usage, backups, import, and export.","\uEDA2",160),
            C("accessibility","Accessibility","Contrast, motion, text, focus, labels, and input alternatives.","\uE776",170),
            C("help","Help and About","Help, privacy, security, notices, diagnostics, and version details.","\uE897",180),
            C("advanced","Advanced","Compatibility, database health, logging, and experimental integrations.","\uE713",190,true),
            C("reset-recovery","Reset and Recovery","Disruptive reset and recovery actions, isolated from normal settings.","\uE777",200,true,true),
        ];

        List<SettingDescriptor> settings =
        [
            S("personalization.theme","personalization","theme","Theme","Choose Nexa or the original Nexa Crossbar presentation.",["crossbar","wave","appearance"],SettingControlType.Navigation,null,null,10,recommended:true),
            S("personalization.layout","personalization","layout","Layout","Choose the shared content layout and dashboard composition.",["dashboard","home rows"],SettingControlType.Choice,"Interface.LayoutId","modern-dashboard",20),
            S("personalization.background","personalization","appearance","Background","Choose a solid, gradient, or user-approved background.",["wallpaper","wave"],SettingControlType.Navigation,null,null,30),
            S("personalization.accent","personalization","appearance","Accent color","Set the interface accent and readable contrast pair.",["teal","color"],SettingControlType.Navigation,null,null,40),
            S("personalization.color-scheme","personalization","appearance","Color scheme","Choose Black & Gold or another palette, with adjustable accents across all themes.",["gold","black","palette","colors"],SettingControlType.Navigation,null,null,35),
            S("personalization.scale","personalization","appearance","Interface scale","Scale NexaStation controls independently of content.",["zoom","text"],SettingControlType.Slider,"InterfaceScale",1d,50,validation:new("Range",.75,2)),
            S("personalization.density","personalization","layout","Card density","Adjust card density without changing library data.",["cards","compact"],SettingControlType.Choice,"Interface.CardDensity","Balanced",60),
            S("personalization.sounds","personalization","feedback","Interface sounds","Choose or disable original NexaStation UI sounds.",["volume","audio"],SettingControlType.Navigation,null,null,70),
            S("personalization.reduced-motion","personalization","motion","Reduced Motion","Stop decorative motion and use static Crossbar backgrounds.",["animation","accessibility","wave"],SettingControlType.Toggle,"Interface.Performance.ReducedMotion",false,80,recommended:true),

            S("input.detected","input","devices","Detected controllers","Review connected controller representations and status.",["gamepad","dualsense","playstation","xbox","nintendo"],SettingControlType.Navigation,null,null,10),
            S("input.prompt-style","input","prompts","Prompt style","Choose Automatic, Xbox, PlayStation Shapes, Nintendo, Generic, Keyboard, or Minimal Text.",["glyphs","cross","circle","dualsense"],SettingControlType.Choice,"Input.PromptStyle","Automatic",20),
            S("input.mapping","input","mapping","Controller mapping","Stage and validate semantic action mappings.",["remap","buttons"],SettingControlType.Navigation,null,null,30,save:SettingSaveBehavior.Staged),
            S("input.deadzone","input","navigation","Navigation deadzone","Ignore small analog movement and stick drift.",["analog","drift"],SettingControlType.Slider,"Input.NavigationDeadzone",.55,40,validation:new("Range",.1,.95)),
            S("input.repeat-delay","input","navigation","Repeat delay","Set the initial controller navigation repeat delay.",["navigation","milliseconds"],SettingControlType.Number,"Input.RepeatDelayMilliseconds",350,50,validation:new("Range",150,1000)),
            S("input.desktop-control","input","desktop","Desktop Controller Mode","Configure explicit controller-driven desktop input.",["mouse","keyboard"],SettingControlType.Navigation,null,null,60),
            S("input.emergency-reset","input","recovery","Emergency input reset","View the reserved keyboard and controller recovery controls.",["ctrl shift i","recovery"],SettingControlType.Summary,null,null,70),

            S("providers.steam","providers","launchers","Steam","Detect, import, refresh, and configure Steam libraries.",["games","launcher"],SettingControlType.Navigation,null,null,10),
            S("providers.other","providers","launchers","Other game launchers","Review Epic, GOG, Ubisoft, EA App, Battle.net, and Xbox capabilities.",["epic","gog","ubisoft","ea","battle.net","microsoft store"],SettingControlType.Navigation,null,null,20),
            S("providers.refresh-startup","providers","refresh","Refresh providers at startup","Refresh enabled providers after startup health checks.",["scan","import"],SettingControlType.Toggle,"Providers.RefreshOnStartup",false,30),
            S("providers.hidden","providers","visibility","Hidden provider items","Review provider items hidden from NexaStation views.",["restore","missing"],SettingControlType.Navigation,null,null,40),

            S("emulators.installed","emulators","installations","Installed emulators","Manage configured emulator executables without modifying their files.",["dolphin","pcsx2","ppsspp","retroarch","rpcs3"],SettingControlType.Navigation,null,null,10),
            S("emulators.find","emulators","discovery","Find installed emulators","Run conservative discovery and review candidates before adding.",["detect","scan"],SettingControlType.Action,null,null,20),
            S("emulators.folders","emulators","discovery","Discovery folders","Approve bounded portable-emulator discovery folders.",["portable","folder"],SettingControlType.Navigation,null,null,30,save:SettingSaveBehavior.Staged),
            S("emulators.platform-defaults","emulators","platforms","Platform defaults","Choose the preferred configured adapter per platform.",["adapter","default"],SettingControlType.Navigation,null,null,40,save:SettingSaveBehavior.Staged),
            S("emulators.game-folders","emulators","games","ROM folders","Manage user-approved game folders; no ROM downloads are offered.",["games","sources"],SettingControlType.Navigation,null,null,50,save:SettingSaveBehavior.Staged),

            S("artwork.providers","artwork","providers","Metadata providers","Enable and prioritize approved metadata and artwork providers.",["steamgriddb","igdb","screenscraper","thegamesdb"],SettingControlType.Navigation,null,null,10,save:SettingSaveBehavior.Staged),
            S("artwork.matching","artwork","matching","Matching behavior","Configure confidence thresholds and manual review behavior.",["identity","confidence","review"],SettingControlType.Navigation,null,null,20),
            S("artwork.bulk","artwork","jobs","Bulk artwork jobs","Find missing artwork with bounded concurrency and review.",["scrape","download","cache"],SettingControlType.Action,null,null,30),
            S("artwork.review","artwork","matching","Review queue","Review uncertain metadata and artwork matches.",["match","manual"],SettingControlType.Navigation,null,null,40),
            S("artwork.cache","artwork","storage","Artwork cache","Review artwork cache usage and reproducible cleanup.",["disk","memory"],SettingControlType.Navigation,null,null,50),

            S("applications.start-menu","applications","discovery","Start Menu scanning","Scan current-user and all-users Start Menu entries read-only.",["shortcuts","lnk"],SettingControlType.Navigation,null,null,10),
            S("applications.packaged","applications","discovery","Packaged applications","Include current-user Windows packaged applications.",["store","apps folder"],SettingControlType.Toggle,"Providers.WindowsApps.IncludeAppsFolder",true,20),
            S("applications.discover","applications","catalogue","Discover Apps","Browse the curated local application catalogue.",["install","preferred apps"],SettingControlType.Navigation,null,null,30),
            S("applications.hidden","applications","visibility","Hidden applications","Review applications hidden from NexaStation.",["restore","categories"],SettingControlType.Navigation,null,null,40),

            S("media.default","media-player","player","Default video player","Choose NexaStation or a validated external player.",["open with","preferred"],SettingControlType.Choice,"Media.DefaultPlayer","NexaStation",10),
            S("media.external","media-player","external","Preferred external player","Manage validated external media-player registrations.",["vlc","mpv","open with"],SettingControlType.Navigation,null,null,20),
            S("media.resume","media-player","progress","Resume behavior","Configure local resume prompts and completion thresholds.",["continue watching","progress"],SettingControlType.Navigation,null,null,30),
            S("media.speed","media-player","playback","Playback speed","Choose whether playback speed resets or is remembered.",["rate","1.5x"],SettingControlType.Navigation,null,null,40),
            S("media.subtitles","media-player","tracks","Subtitles","Configure embedded and approved SRT/VTT subtitle behavior.",["srt","vtt","captions"],SettingControlType.Navigation,null,null,50),
            S("media.aspect","media-player","video","Aspect ratio","Choose Fit, Fill, Original, or explicit Stretch.",["video","fullscreen"],SettingControlType.Choice,"MediaPlayer.AspectRatio","Fit",60),

            S("gallery.sources","gallery","sources","Gallery sources","Manage explicitly approved photo and video folders.",["folders","pictures"],SettingControlType.Navigation,null,null,10,save:SettingSaveBehavior.Staged),
            S("gallery.pictures","gallery","sources","Pictures folder","Review the proposed Windows Pictures source and indexing state.",["photos","onboarding"],SettingControlType.Navigation,null,null,20),
            S("gallery.private","gallery","privacy","Private sources","Prevent private source titles and thumbnails from leaking to public views.",["guest","privacy"],SettingControlType.Navigation,null,null,30),
            S("gallery.home","gallery","visibility","Home visibility","Choose whether approved Gallery previews appear on Home.",["recent photos"],SettingControlType.Toggle,"Gallery.ShowOnHome",true,40),
            S("gallery.cache","gallery","storage","Thumbnail cache","Review and clear reproducible Gallery thumbnails.",["disk","memory","video thumbnails"],SettingControlType.Navigation,null,null,50),

            S("capture.explanation","capture","consent","Manual capture only","Screenshots are never taken automatically.",["screenshot","privacy","manual"],SettingControlType.Summary,null,null,10,recommended:true),
            S("capture.folder","capture","files","Screenshot folder","Choose a user-approved destination folder.",["pictures","path"],SettingControlType.Folder,"Capture.ScreenshotFolder",string.Empty,20,save:SettingSaveBehavior.Staged),
            S("capture.gallery","capture","files","Add screenshots to Gallery","Index successful captures only when the destination is an approved source.",["photos"],SettingControlType.Toggle,"Capture.AddToGallery",false,30),
            S("capture.hotkey","capture","input","Global keyboard hotkey","Configure the opt-in manual screenshot hotkey.",["keyboard","shortcut"],SettingControlType.Navigation,null,null,40,save:SettingSaveBehavior.Staged),

            S("audio.system-media","audio","sessions","System media controls","Enable supported system media-session actions.",["play pause","now playing"],SettingControlType.Toggle,"MediaSessions.Enabled",true,10),
            S("audio.metadata","audio","privacy","Media metadata visibility","Control title and artwork visibility in shared surfaces.",["privacy","guest"],SettingControlType.Navigation,null,null,20),
            S("audio.preferred-video","audio","players","Preferred video player","Choose the preferred application for video actions.",["external player"],SettingControlType.Navigation,null,null,30),

            S("display.preferred","display","screen","Preferred display","Choose the display NexaStation uses when available.",["monitor","television","tv"],SettingControlType.Navigation,null,null,10,save:SettingSaveBehavior.Staged),
            S("display.fullscreen","display","window","Fullscreen","Start or enter the frontend in fullscreen where supported.",["window"],SettingControlType.Toggle,"FullscreenEnabled",true,20),
            S("display.safe-area","display","television","Television safe area","Calibrate overscan-safe interface margins.",["overscan","tv"],SettingControlType.Navigation,null,null,30,save:SettingSaveBehavior.Staged),
            S("display.scale","display","readability","Interface scale","Set interface scale with preview and validation.",["dpi","zoom"],SettingControlType.Slider,"InterfaceScale",1d,40,validation:new("Range",.75,2)),
            S("display.calibration","display","television","Display calibration","Stage display calibration and confirm before applying.",["hdr","contrast","text"],SettingControlType.Navigation,null,null,50,save:SettingSaveBehavior.Staged),

            S("network.status","network","status","Network status","View local network availability without exposing addresses.",["offline","online"],SettingControlType.Summary,null,null,10),
            S("network.remote-play","network","remote","Remote-play profiles","Manage approved Steam Link, Moonlight, Parsec, and custom app profiles.",["streaming","sunshine"],SettingControlType.Navigation,null,null,20,save:SettingSaveBehavior.Staged),
            S("network.privacy","network","privacy","Network privacy","Review which optional integrations may access the network.",["permissions","online"],SettingControlType.Navigation,null,null,30),

            S("privacy.activity","privacy","activity","Activity history","Review local activity history, retention, and export.",["timeline","recent"],SettingControlType.Navigation,null,null,10),
            S("privacy.guest","privacy","profiles","Guest Mode","Configure local Guest restrictions and private-content suppression.",["owner pin","restrictions"],SettingControlType.Navigation,null,null,20,save:SettingSaveBehavior.Staged),
            S("privacy.diagnostics","privacy","diagnostics","Diagnostic data","Control local diagnostic export contents and redaction.",["logs","privacy"],SettingControlType.Toggle,"Privacy.DiagnosticsExportIncludesLogs",true,30),
            S("privacy.online-artwork","privacy","online","Online artwork permissions","Control approved remote metadata and artwork access.",["download","providers"],SettingControlType.Navigation,null,null,40),
            S("privacy.presence","privacy","online","Presence permissions","Control optional presence integrations and private title suppression.",["discord","status"],SettingControlType.Navigation,null,null,50),

            S("startup.sign-in","startup","startup","Launch at sign-in","Use the supported packaged startup task.",["windows","boot"],SettingControlType.Toggle,"Startup.Enabled",false,10),
            S("startup.exit","startup","window","Exit behavior","Choose minimize, hide, or return-to-desktop behavior.",["close","minimize"],SettingControlType.Choice,"Startup.ExitBehavior","ReturnToDesktop",20),
            S("startup.health","startup","recovery","Startup health","Review failure counters, recovery, and safe-mode status.",["crash","safe mode"],SettingControlType.Navigation,null,null,30),
            S("startup.single-instance","startup","status","Single-instance status","View activation and current instance health.",["windows integration"],SettingControlType.Summary,null,null,40),
            S("startup.close-application","startup","window","Close NexaStation","Close the NexaStation application after confirmation.",["exit","quit","close application"],SettingControlType.Action,null,null,50),

            S("updates.version","updates","status","Current version","View display/package versions and release channel.",["build","alpha"],SettingControlType.Summary,null,null,10),
            S("updates.channel","updates","channel","Release channel","Choose an approved update release channel.",["development","alpha","beta"],SettingControlType.Choice,"Updates.Channel","Development",20,restart:true),
            S("updates.checks","updates","checks","Automatic update checks","Enable signed update-feed checks when configured.",["feed","download"],SettingControlType.Toggle,"Updates.AutomaticChecksEnabled",false,30),
            S("updates.diagnostics","updates","diagnostics","Update diagnostics","Review the last check and validated update state.",["release notes","signature"],SettingControlType.Navigation,null,null,40),

            S("storage.location","storage","usage","Data folder","Open the local NexaStation data folder.",["database","files"],SettingControlType.Action,null,null,10),
            S("storage.usage","storage","usage","Storage summary","Review database, artwork, Gallery, logs, backups, and cache usage.",["disk","cache"],SettingControlType.Summary,null,null,20),
            S("storage.backups","storage","backup","Backups and restore","Create validated backups or stage a restore operation.",["database","recovery"],SettingControlType.Navigation,null,null,30,save:SettingSaveBehavior.Staged),
            S("storage.export","storage","transfer","Export settings","Export non-secret interface settings.",["configuration","backup"],SettingControlType.Action,null,null,40),
            S("storage.import","storage","transfer","Import settings","Validate and stage a settings import.",["configuration","restore"],SettingControlType.Action,null,null,50,save:SettingSaveBehavior.Staged),

            S("accessibility.contrast","accessibility","visual","High Contrast","Use color-independent system-aware surfaces and text fallbacks.",["contrast","colors"],SettingControlType.Navigation,null,null,10),
            S("accessibility.motion","accessibility","visual","Reduced Motion","Stop waves and decorative transitions.",["animation","crossbar"],SettingControlType.Toggle,"Interface.Performance.ReducedMotion",false,20),
            S("accessibility.text","accessibility","readability","Text scaling","Review behavior at Windows text scaling up to 200 percent.",["font","readability"],SettingControlType.Navigation,null,null,30),
            S("accessibility.focus","accessibility","navigation","Focus visibility","Keep keyboard and controller focus visible and actionable.",["outline","selection"],SettingControlType.Summary,null,null,40),
            S("accessibility.labels","accessibility","assistive","Screen-reader labels","Review accessible names and descriptive controller prompts.",["narrator","automation"],SettingControlType.Summary,null,null,50),

            S("help.about","help","about","About NexaStation","View version, architecture, database schema, and local build details.",["version","license"],SettingControlType.Navigation,null,null,10),
            S("help.known-issues","help","support","Known issues","Review current Alpha limitations and Beta blockers.",["bugs","alpha"],SettingControlType.Navigation,null,null,20),
            S("help.report","help","support","Report a problem","Create a redacted local reproduction bundle and issue draft.",["feedback","diagnostics"],SettingControlType.Action,null,null,30),
            S("help.privacy","help","legal","Privacy and security","Read local privacy and security documentation.",["security","policy"],SettingControlType.Navigation,null,null,40),
            S("help.notices","help","legal","Third-party notices","Review licenses, interoperability names, and asset provenance.",["open source","licence"],SettingControlType.Navigation,null,null,50),

            S("advanced.compatibility","advanced","compatibility","Compatibility profiles","Review built-in and local compatibility rules.",["profiles","safe rules"],SettingControlType.Navigation,null,null,10,advanced:true),
            S("advanced.database","advanced","diagnostics","Database health","Run non-destructive integrity and schema checks.",["sqlite","migrations"],SettingControlType.Navigation,null,null,20,advanced:true),
            S("advanced.logging","advanced","diagnostics","Logging","Review local logging and diagnostic redaction behavior.",["logs","tracing"],SettingControlType.Navigation,null,null,30,advanced:true),
            S("advanced.integrations","advanced","experimental","Experimental integrations","Review capability-gated experimental ecosystem features.",["discord","achievements"],SettingControlType.Navigation,null,null,40,advanced:true,experimental:true),

            S("reset.caches","reset-recovery","caches","Clear reproducible caches","Remove only reproducible artwork and thumbnail caches.",["dangerous","storage"],SettingControlType.Action,null,null,10,dangerous:true,advanced:true),
            S("reset.interface","reset-recovery","interface","Reset interface","Restore default theme, layout, rows, and visual preferences.",["dangerous","theme"],SettingControlType.Action,null,null,20,dangerous:true,advanced:true),
            S("reset.onboarding","reset-recovery","onboarding","Reset onboarding","Return onboarding state to first-run without deleting user media.",["dangerous","setup"],SettingControlType.Action,null,null,30,dangerous:true,advanced:true),
            S("reset.input","reset-recovery","input","Reset input mappings","Restore safe built-in keyboard and controller mappings.",["dangerous","controller"],SettingControlType.Action,null,null,40,dangerous:true,advanced:true),
            S("reset.data","reset-recovery","data","Reset NexaStation data","Remove NexaStation-managed state after typed confirmation; source files remain untouched.",["dangerous","database"],SettingControlType.Action,null,null,50,dangerous:true,advanced:true,save:SettingSaveBehavior.Staged),
        ];
#if DEBUG
        settings.Add(S("advanced.developer","advanced","developer","Developer diagnostics","Development-only navigation and diagnostic instrumentation.",["developer","debug"],SettingControlType.Navigation,null,null,90,advanced:true,experimental:true));
#endif
        return new SettingsCatalog(categories, settings);
    }

    private static SettingsCategoryDescriptor C(string id,string title,string description,string icon,int order,bool advanced=false,bool dangerous=false)=>new(id,title,description,icon,order,advanced,dangerous);
    private static SettingDescriptor S(string id,string category,string group,string title,string description,string[] keywords,SettingControlType type,string? binding,object? defaultValue,int order,SettingValidationRule? validation=null,bool restart=false,bool experimental=false,bool dangerous=false,bool advanced=false,bool recommended=false,SettingSaveBehavior save=SettingSaveBehavior.Immediate)=>new(id,category,group,title,description,keywords,type,binding,defaultValue,validation,restart,experimental,dangerous,advanced,null,null,order,SaveBehavior:save,Recommended:recommended);
}
