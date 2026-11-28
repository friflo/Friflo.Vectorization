# Friflo.SmGui.Uno


SmGui backend for Uno using Skia: https://github.com/unoplatform/uno


## Example

MainPage.xaml
```xml
<Page x:Class="UnoApp.MainPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:smGui="using:Friflo.SmGui.Uno"
      Background="{ThemeResource BackgroundBrush}">

    <smGui:SkiaGuiControl x:Name="GuiControl" />
</Page>
```

MainPage.xaml.cs
```csharp
public sealed partial class MainPage : Page
{
    public MainPage() {
        InitializeComponent();
        GuiControl.CreateGuiApp(backend => new MyGuiApp(backend), new DefaultGuiAssets());
    }
}
```