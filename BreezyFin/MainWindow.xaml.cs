using System;
using System.Collections.ObjectModel;
using System.Windows;

using Breezy.Fin.Model.Factories;
using Breezy.Fin.Model;
using Breezy.Fin.ViewModel;
using Breezy.Fin.Model.IB;


namespace Breezy.Fin
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {   
            InitializeComponent();
        }

        /// <summary>
        /// Handle clicks on the listview column heading
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnColumnHeaderClick(object sender, RoutedEventArgs e)
        {
        }

        private void AddNewItem(object sender, RoutedEventArgs e)
        {
        }

        private void OnMainWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            (this.DataContext as MainWindowViewModel).DisconnectIB();
        }

    }
}
