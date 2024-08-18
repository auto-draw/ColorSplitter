using System;
using System.IO;
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
        
        Config.init();
        
        // Taskbar components
        CloseAppButton.Click += (_, _) => Close();
        MinimizeAppButton.Click += (_, _) => WindowState = WindowState.Minimized;
        
        // Image components
        
        var template = new FuncDataTemplate<colorButton>((value, namescope) =>
            new TextBlock
            {
                [!TextBlock.TextProperty] = new Binding("FirstName"),
            });
    }

    public static FilePickerFileType ImageFileFilters { get; } = new("Image files")
    {
        // gets first frame out of a gif file
        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp" },
        AppleUniformTypeIdentifiers = new[] { "public.image" },
        MimeTypes = new[] { "image/*" }
    };

    public static FilePickerFileType ZipFileFilters { get; } = new("Zip file")
    {
        Patterns = new[] { "*.zip" },
        MimeTypes = new[] { "application/zip" }
    };

    private async void ImageExportZIP(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export image as a ZIP file",
            FileTypeChoices = new[] { ZipFileFilters }
        });

        if (file is null) return;
        
        // currently writes "Text file", the zip file will literally just be a text file that you can open in Notepad
        
        await using var stream = await file.OpenWriteAsync();
        using var streamWriter = new StreamWriter(stream);
        await streamWriter.WriteLineAsync("Text file");
    }
    
    private async void ImageExportFolder(object? sender, RoutedEventArgs e)
    {
        var folder = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions 
        {
            Title = "Export image into a folder"
        });

        if (folder.Count != 1) return;
        
        string path = folder[0].TryGetLocalPath() ?? throw new ArgumentNullException("Invalid path.");

        // loops 0-9
        for (int i = 0; i < 10; i++)
        {
            using (StreamWriter outputFile = new StreamWriter(Path.Combine(path, "image-"+i+".txt")))
                outputFile.WriteLine("this is a totally real image!");
        }
    }
    
    private async void ImageButton(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select an image to import",
            FileTypeFilter = new[] { ImageFileFilters },
            AllowMultiple = false
        });

        if (file.Count != 1) return;
        
        string path = file[0].TryGetLocalPath() ?? throw new ArgumentNullException("Invalid file.");
        
        ImportImage(path);
    }
    
    public void ImportImage(string? path, byte[]? img = null) // string? path 
    {
        _rawBitmap.Dispose();
        _rawBitmap = img is null ? SKBitmap.Decode(path).NormalizeColor() : SKBitmap.Decode(img).NormalizeColor();
        
        _previewBitmap = _rawBitmap.ConvertToAvaloniaBitmap();
        
        ImagePreview.Image = _previewBitmap;
    }
}