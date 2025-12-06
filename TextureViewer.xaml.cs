using System.Windows;
using System.Windows.Media;

namespace AnarkBrowser
{
    public partial class TextureViewer : Window
    {
        public TextureViewer(ImageSource image, string info)
        {
            InitializeComponent();

            TextureImage.Source = image;
            InfoText.Text = info;
        }
    }
}