using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Platform.Storage;
using Avalonia.Skia;
using Avalonia.Styling;
using Avalonia.Threading;
using SkiaSharp;

namespace csp.Views;


public partial class MainWindow : Window
{
    public class listedTheme
    {
        public string Title { get; set; }
        public Bitmap Image { get; set; }
        public SolidColorBrush Color { get; set; }
        public Bitmap ButtonIcon { get; set; }
        
        public string HideShow { get; set; }
    }
    
    private static SKBitmap _rawBitmap = ImageExtensions.ImageHelper.LoadFromResource(new Uri("avares://Color-Splitter/Assets/Example.png")).ConvertToSKBitmap();
    private SKBitmap _editedBitmap = _rawBitmap;
    private Bitmap _previewBitmap;
    
    
    public MainWindow()
    {
        InitializeComponent();
        
        Config.init();

        ImageSplitting.backgroundColor = new Color(255, 0, 0, 0);
        AlphaColor.Background = new SolidColorBrush(ImageSplitting.backgroundColor);
        
        // Image Handling
        ImportButton.Click += ImageButton;
        ExportButton.Click += ImageExportFolder;
        ColorsTextBox.TextChanged += ColorsTextBoxOnTextChanged;
        SmoothingTextBox.TextChanged += SmoothingTextBoxOnTextChanged;
        
        // Taskbar components
        CloseAppButton.Click += (_, _) => Close();
        MinimizeAppButton.Click += (_, _) => WindowState = WindowState.Minimized;

    }

    private int changedKey = 0;
    private void ColorsTextBoxOnTextChanged(object? sender, TextChangedEventArgs e)
    {
        var _Number = int.TryParse(ColorsTextBox.Text, out var colors) ? colors : (int?)null;

        if (_Number is null)
        {
            ColorsTextBox.Text = "1";
            return;
        }
        
        if (_Number > 255)
        {
            ColorsTextBox.Text = "255";
            return;
        }
        
        if (_Number < 1)
        {
            ColorsTextBox.Text = "1";
            return;
        }
        
        ImageSplitting.Colors = (byte)_Number;

        Thread thread = new Thread(new ThreadStart(() =>
        {
            Random random = new Random();
            int _ourKey = random.Next();
            changedKey = _ourKey;
            Thread.Sleep(300);
            if (changedKey == _ourKey)
            {
                Dispatcher.UIThread.Invoke(updateQuantize);
            }
        }));
        thread.Start();
    }

    private void SmoothingTextBoxOnTextChanged(object? sender, TextChangedEventArgs e)
    {
        var _Number = int.TryParse(SmoothingTextBox.Text, out var colors) ? colors : (int?)null;
        
        if (_Number is null)
        {
            SmoothingTextBox.Text = "1";
            return;
        }
        
        if (_Number > 255)
        {
            SmoothingTextBox.Text = "255";
            return;
        }
        
        if (_Number < 1)
        {
            SmoothingTextBox.Text = "1";
            return;
        }

        ImageSplitting.Smoothing = (byte)_Number;
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
        
        Dictionary<SKBitmap, string> layers = ImageSplitting.getLayers(false);
        
        foreach (var (key, value) in layers)
        {
            var encodedData = key.Encode(SKEncodedImageFormat.Png, 100);
            using (StreamWriter outputFile = new StreamWriter(Path.Combine(path, value + ".png")))
                encodedData.SaveTo(outputFile.BaseStream);
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

        updateQuantize();
    }

    private void updateQuantize()
    {
        if (_rawBitmap is null) return;
        (SKBitmap bmp, Dictionary<Color, int> colors) = ImageSplitting.colorQuantize(_rawBitmap);
        _editedBitmap = bmp;
        
        _previewBitmap = _editedBitmap.ConvertToAvaloniaBitmap();
        
        ImagePreview.Image = _previewBitmap;
        
        ColorsList.Children.Clear();
        Objects.Items.Clear();
        foreach (var (key, value) in colors)
        {
            // This was just easier than using bindings.
            var newItem = new Button();
            newItem.Width = 14;
            newItem.Height = 14;
            newItem.Margin = new Thickness(2);
            newItem.BorderThickness = new Thickness(0);
            newItem.Background = new SolidColorBrush(key);
            newItem.Classes.Add("noChange");
            newItem.Classes.Add("ColorItem");
            newItem.Cursor = new Cursor(StandardCursorType.Hand);
            ColorsList.Children.Add(newItem);
            
            var newListItem = new listedTheme();
            newListItem.Title = "Name: "+ key.R.ToString("X2") + key.G.ToString("X2") + key.B.ToString("X2");
            newListItem.Color = new SolidColorBrush(key);
            
            //newListItem.Image = ImageSplitting.getLayer(key).ConvertToAvaloniaBitmap();
                //do NOT uncomment, it kills performance atm, will rewrite later to be more performant and load only in view.
            if (App.CurrentTheme == "avares://Color-Splitter/Styles/light.axaml")
            {
                newListItem.ButtonIcon = ImageExtensions.ImageHelper.LoadFromResource(new Uri("avares://Color-Splitter/Assets/Light/eye-solid.png"));
            }
            else
            {
                newListItem.ButtonIcon = ImageExtensions.ImageHelper.LoadFromResource(new Uri("avares://Color-Splitter/Assets/Dark/eye-solid.png"));
            }
            Objects.Items.Add(newListItem);
        }
    }

    private void ShowHideIcon(object? sender, RoutedEventArgs e)
    {
        string location = (string)((Button)sender).CommandParameter;
        Console.WriteLine("Number "+location+" is feeling shy/unshy.");
    }
}