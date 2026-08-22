namespace ReelVault.App;

// Phase 6.5 (final minimalist pass): tweakable spacing/sizing knobs for layout built from code
// (DetailPage's dynamic "from the reel" field rows, which can't be tuned directly in XAML the way
// the static sections around them can). The app's ONE accent color lives in
// Resources/Styles/Colors.xaml (key "Primary") - change it there, not here.
public static class DesignTuning
{
    // Matches the padding/spacing used by DetailPage.xaml's static VerticalStackLayout sections, so
    // code-built rows sit at the same rhythm as everything around them.
    public const double DetailContentPadding = 24;
    public const double DetailSectionSpacing = 20;

    // Spacing between the label and value line within one dynamically-built "from the reel" field row.
    public const double DetailFieldRowSpacing = 2;
}
