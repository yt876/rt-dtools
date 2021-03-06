﻿using System;
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
using System.Windows.Shapes;

namespace RTDicomViewer.View.Dialogs
{
    /// <summary>
    /// Interaction logic for GammaWindowView.xaml
    /// </summary>
    public partial class GammaWindowView : Window
    {
        public GammaWindowView()
        {
            InitializeComponent();
        }

        private void GroupBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if(e.LeftButton == MouseButtonState.Pressed)
                this.DragMove();
        }
    }
}
