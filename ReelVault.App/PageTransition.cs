namespace ReelVault.App;

// Phase 6.4 (B1): a light fade + slight slide-up on a page's content when it appears, layered on top
// of the platform's own native push/pop slide transition, so navigating Home -> Browse -> Detail
// feels like content "settles in" rather than just snapping into place. Fire-and-forget from
// OnAppearing - runs concurrently with whatever data load the page kicks off, never blocking it.
public static class PageTransition
{
    public static async Task AnimateInAsync(VisualElement content)
    {
        content.Opacity = 0;
        content.TranslationY = AnimationTuning.PageContentSlideOffset;

        await Task.WhenAll(
            content.FadeToAsync(1, AnimationTuning.PageContentFadeMs, Easing.CubicOut),
            content.TranslateToAsync(0, 0, AnimationTuning.PageContentFadeMs, Easing.CubicOut));
    }
}
