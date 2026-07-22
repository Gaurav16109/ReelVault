using System.Text.Json;
using ReelVault.App.Services;

namespace ReelVault.App;

public partial class MainPage : ContentPage
{
	private readonly IReelVaultApiClient _apiClient;

	public MainPage(IReelVaultApiClient apiClient)
	{
		InitializeComponent();
		_apiClient = apiClient;
	}

	private async void OnTestConnectionClicked(object sender, EventArgs e)
	{
		TestConnectionButton.IsEnabled = false;
		ResultLabel.Text = "Calling API...";

		try
		{
			var health = await _apiClient.GetHealthAsync();
			ResultLabel.Text = JsonSerializer.Serialize(health, new JsonSerializerOptions { WriteIndented = true });
		}
		catch (Exception ex)
		{
			ResultLabel.Text = $"Error: {ex.Message}";
		}
		finally
		{
			TestConnectionButton.IsEnabled = true;
		}
	}
}
