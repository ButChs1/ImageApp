using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using ImageApp.core;

namespace ImageApp.Desktop;

public partial class MainWindow : Window
{
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl = "http://localhost:5281/api/images";

    public MainWindow()
    {
        InitializeComponent();
        _httpClient = new HttpClient();
        ImagesListView.SelectionChanged += (s, e) => UpdateButtonsState();

        Loaded += async (s, e) => await InitializeApp();
    }

    private async Task InitializeApp()
    {
        await LoadImages();
    }

    private async Task LoadImages()
    {
        try
        {
            // Отправка GET запроса к API для получения всех изображений
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/all");
            if (response.IsSuccessStatusCode)
            {
                // Десериализация JSON ответа в список объектов Image
                var images = await response.Content.ReadFromJsonAsync<List<core.Image>>();

                // Преобразование моделей данных в ViewModel'ы для отображения в UI
                // Select используется для проекции каждой модели в ViewModel
                ImagesListView.ItemsSource = images?.Select(img => new ImageViewModel
                {
                    Id = img.Id,
                    Name = img.Name,
                    ImageSource = LoadImage(img.Data) // Конвертация byte[] в BitmapImage
                });
            }
            else
            {
                MessageBox.Show($"Ошибка сервера: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки изображений: {ex.Message}");
        }
    }

    // Конвертирует массив байтов в BitmapImage для отображения в WPF Image control
    // Это сложный процесс из-за особенностей работы с изображениями в WPF
    private BitmapImage? LoadImage(byte[]? imageData)
    {
        if (imageData == null || imageData.Length == 0) return null;

        var image = new BitmapImage();
        using (var mem = new MemoryStream(imageData))
        {
            mem.Position = 0;

            // Инициализация BitmapImage с особыми настройками для производительности
            image.BeginInit();
            image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = null;
            image.StreamSource = mem;
            image.EndInit();
        }
        image.Freeze(); // Делает изображение неизменяемым и потокобезопасным
        return image;
    }

    private void UpdateButtonsState()
    {
        // Активация/деактивация кнопок в зависимости от выбора в списке
        bool hasSelection = ImagesListView.SelectedItem != null;
        EditButton.IsEnabled = hasSelection;
        DeleteButton.IsEnabled = hasSelection;
    }

    private async void AddButton_Click(object sender, RoutedEventArgs e)
    {
        // Стандартный диалог выбора файлов Windows
        var openFileDialog = new OpenFileDialog
        {
            Title = "Добавить изображение",
            Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            await UploadImage(openFileDialog.FileName, "add");
        }
    }

    private async void EditButton_Click(object sender, RoutedEventArgs e)
    {
        // Проверка что элемент выбран и приведение типа к ImageViewModel
        if (ImagesListView.SelectedItem is ImageViewModel selectedImage)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Изменить изображение",
                Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                // Передача ID выбранного изображения в endpoint
                await UploadImage(openFileDialog.FileName, $"update/{selectedImage.Id}");
            }
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (ImagesListView.SelectedItem is ImageViewModel selectedImage)
        {
            var result = MessageBox.Show("Вы уверены, что хотите удалить изображение?",
                "Подтверждение удаления", MessageBoxButton.YesNo);

            if (result == MessageBoxResult.Yes)
            {
                await DeleteImage(selectedImage.Id);
            }
        }
    }

    // Универсальный метод для загрузки и обновления изображений
    // Обрабатывает как POST (добавление), так и PUT (обновление) запросы
    private async Task UploadImage(string filePath, string endpoint)
    {
        try
        {
            // Создание multipart/form-data контента для загрузки файла
            using var formData = new MultipartFormDataContent();
            using var fileStream = File.OpenRead(filePath);
            using var fileContent = new StreamContent(fileStream);

            // Добавление файла в форму с именем "file"
            formData.Add(fileContent, "file", System.IO.Path.GetFileName(filePath));

            HttpResponseMessage response;

            if (endpoint.StartsWith("update"))
            {
                // Для PUT запросов нужно использовать HttpRequestMessage
                // HttpClient не имеет прямого метода для PUT с multipart/form-data
                var request = new HttpRequestMessage(HttpMethod.Put, $"{_apiBaseUrl}/{endpoint}")
                {
                    Content = formData
                };

                response = await _httpClient.SendAsync(request);
            }
            else
            {
                // Стандартный POST запрос для добавления
                response = await _httpClient.PostAsync($"{_apiBaseUrl}/{endpoint}", formData);
            }

            if (response.IsSuccessStatusCode)
            {
                await LoadImages(); // Обновляем список после успешной операции
                MessageBox.Show(endpoint.StartsWith("update") ?
                    "Изображение обновлено" : "Изображение добавлено");
            }
            else
            {
                MessageBox.Show($"Ошибка сервера: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка: {ex.Message}");
        }
    }

    private async Task DeleteImage(int imageId)
    {
        try
        {
            // DELETE запрос для удаления изображения по ID
            var response = await _httpClient.DeleteAsync($"{_apiBaseUrl}/delete/{imageId}");

            if (response.IsSuccessStatusCode)
            {
                await LoadImages();
                MessageBox.Show("Изображение удалено");
            }
            else
            {
                MessageBox.Show($"Ошибка сервера: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка удаления: {ex.Message}");
        }
    }
}

// ViewModel для отображения изображения в WPF ListView
// Отделяет модель данных от представления, добавляя свойства для UI
public class ImageViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public BitmapImage? ImageSource { get; set; } // Конвертированное изображение для WPF
}