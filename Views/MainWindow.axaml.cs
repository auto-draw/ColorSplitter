using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using SkiaSharp;

namespace csp.Views;


public class colorButton
{
}

public partial class MainWindow : Window
{
    private SKBitmap _rawBitmap = new SKBitmap();
    private Bitmap _previewBitmap;
    public object ColorsContent => new colorButton() ;
    
    public MainWindow()
    {
        InitializeComponent();
        
        // Taskbar components
        CloseAppButton.Click += (_, _) => Close();
        //MinimizeAppButton.Click += (_, _) => WindowState = WindowState.Minimized;
        
        // Image components
        
        var template = new FuncDataTemplate<colorButton>((value, namescope) =>
            new TextBlock
            {
                [!TextBlock.TextProperty] = new Binding("FirstName"),
            });
        
        MinimizeAppButton.Click += ImageButton;
    }

    private async void ImageButton(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select an image to import.",
            FileTypeFilter = new[] { ImageFileFilters },
            AllowMultiple = false
        });

        if (file.Count != 1) return;
        
        string path = file[0].TryGetLocalPath() ?? throw new ArgumentNullException("Invalid file.");
        
        ImportImage(path);
    }

    public static FilePickerFileType ImageFileFilters { get; } = new("Image files")
    {
        Patterns = new[] { "*.png", "*.jpg", "*.jpeg" }
    };
    
    public void ImportImage(string? path, byte[]? img = null) // string? path 
    {
        _rawBitmap.Dispose();
        _rawBitmap = img is null ? SKBitmap.Decode(path).NormalizeColor() : SKBitmap.Decode(img).NormalizeColor();
        
        _previewBitmap = _rawBitmap.ConvertToAvaloniaBitmap();
        
        ImagePreview.Image = _previewBitmap;
    }
}