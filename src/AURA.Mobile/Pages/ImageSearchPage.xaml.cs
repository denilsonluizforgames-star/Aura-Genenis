using AURA.Mobile.Diagnostics;

namespace AURA.Mobile.Pages
{
    public partial class ImageSearchPage : ContentPage
    {
        private readonly List<ImageSearchProvider> _providers = SearchCatalog.ImageProviders;

        public ImageSearchPage()
        {
            InitializeComponent();
            ProviderPicker.ItemsSource = _providers.Select(p => p.Name).ToList();
            ProviderPicker.SelectedIndex = 0;
        }

        private ImageSearchProvider CurrentProvider =>
            _providers[Math.Max(0, ProviderPicker.SelectedIndex)];

        private async void OnByUrlClicked(object sender, EventArgs e)
        {
            string input = ImageUrlEntry.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                await ShowStatus("Informe a URL da imagem (https://...).");
                return;
            }

            if (!input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                input = "https://" + input;
            }

            AuraLog.Info($"ImageSearch: buscando por URL via {CurrentProvider.Name}");

            string encoded = Uri.EscapeDataString(input);
            string url = string.Format(CurrentProvider.ByUrlTemplate, encoded);
            await ShowStatus($"Buscando em {CurrentProvider.Name}...");
            Results.IsVisible = true;
            Results.Source = url;
        }

        private async void OnGalleryClicked(object sender, EventArgs e)
        {
            try
            {
                FileResult? photo = await MediaPicker.Default.PickPhotoAsync();
                if (photo == null)
                {
                    return;
                }

                if (!CurrentProvider.SupportsUpload)
                {
                    await ShowStatus($"{CurrentProvider.Name} só aceita URL. Escolha Google Lens ou Bing Visual para enviar arquivo.");
                    return;
                }

                await ShowStatus($"Enviando para {CurrentProvider.Name}...");

                byte[] bytes = await ReadAllBytesAsync(photo);
                string contentType = "image/" + (photo.ContentType?.ToLowerInvariant().EndsWith("png") == true ? "png" : "jpeg");

                string resultUrl = await UploadImageAsync(
                    CurrentProvider.UploadEndpoint,
                    CurrentProvider.Name,
                    bytes,
                    contentType,
                    Path.GetExtension(photo.FileName).TrimStart('.') ?? "jpg");

                if (string.IsNullOrWhiteSpace(resultUrl))
                {
                    await ShowStatus("Upload concluído, mas não foi possível obter a página de resultados. Tente por URL.");
                    return;
                }

                await ShowStatus($"Resultados de {CurrentProvider.Name}.");
                Results.IsVisible = true;
                Results.Source = resultUrl;
            }
            catch (Exception ex)
            {
                AuraLog.Exception("ImageSearchPage.Upload", ex);
                await ShowStatus("Falha no upload da imagem: " + ex.Message);
            }
        }

        private static async Task<byte[]> ReadAllBytesAsync(FileResult photo)
        {
            using var stream = await photo.OpenReadAsync();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            return ms.ToArray();
        }

        private static async Task<string> UploadImageAsync(
            string endpoint,
            string providerName,
            byte[] bytes,
            string contentType,
            string ext)
        {
            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var http = new HttpClient(handler);
            http.Timeout = TimeSpan.FromSeconds(40);

            using var form = new MultipartFormDataContent();
            var imageContent = new ByteArrayContent(bytes);
            imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            form.Add(imageContent, "encoded_image", "upload." + ext);
            form.Add(new StringContent(string.Empty), "content");

            using var resp = await http.PostAsync(endpoint, form);

            string? location = resp.Headers.Location?.ToString();
            if (!string.IsNullOrWhiteSpace(location))
            {
                if (location.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    location.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    return location;
                }

                var baseUri = new Uri(endpoint);
                return baseUri.GetLeftPart(UriPartial.Authority) + location;
            }

            if (resp.IsSuccessStatusCode)
            {
                return resp.RequestMessage?.RequestUri?.ToString() ?? endpoint;
            }

            return string.Empty;
        }

        private void OnNavigating(object sender, WebNavigatingEventArgs e)
        {
            AuraLog.Info("ImageSearch: resultados em " + e.Url);
        }

        private async Task ShowStatus(string message)
        {
            StatusLabel.Text = message;
            StatusLabel.IsVisible = true;
            AuraLog.Info("ImageSearch: " + message);
            await Task.CompletedTask;
        }
    }
}
