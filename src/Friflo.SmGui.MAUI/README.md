# Friflo.SmGui.MAUI


SmGui backend for MAUI using Skia: https://github.com/dotnet/maui


## Example

MainPage.xaml
```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:smGui="using:Friflo.SmGui.MAUI"
             x:Class="SkiaMaui.MainPage">

    <smGui:SkiaGuiView x:Name="GuiView"/>
    
</ContentPage>
```

MainPage.xaml.cs
```csharp
public partial class MainPage
{
    public MainPage()
    {
        InitializeComponent();
        GuiView.CreateGuiApp(backend => new MyGuiApp(backend), new DefaultGuiAssets());
    }
}
```