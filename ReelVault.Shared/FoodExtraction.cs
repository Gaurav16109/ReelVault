namespace ReelVault.Shared;

// Every field is nullable: null means "not mentioned in the caption", never a guess.
public class FoodExtraction
{
    public string? Name { get; set; }
    public string? Area { get; set; }
    public string? City { get; set; }
    public string? Cuisine { get; set; }
    public string? PriceRange { get; set; }
    public List<string>? MustTry { get; set; }
    public string? Rating { get; set; }
    public string? OpeningHours { get; set; }
    public string? MapQuery { get; set; }
    public string? VegOptions { get; set; }
    public string? Parking { get; set; }
    public string? Summary { get; set; }
}
