using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Group_Project.UserControls
{
    /// <summary>
    /// Interaction logic for CardHour.xaml
    /// </summary>
    public partial class CardHour : UserControl
    {
        public CardHour()
        {
            InitializeComponent();
        }
        public string Hour
        {
            get { return (string)GetValue(HourProperty); }
            set { SetValue(HourProperty, value); }
        }
        public static readonly DependencyProperty HourProperty = DependencyProperty.Register("Hour", typeof(string), typeof(CardHour));

        public string Temp
        {
            get { return (string)GetValue(tempProperty); }
            set { SetValue(tempProperty, value); }
        }
        public static readonly DependencyProperty tempProperty = DependencyProperty.Register("Temp", typeof(string), typeof(CardHour));

        public ImageSource Source
        {
            get { return (ImageSource)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }
        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register("Source", typeof(ImageSource), typeof(CardHour));

        public string Prcpt
        {
            get { return (string)GetValue(PrcptProperty); }
            set { SetValue(PrcptProperty, value); }
        }
        public static readonly DependencyProperty PrcptProperty = DependencyProperty.Register("Prcpt", typeof(string), typeof(CardHour));
    }
}
