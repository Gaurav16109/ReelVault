namespace ReelVault.App;

// Phase 6.4 (B3): gentle fade + slide-up entrance for browse cards as they first appear, with a
// small per-cell stagger so the grid reads as a soft cascade rather than a flat snap. Cheap (two
// quick property animations per cell, played once via each cell's Loaded event) - no continuous or
// looping animation, so it costs nothing once settled and never competes with scrolling.
public static class EntranceAnimation
{
    public static async void PlayOnLoad(VisualElement element, int staggerIndex)
    {
        element.Opacity = 0;
        element.TranslationY = AnimationTuning.EntranceSlideOffset;

        var delay = Math.Min(staggerIndex * AnimationTuning.EntranceStaggerStepMs, AnimationTuning.EntranceStaggerCapMs);
        if (delay > 0)
        {
            await Task.Delay((int)delay);
        }

        await Task.WhenAll(
            element.FadeToAsync(1, AnimationTuning.EntranceFadeMs, Easing.CubicOut),
            element.TranslateToAsync(0, 0, AnimationTuning.EntranceFadeMs, Easing.CubicOut));
    }
}
