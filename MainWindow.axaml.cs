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

    private object? ColorViewUser;
    private EventHandler<ColorChangedEventArgs>? ColorViewBinding;
    private EventHandler<RoutedEventArgs>? ColorViewCloseBinding;


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
        AlphaColor.Click += (sender, args) =>
        {
            // On Alpha Color View Click, show Color View, with binding to change color, and update Quantization on completion.
            EnableColorView(sender!, 
                (o, eventArgs) =>
                {
                    AlphaColor.Background = new SolidColorBrush(eventArgs.NewColor);
                    ImageSplitting.backgroundColor = eventArgs.NewColor;
                },
                (o, eventArgs) =>
                {
                    updateQuantize();
                });
        };
        
        // Init Color View Config
        ColorView.Palette = new SixteenColorPalette();

        // Taskbar components
        CloseAppButton.Click += (_, _) => Close();
        MinimizeAppButton.Click += (_, _) => WindowState = WindowState.Minimized;

    }

    private int changedKey; // Debounce Variable
    private void ColorsTextBoxOnTextChanged(object? sender, TextChangedEventArgs e)
    {
        // Fetch Number from ColorsTextBox Text Input, and convert to a number, if cannot, return null.
        var _Number = int.TryParse(ColorsTextBox.Text, out var colors) ? colors : (int?)null;

        // If null, default to "1", will loop back to re-run ColorsTextBoxOnTextChanged with a correct input.
        if (_Number is null)
        {
            ColorsTextBox.Text = "1";
            return;
        }
        
        // If over "255", default to "255", will loop back to re-run ColorsTextBoxOnTextChanged with a correct input.
        if (_Number > 255)
        {
            ColorsTextBox.Text = "255";
            return;
        }
        
        // If number is under "1", default to "1", will loop back to re-run ColorsTextBoxOnTextChanged with a correct input.
        if (_Number < 1)
        {
            ColorsTextBox.Text = "1";
            return;
        }
        
        // Set the ImageSplitter Colors to the input number
        ImageSplitting.Colors = (byte)_Number;

        // Spawn Debounce Thread
        // If 0.3s elapsed since change, and no more changes occured, update image.
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
        // Fetch Number from SmoothingTextBox Text Input, and convert to a number, if cannot, return null.
        var _Number = int.TryParse(SmoothingTextBox.Text, out var colors) ? colors : (int?)null;
        
        // If null, default to "1", will loop back to re-run ColorsTextBoxOnTextChanged with a correct input.
        if (_Number is null)
        {
            SmoothingTextBox.Text = "1";
            return;
        }
        
        // If over "255", default to "255", will loop back to re-run ColorsTextBoxOnTextChanged with a correct input.
        if (_Number > 255)
        {
            SmoothingTextBox.Text = "255";
            return;
        }
        
        // If number is under "1", default to "1", will loop back to re-run ColorsTextBoxOnTextChanged with a correct input.
        if (_Number < 1)
        {
            SmoothingTextBox.Text = "1";
            return;
        }

        // Set the ImageSplitter Colors to the input number
        ImageSplitting.Smoothing = (byte)_Number;
    }

    public static FilePickerFileType ImageFileFilters { get; } = new("Image files")
    {
        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp" },
        AppleUniformTypeIdentifiers = new[] { "public.image" },
        MimeTypes = new[] { "image/*" }
    };

    public static FilePickerFileType ZipFileFilters { get; } = new("Zip file")
    {
        Patterns = new[] { "*.zip" },
        MimeTypes = new[] { "application/zip" }
    };

    /* Depreciated until further notice.
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
    */
    
    // Export all Layers to HexColor
    private async void ImageExportFolder(object? sender, RoutedEventArgs e)
    {
        // Spawn a Folder Picker
        var folder = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions 
        {
            Title = "Export image into a folder"
        });

        // If no folder chosen, return.
        if (folder.Count != 1) return;
        
        // Get path. If path not exist or invalid, throw.
        string path = folder[0].TryGetLocalPath() ?? throw new ArgumentNullException("Invalid path.");
        
        // Get Dictionary, containing <Image,HexColor> for all colors
        Dictionary<SKBitmap, string> layers = ImageSplitting.getLayers(false);
        
        // Export each image as Lossless PNG to folder, under HexColor.png
        foreach (var (key, value) in layers)
        {
            var encodedData = key.Encode(SKEncodedImageFormat.Png, 100);
            using (StreamWriter outputFile = new StreamWriter(Path.Combine(path, value + ".png")))
                encodedData.SaveTo(outputFile.BaseStream);
        }
    }
    
    // Import Image Handler
    private async void ImageButton(object? sender, RoutedEventArgs e)
    {
        // Spawn Open File Dialog
        var file = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select an image to import",
            FileTypeFilter = new[] { ImageFileFilters },
            AllowMultiple = false
        });

        // If no file selected, return.
        if (file.Count != 1) return;
        
        // Get file. If file not exist or invalid, throw.
        string path = file[0].TryGetLocalPath() ?? throw new ArgumentNullException("Invalid file.");
        
        // Import the image via a Path.
        ImportImage(path);
    }
    
    // Import Image
    public void ImportImage(string? path, byte[]? img = null) // string? path 
    {
        // Clear old bitmap from memory.
        _rawBitmap.Dispose();
        // Decode new bitmap, from path or image.
        _rawBitmap = img is null ? SKBitmap.Decode(path).NormalizeColor() : SKBitmap.Decode(img).NormalizeColor();

        // Update Quantized view.
        updateQuantize();
    }

    private void updateQuantize()
    {
        // If no bitmap available, return.
        if (_rawBitmap is null) return;
        // Get Image post Quantize, alongside Colors Dictionary.
        (SKBitmap bmp, Dictionary<Color, int> colors) = ImageSplitting.colorQuantize(_rawBitmap);
        // Store new variable for quantized image, also stops GarbageCollection.
        _editedBitmap = bmp;
        
        // Convert the Quantized Image to Avalonia Bitmap
        _previewBitmap = _editedBitmap.ConvertToAvaloniaBitmap();
        
        // Apply Avalonia Bitmap to ImagePreview UI Element
        ImagePreview.Image = _previewBitmap;
        
        // Clear the colors list, and lists out new layers.
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
            
            // This however, worked out for bindings... Mostly
            var newListItem = new listedTheme();
            newListItem.Title = "Name: "+ key.R.ToString("X2") + key.G.ToString("X2") + key.B.ToString("X2");
            newListItem.Color = new SolidColorBrush(key);
            
            //newListItem.Image = ImageSplitting.getLayer(key).ConvertToAvaloniaBitmap();
                //do NOT uncomment, it kills performance atm, will rewrite later to be more performant and load only in view.
                
            // Applies Icon dependent on theme, since no Classes can be applied via Binding.
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
    
    // Enable the Color Picker view.
    private void EnableColorView(object sender, EventHandler<ColorChangedEventArgs> requestBinding, EventHandler<RoutedEventArgs>? closeBinding)
    {
        // If color view in use, return.
        if (ColorViewUser is not null)
        {
            return;
        }
        // Enable visibility
        ColorPalette.IsVisible = true;

        // Save the sender of the function.
        ColorViewUser = sender;
        // Save the binding to a variable
        ColorViewBinding = requestBinding;
        // Bind colorChanged to provided ColorChangedBinding
        ColorView.ColorChanged += ColorViewBinding;
        CloseColorView.Click += CloseColorViewOnClick;

        // if CloseBinding provided, save to variable
        if (closeBinding is not null)
        {
            ColorViewCloseBinding = closeBinding;
        }
    }

    private void CloseColorViewOnClick(object? sender, RoutedEventArgs e)
    {
        // Clear ColorView Usage.
        ColorViewUser = null;
        // Hide ColorPalette.
        ColorPalette.IsVisible = false;
        // Clear Binding for ColorChanged
        ColorView.ColorChanged -= ColorViewBinding;
        // Unbind Self.
        CloseColorView.Click -= CloseColorViewOnClick;
        // If ColorViewCloseBinding exists, invoke the binding, and then clear it.
        if (ColorViewCloseBinding is not null)
        {
            ColorViewCloseBinding.Invoke(sender,e);
            ColorViewCloseBinding = null;
        }
    }

    private void ShowHideIcon(object? sender, RoutedEventArgs e)
    {
        // Icon handling, TBA.
        string location = (string)((Button)sender).CommandParameter;
        Console.WriteLine("Number "+location+" is feeling shy/unshy.");
    }
}