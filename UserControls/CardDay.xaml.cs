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
    /// Interaction logic for CardDay.xaml
    /// </summary>
    public partial class CardDay : UserControl
    {
        public CardDay()
        {
            InitializeComponent();
        }
        public string Day
        {
            get { return (string)GetValue(DayProperty); }
            set { SetValue(DayProperty, value); }
        }
        public static readonly DependencyProperty DayProperty = DependencyProperty.Register("Day", typeof(string), typeof(CardDay));

        public string Maxtemp
        {
            get { return (string)GetValue(MaxtempProperty); }
            set { SetValue(MaxtempProperty, value); }
        }
        public static readonly DependencyProperty MaxtempProperty = DependencyProperty.Register("Maxtemp", typeof(string), typeof(CardDay));

        public string Mintemp
        {
            get { return (string)GetValue(MintempProperty); }
            set { SetValue(MintempProperty, value); }
        }
        public static readonly DependencyProperty MintempProperty = DependencyProperty.Register("Mintemp", typeof(string), typeof(CardDay));

        public ImageSource Source
        {
            get { return (ImageSource)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }
        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register("Source", typeof(ImageSource), typeof(CardDay));

        public string Prcpt
        {
            get { return (string)GetValue(PrcptProperty); }
            set { SetValue(PrcptProperty, value); }
        }
        public static readonly DependencyProperty PrcptProperty = DependencyProperty.Register("Prcpt", typeof(string), typeof(CardDay));
    }
}
