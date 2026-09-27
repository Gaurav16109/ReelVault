namespace ReelVault.App;

// Phase 6.4: every tweakable motion value used by PressFeedback/EntranceAnimation, gathered in one
// place. The user judges feel on Catalyst and may ask for a nudge - change a value here rather than
// hunting through each page's code-behind.
//
// Note: a page-level entrance animation (fade + slide-up on OnAppearing, layered over the native
// push/pop transition) was removed here - OnAppearing fires on EVERY appearance, including when
// popping back to a page whose content was already fully rendered and visible before being covered.
// Re-running a fade-from-0 on that already-visible content caused a guaranteed hide-then-refade
// flicker on back navigation (and a timing-dependent one on forward navigation, racing the native
// push). MAUI has no reliable pre-render hook to set the initial state before the native transition
// starts, so the fix was to drop the custom animation and rely on the native transition alone.
public static class AnimationTuning
{
    // B2 - press feedback (buttons, Home tiles, browse cards). Scale-down-then-up "punch", plus a
    // brief glow flash for elements that have a PressGlow overlay.
    public const double PressScaleDown = 0.95;
    public const uint PressDownMs = 70;
    public const uint PressUpMs = 110;
    public const float PressGlowOpacity = 0.16f;

    // B3 - gentle card/list entrance (fade + slight slide-up) as browse cards first appear. Safe
    // from the flicker above: this plays on each CollectionView cell's Loaded event, which fires
    // once when a cell is actually (re)created for fresh data - not on every page appearance.
    public const uint EntranceFadeMs = 180;
    public const double EntranceSlideOffset = 14;
    public const uint EntranceStaggerStepMs = 35;
    public const uint EntranceStaggerCapMs = 350;
}
