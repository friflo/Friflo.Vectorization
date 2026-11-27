# Friflo.SmGui.SkiaMAUI


SmGui backend for SkiaSharp + MAUI
- SkiaSharp: https://github.com/mono/skiasharp
- MAUI:      https://github.com/dotnet/maui


## Example

MainPage.xaml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:smGuiMaui="clr-namespace:Friflo.SmGui.MAUI;assembly=Friflo.SmGui.MAUI"
             x:Class="SkiaMaui.MainPage">

    <smGuiMaui:SkiaGuiView x:Name="SkiaGuiView"
                       Background="Cornflowerblue"/>
    
</ContentPage>
```

MainPage.xaml.cs
```csharp
public partial class MainPage
{
    public MainPage()
    {
        InitializeComponent();
        
        SkiaGuiView.CreateGuiApp(backend => new BasicGuiApp(backend), new DefaultGuiAssets());
    }
}
```