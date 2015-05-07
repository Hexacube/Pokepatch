using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

namespace Pokepatch
{
    /// <summary>
    /// The main Pokepatch userinterface.
    /// </summary>
    public partial class Mainform : Form
    {
        const string DIALOG_TITLE = "Please pick a ROM which "
            + "you want to patch or which you have modified";
        Stream rom_s;

        /// <summary>
        /// Loads all components of the form.
        /// </summary>
        public Mainform()
        {
            InitializeComponent();
            Size = new Size(492, 332);
        }

        /// <summary>
        /// Displays the about-form.
        /// </summary>
        private void ClickAbout(object obj, EventArgs arg)
        {
            Aboutform about = new Aboutform();
                      about.ShowDialog();
                      about.Dispose();
        }

        private void ClickOpen(object obj, EventArgs arg)
        {
            OpenFileDialog dialog = new OpenFileDialog();
                           dialog.Title = DIALOG_TITLE;
        }

        /// <summary>
        /// Exits the application.
        /// </summary>
        private void ExitApp(object obj, EventArgs arg)
        {
            Close();
            Dispose();
            Application.Exit();
            Environment.Exit(0);
        }
    }
}