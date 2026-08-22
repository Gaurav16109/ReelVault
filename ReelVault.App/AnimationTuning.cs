namespace ReelVault.App;

// Phase 6.4: every tweakable motion value used by PressFeedback/EntranceAnimation/page-transition
// helpers, gathered in one place. The user judges feel on Catalyst and may ask for a nudge - change
// a value here rather than hunting through each page's code-behind.
public static class AnimationTuning
{
    // B2 - press feedback (buttons, Home tiles, browse cards). Scale-down-then-up "punch", plus a
    // brief glow flash for elements that have a PressGlow overlay.
    public const double PressScaleDown = 0.95;
    public const uint PressDownMs = 70;
    public const uint PressUpMs = 110;
    public const float PressGlowOpacity = 0.16f;

    // B3 - gentle card/list entrance (fade + slight slide-up) as browse cards first appear.
    public const uint EntranceFadeMs = 180;
    public const double EntranceSlideOffset = 14;
    public const uint EntranceStaggerStepMs = 35;
    public const uint EntranceStaggerCapMs = 350;

    // B1 - page content entrance, layered on top of the platform's native push/pop slide so a new
    // screen's content settles in rather than just snapping into place.
    public const uint PageContentFadeMs = 220;
    public const double PageContentSlideOffset = 12;
}
