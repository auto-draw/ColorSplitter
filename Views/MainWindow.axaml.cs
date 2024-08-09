using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace csp.Views;

public partial class MainWindow : Window
{
    private SKBitmap _rawBitmap;
    private Bitmap _previewBitmap;
    
    public MainWindow()
    {
        InitializeComponent();
    }

    public void ImportImage(string? path, byte[]? img = null)
    {
        _rawBitmap = img is null ? SKBitmap.Decode(path).NormalizeColor() : SKBitmap.Decode(img).NormalizeColor();

        _previewBitmap = _rawBitmap.ConvertToAvaloniaBitmap();
        
        ImagePreview.Image = _previewBitmap;
    }
}